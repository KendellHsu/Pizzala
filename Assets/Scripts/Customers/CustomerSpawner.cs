// ─────────────────────────────────────────────────────────────
// CustomerSpawner.cs — 客人動態生成(槽位制,移植自舊版原型)
// 掛載:場景根物件 "CustomerSpawner",放在玩家站位(原點)。
// Inspector:
//   customerPrefabs — 客人 Prefab 清單(多角色混合,生成時均勻隨機挑一個,
//                     例:PZ_Customer(Soldier)+ PZ_Customer_UncleB)
//   customerPrefab  — 單一客人 Prefab(舊欄位/備援);customerPrefabs 為空才用
//   sectorCount / sectorJitter / distanceTiers — 環形槽位幾何
//   maxSpawnedCustomers — 同時在場的生成客人上限(人口密度上限)
//   minSpacing — 與任何現有客人(含場景預擺)的最小間距(公尺)
// 生成節奏(initialSpawnCount、min/maxSpawnInterval、customerLifetime、
// customerLeaveDelay)讀 GameManager.tuning(ThrowTuning 資產)。
//
// 槽位 = 360° 均分 sectorCount 個扇區 × N 個距離層,環繞整個空間,
// 同一槽位同時只站一個客人,離場才釋放;再加 minSpacing 距離檢查與
// 在場上限,三重保證人口密度不會過擠。
// 客人的 TargetSector(左/中/右,方位統計用)由生成角度換算。
// 生成的客人透過 GameManager.RegisterCustomer 進訂單池,
// 訂單結束(滿意/生氣/超時)或閒置超過 customerLifetime 就離場讓位。
// 場景預擺的客人不佔槽位,由 minSpacing 距離檢查避開。
// ─────────────────────────────────────────────────────────────
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Pizzala.Core;
using Pizzala.Data;

namespace Pizzala.Customers
{
    [System.Serializable]
    public class SpawnDistanceTier
    {
        public float minRadius = 2.4f;
        public float maxRadius = 3f;

        public SpawnDistanceTier() { }
        public SpawnDistanceTier(float min, float max) { minRadius = min; maxRadius = max; }
    }

    public class CustomerSpawner : MonoBehaviour
    {
        [Header("客人 Prefab")]
        [Tooltip("客人 Prefab 清單,生成時均勻隨機挑一個(多角色混合)。留空則用下方單一 customerPrefab。")]
        public GameObject[] customerPrefabs;

        [Tooltip("單一客人 Prefab(舊欄位/備援);customerPrefabs 為空時才用。")]
        public GameObject customerPrefab;

        [Header("環形槽位(以本物件為圓心,360° 環繞整個空間)")]
        [Tooltip("整圈均分幾個扇區(越多槽位越密)")]
        public int sectorCount = 6;

        [Tooltip("扇區內的角度隨機抖動比例,0=永遠站扇區正中央")]
        [Range(0f, 0.5f)]
        public float sectorJitter = 0.35f;

        [Tooltip("距離層:每層一個半徑帶")]
        public SpawnDistanceTier[] distanceTiers =
        {
            new SpawnDistanceTier(2.4f, 3f),
            new SpawnDistanceTier(4.6f, 5.4f),
        };

        [Header("人口密度")]
        [Tooltip("同時在場的生成客人上限(不含場景預擺的)")]
        public int maxSpawnedCustomers = 6;

        [Tooltip("生成點與任何現有客人的最小間距(公尺),太近的槽位這輪跳過")]
        public float minSpacing = 1.5f;

        readonly HashSet<(int sector, int tier)> occupiedSlots = new HashSet<(int, int)>();

        IEnumerator Start()
        {
            while (true)
            {
                // 等 GameManager 就緒且回合開始
                while (GameManager.Instance == null || !GameManager.Instance.RoundActive)
                    yield return null;

                var tuning = GameManager.Instance.tuning;

                for (int i = 0; i < tuning.initialSpawnCount; i++)
                    SpawnAtRandomFreeSlot();

                while (GameManager.Instance.RoundActive)
                {
                    yield return new WaitForSeconds(
                        Random.Range(tuning.minSpawnInterval, tuning.maxSpawnInterval));
                    if (GameManager.Instance.RoundActive)
                        SpawnAtRandomFreeSlot();
                }
                // 回合結束:停止生成,場上客人各自跑完生命週期;等下一回合再來
            }
        }

        void SpawnAtRandomFreeSlot()
        {
            if (!HasAnyCustomerPrefab()) return;
            if (occupiedSlots.Count >= maxSpawnedCustomers) return; // 人口上限,這輪不生

            var freeSlots = new List<(int sector, int tier)>();
            for (int s = 0; s < sectorCount; s++)
                for (int d = 0; d < distanceTiers.Length; d++)
                    if (!occupiedSlots.Contains((s, d)))
                        freeSlots.Add((s, d));

            // 隨機順序逐一試,挑到第一個離所有現有客人夠遠的槽位;都太擠就這輪跳過
            for (int i = freeSlots.Count - 1; i >= 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (freeSlots[i], freeSlots[j]) = (freeSlots[j], freeSlots[i]);

                var slot = freeSlots[i];
                ComputeSpawnPose(slot.sector, slot.tier, out var position, out var rotation);
                if (!IsFarEnoughFromEveryone(position)) continue;

                SpawnCustomer(slot.sector, slot.tier, position, rotation);
                return;
            }
        }

