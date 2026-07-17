// ─────────────────────────────────────────────────────────────
// DirtManager.cs — 在失誤落點生成醬汁髒污
// 掛載:"Systems" 物件。
// Inspector:
//   splatPrefabs — 髒污 Prefab 陣列(隨機挑一個生成)
// 髒污 Prefab 做法(美術組 + 遊戲組合作):
//   方案 A(現行):URP Decal Projector + 醬汁貼圖,會包覆身體/斜面
//     ※ 一鍵設定:選單 Tools > Pizzala > Convert Sauce Splats To Decals
//       (自動加 Decal Renderer Feature、產 Decal prefab、接回本元件)
//   方案 B(保底):一片 Quad + 透明醬汁貼圖(不用改 renderer 設定)
// 液體感:命中時另外噴出數顆液滴(SauceDroplet),落地各生成
//   一塊縮小髒污,讓醬料潑灑到地面;砸中客人/陡面時液滴更多。
// 砸中客人:優先 texture-space 染色(SaucePaintable,把醬料畫進
//   角色貼圖的 UV 空間,完全跟著蒙皮動畫);染不了才退回 Decal 掛骨頭。
// ─────────────────────────────────────────────────────────────
using System.Collections.Generic;
using UnityEngine;
using Pizzala.Data;

namespace Pizzala.Dirt
{
    // 一種口味一組髒污 Prefab(Inspector 用)
    [System.Serializable]
    public class FlavorSplatSet
    {
        public GameObject[] prefabs;
    }

    public class DirtManager : MonoBehaviour
    {
        public static DirtManager Instance { get; private set; }

        [Tooltip("依 PizzaFlavor 列舉順序:0=Margherita 1=Pepperoni 2=CosmicPinkMarshmallow;該口味有填就優先從中挑")]
        public FlavorSplatSet[] flavorSplats;

        [Tooltip("不分口味的後備陣列(口味未知或上面沒填時用)")]
        public GameObject[] splatPrefabs;

        [Tooltip("避免 z-fighting,沿法線抬起的距離")]
        public float surfaceOffset = 0.01f;

        [Header("液體噴濺")]
        [Tooltip("命中平面/牆面時噴出的液滴數(隨機取 x~y 顆)")]
        public Vector2Int dropletCount = new Vector2Int(3, 5);

        [Tooltip("砸中客人或陡面時的液滴數(醬料大多流到地上)")]
        public Vector2Int dropletCountSteep = new Vector2Int(6, 9);

        [Tooltip("液滴初速範圍 (m/s)")]
        public Vector2 dropletSpeed = new Vector2(1.5f, 3.5f);

        [Tooltip("液滴直徑 (m)")]
        public float dropletSize = 0.045f;

        [Tooltip("液滴落地生成的小髒污相對縮放")]
        public float dropletSplatScale = 0.35f;

        [Tooltip("砸中客人時貼在身上的髒污縮小比例(其餘醬料變成液滴灑到地上)")]
        public float characterSplatScale = 0.55f;

        [Tooltip("砸中客人時把髒污掛到離命中點最近的骨頭上,跟著動畫走(關掉 = 掛根物件,走路會滑動穿幫)")]
        public bool attachToNearestBone = true;

        [Tooltip("客人身上的 Decal 投影深度縮放,越小越不會誤染揮過投影框的手臂(染色失敗退回 Decal 時用)")]
        public float characterDecalDepthScale = 0.5f;

        [Header("Texture-Space 染色(砸中客人)")]
        [Tooltip("砸中客人時直接把醬料畫進角色貼圖(UV 空間),完全跟著動畫;失敗才退回 Decal 掛骨頭")]
        public bool paintOnCharacters = true;

        [Tooltip("畫進角色貼圖的醬料直徑範圍(公尺,隨機取)")]
        public Vector2 paintSize = new Vector2(0.28f, 0.42f);

        [Tooltip("染色沿命中法線的作用厚度(公尺),太大會染穿到身體另一側")]
        public float paintWrapDepth = 0.25f;

        [Tooltip("normal.y 低於此值視為陡面,改用較多液滴")]
        [Range(0f, 1f)] public float steepThreshold = 0.6f;

        [Tooltip("液滴落地判定的圖層")]
        public LayerMask dropletHitMask = ~0;

        [Tooltip("液滴顏色,依 PizzaFlavor 列舉順序;口味未知用第 0 個")]
        public Color[] flavorDropletColors =
        {
            new Color(0.75f, 0.12f, 0.05f), // Margherita 番茄紅
            new Color(0.55f, 0.08f, 0.03f), // Pepperoni 深紅
            new Color(1.00f, 0.45f, 0.70f), // CosmicPinkMarshmallow 粉紅
        };

        [Tooltip("(選填)自訂液滴 Prefab;留空則執行期用小球體代替")]
        public GameObject dropletPrefab;

        public int DirtCount { get; private set; }

