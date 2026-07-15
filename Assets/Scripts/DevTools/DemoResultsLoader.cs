// DEMO ONLY - not part of the real game flow. GameManager.EndRound() is the real path
// that builds SessionData live and calls resultsScreen.Show(); this bypasses all of that
// to preview the results screen with a real captured session instead of waiting on a
// live headset playtest. Reads from <repo root>/Data/ (outside Assets, Editor-only -
// this will not work in a build, which is fine, it's not meant to).
//
// The referenced JSON predates the PhotoRecord upgrade, so its three photo fields are
// still bare path strings - LegacySessionData parses that shape and RemapPhoto() converts
// each path to a PhotoRecord, guessing a caption from the filename prefix and pointing at
// the local Data/photos/ copy instead of the original recording machine's absolute path.
//
// Press 1 = Control, 2 = Middle, 3 = Experimental - same underlying data, only
// SessionData.condition changes, so you can compare all three against one real session.
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using Pizzala.Data;
using Pizzala.UI;
using Pizzala.LLM;

namespace Pizzala.DevTools
{
    public class DemoResultsLoader : MonoBehaviour
    {
        public ResultsScreenController resultsScreen;
        public BossCommentService bossCommentService; // optional - Experimental preview calls Gemini for real if set

        [Tooltip("Filename only - looked up under <repo root>/Data/session_20260715_013824_Control/")]
        public string sessionFileName = "session_20260715_092220_Control.json";

        SessionData cached;

        void Update()
        {
            if (Keyboard.current == null) return;
            if (Keyboard.current.digit1Key.wasPressedThisFrame) ShowAs(ExperimentCondition.Control);
            if (Keyboard.current.digit2Key.wasPressedThisFrame) ShowAs(ExperimentCondition.Middle);
            if (Keyboard.current.digit3Key.wasPressedThisFrame) ShowAs(ExperimentCondition.Experimental);
        }

        void ShowAs(ExperimentCondition condition)
        {
            if (resultsScreen == null)
            {
                Debug.LogError("DemoResultsLoader: resultsScreen not assigned.");
                return;
            }

            if (cached == null)
            {
                cached = Load();
                if (cached == null) return;
            }

            cached.condition = condition;
            resultsScreen.Show(cached); // puts up the "writing a note..." placeholder for Experimental

            if (condition == ExperimentCondition.Experimental && bossCommentService != null)
            {
                bossCommentService.GetComment(cached.summary, comment =>
                {
                    cached.bossComment = comment;
                    resultsScreen.SetBossComment(comment);
                });
            }

            Debug.Log($"DemoResultsLoader: showing {sessionFileName} as {condition}.");
        }

        SessionData Load()
        {
            string dataRoot = Path.Combine(Path.GetFullPath(Path.Combine(Application.dataPath, "..")), "Data");
            string jsonPath = Path.Combine(dataRoot, "session_20260715_013824_Control", sessionFileName);
            string photosDir = Path.Combine(dataRoot, "photos");

            if (!File.Exists(jsonPath))
            {
                Debug.LogError($"DemoResultsLoader: session file not found at {jsonPath}");
                return null;
            }

            var legacy = JsonUtility.FromJson<LegacySessionData>(File.ReadAllText(jsonPath));
            var data = new SessionData
            {
                sessionId = legacy.sessionId,
                participantId = legacy.participantId,
                startedAtIso = legacy.startedAtIso,
                throws = legacy.throws ?? new List<ThrowRecord>(),
                dodges = legacy.dodges ?? new List<DodgeRecord>(),
                sensorTimeline = legacy.sensorTimeline ?? new List<SensorSample>(),
                summary = legacy.summary,
            };

            foreach (var p in legacy.customerFacePhotos ?? new List<string>())
                data.customerFacePhotos.Add(RemapPhoto(p, photosDir));
            foreach (var p in legacy.environmentPhotos ?? new List<string>())
                data.environmentPhotos.Add(RemapPhoto(p, photosDir));
            foreach (var p in legacy.playerFacePhotos ?? new List<string>())
                data.playerFacePhotos.Add(RemapPhoto(p, photosDir));

            return data;
        }

        static PhotoRecord RemapPhoto(string originalPath, string photosDir)
        {
            string fileName = Path.GetFileName(originalPath.Replace('\\', '/'));
            string caption = fileName.StartsWith("face_") ? "Hit in the face"
                            : fileName.StartsWith("env_") ? "Store overview"
                            : fileName.StartsWith("player_") ? "Got hit"
                            : "";
            return new PhotoRecord { path = Path.Combine(photosDir, fileName), gameTime = 0f, caption = caption };
        }

        // Mirrors the pre-PhotoRecord SessionData shape (see git history on GameData.cs) -
        // only exists here to parse the old JSON files sitting in Data/.
        [Serializable]
        class LegacySessionData
        {
            public string sessionId;
            public string participantId;
            public string startedAtIso;
            public List<ThrowRecord> throws;
            public List<DodgeRecord> dodges;
            public List<SensorSample> sensorTimeline;
            public List<string> customerFacePhotos;
            public List<string> environmentPhotos;
            public List<string> playerFacePhotos;
            public SessionSummary summary;
        }
    }
}