        // 人口密度檢查:與場上所有客人(含場景預擺)保持 minSpacing 以上
        bool IsFarEnoughFromEveryone(Vector3 position)
        {
            foreach (var c in GameManager.Instance.ActiveCustomers)
            {
                if (c == null) continue;
                Vector3 d = c.transform.position - position;
                d.y = 0f;
                if (d.sqrMagnitude < minSpacing * minSpacing) return false;
            }
            return true;
        }

        void SpawnCustomer(int sector, int tier, Vector3 position, Quaternion rotation)
        {
            var prefab = PickCustomerPrefab();
            if (prefab == null) return; // 理論上 HasAnyCustomerPrefab 已擋掉

            occupiedSlots.Add((sector, tier));

            var go = Instantiate(prefab, position, rotation, transform);

            var customer = go.GetComponent<CustomerController>();
            if (customer == null)
            {
                Debug.LogError($"[CustomerSpawner] {prefab.name} 上找不到 CustomerController");
                Destroy(go);
                occupiedSlots.Remove((sector, tier));
                return;
            }

            customer.sector = ClassifySector(position);
            GameManager.Instance.RegisterCustomer(customer); // 指派 customerId、訂閱事件、推移動參數
            go.name = $"{prefab.name} (spawned {customer.customerId})";

            StartCoroutine(LifeRoutine(customer, sector, tier));
        }

        // 生成用候選:優先用 customerPrefabs(蓄水池抽樣均勻隨機,忽略 null),
        // 清單為空才退回單一 customerPrefab。
        GameObject PickCustomerPrefab()
        {
            GameObject pick = null;
            if (customerPrefabs != null)
            {
                int seen = 0;
                foreach (var p in customerPrefabs)
                {
                    if (p == null) continue;
                    seen++;
                    if (Random.Range(0, seen) == 0) pick = p;
                }
            }
            return pick != null ? pick : customerPrefab;
        }

        bool HasAnyCustomerPrefab()
        {
            if (customerPrefab != null) return true;
            if (customerPrefabs != null)
                foreach (var p in customerPrefabs)
                    if (p != null) return true;
            return false;
        }

        // 在扇區角度與半徑帶內隨機散佈,不是死板格點。
        // 公開是為了不進 Play Mode 也能驗證擺位數學。
        public void ComputeSpawnPose(int sector, int tier, out Vector3 position, out Quaternion rotation)
        {
            float sectorWidth = 360f / sectorCount;
            float angle = sector * sectorWidth
                          + Random.Range(-sectorWidth * sectorJitter, sectorWidth * sectorJitter);
            float radius = Random.Range(distanceTiers[tier].minRadius, distanceTiers[tier].maxRadius);

            Vector3 dir = Quaternion.Euler(0f, angle, 0f) * transform.forward;
            position = transform.position + dir * radius;
            rotation = Quaternion.LookRotation(-dir); // 背對外圈 = 面向玩家
        }

        // 由生成位置換算方位統計用的 TargetSector:
        // 相對正前方 ±60° 內算中路,右邊算右路,左邊算左路(正後方依左右半邊歸類)
        public TargetSector ClassifySector(Vector3 position)
        {
            Vector3 to = position - transform.position;
            float signedAngle = Vector3.SignedAngle(transform.forward, to, Vector3.up);
            if (Mathf.Abs(signedAngle) <= 60f) return TargetSector.Center;
            return signedAngle > 0f ? TargetSector.Right : TargetSector.Left;
        }

        // 生命週期:訂單結束(滿意/生氣/超時)→ 停留一下讓玩家看表情 → 離場;
        // 一直沒接到訂單的話,閒置超過 customerLifetime 也會離場讓位。
        IEnumerator LifeRoutine(CustomerController customer, int sector, int tier)
        {
            var tuning = GameManager.Instance.tuning;

            bool orderFinished = false;
            System.Action<CustomerController, bool> onResolved = (_, _2) => orderFinished = true;
            System.Action<CustomerController> onTimeout = _ => orderFinished = true;
            customer.OnOrderResolved += onResolved;
            customer.OnOrderTimeout += onTimeout;

            float idleDeadline = Time.time + tuning.customerLifetime;
            while (customer != null && !orderFinished
                   && !(Time.time >= idleDeadline && !customer.HasActiveOrder))
                yield return null;

            if (customer != null)
            {
                customer.OnOrderResolved -= onResolved;
                customer.OnOrderTimeout -= onTimeout;
                customer.IsLeaving = true; // 不再接新訂單
                yield return new WaitForSeconds(tuning.customerLeaveDelay);
            }

            occupiedSlots.Remove((sector, tier));
            if (customer != null)
            {
                if (GameManager.Instance != null)
                    GameManager.Instance.UnregisterCustomer(customer);
                Destroy(customer.gameObject);
            }
        }
    }
}