        Mesh sphereMesh;
        Material[] dropletMats;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            Debug.Log($"[DirtManager] Awake:染色版已載入,paintOnCharacters={paintOnCharacters}, ShaderAvailable={SaucePaintable.ShaderAvailable}");
        }

        public void SpawnSplat(Vector3 point, Vector3 normal,
                               PizzaFlavor? flavor = null, Transform parent = null)
        {
            bool onCharacter = parent != null;
            bool steep = normal.y < steepThreshold;

            Debug.Log($"[DirtManager] SpawnSplat 被呼叫:parent={(parent != null ? parent.name : "null(環境/牆面)")}, onCharacter={onCharacter}");

            // 砸中客人優先 texture-space 染色;染不了(沒 SkinnedMeshRenderer、
            // shader 缺、醬料貼圖抽不到)才退回 Decal 掛骨頭
            bool painted = onCharacter && paintOnCharacters
                           && TryPaintCharacter(parent, point, normal, flavor);
            if (onCharacter)
                Debug.Log($"[DirtManager] 染色結果 painted={painted}(false=走 Decal 後備)");
            if (!painted)
                SpawnSplatVisual(point, normal, flavor, parent, onCharacter ? characterSplatScale : 1f);

            var count = (onCharacter || steep) ? dropletCountSteep : dropletCount;
            SpawnDroplets(point, normal, flavor, Random.Range(count.x, count.y + 1));

            DirtCount++;
        }

        // 純視覺髒污(液滴落地、滑行痕跡用):不噴新液滴、不計入 DirtCount
        public void SpawnSplatMark(Vector3 point, Vector3 normal,
                                   PizzaFlavor? flavor, float scaleMul)
        {
            SpawnSplatVisual(point, normal, flavor, null, scaleMul);
        }

        // 飛行中甩出的液滴(SauceSpray 用):給定位置與初速直接發射,
        // 落地生成縮小髒污,不計入 DirtCount
        public void SpawnFlightDroplet(Vector3 position, Vector3 velocity,
                                       PizzaFlavor? flavor, float splatScale)
        {
            var go = CreateDropletInstance(flavor);
            go.transform.position = position;
            go.transform.localScale = new Vector3(dropletSize, dropletSize, dropletSize * 1.8f);

            var droplet = go.GetComponent<SauceDroplet>();
            if (droplet == null) droplet = go.AddComponent<SauceDroplet>();
            droplet.Launch(velocity, flavor, dropletHitMask, splatScale);
        }

        void SpawnSplatVisual(Vector3 point, Vector3 normal,
                              PizzaFlavor? flavor, Transform parent, float scaleMul)
        {
            var pool = PickPool(flavor);
            if (pool == null || pool.Length == 0) return;

            var prefab = pool[Random.Range(0, pool.Length)];
            // Decal Projector 朝 -normal 投影;Quad 版本 Prefab 的正面朝 +Z 即可通用
            var rot = Quaternion.LookRotation(-normal)
                      * Quaternion.Euler(0f, 0f, Random.Range(0f, 360f)); // 隨機旋轉增加變化
            var go = Instantiate(prefab, point + normal * surfaceOffset, rot);
            go.transform.localScale *= Random.Range(0.8f, 1.3f) * scaleMul;
            if (parent != null) // 砸中客人時跟著客人
            {
                // 掛到最近的骨頭,動畫(走路/丟回)時髒污才不會在身上滑動
                var anchor = attachToNearestBone ? FindNearestBone(parent, point) : parent;
                var ls = go.transform.localScale;
                ls.z *= characterDecalDepthScale; // 縮短投影深度,減少誤染揮過的手臂
                go.transform.localScale = ls;
                go.transform.SetParent(anchor, true);
            }
        }

        // ── Texture-space 染色 ───────────────────────────────────

        readonly Dictionary<int, Texture[]> paintTexCache = new Dictionary<int, Texture[]>();

        bool TryPaintCharacter(Transform parent, Vector3 point, Vector3 normal, PizzaFlavor? flavor)
        {
            if (!SaucePaintable.ShaderAvailable)
            {
                Debug.LogWarning("[DirtManager] 染色 shader 載入失敗(Resources/Shaders/SauceUnwrapPaint,看 Console 有無 shader 編譯錯誤),退回 Decal");
                return false;
            }
            var textures = GetPaintTextures(flavor);
            if (textures.Length == 0)
            {
                Debug.LogWarning("[DirtManager] 從髒污 Prefab 抽不到醬料貼圖,退回 Decal");
                return false;
            }

            var paintable = parent.GetComponent<SaucePaintable>();
            if (paintable == null) paintable = parent.gameObject.AddComponent<SaucePaintable>();

            var tex = textures[Random.Range(0, textures.Length)];
            bool ok = paintable.Paint(point, normal, tex,
                                      Random.Range(paintSize.x, paintSize.y), paintWrapDepth);
            if (!ok)
                Debug.LogWarning($"[DirtManager] {parent.name} 身上找不到可染色的 Renderer,退回 Decal");
            return ok;
        }

