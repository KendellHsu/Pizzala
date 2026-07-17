// ─────────────────────────────────────────────────────────────
// FrisbeeFlight.cs — 放手後的飛盤氣動模擬(升力/阻力/力矩,由自旋力道決定)
// 掛載:披薩 Prefab 根物件(和 PizzaProjectile / XRGrabInteractable 同物件)。
// 參考 Hummel & Hubbard "Simulation of Frisbee Flight"(2000)與 VRisbee 專案。
//
// 核心概念:自旋不是我們捏造的,是「使用者手腕甩的力道」——
//   XR Grab 的 Throw On Detach 已把手的角速度轉進 rb.angularVelocity。
//   我們讀沿盤軸的自旋來決定盤子飛不飛,並套用真實氣動力與力矩:
//     甩得夠快又夠乾淨(spinRatio 高)→ 升力 + 陀螺穩定 → 平飛旋轉;
//     甩得軟或亂翻 → 沒升力、俯仰力矩讓它翻覆 → 直接掉(像沒丟好)。
//
// 力矩(俯仰/滾轉/自旋衰減)是真實感關鍵:盤子有自旋(角動量沿盤軸),
//   俯仰力矩經陀螺進動轉成緩慢的 bank,產生飛盤標誌性的 turn & fade 弧線。
//   這段耦合由 Unity 剛體物理自然算出(前提:盤軸慣量 > 徑向慣量,扁盒 collider 本來就是)。
//
// 重要:Unity 剛體 maxAngularVelocity 預設只有 7 rad/s,會把自旋砍掉,
//   Awake 會把它調高(frisbeeMaxAngularVelocity),否則機制會失效。
// 手感參數集中在 ThrowTuning 資產,現場調參改那個 asset、Play 中即時生效。
// ─────────────────────────────────────────────────────────────
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using Pizzala.Core;

