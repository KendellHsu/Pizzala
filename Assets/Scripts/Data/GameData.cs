// ─────────────────────────────────────────────────────────────
// GameData.cs — 所有數據結構與列舉的唯一定義處
// 不需掛在任何物件上,純資料定義。
// 實驗分析時,SessionData 會整包輸出成 JSON。
// ─────────────────────────────────────────────────────────────
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Pizzala.Data
{
    public enum ExperimentCondition { Control, Experimental } // 對照組 / 實驗組

    public enum PizzaFlavor { Margherita, Pepperoni, CosmicPinkMarshmallow }

    // Unknown 同時作為「不限投擲方式」(Any) 使用
    public enum ThrowType { Unknown, Backhand, Forehand, Overhead, Underhand }

    public enum ThrowOutcome
    {
        Hit,             // 命中客人手掌且口味正確
        WrongFlavor,     // 到手了但口味錯
        MissFace,        // 砸到客人臉(觸發髒臉照片)
        MissBody,        // 砸到客人身體
        MissEnvironment  // 砸到環境(生成髒污 decal)
    }

    public enum TargetSector { Left, Center, Right }

    public enum DodgeDirection { None, Left, Right, Duck }

    // 手勢分類的原始特徵值——全部保留,事後可重新調閾值重跑分類
    [Serializable]
    public class ThrowFeatures
    {
        public float releaseSpeed;          // 出手速度 m/s(力道)
        public float releaseElevationDeg;   // 出手仰角(相對水平面)
        public float releaseHeightRelHead;  // 放手高度,相對頭部(公尺,正=高於頭)
        public float swingStartSide;        // 揮動起點側向偏移:正=持盤手同側,負=橫過身體(反手)
        public float verticalPlaneRatio;    // 軌跡垂直成分比例 0~1(高=過頭砸)
        public float rollAngleDeg;          // 放手瞬間控制器 roll(手心朝向)
        public float peakWristAngularSpeed; // 手腕甩動角速度峰值 deg/s(wrist snap)
    }

    [Serializable]
    public class ThrowRecord
    {
        public int throwId;
        public float gameTime;              // 回合開始起算的秒數
        public string hand;                 // "Left" / "Right"
        public ThrowType throwType;
        public ThrowFeatures features = new ThrowFeatures();

        public int targetCustomerId = -1;
        public TargetSector targetSector;
        public PizzaFlavor requestedFlavor;
        public PizzaFlavor thrownFlavor;
        public ThrowOutcome outcome;

        public float reactionTime = -1f;    // 客人舉手 → 出手(秒),無對應訂單為 -1
        public float flightTime;
        public Vector3 landingPosition;
        public string photoPath = "";       // 若這一丟觸發了截圖
    }

    [Serializable]
    public class DodgeRecord
    {
        public float gameTime;
        public bool dodged;                 // true=閃過, false=被砸中臉
        public float reactionTime;          // 預警開始 → 頭部開始移動(秒),沒動為 -1
        public DodgeDirection direction;
    }

    [Serializable]
    public class SensorSample
    {
        public float gameTime;
        public int hr;   // 心率 bpm,-1 = 無感測器
        public int gsr;  // 皮膚電導原始值,-1 = 無感測器
    }

    [Serializable]
    public class SessionSummary
    {
        public int totalThrows;
        public int hits;
        public float accuracyPercent;
        public float maxReleaseSpeed;
        public float maxWristSnapDegPerSec;

        public int backhandCount, forehandCount, overheadCount, underhandCount;

        public int leftThrows, leftHits;
        public int centerThrows, centerHits;
        public int rightThrows, rightHits;

        public int missedOrders;            // 超時沒送到的訂單數
        public int dirtCount;               // 環境髒污總數
        public int playerFaceHits;          // 玩家被砸中臉次數
        public int dodgeSuccess, dodgeTotal;

        public float avgReactionTime;
        public float totalHeadDistance;     // 頭部總移動距離(公尺)
        public int squatCount;
        public float turnDegreesTotal;
        public float estimatedCalories;     // 極粗估,僅供結算畫面顯示

        public int avgHeartRate = -1, maxHeartRate = -1;
    }

    // A captured photo plus when it was taken and what it shows - lets the results
    // screen display a timestamp/caption under each polaroid without guessing.
    [Serializable]
    public class PhotoRecord
    {
        public string path;
        public float gameTime;
        public string caption = "";
    }

    [Serializable]
    public class SessionData
    {
        public string sessionId;
        public string participantId = "";
        public ExperimentCondition condition;
        public string startedAtIso;

        public List<ThrowRecord> throws = new List<ThrowRecord>();
        public List<DodgeRecord> dodges = new List<DodgeRecord>();
        public List<SensorSample> sensorTimeline = new List<SensorSample>();

        public List<PhotoRecord> customerFacePhotos = new List<PhotoRecord>();
        public List<PhotoRecord> environmentPhotos = new List<PhotoRecord>();
        public List<PhotoRecord> playerFacePhotos = new List<PhotoRecord>();

        public SessionSummary summary = new SessionSummary();
    }
}
