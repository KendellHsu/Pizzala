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
using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
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
            // Paused, or the round is over: letting go must not count as a throw. Blocking
            // it in GameManager.OnPizzaReleased() alone isn't enough - that only skips the
            // record, while the pizza still arms itself, sprays sauce and (via XRI's
            // throwOnDetach) flies off with the hand's velocity. That's the bug where you
            // could keep throwing after time ran out.
            if (GameManager.Instance != null && !GameManager.Instance.CanThrow)
            {
                CancelRelease(args.interactorObject);
                return;
            }

            WasThrown = true;
            inFlight = true;
            releaseTime = Time.time;

            if (GameManager.Instance != null)
                record = GameManager.Instance.OnPizzaReleased(this, currentSampler);

            Pizzala.Audio.GameAudioController.PlayPizzaThrow();

            // 飛行中沿路甩醬(垂直滴落 + 盤緣切線甩出)
            var spray = GetComponent<Pizzala.Dirt.SauceSpray>();
            if (spray == null) spray = gameObject.AddComponent<Pizzala.Dirt.SauceSpray>();
            spray.Activate(flavor);
        }

        // XRI has already detached and (if throwOnDetach) launched the pizza by the time
        // selectExited fires, so undo both: kill the velocity it was just given, then put it
        // back in the hand on the next frame. Re-selecting has to wait a frame - doing it
        // inside the exit callback re-enters the interaction manager mid-update. Plain
        // "yield return null" is used rather than any WaitForSeconds because frames still
        // tick while paused (timeScale = 0) but seconds do not.
        void CancelRelease(IXRSelectInteractor interactor)
        {
            var rb = GetComponent<Rigidbody>();
            if (rb != null && !rb.isKinematic)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            if (interactor != null) StartCoroutine(ReturnToHand(interactor));
        }

        IEnumerator ReturnToHand(IXRSelectInteractor interactor)
        {
            yield return null;
            if (grab == null || grab.interactionManager == null) yield break;
            if (grab.isSelected) yield break;               // something else grabbed it already
            if (interactor.transform == null) yield break;  // controller went away
            grab.interactionManager.SelectEnter(interactor, grab);
        }

        void OnCollisionEnter(Collision c)
        {
            var contact = c.GetContact(0);
            Resolve(c.collider, contact.point, contact.normal, c.relativeVelocity);
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
            Rigidbody body = GetComponent<Rigidbody>();
            Vector3 impactVelocity = body != null ? body.linearVelocity : transform.forward;
            Resolve(
                other,
                point,
                normal.sqrMagnitude > 1e-6f ? normal.normalized : Vector3.up,
                impactVelocity);
        }

        void Resolve(Collider col, Vector3 point, Vector3 normal, Vector3 impactVelocity)
        {
            if (!inFlight || Time.time - releaseTime < ArmDelay) return;

            // 忽略碰到玩家自己的頭
            if (col.GetComponentInParent<PlayerHeadHitbox>() != null) return;

            var zone = col.GetComponentInParent<CustomerHitZone>();
            inFlight = false;

            var hitCustomer = zone != null && zone.customer != null
                ? zone.customer
                : col.GetComponentInParent<CustomerController>();
            if (hitCustomer != null)
                Pizzala.Audio.GameAudioController.PlayCustomerHit();

            GetComponent<PizzaJelly>()?.Punch();
            GetComponent<PizzaCometTrail>()?.StopEmit();
            // Face and body use the new 3D shrink-wrap sauce system when the
            // spawned customer has it. Hand hits intentionally keep their normal
            // catch / order-resolution behavior.
            bool useCustomerSurfaceSauce = false;
            if (zone != null && zone.customer != null)
            {
                var customerSurfaceSauce = zone.customer.GetComponent<CustomerSurfaceSauce>();
                if (customerSurfaceSauce != null && customerSurfaceSauce.Handles(zone))
                {
                    useCustomerSurfaceSauce = true;
                    float releaseSpeed = record != null && record.features != null
                        ? record.features.releaseSpeed
                        : 0f;
                    if (!customerSurfaceSauce.TryCreate(
                            zone,
                            point,
                            impactVelocity,
                            normal,
                            flavor,
                            releaseSpeed))
                        Debug.LogWarning($"[SurfaceSauce] Could not create 3D sauce on {zone.customer.name}.");
                }
            }

            if (GameManager.Instance != null && record != null)
                GameManager.Instance.OnPizzaLanded(
                    this,
                    record,
                    zone,
                    point,
                    normal,
                    Time.time - releaseTime,
                    useCustomerSurfaceSauce,
                    col);

            record = null;

            // 丟偏落在環境上(沒中客人任何 HitZone)= 地上一顆殘局,登記成可撿。
            // 命中客人手/臉/身的那顆會被收下或彈開,不算地上可撿。
            if (zone == null) GroundPizzaRegistry.Register(this);

            var spray = GetComponent<Pizzala.Dirt.SauceSpray>();
            if (spray != null) spray.Deactivate(); // 落地後換滑行痕跡接手

            // 落地後開始留醬汁痕跡:之後的彈跳/滑行沿路留下一排髒污
            var trail = GetComponent<Pizzala.Dirt.SauceTrail>();
            if (trail == null) trail = gameObject.AddComponent<Pizzala.Dirt.SauceTrail>();
            trail.Activate(flavor);
        }

        void OnDestroy() => GroundPizzaRegistry.Unregister(this); // 被撿走/清場時退出登記表
    }
}
