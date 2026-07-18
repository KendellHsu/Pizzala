// ─────────────────────────────────────────────────────────────
// PizzaJelly.cs — 披薩果凍軟身視覺變形(沿盤軸的 squash & stretch)
// 掛載:披薩 Prefab 根物件(和 Rigidbody / PizzaProjectile 同物件)。
//
// 核心設計:Awake 時在視覺子物件(fbx 的 model_LOD0)外面插一層對齊
//   root 軸的「JellyPivot」空物件,變形只縮放 pivot 的 localScale,
//   完全不碰 root 的 Rigidbody / Collider,飛盤氣動(FrisbeeFlight)與
//   命中判定(PizzaProjectile)不受影響。
//
// 範圍界定(重要):披薩是扁飛盤,撞擊法線幾乎都在水平面,盤軸(厚度)
//   方向本來就不會有明顯形變。真正的「局部凹陷」需要逐頂點/shader 形變,
//   CP 值不高故不做。這裡只做沿盤軸的整體 squash & stretch,當作飛行/
//   撞擊的輕微 Q 彈點綴。
//
// 兩種變形來源:
//   1. 飛行呼吸感:飛行中盤厚輕微正弦起伏(jellyFlightAmount)。
//   2. 撞擊 punch:PizzaProjectile / ThrowbackProjectile 命中時呼叫
//      Punch(),沿盤厚壓一下再彈回;另有自動偵測(沒被抓著時單幀速度
//      向量變化超過門檻視為撞擊,涵蓋落地後每次彈跳),兩路徑 0.1s 冷卻
//      防疊加。撞擊速度用「前一幀」速度(碰撞解算後 rb 速度已被吃掉)。
//   3. 阻尼彈簧回復:變形量朝目標值回彈,jellyStiffness / jellyDamping
//      決定 Q 彈的快慢與晃幾下。
//
// 體積守恆近似:盤厚壓多少,徑向就撐開一半,像果凍不像縮放。
// 手感參數集中在 ThrowTuning 資產,現場調參改那個 asset、Play 中即時生效。
// ─────────────────────────────────────────────────────────────
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using Pizzala.Core;

namespace Pizzala.Throwing
{
    [RequireComponent(typeof(Rigidbody))]
    public class PizzaJelly : MonoBehaviour
    {
        [Tooltip("要變形的視覺子物件;留空 = 自動抓第一個帶 MeshRenderer 的子物件")]
        public Transform visualTransform;

        // 沒有 GameManager/tuning 時(單獨測試)用這組後備預設值(對齊 ThrowTuning 預設)
        const float FbStiffness = 100f;
        const float FbDamping = 9f;
        const float FbMaxDeform = 0.5f;
        const float FbFlightAmount = 0.05f;
        const float FbImpactAmount = 0.55f;

        const float BreatheHz = 2.5f;        // 飛行呼吸頻率(Hz),固定值,微妙效果不需現場調
        const float FlightMinSpeed = 1.5f;   // 速度低於此值不算飛行,不呼吸
        const float ImpactRefSpeed = 3.5f;   // punch 強度以此撞擊速度為 1 倍基準(VR 出手約 3~5 m/s)
        const float AutoPunchDeltaV = 1.5f;  // 單幀速度向量變化超過此值(m/s)視為撞擊,自動 punch
        const float PunchCooldown = 0.1f;    // punch 冷卻,防 Resolve 與自動偵測同一撞擊疊兩次
        const float MaxDeltaTime = 0.05f;    // 彈簧積分的 dt 上限,防掉幀時爆掉

        Rigidbody rb;
        XRGrabInteractable grab;
        Transform pivot;       // squash pivot:對齊 root 軸,變形只縮放這層
        float deform;          // 目前盤厚變形量(正 = 壓扁、負 = 拉厚)
        float deformVel;       // 變形速度(彈簧狀態)
        float breathePhase;    // 呼吸相位
        Vector3 lastVelocity;  // 前一幀速度向量,撞擊偵測與強度估算用
        float punchReadyAt;    // 冷卻結束時間

        static ThrowTuning Tuning => GameManager.Instance != null ? GameManager.Instance.tuning : null;

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            grab = GetComponent<XRGrabInteractable>();

