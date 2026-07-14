// ─────────────────────────────────────────────────────────────
// ThrowbackProjectile.cs — 客人丟回來的披薩
// 掛載:丟回披薩 Prefab 根物件(可以直接複製玩家披薩 Prefab,
//   移除 XRGrabInteractable 和 PizzaProjectile,換掛這支)。
// Prefab 需求:Rigidbody + Collider(不勾 Trigger)。
// 關鍵設計:發射時鎖定「當下」的玩家頭部位置,不追蹤,
//   所以玩家側身/下蹲躲得掉。
// ─────────────────────────────────────────────────────────────
using UnityEngine;
using Pizzala.Core;
using Pizzala.Data;

namespace Pizzala.Customers
{
    [RequireComponent(typeof(Rigidbody))]
    public class ThrowbackProjectile : MonoBehaviour
    {
        public PizzaFlavor flavor; // 生成時由 GameManager 覆寫,落空髒污用
        public System.Action<bool> onResolved; // true = 砸中玩家臉
        bool resolved;
        float launchTime;

        const float Lifetime = 5f;

        public void Launch(Vector3 targetPos, float speed)
        {
            launchTime = Time.time;
            var rb = GetComponent<Rigidbody>();

            // 簡單拋物線補償:依飛行時間加一點向上初速抵銷重力
            Vector3 toTarget = targetPos - transform.position;
            float flightTime = toTarget.magnitude / Mathf.Max(speed, 0.1f);
            Vector3 velocity = toTarget / flightTime;
            velocity.y += 0.5f * Mathf.Abs(Physics.gravity.y) * flightTime;

            rb.linearVelocity = velocity;
            rb.angularVelocity = new Vector3(0f, 10f, 0f); // 轉一下比較像飛盤

            Destroy(gameObject, Lifetime);
        }

        void OnCollisionEnter(Collision c) => Resolve(c.collider, c.GetContact(0).point, c.GetContact(0).normal);
        void OnTriggerEnter(Collider other) => Resolve(other, transform.position, Vector3.up);

        void Resolve(Collider col, Vector3 point, Vector3 normal)
        {
            if (resolved || Time.time - launchTime < 0.1f) return;

            bool hitPlayer = col.GetComponentInParent<PlayerHeadHitbox>() != null;
            resolved = true;

            if (!hitPlayer && Pizzala.Dirt.DirtManager.Instance != null)
                Pizzala.Dirt.DirtManager.Instance.SpawnSplat(point, normal, flavor); // 閃過→牆上多一塊髒污

            onResolved?.Invoke(hitPlayer);
            Destroy(gameObject, hitPlayer ? 0f : 1.5f);
        }
    }
}
