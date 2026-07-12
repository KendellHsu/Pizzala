// ─────────────────────────────────────────────────────────────
// ThrowClassifier.cs — 規則式投擲手勢分類(純函式,不掛物件)
// 由 PizzaProjectile 在放手瞬間呼叫。
// 分類優先序:過頭砸 → 低手拋 → 反手 → 正手
// 所有原始特徵值都會寫進 ThrowFeatures 保存,
// 事後可以拿 JSON 重新調閾值重新分類,數據不會浪費。
// ─────────────────────────────────────────────────────────────
using System.Collections.Generic;
using UnityEngine;
using Pizzala.Data;

namespace Pizzala.Throwing
{
    public static class ThrowClassifier
    {
        public static ThrowType Classify(List<MotionSample> samples, Transform head,
                                         bool isLeftHand, ThrowTuning tuning,
                                         ThrowFeatures features)
        {
            if (samples == null || samples.Count < 3 || head == null)
                return ThrowType.Unknown;

            MotionSample release = samples[samples.Count - 1];

            // ── 基本特徵 ──
            float peakSpeed = 0f, peakAngular = 0f;
            foreach (var s in samples)
            {
                peakSpeed = Mathf.Max(peakSpeed, s.velocity.magnitude);
                peakAngular = Mathf.Max(peakAngular, s.angularSpeedDeg);
            }

            Vector3 v = release.velocity;
            float horizontalSpeed = new Vector2(v.x, v.z).magnitude;

            features.releaseSpeed = v.magnitude;
            features.releaseElevationDeg = Mathf.Atan2(v.y, Mathf.Max(horizontalSpeed, 0.001f)) * Mathf.Rad2Deg;
            features.releaseHeightRelHead = release.worldPos.y - head.position.y;
            features.peakWristAngularSpeed = peakAngular;
            features.rollAngleDeg = Vector3.Angle(release.worldRot * Vector3.up, Vector3.up);

            // ── 找出「揮動段」起點:從放手往回走,直到速度低於峰值的一定比例 ──
            int swingStart = samples.Count - 1;
            float speedGate = peakSpeed * tuning.swingSpeedFraction;
            for (int i = samples.Count - 1; i >= 0; i--)
            {
                if (samples[i].velocity.magnitude < speedGate) break;
                swingStart = i;
            }

            // ── 揮動起點在身體哪一側(正=持盤手同側,負=橫過中線=反手)──
            Vector3 headRight = head.right; headRight.y = 0f; headRight.Normalize();
            Vector3 toStart = samples[swingStart].worldPos - head.position;
            float lateral = Vector3.Dot(toStart, headRight);
            if (isLeftHand) lateral = -lateral; // 統一成「+ = 持盤手同側」
            features.swingStartSide = lateral;

            // ── 軌跡垂直成分比例 ──
            float dy = 0f, dxz = 0f;
            for (int i = swingStart + 1; i < samples.Count; i++)
            {
                Vector3 d = samples[i].worldPos - samples[i - 1].worldPos;
                dy += Mathf.Abs(d.y);
                dxz += new Vector2(d.x, d.z).magnitude;
            }
            features.verticalPlaneRatio = (dy + dxz) > 0.001f ? dy / (dy + dxz) : 0f;

            // ── 規則分類 ──
            if (features.releaseHeightRelHead > tuning.overheadHeightOffset
                && features.verticalPlaneRatio > tuning.overheadVerticalRatio)
                return ThrowType.Overhead;

            if (features.releaseHeightRelHead < -tuning.underhandHeightOffset
                && features.releaseElevationDeg > tuning.underhandMinElevationDeg)
                return ThrowType.Underhand;

            if (features.swingStartSide < -tuning.crossBodyThreshold)
                return ThrowType.Backhand;

            return ThrowType.Forehand;
        }
    }
}
