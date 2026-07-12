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

namespace Pizzala.Dirt
{
    public class DirtManager : MonoBehaviour
    {
        public static DirtManager Instance { get; private set; }

        public GameObject[] splatPrefabs;

        [Tooltip("避免 z-fighting,沿法線抬起的距離")]
        public float surfaceOffset = 0.01f;

        public int DirtCount { get; private set; }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        public void SpawnSplat(Vector3 point, Vector3 normal)
        {
            if (splatPrefabs == null || splatPrefabs.Length == 0) { DirtCount++; return; }

            var prefab = splatPrefabs[Random.Range(0, splatPrefabs.Length)];
            // Decal Projector 朝 -normal 投影;Quad 版本 Prefab 的正面朝 +Z 即可通用
            var rot = Quaternion.LookRotation(-normal)
                      * Quaternion.Euler(0f, 0f, Random.Range(0f, 360f)); // 隨機旋轉增加變化
            var go = Instantiate(prefab, point + normal * surfaceOffset, rot);
            go.transform.localScale *= Random.Range(0.8f, 1.3f);

            DirtCount++;
        }

        public void ResetCount() => DirtCount = 0;
    }
}
