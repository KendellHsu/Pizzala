// ─────────────────────────────────────────────────────────────
// SauceTrail.cs — 披薩落地後,彈跳/滑行沿路留下醬汁痕跡
// 由 PizzaProjectile / ThrowbackProjectile 在第一次結算後
// 動態 AddComponent 並 Activate,不需手動掛載。
// 髒污為純視覺(走 DirtManager.SpawnSplatMark),不計入 DirtCount。
// 「醬料存量」用完就不再留痕,痕跡隨存量遞減變小。
// ─────────────────────────────────────────────────────────────
using UnityEngine;
using Pizzala.Data;

namespace Pizzala.Dirt
{
    [RequireComponent(typeof(Rigidbody))]
    public class SauceTrail : MonoBehaviour
    {
        [Tooltip("滑行時每移動多遠留一塊痕跡 (m)")]
        public float spacing = 0.18f;

        [Tooltip("速度低於此值不留痕(視為靜止)")]
        public float minSpeed = 0.25f;

        [Tooltip("醬料存量:總共能留幾塊痕跡")]
        public int budget = 12;

        [Tooltip("第一塊痕跡的縮放,之後隨存量遞減到 minScale")]
        public float startScale = 0.5f;
        public float minScale = 0.15f;

        PizzaFlavor? flavor;
        Rigidbody rb;
        bool active;
        Vector3 lastMarkPos;
        int left;

        void Awake() => rb = GetComponent<Rigidbody>();

        public void Activate(PizzaFlavor? flavor)
        {
            this.flavor = flavor;
            active = true;
            left = budget;
            lastMarkPos = transform.position; // 落點已有主髒污,別馬上重疊補一塊
        }

        void OnCollisionEnter(Collision c) => TryMark(c, isImpact: true);
        void OnCollisionStay(Collision c)  => TryMark(c, isImpact: false);

        void TryMark(Collision c, bool isImpact)
        {
            if (!active || left <= 0 || DirtManager.Instance == null) return;

            // 滑行:隔一段距離、且還在動,才留下一塊
            if (!isImpact)
            {
                if ((transform.position - lastMarkPos).sqrMagnitude < spacing * spacing) return;
                if (rb.linearVelocity.sqrMagnitude < minSpeed * minSpeed) return;
            }

            var contact = c.GetContact(0);
            float scale = Mathf.Lerp(minScale, startScale, (float)left / budget);
            DirtManager.Instance.SpawnSplatMark(contact.point, contact.normal, flavor, scale);

            lastMarkPos = transform.position;
            left--;
        }
    }
}
