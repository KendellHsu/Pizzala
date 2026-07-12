// ─────────────────────────────────────────────────────────────
// CustomerHitZone.cs — 客人身上的命中區域標記
// 掛載:客人 Prefab 底下的三個子物件,各掛一個:
//   Hand — 手掌前一顆 Sphere Collider(Is Trigger 打勾,半徑約 0.15)
//   Face — 臉部前一顆 Sphere Collider(Is Trigger 打勾,半徑約 0.12)
//   Body — 身體 Collider(不勾 Trigger,披薩會實際砸到彈開)
// Inspector:zone 選對應類型,customer 拖入客人根物件的 CustomerController。
// ─────────────────────────────────────────────────────────────
using UnityEngine;

namespace Pizzala.Customers
{
    public enum HitZoneType { Hand, Face, Body }

    public class CustomerHitZone : MonoBehaviour
    {
        public HitZoneType zone;
        public CustomerController customer;

        void Awake()
        {
            if (customer == null) customer = GetComponentInParent<CustomerController>();
        }
    }
}
