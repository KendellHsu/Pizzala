// ─────────────────────────────────────────────────────────────
// SessionLogger.cs — 收集整場數據,結束時計算統計並存 JSON
// 掛載:場景中一個空物件(建議命名 "Systems"),整個場景只能有一個。
// Inspector:不用填任何東西。
// 輸出:Application.persistentDataPath/sessions/*.json
//   Quest 上路徑為 /sdcard/Android/data/<包名>/files/sessions/
//   可用 SideQuest 或 adb pull 取回。
// ─────────────────────────────────────────────────────────────
using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace Pizzala.Data
{
    public class SessionLogger : MonoBehaviour
    {
        public static SessionLogger Instance { get; private set; }

        public SessionData Session { get; private set; }
        public bool SessionActive { get; private set; }
        public float SessionStartTime { get; private set; }

        int nextThrowId = 1;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        public float GameTime => SessionActive ? Time.time - SessionStartTime : 0f;

        public void BeginSession(ExperimentCondition condition, string participantId)
        {
            Session = new SessionData
            {
                sessionId = DateTime.Now.ToString("yyyyMMdd_HHmmss"),
                participantId = participantId,
                condition = condition,
                startedAtIso = DateTime.Now.ToString("o")
            };
            nextThrowId = 1;
            SessionStartTime = Time.time;
            SessionActive = true;
        }

        public ThrowRecord CreateThrow()
        {
            var r = new ThrowRecord { throwId = nextThrowId++, gameTime = GameTime };
            return r;
        }

        public void Record(ThrowRecord r)
        {
            if (SessionActive) Session.throws.Add(r);
        }

        public void RecordDodge(bool dodged, float reactionTime, DodgeDirection dir)
        {
            if (!SessionActive) return;
            Session.dodges.Add(new DodgeRecord
            {
                gameTime = GameTime,
                dodged = dodged,
                reactionTime = reactionTime,
                direction = dir
            });
        }

        public void AddSensorSample(int hr, int gsr)
        {
            if (!SessionActive) return;
            Session.sensorTimeline.Add(new SensorSample { gameTime = GameTime, hr = hr, gsr = gsr });
        }

        public void AddCustomerFacePhoto(string path) { if (SessionActive && !string.IsNullOrEmpty(path)) Session.customerFacePhotos.Add(path); }
        public void AddEnvironmentPhoto(string path) { if (SessionActive && !string.IsNullOrEmpty(path)) Session.environmentPhotos.Add(path); }
        public void AddPlayerFacePhoto(string path)  { if (SessionActive && !string.IsNullOrEmpty(path)) Session.playerFacePhotos.Add(path); }

        // 回合結束時呼叫:計算所有統計數據
        public void BuildSummary(int dirtCount, int missedOrders,
                                 float headDistance, int squatCount, float turnDegrees)
        {
            var s = new SessionSummary();
            s.dirtCount = dirtCount;
            s.missedOrders = missedOrders;
            s.totalHeadDistance = headDistance;
            s.squatCount = squatCount;
            s.turnDegreesTotal = turnDegrees;

            float reactionSum = 0f; int reactionN = 0;

            foreach (var t in Session.throws)
            {
                s.totalThrows++;
                if (t.outcome == ThrowOutcome.Hit) s.hits++;
                s.maxReleaseSpeed = Mathf.Max(s.maxReleaseSpeed, t.features.releaseSpeed);
                s.maxWristSnapDegPerSec = Mathf.Max(s.maxWristSnapDegPerSec, t.features.peakWristAngularSpeed);

                switch (t.throwType)
                {
                    case ThrowType.Backhand: s.backhandCount++; break;
                    case ThrowType.Forehand: s.forehandCount++; break;
                    case ThrowType.Overhead: s.overheadCount++; break;
                    case ThrowType.Underhand: s.underhandCount++; break;
                }

                if (t.targetCustomerId >= 0)
                {
                    bool hit = t.outcome == ThrowOutcome.Hit;
                    switch (t.targetSector)
                    {
                        case TargetSector.Left: s.leftThrows++; if (hit) s.leftHits++; break;
                        case TargetSector.Center: s.centerThrows++; if (hit) s.centerHits++; break;
                        case TargetSector.Right: s.rightThrows++; if (hit) s.rightHits++; break;
                    }
                }

                if (t.reactionTime > 0f) { reactionSum += t.reactionTime; reactionN++; }
            }

            s.accuracyPercent = s.totalThrows > 0 ? 100f * s.hits / s.totalThrows : 0f;
            s.avgReactionTime = reactionN > 0 ? reactionSum / reactionN : -1f;

            foreach (var d in Session.dodges)
            {
                s.dodgeTotal++;
                if (d.dodged) s.dodgeSuccess++;
                else s.playerFaceHits++;
            }

            int hrSum = 0, hrN = 0;
            foreach (var samp in Session.sensorTimeline)
            {
                if (samp.hr > 0) { hrSum += samp.hr; hrN++; s.maxHeartRate = Mathf.Max(s.maxHeartRate, samp.hr); }
            }
            if (hrN > 0) s.avgHeartRate = hrSum / hrN;

            // 極粗估熱量:僅供顯示製造「運動感」,不具生理準確性
            s.estimatedCalories = headDistance * 0.35f + s.totalThrows * 0.4f + squatCount * 0.5f;

            Session.summary = s;
            SessionActive = false;
        }

        public string SaveToDisk()
        {
            string dir = Path.Combine(Application.persistentDataPath, "sessions");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, $"session_{Session.sessionId}_{Session.condition}.json");
            File.WriteAllText(path, JsonUtility.ToJson(Session, true), Encoding.UTF8);
            Debug.Log($"[SessionLogger] 已儲存:{path}");
            return path;
        }
    }
}
