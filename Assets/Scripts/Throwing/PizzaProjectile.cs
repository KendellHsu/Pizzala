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

            var spray = GetComponent<Pizzala.Dirt.SauceSpray>();
            if (spray != null) spray.Deactivate(); // 空中被接回手上就停止甩醬
        }

        void OnReleased(SelectExitEventArgs args)
        {
            WasThrown = true;
            inFlight = true;
            releaseTime = Time.time;

            if (GameManager.Instance != null)
                record = GameManager.Instance.OnPizzaReleased(this, currentSampler);

            // 飛行中沿路甩醬(垂直滴落 + 盤緣切線甩出)
            var spray = GetComponent<Pizzala.Dirt.SauceSpray>();
            if (spray == null) spray = gameObject.AddComponent<Pizzala.Dirt.SauceSpray>();
            spray.Activate(flavor);
        }

        void OnCollisionEnter(Collision c)
        {
            var contact = c.GetContact(0);
            Resolve(c.collider, contact.point, contact.normal);
        }

        void OnTriggerEnter(Collider other)
        {
            // 客人手掌/臉/身體是 trigger,沒有接觸點資訊:
            // 用 collider 上離披薩最近的點當命中點,法線取外指方向,
            // 髒污(Decal)才會貼著身體投影,不會平躺懸在半空。
            Vector3 point = other.ClosestPoint(transform.position);
            Vector3 normal = transform.position - point;
            if (normal.sqrMagnitude < 1e-6f) // 披薩中心已進到 collider 內,退而用水平外指方向
            {
                var center = other.bounds.center;
                center.y = transform.position.y;
                normal = transform.position - center;
            }
            Resolve(other, point, normal.sqrMagnitude > 1e-6f ? normal.normalized : Vector3.up);
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

            var spray = GetComponent<Pizzala.Dirt.SauceSpray>();
            if (spray != null) spray.Deactivate(); // 落地後換滑行痕跡接手

            // 落地後開始留醬汁痕跡:之後的彈跳/滑行沿路留下一排髒污
            var trail = GetComponent<Pizzala.Dirt.SauceTrail>();
            if (trail == null) trail = gameObject.AddComponent<Pizzala.Dirt.SauceTrail>();
            trail.Activate(flavor);
        }
    }
}
