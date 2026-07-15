// ─────────────────────────────────────────────────────────────
// FrisbeeEdgeGrab.cs — 抓取時把握點吸附到披薩盤緣(像捏飛盤)
// 掛載:披薩 Prefab 根物件(和 PizzaProjectile / XRGrabInteractable 同物件)。
// Prefab 需求:
//   XRGrabInteractable 勾 Use Dynamic Attach(Match Attach Position/Rotation 開)
//   一顆圓盤形 BoxCollider(用來自動推算盤半徑)
// 效果:不論手抓在盤面哪裡,握點都滑到最近的盤緣,disc 由此往內延伸。
// 手感參數集中在 ThrowTuning 資產(飛盤物理區),透過 GameManager.Instance.tuning 讀取,
// 現場調參改那個 asset、三顆披薩共用、Play 中即時生效。
// ─────────────────────────────────────────────────────────────
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using Pizzala.Core;

namespace Pizzala.Throwing
{
    [RequireComponent(typeof(XRGrabInteractable))]
    public class FrisbeeEdgeGrab : MonoBehaviour
    {
        const float FallbackDiscRadius = 0.15f; // 沒有 tuning 時的後備半徑

        XRGrabInteractable grab;
        float autoRadius; // 從 BoxCollider 推算的後備半徑

        static ThrowTuning Tuning => GameManager.Instance != null ? GameManager.Instance.tuning : null;

        void Awake()
        {
            grab = GetComponent<XRGrabInteractable>();

            var box = GetComponent<BoxCollider>();
            autoRadius = box != null ? box.size.x * 0.5f : FallbackDiscRadius;

            if (grab != null)
                grab.selectEntered.AddListener(OnSelectEntered);
        }

        void OnDestroy()
        {
            if (grab != null)
                grab.selectEntered.RemoveListener(OnSelectEntered);
        }

        void OnSelectEntered(SelectEnterEventArgs args)
        {
            var t = Tuning;
            // tuning 的盤半徑 <= 0 表示要自動推算;沒有 tuning 就走後備
            float discRadius = (t != null && t.frisbeeDiscRadius > 0f) ? t.frisbeeDiscRadius : autoRadius;
            float gripHeight = t != null ? t.frisbeeGripHeight : 0f;
            bool alignToController = t == null || t.frisbeeAlignToController;
            Vector3 gripRotationEuler = t != null ? t.frisbeeGripRotationEuler : Vector3.zero;

            // 手(抓取者)目前的 attach 世界座標
            Transform handAttach = args.interactorObject.GetAttachTransform(grab);
            if (handAttach == null) return;

            // 轉進披薩本地座標,壓平到盤面 XZ,取最近盤緣方向
            Vector3 local = transform.InverseTransformPoint(handAttach.position);
            Vector3 planar = new Vector3(local.x, 0f, local.z);
            Vector3 dir = planar.sqrMagnitude > 1e-6f ? planar.normalized : Vector3.forward;
            Vector3 rimLocal = dir * discRadius + Vector3.up * gripHeight;

            // 這次抓取用的(動態)attach transform,移到盤緣點
            Transform at = grab.GetAttachTransform(args.interactorObject);
            if (at == null) return;

            if (at.parent == transform)
                at.localPosition = rimLocal;
            else
                at.position = transform.TransformPoint(rimLocal);

            // 盤面對齊控制器:覆寫 attach 旋轉(在 XRI 設好動態 attach 之後跑,會蓋過 Match Rotation)
            if (alignToController)
            {
                Quaternion offset = Quaternion.Euler(gripRotationEuler);
                if (at.parent == transform)
                    at.localRotation = offset;
                else
                    at.rotation = transform.rotation * offset;
            }
        }
    }
}
