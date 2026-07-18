// ─────────────────────────────────────────────────────────────
// FrisbeeGrabInteractable.cs — 會把握點吸附到盤緣的 XR Grab Interactable
// 掛載:披薩 Prefab 根物件(取代原本的 XRGrabInteractable)。
//
// 為什麼是子類別而不是外掛監聽:
//   XRGeneralGrabTransformer 會在 OnGrab 當下「快取」attach 偏移,之後每幀只用快取。
//   OnGrab 發生在 selectEntered 事件「之前」,所以用 selectEntered 監聽改 attach 沒用
//   (改到的是已被快取、沒人再看的 attach)。
//   InitializeDynamicAttachPose 是 XRI 官方留的擴充點,在快取「之前」執行 → 在這裡把
//   動態 attach 移到盤緣才會被 transformer 採用。
//
// Prefab 需求:Use Dynamic Attach 勾起來(否則不會呼叫本方法)。
// 手感參數集中在 ThrowTuning 資產,透過 GameManager.Instance.tuning 讀取,Play 中即時生效。
// ─────────────────────────────────────────────────────────────
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using Unity.XR.CoreUtils;
using Pizzala.Core;

namespace Pizzala.Throwing
{
    public class FrisbeeGrabInteractable : XRGrabInteractable
    {
        const float FallbackDiscRadius = 0.15f;

        static ThrowTuning Tuning => GameManager.Instance != null ? GameManager.Instance.tuning : null;

        // Refuse NEW grabs whenever play isn't live - tutorial, countdown, pause, results.
        // Without this the trigger doubles as "grab pizza" during the tutorial, and the player
        // ends up holding one they can't legally throw (OnReleased cancels and snaps it back).
        // A hand that's ALREADY holding this pizza is always allowed to keep it, so pausing
        // mid-hold doesn't rip it out of the hand.
        // Dev note: scenes with a GameManager but no running round (the _Test_* scenes) block
        // grabbing too - turn on GameManager.autoStart there, that's what it's for.
        public override bool IsSelectableBy(IXRSelectInteractor interactor)
        {
            var gm = GameManager.Instance;
            bool newGrabsAllowed = gm == null || gm.CanThrow;
            return base.IsSelectableBy(interactor)
                   && (newGrabsAllowed || interactorsSelecting.Contains(interactor));
        }

        protected override void InitializeDynamicAttachPose(IXRSelectInteractor interactor, Transform dynamicAttachTransform)
        {
            // 先讓 XRI 做預設(依 Match Position/Rotation + Snap 把 attach 放到手的位置)
            base.InitializeDynamicAttachPose(interactor, dynamicAttachTransform);

            var t = Tuning;
            float discRadius = (t != null && t.frisbeeDiscRadius > 0f) ? t.frisbeeDiscRadius : AutoRadius();
            float gripHeight = t != null ? t.frisbeeGripHeight : 0f;
            bool alignToController = t == null || t.frisbeeAlignToController;
            Vector3 gripRotationEuler = t != null ? t.frisbeeGripRotationEuler : Vector3.zero;

            // 手(抓取者)目前的 attach 世界座標
            Transform handAttach = interactor.GetAttachTransform(this);
            if (handAttach == null) return;

            // 取得玩家身體方向(XROrigin 或 Head 的 forward)
            Vector3 bodyForward = GetPlayerBodyForward();

            // 轉進披薩本地座標
            Vector3 local = transform.InverseTransformPoint(handAttach.position);
            Vector3 planar = new Vector3(local.x, 0f, local.z);

            // 優化:把身體前向也轉進披薩本地座標,用它作為「偏好」方向
            // 這樣握點會選在相對身體最自然的方向,丟出去更直順
            Vector3 bodyForwardLocal = transform.InverseTransformDirection(bodyForward);
            Vector3 bodyForwardPlanar = new Vector3(bodyForwardLocal.x, 0f, bodyForwardLocal.z).normalized;

            // 如果手已經有明確位置,優先用手的方向;
            // 如果手太靠近中心,則用身體前向作為握點方向
            Vector3 dir;
            if (planar.sqrMagnitude > 0.001f)
            {
                dir = planar.normalized;
            }
            else
            {
                // 手幾乎在中心時,用身體前向決定握點
                dir = bodyForwardPlanar.sqrMagnitude > 0.1f ? bodyForwardPlanar : Vector3.forward;
            }

            Vector3 rimLocal = dir * discRadius + Vector3.up * gripHeight;

            // dynamicAttachTransform 已是 this 的子物件(見 XRI 文件)
            if (dynamicAttachTransform.parent == transform)
                dynamicAttachTransform.localPosition = rimLocal;
            else
                dynamicAttachTransform.position = transform.TransformPoint(rimLocal);

            // 盤面對齊控制器
            if (alignToController)
            {
                Quaternion offset = Quaternion.Euler(gripRotationEuler);
                if (dynamicAttachTransform.parent == transform)
                    dynamicAttachTransform.localRotation = offset;
                else
                    dynamicAttachTransform.rotation = transform.rotation * offset;
            }
        }

        Vector3 GetPlayerBodyForward()
        {
            // 尋找玩家頭部或 XROrigin,用其 forward 代表身體方向
            var head = Camera.main?.transform;
            if (head != null) return head.forward;

            // 備援:尋找 XROrigin 或 XR Rig
            var xrOrigin = Object.FindFirstObjectByType<XROrigin>();
            if (xrOrigin != null) return xrOrigin.transform.forward;

            // 最後備援:用當前世界的 forward
            return Vector3.forward;
        }

        float AutoRadius()
        {
            var box = GetComponent<BoxCollider>();
            return box != null ? box.size.x * 0.5f : FallbackDiscRadius;
        }
    }
}
