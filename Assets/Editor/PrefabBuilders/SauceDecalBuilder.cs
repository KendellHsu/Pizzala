using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Pizzala.Dirt;

namespace Pizzala.EditorTools
{
    // 把 Quad 版醬料髒污升級成 URP Decal Projector 版(Problem.md #2:
    // 髒污要貼合客人模型/斜面,不再懸空)。一鍵完成:
    //   1. Mobile_Renderer / PC_Renderer 加上 Decal Renderer Feature
    //   2. 每個 PZ_SauceSplat_* prefab 產生對應 PZ_SauceDecal_* prefab
    //      (DecalProjector 沿 +Z 投影,DirtManager 的 LookRotation(-normal) 直接通用)
    //   3. 每張醬料貼圖建一個 URP/Decal 材質(Assets/Art/Meterials/Decal)
    //   4. 自動把目前開啟場景裡 DirtManager 的髒污池換成 Decal 版
    // 可重複執行:材質與 Prefab 會原地更新,不會重複建、不會斷參照。
    public static class SauceDecalBuilder
    {
        const string SplatFolder = "Assets/Prefabs/SauceSplat";
        const string DecalPrefabFolder = SplatFolder + "/Decal";
        const string MatParentFolder = "Assets/Art/Meterials";
        const string DecalMatFolder = MatParentFolder + "/Decal";
        const float DecalDepth = 0.35f; // 投影深度:要夠深才能包住軀幹/手臂的曲面

        static readonly string[] RendererPaths =
        {
            "Assets/Settings/Mobile_Renderer.asset",
            "Assets/Settings/PC_Renderer.asset",
        };

        // URP 內建 decal shadergraph 的套件路徑(名稱是 "Shader Graphs/Decal",
        // 用路徑載入最穩,不怕 Shader.Find 沒載到或跟自製 shadergraph 撞名)
        const string DecalShaderPath =
            "Packages/com.unity.render-pipelines.universal/Shaders/Decal.shadergraph";

        [MenuItem("Tools/Pizzala/Convert Sauce Splats To Decals")]
        public static void Convert()
        {
            var decalShader = AssetDatabase.LoadAssetAtPath<Shader>(DecalShaderPath);
            if (decalShader == null) decalShader = Shader.Find("Shader Graphs/Decal");
            if (decalShader == null)
            {
                Debug.LogError($"SauceDecalBuilder: 找不到 URP Decal shader({DecalShaderPath}),請確認 URP 套件完整。");
                return;
            }

            foreach (var path in RendererPaths) AddDecalFeature(path);

            EnsureFolder(SplatFolder, "Decal");
            EnsureFolder(MatParentFolder, "Decal");

            var matCache = new Dictionary<Material, Material>();
            var decals = new List<GameObject>();
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { SplatFolder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.StartsWith(DecalPrefabFolder)) continue; // 跳過已產出的 Decal 版
                var decal = BuildDecalPrefab(path, decalShader, matCache);
                if (decal != null) decals.Add(decal);
            }

            AssetDatabase.SaveAssets();
            AssignToDirtManager(decals);
            Debug.Log($"SauceDecalBuilder: 完成,共 {decals.Count} 個 Decal prefab、{matCache.Count} 個 Decal 材質。");
        }

        // ── Renderer Feature ─────────────────────────────────────

        static void AddDecalFeature(string path)
        {
            var data = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(path);
            if (data == null)
            {
                Debug.LogWarning($"SauceDecalBuilder: 找不到 {path},請手動在該 Renderer 加 Decal Renderer Feature。");
                return;
            }
            if (data.rendererFeatures.Any(f => f is DecalRendererFeature)) return;

            var feature = ScriptableObject.CreateInstance<DecalRendererFeature>();
            feature.name = "Decal";
            AssetDatabase.AddObjectToAsset(feature, data);
            AssetDatabase.SaveAssets();
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long localId);

