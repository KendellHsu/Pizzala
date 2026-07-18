using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Pizzala.Customers;

namespace Pizzala.EditorTools
{
    // 為統一 Armature 骨架的每個 NPC 產生/更新一個客人 Prefab(PZ_Customer 的 Variant)。
    // 前置:先跑 Tools/Pizzala/Setup NPC Animations,把每個 <name>_animation.fbx 切好
    //       standing/walking/throwing 三 clip 並產生 <name>_animation_animator.controller。
    //
    // 每個角色一鍵:
    //   1. Instantiate 基底 PZ_Customer(保留與基底的連結 → 存成 Variant)。
    //   2. 把基底的舊模型換成該角色的 _animation.fbx 模型(一律加成根的子物件,自帶骨架);
    //      基底舊模型在子物件(UncleB 式)或直接在根上(原始 Soldier 基底)兩種都支援。
    //      沿用原模型的 local 位置/旋轉/層級(縮放用該 fbx 匯入尺度)。
    //   3. 模型上的單一 Animator 掛該角色的 controller,Apply Root Motion = off。
    //   4. CustomerController.animatorController = 該角色 controller。
    //   5. SaveAsPrefabAssetAndConnect 存到 Assets/Prefabs/Customer/PZ_Customer_<Name>.prefab。
    //   6. 把場景 CustomerSpawner.customerPrefabs 設為全部角色(混合隨機生成)。
    //
    // 可重複執行:prefab 原地覆蓋。體型差異(站姿貼地、Zone 碰撞框、ThrowOrigin/FaceAnchor 高度)
    // 仍需在 Unity 裡人工驗收微調——見各 prefab。
    public static class NpcCustomerBuilder
    {
        const string BasePrefabPath = "Assets/Prefabs/Customer/PZ_Customer.prefab";
        const string OutDir         = "Assets/Prefabs/Customer";
        const string NpcRoot        = "Assets/Art/NPC";

        [MenuItem("Tools/Pizzala/Build NPC Customer Prefabs")]
        public static void BuildAll()
        {
            var basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BasePrefabPath);
            if (basePrefab == null)
            { Debug.LogError($"[NpcCustomerBuilder] 找不到基底 {BasePrefabPath}"); return; }

            var fbxPaths = AssetDatabase.FindAssets("t:Model", new[] { NpcRoot })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => p.EndsWith("_animation.fbx"))
                .Distinct()
                .OrderBy(p => p)
                .ToList();

            if (fbxPaths.Count == 0)
            { Debug.LogError($"[NpcCustomerBuilder] {NpcRoot} 底下找不到 *_animation.fbx。"); return; }

            var built = new List<GameObject>();
            foreach (var fbxPath in fbxPaths)
            {
                var outPath = BuildOne(basePrefab, fbxPath);
                if (outPath != null)
                {
                    var go = AssetDatabase.LoadAssetAtPath<GameObject>(outPath);
                    if (go != null) built.Add(go);
                }
            }

