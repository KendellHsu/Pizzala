// ─────────────────────────────────────────────────────────────
// DirtManager.cs — 在失誤落點生成醬汁髒污
// 掛載:"Systems" 物件。
// Inspector:
//   splatPrefabs — 髒污 Prefab 陣列(隨機挑一個生成)
// 髒污 Prefab 做法(美術組 + 遊戲組合作):
//   方案 A(建議):URP Decal Projector + 醬汁貼圖
//     ※ 需在 Mobile_Renderer 加 Decal Renderer Feature(見 SETUP.md)
//   方案 B(保底):一片 Quad + 透明醬汁貼圖(不用改 renderer 設定)
// ─────────────────────────────────────────────────────────────
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

        public int DirtCount { get; private set; }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        public void SpawnSplat(Vector3 point, Vector3 normal,
                               PizzaFlavor? flavor = null, Transform parent = null)
        {
            var pool = PickPool(flavor);
            if (pool == null || pool.Length == 0) { DirtCount++; return; }

            var prefab = pool[Random.Range(0, pool.Length)];
            // Decal Projector 朝 -normal 投影;Quad 版本 Prefab 的正面朝 +Z 即可通用
            var rot = Quaternion.LookRotation(-normal)
                      * Quaternion.Euler(0f, 0f, Random.Range(0f, 360f)); // 隨機旋轉增加變化
            var go = Instantiate(prefab, point + normal * surfaceOffset, rot);
            go.transform.localScale *= Random.Range(0.8f, 1.3f);
            if (parent != null) go.transform.SetParent(parent, true); // 砸中客人時跟著客人

            DirtCount++;
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
