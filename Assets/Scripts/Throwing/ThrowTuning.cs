// ─────────────────────────────────────────────────────────────
// ThrowTuning.cs — 所有可調參數集中在一個 ScriptableObject
// 建立方式:Project 視窗右鍵 → Create → Pizzala → Throw Tuning
// 建好後拖到 GameManager 的 tuning 欄位。
// 現場調參就改這個資產,不用改程式碼。
// ─────────────────────────────────────────────────────────────
using UnityEngine;

namespace Pizzala.Throwing
{
    [CreateAssetMenu(fileName = "ThrowTuning", menuName = "Pizzala/Throw Tuning")]
    public class ThrowTuning : ScriptableObject
    {
        [Header("手勢分類閾值")]
        [Tooltip("放手點高於頭部多少公尺算過頭砸")]
        public float overheadHeightOffset = 0.05f;

        [Tooltip("放手點低於頭部多少公尺算低手拋(約腰部)")]
        public float underhandHeightOffset = 0.45f;

        [Tooltip("低手拋最少要有的出手仰角(度)")]
        public float underhandMinElevationDeg = 15f;

        [Tooltip("過頭砸的軌跡垂直成分比例下限 0~1")]
        public float overheadVerticalRatio = 0.45f;

        [Tooltip("揮動起點越過身體中線多少公尺算反手")]
        public float crossBodyThreshold = 0.05f;

        [Tooltip("認定「揮動段」的速度門檻(峰值速度的比例)")]
        [Range(0.1f, 0.6f)]
        public float swingSpeedFraction = 0.3f;

        [Tooltip("分類時往回看的軌跡長度(秒)")]
        public float swingWindowSeconds = 0.6f;

        [Header("客人與丟回機制")]
        [Tooltip("客人耐心(秒),超時就生氣")]
        public float customerPatience = 15f;

        [Tooltip("丟回前的預警時間(秒),給玩家反應窗口")]
        public float telegraphSeconds = 0.8f;

        [Tooltip("丟回披薩的飛行速度 m/s(越慢越好躲)")]
        public float throwbackSpeed = 5f;

        [Header("回合設定")]
        public float roundDurationSeconds = 180f;

        [Tooltip("每隔幾秒產生一張新訂單")]
        public float orderIntervalSeconds = 6f;
    }
}
