// ─────────────────────────────────────────────────────────────
// SauceDroplet.cs — 醬汁液滴(由 DirtManager 在命中時噴出)
// 不掛 Rigidbody:自己積分重力 + 沿路徑 Raycast,落地時
// 請 DirtManager 生成一塊縮小的髒污,讓醬料看起來潑灑到地面。
// 由 DirtManager 於執行期動態建立,不需手動掛載。
// ─────────────────────────────────────────────────────────────
using UnityEngine;
using Pizzala.Data;

namespace Pizzala.Dirt
{
    public class SauceDroplet : MonoBehaviour
    {
        Vector3 velocity;
        PizzaFlavor? flavor;
        LayerMask hitMask;
        float splatScale;
        float life;

        const float MaxLife = 3f;   // 保險:一直沒落地就消失
        const float Skin = 0.02f;   // Raycast 額外前伸,避免穿過薄面

        public void Launch(Vector3 initialVelocity, PizzaFlavor? flavor,
                           LayerMask hitMask, float splatScale)
        {
            velocity = initialVelocity;
            this.flavor = flavor;
            this.hitMask = hitMask;
            this.splatScale = splatScale;
        }

        void Update()
        {
            life += Time.deltaTime;
            if (life > MaxLife) { Destroy(gameObject); return; }

            velocity += Physics.gravity * Time.deltaTime;
            Vector3 step = velocity * Time.deltaTime;

            // 忽略 Trigger,才不會被客人的 HitZone 擋下
            if (Physics.Raycast(transform.position, step.normalized, out var hit,
                                step.magnitude + Skin, hitMask, QueryTriggerInteraction.Ignore))
            {
                if (DirtManager.Instance != null)
                    DirtManager.Instance.SpawnSplatMark(hit.point, hit.normal, flavor, splatScale);
                Destroy(gameObject);
                return;
            }

            transform.position += step;
            // 沿速度方向拉長,看起來更像飛濺的液體
            if (velocity.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.LookRotation(velocity);
        }
    }
}