            AssetDatabase.SaveAssets();
            WireSpawner(built);
            Debug.Log($"[NpcCustomerBuilder] 完成:{built.Count}/{fbxPaths.Count} 個客人 prefab。" +
                      "記得在 Unity 裡人工驗收站姿貼地與碰撞框,並存場景(Ctrl+S)。");
        }

        // 回傳產生的 prefab 路徑;失敗回 null。
        static string BuildOne(GameObject basePrefab, string fbxPath)
        {
            // cowboy_animation → CharName "cowboy";產出 PZ_Customer_cowboy.prefab
            string fbxName = Path.GetFileNameWithoutExtension(fbxPath);            // cowboy_animation
            string charName = fbxName.EndsWith("_animation")
                ? fbxName.Substring(0, fbxName.Length - "_animation".Length)
                : fbxName;
            string ctrlPath = $"{Path.GetDirectoryName(fbxPath).Replace('\\', '/')}/{fbxName}_animator.controller";
            string outPath  = $"{OutDir}/PZ_Customer_{charName}.prefab";

            var modelFbx = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            var ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ctrlPath);
            if (modelFbx == null)
            { Debug.LogError($"[NpcCustomerBuilder] {charName}: 找不到模型 {fbxPath}"); return null; }
            if (ctrl == null)
            { Debug.LogError($"[NpcCustomerBuilder] {charName}: 找不到 controller {ctrlPath}(先跑 Setup NPC Animations)"); return null; }

            var previewScene = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab, previewScene);

                var cc = root.GetComponent<CustomerController>();
                if (cc == null)
                { Debug.LogError($"[NpcCustomerBuilder] {charName}: 基底根物件沒有 CustomerController。"); return null; }

                // 基底的舊模型有兩種擺法:
                //  (A) 掛在「非根」子物件上(帶 Animator)——UncleB variant 那種;
                //  (B) 直接掛在根物件上(SkinnedMeshRenderer + Animator 都在 root)——原始 Soldier 基底那種。
                // 統一做法:新模型 FBX 一律以「根的子物件」加入(自帶 Animator/骨架),
                //           位置沿用舊模型(A 取子物件、B 取根),再把舊模型清乾淨。
                var oldModelAnim = root.GetComponentsInChildren<Animator>(true)
                                       .FirstOrDefault(a => a.gameObject != root);

                Transform parent; Vector3 lp; Quaternion lr; int sib;
                if (oldModelAnim != null)
                {
                    // (A) 換掉非根的舊模型子物件
                    var st = oldModelAnim.transform;
                    parent = st.parent; lp = st.localPosition; lr = st.localRotation; sib = st.GetSiblingIndex();
                }
                else
                {
                    // (B) 舊模型在根上:新模型放在根底下,沿用原點。
                    parent = root.transform; lp = Vector3.zero; lr = Quaternion.identity; sib = 0;
                    // 移除根上失效的 SkinnedMeshRenderer(mesh 已隨舊 FBX 被刪)與根 Animator,
                    // 否則 CustomerController.Awake 的 GetComponentInChildren<Animator> 會先抓到根的空 Animator。
                    var rootSmr = root.GetComponent<SkinnedMeshRenderer>();
                    if (rootSmr != null) Object.DestroyImmediate(rootSmr);
                    var rootMf = root.GetComponent<MeshFilter>();
                    if (rootMf != null) Object.DestroyImmediate(rootMf);
                    var rootMr = root.GetComponent<MeshRenderer>();
                    if (rootMr != null) Object.DestroyImmediate(rootMr);
                    var rootAnim = root.GetComponent<Animator>();
                    if (rootAnim != null) Object.DestroyImmediate(rootAnim);
                }

                var model = (GameObject)PrefabUtility.InstantiatePrefab(modelFbx, parent);
                model.name = $"{charName}_Model";
                model.transform.localPosition = lp;
                model.transform.localRotation = lr;
                // localScale 用 fbx 匯入尺度(各角色體型不同,不套基底縮放)
                model.transform.SetSiblingIndex(sib);

                var anim = model.GetComponent<Animator>();
                if (anim == null) anim = model.AddComponent<Animator>();
                anim.runtimeAnimatorController = ctrl;
                anim.applyRootMotion = false;

                if (oldModelAnim != null)
                    Object.DestroyImmediate(oldModelAnim.gameObject); // 移除非根的舊模型子物件

                cc.animatorController = ctrl;

                PrefabUtility.SaveAsPrefabAssetAndConnect(root, outPath, InteractionMode.AutomatedAction);
                Debug.Log($"[NpcCustomerBuilder] 已產生 {outPath}(PZ_Customer 的 Variant)");
                return outPath;
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(previewScene);
            }
        }

        static void WireSpawner(List<GameObject> prefabs)
        {
            if (prefabs.Count == 0) return;
            var spawner = Object.FindFirstObjectByType<CustomerSpawner>(FindObjectsInactive.Include);
            if (spawner == null)
            {
                Debug.LogWarning("[NpcCustomerBuilder] 目前場景沒有 CustomerSpawner(請開 BackBone.unity 再跑一次)," +
                                 "請手動把產生的 PZ_Customer_* 加進 customerPrefabs。");
                return;
            }
            Undo.RecordObject(spawner, "Wire NPC customers into CustomerSpawner");
            spawner.customerPrefabs = prefabs.ToArray();
            EditorUtility.SetDirty(spawner);
            EditorSceneManager.MarkSceneDirty(spawner.gameObject.scene);
            Debug.Log($"[NpcCustomerBuilder] 已把 CustomerSpawner.customerPrefabs 設為 {prefabs.Count} 個角色(記得存場景 Ctrl+S)。");
        }
    }
}
