// DEMO ONLY - not part of the real game flow. GameManager.EndRound() is the real path
// that builds SessionData live and calls resultsScreen.Show(); this bypasses all of that
// to preview the results screen with a real captured session instead of waiting on a
// live headset playtest. Reads from <repo root>/Data/sessions/ + Data/photos/ (outside
// Assets, Editor-only - this will not work in a build, which is fine, it's not meant to).
//
// The referenced JSON predates the PhotoRecord upgrade, so its three photo fields are
// still bare path strings - LegacySessionData parses that shape and RemapPhoto() converts
// each path to a PhotoRecord, guessing a caption from the filename prefix and pointing at
// the local Data/photos/ copy instead of the original recording machine's absolute path.
//
// Press 1 = Control, 2 = Middle, 3 = Experimental - same underlying data, only
// SessionData.condition changes, so you can compare all three against one real session.
// Press Space to advance to the next page within a condition (Middle/Experimental are
// now multi-page: P1 player photo+data -> P2 photo wall -> P3 boss note for Experimental).
// This key is a placeholder for whatever the real "next page" trigger ends up being.
using System;
using System.Collections;
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

        [Tooltip("Filename only - looked up under <repo root>/Data/sessions/")]
        public string sessionFileName = "session_20260716_105321_Control.json";

        [Header("Auto Load (skip the 1/2/3 keys, e.g. for a scene dedicated to one condition)")]
        public bool autoLoadOnStart = false;
        public ExperimentCondition autoLoadCondition = ExperimentCondition.Experimental;

        // Keyed by file name: the memorial-hall idea puts MANY pizza boxes in one scene,
        // each opening its own session's photos - so one flat cache slot isn't enough.
        readonly Dictionary<string, SessionData> loadedSessions = new Dictionary<string, SessionData>();

        void Start()
        {
            if (autoLoadOnStart) StartCoroutine(AutoLoadRoutine());
        }

        // One frame late on purpose: ResultsScreenController.Start() calls HideAllPanels()
        // to initialise itself, and Unity doesn't guarantee whose Start() runs first - show
        // the page in the same frame and the controller may immediately hide it again.
        // Waiting a frame guarantees every Start() has finished before we put the page up.
        // (The 1/2/3 keys never hit this because Update() always runs after all Starts.)
        IEnumerator AutoLoadRoutine()
        {
            yield return null;
            ShowAs(autoLoadCondition);
        }

        void Update()
        {
            if (Keyboard.current == null) return;
            if (Keyboard.current.digit1Key.wasPressedThisFrame) ShowAs(ExperimentCondition.Control);
            if (Keyboard.current.digit2Key.wasPressedThisFrame) ShowAs(ExperimentCondition.Middle);
            if (Keyboard.current.digit3Key.wasPressedThisFrame) ShowAs(ExperimentCondition.Experimental);
            if (Keyboard.current.spaceKey.wasPressedThisFrame) resultsScreen?.NextPage();
            // PC stand-in for ray-clicking the photo box (PhotoBoxTrigger needs an XR
            // interactor, which plain-mouse editor testing doesn't have).
            if (Keyboard.current.pKey.wasPressedThisFrame) ShowRecordedPhotos();
        }

        // Hooked up to a clickable prop in the world (see PhotoBoxTrigger) - loads the same
        // sample session this loader already uses for keys 1/2/3, and jumps straight to its
        // photo wall.
        public void ShowRecordedPhotos() => ShowRecordedPhotos(sessionFileName);

        // Per-box variant for a scene with MANY pizza boxes: each box's PhotoBoxTrigger
        // passes its own session file name (type it into the OnActivated event's string
        // field in the Inspector), so every box opens the photos of the round it belongs
        // to. The no-arg overload above stays for the single-box/P-key case.
        public void ShowRecordedPhotos(string fileName)
        {
            if (resultsScreen == null)
            {
                Debug.LogError("DemoResultsLoader: resultsScreen not assigned.");
                return;
            }

            var session = LoadCached(fileName);
            if (session == null) return;

            resultsScreen.ShowPhotoWallOnly(session);
        }

        void ShowAs(ExperimentCondition condition)
        {
            if (resultsScreen == null)
            {
                Debug.LogError("DemoResultsLoader: resultsScreen not assigned.");
                return;
            }

            var cached = LoadCached(sessionFileName);
            if (cached == null) return;

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

        SessionData LoadCached(string fileName)
        {
            if (loadedSessions.TryGetValue(fileName, out var hit)) return hit;
            var data = Load(fileName);
            if (data != null) loadedSessions[fileName] = data;
            return data;
        }

        SessionData Load(string fileName)
        {
            string dataRoot = Path.Combine(Path.GetFullPath(Path.Combine(Application.dataPath, "..")), "Data");
            string jsonPath = Path.Combine(dataRoot, "sessions", fileName);
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

        // Demo-only variety: these old JSON files carry no per-photo event context, so we
        // can't caption "which customer / what flavor". To at least stop every polaroid
        // reading "Hit in the face", pick a line from a pool keyed off the filename - stable
        // per photo (same photo always gets the same line) so re-running the demo doesn't
        // reshuffle. Real gameplay sets richer captions at capture time in GameManager.
        static readonly string[] FaceCaptions =
        {
            "Right in the face!", "Splat!", "Direct hit", "Oops... sorry!",
            "Faceplant special", "That's gonna leave a mark", "Nailed 'em", "Extra sauce, sir?",
        };
        static readonly string[] EnvCaptions =
        {
            "What a mess", "Cleanup on aisle 3", "The aftermath", "Sauce everywhere",
            "Health code violation", "Store overview",
        };
        static readonly string[] PlayerCaptions =
        {
            "You got hit!", "Payback's rough", "Didn't see that coming",
        };

        static PhotoRecord RemapPhoto(string originalPath, string photosDir)
        {
            string fileName = Path.GetFileName(originalPath.Replace('\\', '/'));
            string[] pool = fileName.StartsWith("face_") ? FaceCaptions
                          : fileName.StartsWith("env_") ? EnvCaptions
                          : fileName.StartsWith("player_") ? PlayerCaptions
                          : null;
            string caption = pool != null ? pool[StableIndex(fileName, pool.Length)] : "";
            return new PhotoRecord { path = Path.Combine(photosDir, fileName), gameTime = 0f, caption = caption };
        }

        // Deterministic filename -> pool index, so a given photo always gets the same line.
        static int StableIndex(string key, int count)
        {
            unchecked
            {
                int hash = 17;
                foreach (char c in key) hash = hash * 31 + c;
                return (hash & 0x7fffffff) % count;
            }
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
