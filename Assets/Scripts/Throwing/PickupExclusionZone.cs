// ─────────────────────────────────────────────────────────────
// PickupExclusionZone.cs — 「不可撿披薩」排除區(場景可擺放)
// 掛在場景物件上,物件需帶一個以上 Collider(勾 Is Trigger)。
// 在 Scene 視圖把 Collider 拖大小/位置,圈住不該被超時客人撿走的範圍
// (出餐台、玩家站位周圍的補貨圈等)。落在任一排除區內的地上披薩,
// GroundPizzaRegistry.FindNearestPickable 會略過。
// 同時也是「客人禁止進入區」:CustomerController.UpdateWander 抽遊走目標時,
// 會用 Contains() 把落在區內的點丟掉,客人不會遊走進來。
// 可擺多個;啟用/停用時自動進出清單。
// ─────────────────────────────────────────────────────────────
using System.Collections.Generic;
using UnityEngine;

namespace Pizzala.Throwing
{
    [RequireComponent(typeof(Collider))]
    public class PickupExclusionZone : MonoBehaviour
    {
        static readonly List<PickupExclusionZone> zones = new List<PickupExclusionZone>();

        Collider[] cols;

        void OnEnable()
        {
            cols = GetComponents<Collider>();
            if (!zones.Contains(this)) zones.Add(this);
        }

        void OnDisable() => zones.Remove(this);

        bool ContainsPoint(Vector3 p)
        {
            if (cols == null) return false;
            foreach (var c in cols)
            {
                // ClosestPoint 對凸形 collider(Box/Sphere/Capsule):點在內部時會回傳點本身
                if (c != null && c.enabled && (c.ClosestPoint(p) - p).sqrMagnitude < 1e-6f) return true;
            }
            return false;
        }

        // 世界座標是否落在任一啟用中的排除區內
        public static bool Contains(Vector3 worldPos)
        {
            foreach (var z in zones)
                if (z != null && z.ContainsPoint(worldPos)) return true;
            return false;
        }
    }
}