        // 從髒污 Prefab 的材質抽醬料貼圖(Decal 版或 Quad 版都通),每口味只掃一次
        Texture[] GetPaintTextures(PizzaFlavor? flavor)
        {
            int key = flavor.HasValue ? (int)flavor.Value : -1;
            if (paintTexCache.TryGetValue(key, out var cached)) return cached;

            var list = new List<Texture>();
            var pool = PickPool(flavor);
            if (pool != null)
            {
                foreach (var prefab in pool)
                {
                    if (prefab == null) continue;
                    var proj = prefab.GetComponentInChildren<UnityEngine.Rendering.Universal.DecalProjector>();
                    var mat = proj != null ? proj.material
                              : prefab.GetComponentInChildren<Renderer>() != null
                                ? prefab.GetComponentInChildren<Renderer>().sharedMaterial : null;
                    var tex = ExtractBaseTexture(mat);
                    if (tex != null) list.Add(tex);
                }
            }
            var result = list.ToArray();
            paintTexCache[key] = result;
            return result;
        }

        static Texture ExtractBaseTexture(Material mat)
        {
            if (mat == null) return null;
            if (mat.mainTexture != null) return mat.mainTexture;
            foreach (var name in mat.GetTexturePropertyNames())
            {
                if (!name.ToLowerInvariant().Contains("base")) continue;
                var tex = mat.GetTexture(name);
                if (tex != null) return tex;
            }
            return null;
        }

        static Transform FindNearestBone(Transform root, Vector3 point)
        {
            Transform best = root;
            float bestSq = float.MaxValue;
            foreach (var smr in root.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var bones = smr.bones;
                if (bones == null) continue;
                foreach (var b in bones)
                {
                    if (b == null) continue;
                    float d = (b.position - point).sqrMagnitude;
                    if (d < bestSq) { bestSq = d; best = b; }
                }
            }
            return best;
        }

        void SpawnDroplets(Vector3 point, Vector3 normal, PizzaFlavor? flavor, int count)
        {
            for (int i = 0; i < count; i++)
            {
                // 以法線為主的半球散射,再加一點向上偏移讓液滴拋物線落地
                Vector3 dir = (normal * Random.Range(0.4f, 1f)
                               + Random.insideUnitSphere * 0.6f
                               + Vector3.up * 0.5f).normalized;

                var go = CreateDropletInstance(flavor);
                go.transform.position = point + normal * (surfaceOffset + dropletSize);
                // 沿速度方向拉長
                go.transform.localScale = new Vector3(dropletSize, dropletSize, dropletSize * 1.8f);

                var droplet = go.GetComponent<SauceDroplet>();
                if (droplet == null) droplet = go.AddComponent<SauceDroplet>();
                droplet.Launch(dir * Random.Range(dropletSpeed.x, dropletSpeed.y),
                               flavor, dropletHitMask, dropletSplatScale);
            }
        }

        GameObject CreateDropletInstance(PizzaFlavor? flavor)
        {
            if (dropletPrefab != null) return Instantiate(dropletPrefab);

            var go = new GameObject("SauceDroplet");
            go.AddComponent<MeshFilter>().sharedMesh = SphereMesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = GetDropletMaterial(flavor);
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return go;
        }

        Mesh SphereMesh
        {
            get
            {
                if (sphereMesh == null)
                    sphereMesh = Resources.GetBuiltinResource<Mesh>("New-Sphere.fbx");
                return sphereMesh;
            }
        }

        Material GetDropletMaterial(PizzaFlavor? flavor)
        {
            int idx = flavor.HasValue ? (int)flavor.Value : 0;
            if (flavorDropletColors == null || flavorDropletColors.Length == 0) idx = -1;
            else idx = Mathf.Clamp(idx, 0, flavorDropletColors.Length - 1);

            if (dropletMats == null)
                dropletMats = new Material[Mathf.Max(1, flavorDropletColors?.Length ?? 1)];

            int slot = Mathf.Max(0, idx);
            if (dropletMats[slot] == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Simple Lit");
                if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Standard");
                var mat = new Material(shader);
                var color = idx >= 0 ? flavorDropletColors[idx] : new Color(0.75f, 0.12f, 0.05f);
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
                if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.8f); // 濕潤感
                dropletMats[slot] = mat;
            }
            return dropletMats[slot];
        }

        GameObject[] PickPool(PizzaFlavor? flavor)
        {
            if (flavor.HasValue && flavorSplats != null && (int)flavor.Value < flavorSplats.Length)
            {
                var set = flavorSplats[(int)flavor.Value];
                if (set != null && set.prefabs != null && set.prefabs.Length > 0) return set.prefabs;
            }
            return splatPrefabs;
        }

        public void ResetCount() => DirtCount = 0;
    }
}
