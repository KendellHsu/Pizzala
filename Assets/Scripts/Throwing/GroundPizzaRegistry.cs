// ─────────────────────────────────────────────────────────────
// GroundPizzaRegistry.cs — 地上可撿披薩登記表(static)
// 玩家丟偏、落在環境上的 PizzaProjectile 會登記進來,供超時的客人撿起丟回。
// 落在排除區(PickupExclusionZone,例:出餐台、玩家周圍)內的不算可撿。
// PizzaProjectile 在落地(命中環境)時 Register、銷毀時 Unregister。
// ─────────────────────────────────────────────────────────────
using System.Collections.Generic;
using UnityEngine;

namespace Pizzala.Throwing
{
    public static class GroundPizzaRegistry
    {
        static readonly List<PizzaProjectile> pizzas = new List<PizzaProjectile>();

        public static void Register(PizzaProjectile p)
        {
            if (p != null && !pizzas.Contains(p)) pizzas.Add(p);
        }

        public static void Unregister(PizzaProjectile p)
        {
            pizzas.Remove(p);
        }

        // 找離 from 最近、且不在任何排除區內的可撿披薩;都沒有就回 null。
        public static PizzaProjectile FindNearestPickable(Vector3 from)
        {
            PizzaProjectile best = null;
            float bestSqr = float.MaxValue;
            for (int i = pizzas.Count - 1; i >= 0; i--)
            {
                var p = pizzas[i];
                if (p == null) { pizzas.RemoveAt(i); continue; }        // 已被銷毀,順手清掉
                if (PickupExclusionZone.Contains(p.transform.position)) continue; // 出餐台/玩家圈內不撿

                float d = (p.transform.position - from).sqrMagnitude;
                if (d < bestSqr) { bestSqr = d; best = p; }
            }
            return best;
        }
    }
}