            var so = new SerializedObject(data);
            var features = so.FindProperty("m_RendererFeatures");
            features.arraySize++;
            features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = feature;
            var map = so.FindProperty("m_RendererFeatureMap");
            map.arraySize++;
            map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(data);
            Debug.Log($"SauceDecalBuilder: 已在 {path} 加入 Decal Renderer Feature。");
        }

        // ── Prefab 與材質 ─────────────────────────────────────────

        static GameObject BuildDecalPrefab(string srcPath, Shader shader,
                                           Dictionary<Material, Material> matCache)
        {
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(srcPath);
            var renderer = src != null ? src.GetComponentInChildren<MeshRenderer>() : null;
            if (renderer == null || renderer.sharedMaterial == null)
            {
                Debug.LogWarning($"SauceDecalBuilder: {srcPath} 沒有 MeshRenderer/材質,跳過。");
                return null;
            }

            var decalMat = GetOrCreateDecalMaterial(renderer.sharedMaterial, shader, matCache);
            var name = src.name.Replace("SauceSplat", "SauceDecal");
            var dstPath = $"{DecalPrefabFolder}/{name}.prefab";

            var go = new GameObject(name);
            var proj = go.AddComponent<DecalProjector>();
            proj.material = decalMat;
            // Quad 網格是 1x1m,世界尺寸 = lossyScale;深度沿 +Z 投進表面
            var s = renderer.transform.lossyScale;
            proj.size = new Vector3(s.x, s.y, DecalDepth);
            proj.pivot = new Vector3(0f, 0f, DecalDepth * 0.5f); // 投影體從物件位置開始往前延伸
            proj.scaleMode = DecalScaleMode.InheritFromHierarchy; // DirtManager 靠 localScale 做隨機縮放
            PrefabUtility.SaveAsPrefabAsset(go, dstPath);
            Object.DestroyImmediate(go);
            return AssetDatabase.LoadAssetAtPath<GameObject>(dstPath);
        }

        static Material GetOrCreateDecalMaterial(Material srcMat, Shader shader,
                                                 Dictionary<Material, Material> cache)
        {
            if (cache.TryGetValue(srcMat, out var cached)) return cached;

            var matPath = $"{DecalMatFolder}/M_Decal_{srcMat.name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, matPath);
            }
            else
            {
                mat.shader = shader;
            }

            var tex = GetMainTexture(srcMat);
            if (tex == null)
                Debug.LogWarning($"SauceDecalBuilder: {srcMat.name} 沒有貼圖,{matPath} 請手動指定 Base Map。");
            else if (!AssignBaseTexture(mat, tex))
                Debug.LogWarning($"SauceDecalBuilder: {matPath} 找不到 Base Map 屬性,請手動指定貼圖。");

            EditorUtility.SetDirty(mat);
            cache[srcMat] = mat;
            return mat;
        }

        static Texture GetMainTexture(Material m)
        {
            if (m.mainTexture != null) return m.mainTexture;
            return m.GetTexturePropertyNames().Select(m.GetTexture).FirstOrDefault(t => t != null);
        }

        static bool AssignBaseTexture(Material mat, Texture tex)
        {
            var names = mat.GetTexturePropertyNames();
            var slot = names.FirstOrDefault(n => n.ToLowerInvariant().Contains("base"))
                       ?? names.FirstOrDefault();
            if (slot == null) return false;
            mat.SetTexture(slot, tex);
            return true;
        }

        // ── 場景接線 ─────────────────────────────────────────────

        static void AssignToDirtManager(List<GameObject> decals)
        {
            var dm = Object.FindFirstObjectByType<DirtManager>(FindObjectsInactive.Include);
            if (dm == null)
            {
                Debug.LogWarning("SauceDecalBuilder: 目前場景沒有 DirtManager(請開 BackBone.unity 再跑一次),髒污池請手動指定。");
                return;
            }

            Undo.RecordObject(dm, "Assign sauce decals");
            string[] flavorKeys = { "Margherita", "Pepperoni", "PinkMM" }; // 對應 PizzaFlavor 列舉順序
            dm.flavorSplats = flavorKeys
                .Select(k => new FlavorSplatSet { prefabs = decals.Where(d => d.name.Contains(k)).ToArray() })
                .ToArray();
            dm.splatPrefabs = decals.ToArray();
            EditorUtility.SetDirty(dm);
            EditorSceneManager.MarkSceneDirty(dm.gameObject.scene);
            Debug.Log($"SauceDecalBuilder: 已把 {dm.name} 的髒污池換成 Decal 版(記得存場景)。");
        }

        static void EnsureFolder(string parent, string child)
        {
            if (!AssetDatabase.IsValidFolder($"{parent}/{child}"))
                AssetDatabase.CreateFolder(parent, child);
        }
    }
}