            // 自動後備:抓第一個帶 MeshRenderer 的子物件(root 上只有 TrailRenderer,不會誤抓)
            if (visualTransform == null)
            {
                var mr = GetComponentInChildren<MeshRenderer>();
                if (mr != null && mr.transform != transform) visualTransform = mr.transform;
            }

            if (visualTransform == null)
            {
                Debug.LogWarning($"[PizzaJelly] {name} 找不到視覺子物件,果凍變形停用。");
                return;
            }

            // 插入 squash pivot:掛在視覺物件的原父層下、對齊 root 的軸向
            // (盤軸 = root Y),再把視覺物件搬進 pivot(保留世界姿態)。
            // 之後只縮放 pivot,不管 fbx 子節點烘了什麼旋轉,壓的都是盤厚。
            var pivotGO = new GameObject("PZ_JellyPivot");
            pivot = pivotGO.transform;
            pivot.SetParent(visualTransform.parent, false);
            pivot.position = transform.position;
            pivot.rotation = transform.rotation;
            visualTransform.SetParent(pivot, true);
        }

        /// <summary>撞擊瞬間呼叫:沿盤厚壓一下再彈回。強度隨撞擊前速度縮放。</summary>
        public void Punch()
        {
            if (pivot == null || Time.time < punchReadyAt) return; // 冷卻中(同一撞擊已 punch 過)
            var t = Tuning;
            if (t != null && !t.jellyEnabled) return;
            float impactAmount = t != null ? t.jellyImpactAmount : FbImpactAmount;
            float stiffness = t != null ? t.jellyStiffness : FbStiffness;

            // 以彈簧自然頻率 ω=√k 換算:這個初速度會讓峰值變形 ≈ impactAmount(滿速撞擊時)
            float omega = Mathf.Sqrt(Mathf.Max(stiffness, 1f));
            deformVel += impactAmount * Mathf.Clamp01(lastVelocity.magnitude / ImpactRefSpeed) * omega;
            punchReadyAt = Time.time + PunchCooldown;
        }

        void Update()
        {
            if (pivot == null) return;

            var t = Tuning;
            if (t != null && !t.jellyEnabled)
            {
                // 總開關關閉:回到原始 scale、清空彈簧狀態
                deform = 0f; deformVel = 0f;
                pivot.localScale = Vector3.one;
                return;
            }

            float stiffness = t != null ? t.jellyStiffness : FbStiffness;
            float damping = t != null ? t.jellyDamping : FbDamping;
            float maxDeform = t != null ? t.jellyMaxDeform : FbMaxDeform;
            float flightAmount = t != null ? t.jellyFlightAmount : FbFlightAmount;

            Vector3 velocity = rb != null && !rb.isKinematic ? rb.linearVelocity : Vector3.zero;
            float speed = velocity.magnitude;
            bool held = grab != null && grab.isSelected;

            // ── 自動撞擊偵測:沒被抓著時單幀速度向量變化 = 撞到東西(涵蓋每次
            //    彈跳,含方向反轉但速率差不多的情況)──
            if (!held && (velocity - lastVelocity).magnitude > AutoPunchDeltaV)
                Punch();

            // ── 飛行呼吸感:飛行中彈簧目標值做微小正弦起伏 ──
            float target = 0f;
            if (speed > FlightMinSpeed && flightAmount > 0f)
            {
                breathePhase += Time.deltaTime * BreatheHz * 2f * Mathf.PI;
                target = flightAmount * Mathf.Sin(breathePhase);
            }

            // ── 阻尼彈簧(半隱式歐拉,dt 夾住防爆)──
            float dt = Mathf.Min(Time.deltaTime, MaxDeltaTime);
            deformVel += (-stiffness * (deform - target) - damping * deformVel) * dt;
            deform = Mathf.Clamp(deform + deformVel * dt, -maxDeform, maxDeform);

            // 體積守恆近似:盤厚(Y)壓扁,徑向(XZ)撐開一半
            pivot.localScale = new Vector3(
                1f + 0.5f * deform,
                1f - deform,
                1f + 0.5f * deform);

            lastVelocity = velocity; // 留給下一幀當撞擊前速度
        }
    }
}
