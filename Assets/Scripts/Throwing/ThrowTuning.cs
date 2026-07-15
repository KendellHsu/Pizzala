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

        [Header("客人情緒加速(等餐越久走越快)")]
        [Tooltip("等餐幾秒後開始不耐煩(慢速遊走)")]
        public float customerImpatientAt = 5f;

        [Tooltip("等餐幾秒後變暴躁(快速遊走),需小於 customerPatience")]
        public float customerUrgentAt = 10f;

        [Tooltip("不耐煩時的遊走速度 m/s")]
        public float customerImpatientMoveSpeed = 0.4f;

        [Tooltip("暴躁時的遊走速度 m/s")]
        public float customerUrgentMoveSpeed = 0.9f;

        [Tooltip("遊走範圍半徑(公尺),以站位點為圓心,不會走出這個圈")]
        public float customerWanderRadius = 0.7f;

        [Tooltip("走到遊走目標點後停頓的機率 0~1,越高越常停下來")]
        [Range(0f, 1f)]
        public float customerWanderPauseChance = 0.7f;

        [Tooltip("每次停頓的秒數下限")]
        public float customerWanderPauseMinSeconds = 1f;

        [Tooltip("每次停頓的秒數上限")]
        public float customerWanderPauseMaxSeconds = 2.5f;

        [Header("客人動態生成(CustomerSpawner 讀取)")]
        [Tooltip("回合開始時 Spawner 先補幾個客人(場景預擺的不算)")]
        public int initialSpawnCount = 2;

        [Tooltip("之後每隔幾秒生成一個新客人(下限)")]
        public float minSpawnInterval = 8f;

        [Tooltip("之後每隔幾秒生成一個新客人(上限)")]
        public float maxSpawnInterval = 15f;

        [Tooltip("生成客人閒著沒訂單的最長停留秒數,超過就離場讓位")]
        public float customerLifetime = 40f;

        [Tooltip("訂單結束(滿意/生氣)後停留幾秒才離場,讓玩家看到表情")]
        public float customerLeaveDelay = 2.5f;

        [Tooltip("丟回前的預警時間(秒),給玩家反應窗口")]
        public float telegraphSeconds = 0.8f;

        [Tooltip("丟回披薩的飛行速度 m/s(越慢越好躲)")]
        public float throwbackSpeed = 5f;

        [Header("回合設定")]
        public float roundDurationSeconds = 180f;

        [Tooltip("每隔幾秒產生一張新訂單")]
        public float orderIntervalSeconds = 6f;

        [Header("飛盤氣動模擬(FrisbeeFlight 讀取,參考 Hummel/Hubbard 飛盤模型)")]
        [Tooltip("自旋要多大(rad/s)盤子才會產生升力、開始像飛盤平飛;甩得不夠力就直接掉。VR 手甩約 5~10,故預設調低")]
        public float frisbeeSpinToFly = 6f;

        [Tooltip("自旋純度門檻 0~1:(沿盤軸自旋)² / 總角速度²,越接近 1 越嚴格。VR 手甩帶手臂雜訊,預設放寬")]
        [Range(0f, 1f)]
        public float frisbeeSpinRatioThreshold = 0.5f;

        [Tooltip("剛體自旋上限(rad/s)。Unity 預設只有 7,會把手腕甩出的自旋砍掉,飛盤一定要調高")]
        public float frisbeeMaxAngularVelocity = 100f;

        [Tooltip("氣動力整體倍率(升力/阻力/力矩一起放大)。VR 出手比真實飛盤慢很多(力∝V²),故預設放大補償;太飄調低、太重調高")]
        public float frisbeeAeroScale = 3f;

        [Tooltip("盤面參考面積 A(m²)")]
        public float frisbeeArea = 0.0568f;

        [Tooltip("升力係數:零攻角升力 CL0")]
        public float frisbeeCL0 = 0.1f;

        [Tooltip("升力係數:每弧度攻角的升力斜率 CLα")]
        public float frisbeeCLA = 1.4f;

        [Tooltip("阻力係數:零攻角阻力 CD0")]
        public float frisbeeCD0 = 0.08f;

        [Tooltip("阻力係數:攻角平方項 CDα")]
        public float frisbeeCDA = 2.72f;

        [Tooltip("最小阻力對應的攻角 α0(度)")]
        public float frisbeeAlpha0Deg = -4f;

        [Tooltip("盤直徑 d(m),力矩的力臂")]
        public float frisbeeDiameter = 0.21f;

        [Tooltip("俯仰力矩:零攻角俯仰 CM0(負=盤子天生會低頭,配合自旋產生 fade)")]
        public float frisbeeCM0 = -0.08f;

        [Tooltip("俯仰力矩:每弧度攻角 CMα")]
        public float frisbeeCMA = 0.43f;

        [Tooltip("俯仰力矩:俯仰角速度阻尼 CMq")]
        public float frisbeeCMq = -0.005f;

        [Tooltip("滾轉力矩:隨自旋 CRr(配合陀螺效應造成 turn/bank)")]
        public float frisbeeCRR = 0.014f;

        [Tooltip("滾轉力矩:滾轉角速度阻尼 CRp")]
        public float frisbeeCRP = -0.0055f;

        [Tooltip("自旋衰減力矩 CNr(讓自旋隨飛行慢慢變慢)")]
        public float frisbeeCNR = -0.0000071f;

        [Tooltip("晃動阻尼(1/s):抑制盤面翻擺(垂直盤軸的角速度),讓自旋很快收乾淨、飛行更穩。\n" +
                 "效果隨自旋大小縮放(轉得快才收得快),所以軟 throw 照樣翻滾。0 = 純物理、最會晃;調高 = 更穩但較不真實")]
        public float frisbeeWobbleDamping = 3f;

        [Header("飛盤抓取(FrisbeeEdgeGrab 讀取)")]
        [Tooltip("盤半徑(公尺);≤ 0 會從披薩的 BoxCollider 自動推算")]
        public float frisbeeDiscRadius = 0.15f;

        [Tooltip("握點相對盤面的高度(本地 Y,公尺);0 = 盤面中線")]
        public float frisbeeGripHeight = 0f;

        [Tooltip("抓取時把盤面對齊控制器(而非保留抓取當下的傾角)")]
        public bool frisbeeAlignToController = true;

        [Tooltip("盤面相對控制器的旋轉 offset(尤拉角,度);frisbeeAlignToController 開時生效,自己調到手感對")]
        public Vector3 frisbeeGripRotationEuler = Vector3.zero;
    }
}