namespace Pizzala.Throwing
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(XRGrabInteractable))]
    public class FrisbeeFlight : MonoBehaviour
    {
        const float AirDensity = 1.23f; // ρ,空氣密度 kg/m³

        // 沒有 GameManager/tuning 時(單獨測試)用這組後備預設值(對齊 ThrowTuning 預設)
        const float FbSpinToFly = 6f;
        const float FbSpinRatio = 0.5f;
        const float FbMaxAngular = 100f;
        const float FbAeroScale = 3f;
        const float FbArea = 0.0568f;
        const float FbCL0 = 0.1f, FbCLA = 1.4f;
        const float FbCD0 = 0.08f, FbCDA = 2.72f, FbAlpha0Deg = -4f;
        const float FbDiameter = 0.21f;
        const float FbCM0 = -0.08f, FbCMA = 0.43f, FbCMq = -0.005f;
        const float FbCRR = 0.014f, FbCRP = -0.0055f, FbCNR = -0.0000071f;
        const float FbWobble = 3f;

        Rigidbody rb;
        XRGrabInteractable grab;
        bool flying;

        static ThrowTuning Tuning => GameManager.Instance != null ? GameManager.Instance.tuning : null;

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            grab = GetComponent<XRGrabInteractable>();

            // 放寬剛體自旋上限,否則手腕甩出的自旋會被 Unity 砍到 7 rad/s
            var t = Tuning;
            rb.maxAngularVelocity = t != null ? t.frisbeeMaxAngularVelocity : FbMaxAngular;

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

        void OnGrabbed(SelectEnterEventArgs args) => flying = false;

        void OnReleased(SelectExitEventArgs args)
        {
            flying = true;
            // 抓的當下 tuning 若已就緒,順手更新自旋上限(避免 Awake 時還沒有 GameManager)
            var t = Tuning;
            if (t != null) rb.maxAngularVelocity = t.frisbeeMaxAngularVelocity;
        }

        void FixedUpdate()
        {
            if (!flying || rb.isKinematic) return;

            Vector3 v = rb.linearVelocity;
            float speed = v.magnitude;
            if (speed < 0.05f) return; // 幾乎靜止就不算氣動力(重力交給 Unity)

            var t = Tuning;
            float spinToFly = t != null ? t.frisbeeSpinToFly : FbSpinToFly;
            float spinRatioGate = t != null ? t.frisbeeSpinRatioThreshold : FbSpinRatio;
            float aeroScale = t != null ? t.frisbeeAeroScale : FbAeroScale;
            float area = t != null ? t.frisbeeArea : FbArea;
            float cl0 = t != null ? t.frisbeeCL0 : FbCL0;
            float cla = t != null ? t.frisbeeCLA : FbCLA;
            float cd0 = t != null ? t.frisbeeCD0 : FbCD0;
            float cda = t != null ? t.frisbeeCDA : FbCDA;
            float alpha0 = (t != null ? t.frisbeeAlpha0Deg : FbAlpha0Deg) * Mathf.Deg2Rad;

            Vector3 n = transform.up;      // 盤面法線(盤軸)
            Vector3 vDir = v / speed;

            // ── 自旋力道判定:沿盤軸的自旋大小 + 純度 ──
            Vector3 w = rb.angularVelocity;
            float wMag = w.magnitude;
            float spin = Vector3.Dot(w, n);                                   // 沿盤軸自旋分量
            float spinRatio = wMag > 1e-4f ? (spin * spin) / (wMag * wMag) : 0f;
            bool flyingWell = Mathf.Abs(spin) > spinToFly && spinRatio > spinRatioGate;

            // ── 晃動(章動)阻尼:吃掉垂直盤軸的角速度,效果隨自旋大小縮放 ──
            float wobbleDamping = t != null ? t.frisbeeWobbleDamping : FbWobble;
            if (wobbleDamping > 0f)
            {
                float settle = Mathf.Clamp01(Mathf.Abs(spin) / Mathf.Max(spinToFly, 0.01f));
                Vector3 wTransverse = w - spin * n; // 翻擺成分
                rb.AddTorque(-wobbleDamping * settle * wTransverse, ForceMode.Acceleration);
            }

            // 動壓 q = ½ρV²
            float q = 0.5f * AirDensity * speed * speed;

            // 攻角 α:速度相對盤面的夾角(盤面下方受風為正)
            float vn = Mathf.Clamp(Vector3.Dot(vDir, n), -1f, 1f);
            float alpha = -Mathf.Asin(vn);

            // ── 阻力(一直有):CD = CD0 + CDα(α−α0)² ──
            float da = alpha - alpha0;
            float cd = cd0 + cda * da * da;
            rb.AddForce(-cd * q * area * aeroScale * vDir, ForceMode.Force);

            // ── 升力(只有旋轉夠力才有):CL = CL0 + CLα·α,方向垂直速度、偏向盤軸側 ──
            if (flyingWell)
            {
                float cl = cl0 + cla * alpha;
                Vector3 liftDir = n - vn * vDir; // 盤軸在垂直速度平面上的分量
                if (liftDir.sqrMagnitude > 1e-6f)
                {
                    liftDir.Normalize();
                    rb.AddForce(cl * q * area * aeroScale * liftDir, ForceMode.Force);
                }
            }

            // ── 氣動力矩:俯仰 + 滾轉 + 自旋衰減(turn & fade 的來源)──
            ApplyAeroTorque(t, v, speed, n, w, spin, alpha, q, area, aeroScale);
        }

        // 以盤體氣動座標系套用三軸力矩:
        //   x̂ = 盤面內的前進方向(roll 軸)、ŷ = 盤面內垂直(pitch 軸)、n = 盤軸(spin 軸)
        void ApplyAeroTorque(ThrowTuning t, Vector3 v, float speed, Vector3 n, Vector3 w,
                             float spin, float alpha, float q, float area, float aeroScale)
        {
            if (speed < 0.5f) return;

            float d = t != null ? t.frisbeeDiameter : FbDiameter;
            float cm0 = t != null ? t.frisbeeCM0 : FbCM0;
            float cma = t != null ? t.frisbeeCMA : FbCMA;
            float cmq = t != null ? t.frisbeeCMq : FbCMq;
            float crr = t != null ? t.frisbeeCRR : FbCRR;
            float crp = t != null ? t.frisbeeCRP : FbCRP;
            float cnr = t != null ? t.frisbeeCNR : FbCNR;

            // 盤面內的速度分量 → roll 軸;pitch 軸與之垂直、仍在盤面內
            Vector3 vPlane = v - Vector3.Dot(v, n) * n;
            float vPlaneMag = vPlane.magnitude;
            if (vPlaneMag < 1e-4f) return;

            Vector3 rollAxis = vPlane / vPlaneMag;
            Vector3 pitchAxis = Vector3.Cross(n, rollAxis);

            // 角速度在三軸上的分量
            float p = Vector3.Dot(w, rollAxis);   // 滾轉角速度
            float qRate = Vector3.Dot(w, pitchAxis); // 俯仰角速度
            float r = spin;                          // 自旋角速度(= w·n)

            // 非因次化角速度 rate·d/(2V)
            float inv2V = d / (2f * speed);
            float pHat = p * inv2V, qHat = qRate * inv2V, rHat = r * inv2V;

            float cRoll = crr * rHat + crp * pHat;
            float cPitch = cm0 + cma * alpha + cmq * qHat;
            float cSpin = cnr * rHat;

            float mScale = q * area * d * aeroScale; // ½ρV²·A·d
            Vector3 torque = (cRoll * rollAxis + cPitch * pitchAxis + cSpin * n) * mScale;
            rb.AddTorque(torque, ForceMode.Force);
        }
    }
}
