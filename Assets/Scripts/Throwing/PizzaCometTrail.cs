// ─────────────────────────────────────────────────────────────
// PizzaCometTrail.cs — 飛行中的慧星尾巴拖曳特效
// 掛載:披薩 Prefab 根物件(和 Rigidbody / PizzaProjectile 同物件)。
// Prefab 需求:根物件已有 TrailRenderer(6 顆披薩 prefab 都有,材質
//   M_SauceTrail_01)。本元件只是「驅動」既有的 TrailRenderer,
//   不新增 renderer、不加任何物理力,純視覺,不影響飛盤飛行。
//
// 觸發時機:
//   出手(XR selectExited) → StartEmit(),清空舊尾巴、開始發射(依口味著色)
//   被接住(selectEntered) → StopEmit(),既有尾巴自然淡出
//   落地(PizzaProjectile / ThrowbackProjectile 呼叫)→ StopEmit()
//   速度低於 cometTrailMinSpeed → 自動收尾(emitting=false)
//
// 尾巴顏色:依這顆披薩的口味,取自 DirtManager.flavorDropletColors(和液滴/髒污
//   同一份顏色來源),口味未知或沒有 DirtManager(單獨測試)時退回白色。
// ─────────────────────────────────────────────────────────────
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using Pizzala.Core;
using Pizzala.Data;
using Pizzala.Dirt;
using Pizzala.Customers;

namespace Pizzala.Throwing
{
    [RequireComponent(typeof(Rigidbody))]
    public class PizzaCometTrail : MonoBehaviour
    {
        // 沒有 GameManager/tuning 時(單獨測試)用這組後備預設值(對齊 ThrowTuning 預設)
        const float FbTime = 0.45f;
        const float FbWidth = 0.12f;
        const float FbMinSpeed = 1.5f;

        static readonly Color FallbackColor = Color.white;

        Rigidbody rb;
        TrailRenderer trail;
        XRGrabInteractable grab;
        bool wantEmit;

        static ThrowTuning Tuning => GameManager.Instance != null ? GameManager.Instance.tuning : null;

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            trail = GetComponent<TrailRenderer>();
            grab = GetComponent<XRGrabInteractable>();

            if (trail == null)
            {
                enabled = false; // 沒有 TrailRenderer 就整個停用,no-op
                return;
            }

            trail.emitting = false;

            if (grab != null)
            {
                grab.selectEntered.AddListener(OnGrabbed);
                grab.selectExited.AddListener(OnReleased);
            }
        }

        void OnDestroy()
        {
            if (grab != null)
            {
                grab.selectEntered.RemoveListener(OnGrabbed);
                grab.selectExited.RemoveListener(OnReleased);
            }
        }

        void OnGrabbed(SelectEnterEventArgs args) => StopEmit();

        void OnReleased(SelectExitEventArgs args) => StartEmit();

        /// <summary>開始拖尾(依口味著色)。丟出/丟回時呼叫。</summary>
        public void StartEmit()
        {
            if (trail == null) return;
            var t = Tuning;
            if (t != null && !t.cometTrailEnabled) return;

            ApplyTuning(t);
            ApplyColor(ResolveColor());
            trail.Clear();
            wantEmit = true;
            trail.emitting = true;
        }

        /// <summary>取這顆披薩的口味顏色(和液滴/髒污同一份 DirtManager.flavorDropletColors);
        /// 口味未知或沒有 DirtManager 時退回白色。</summary>
        Color ResolveColor()
        {
            PizzaFlavor? flavor = null;
            var proj = GetComponent<PizzaProjectile>();
            if (proj != null) flavor = proj.flavor;
            else
            {
                var back = GetComponent<ThrowbackProjectile>();
                if (back != null) flavor = back.flavor;
            }

            var dm = DirtManager.Instance;
            if (flavor.HasValue && dm != null && dm.flavorDropletColors != null
                && (int)flavor.Value < dm.flavorDropletColors.Length)
            {
                var c = dm.flavorDropletColors[(int)flavor.Value];
                c.a = 1f; // 透明度由 ApplyColor 的 alpha 漸層決定
                return c;
            }
            return FallbackColor;
        }

        /// <summary>停止拖尾(既有軌跡自然淡出)。落地或被接住時呼叫。</summary>
        public void StopEmit()
        {
            wantEmit = false;
            if (trail != null) trail.emitting = false;
        }

        void Update()
        {
            if (trail == null || !wantEmit) return;

            var t = Tuning;
            if (t != null && !t.cometTrailEnabled) { StopEmit(); return; }

            float minSpeed = t != null ? t.cometTrailMinSpeed : FbMinSpeed;
            float speed = rb != null ? rb.linearVelocity.magnitude : 0f;
            trail.emitting = speed >= minSpeed;
        }

        void ApplyTuning(ThrowTuning t)
        {
            trail.time = t != null ? t.cometTrailTime : FbTime;
            float width = t != null ? t.cometTrailWidth : FbWidth;

            var curve = new AnimationCurve(
                new Keyframe(0f, width),
                new Keyframe(1f, 0f));
            trail.widthCurve = curve;
        }

        void ApplyColor(Color color)
        {
            trail.colorGradient = new Gradient
            {
                colorKeys = new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
                alphaKeys = new[]
                {
                    new GradientAlphaKey(0.85f, 0f), // 頭部亮、較不透明
                    new GradientAlphaKey(0f, 1f)     // 尾端全透明,慧星收尖感
                }
            };
        }
    }
}
