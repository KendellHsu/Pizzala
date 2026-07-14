// ─────────────────────────────────────────────────────────────
// PizzaProjectile.cs — 披薩本體:抓取、放手分類、落地判定
// 掛載:披薩 Prefab 根物件。
// Prefab 需求:
//   Rigidbody(Collision Detection 設 Continuous Dynamic,快速飛行防穿牆)
//   XR Grab Interactable(Movement Type = Velocity Tracking,
//     Throw On Detach 打勾,Smooth Position 建議打勾)
//   一顆 Collider(圓盤形可用壓扁的 Box 或 Capsule)
// Inspector:flavor 設定這顆披薩的口味。
// ─────────────────────────────────────────────────────────────
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using Pizzala.Data;
using Pizzala.Core;
using Pizzala.Customers;

namespace Pizzala.Throwing
{
    [RequireComponent(typeof(Rigidbody))]
    public class PizzaProjectile : MonoBehaviour
    {
        public PizzaFlavor flavor;

        public bool WasThrown { get; private set; }

        XRGrabInteractable grab;
        HandMotionSampler currentSampler;
        ThrowRecord record;
        bool inFlight;
        float releaseTime;

        const float ArmDelay = 0.05f; // 放手後短暫豁免,避免和手/桌面誤判

        void Awake()
        {
            grab = GetComponent<XRGrabInteractable>();
            if (grab != null)
            {
                grab.selectEntered.AddListener(OnGrabbed);
                grab.selectExited.AddListener(OnReleased);
            }
            else
            {
                Debug.LogError($"[PizzaProjectile] {name} 缺 XRGrabInteractable,請在 Prefab 上加。");
            }
        }

        void OnGrabbed(SelectEnterEventArgs args)
        {
            // 從抓取者(控制器)身上找運動取樣器
            currentSampler = args.interactorObject.transform.GetComponentInParent<HandMotionSampler>();
            inFlight = false;
        }

        void OnReleased(SelectExitEventArgs args)
        {
            WasThrown = true;
            inFlight = true;
            releaseTime = Time.time;

            if (GameManager.Instance != null)
                record = GameManager.Instance.OnPizzaReleased(this, currentSampler);
        }

        void OnCollisionEnter(Collision c)
        {
            var contact = c.GetContact(0);
            Resolve(c.collider, contact.point, contact.normal);
        }

        void OnTriggerEnter(Collider other)
        {
            // 客人手掌/臉是 trigger,走這條路
            Resolve(other, transform.position, Vector3.up);
        }

        void Resolve(Collider col, Vector3 point, Vector3 normal)
        {
            if (!inFlight || Time.time - releaseTime < ArmDelay) return;

            // 忽略碰到玩家自己的頭
            if (col.GetComponentInParent<PlayerHeadHitbox>() != null) return;

            var zone = col.GetComponentInParent<CustomerHitZone>();
            inFlight = false;

            if (GameManager.Instance != null && record != null)
                GameManager.Instance.OnPizzaLanded(this, record, zone, point, normal, Time.time - releaseTime);

            record = null;
        }
    }
}
