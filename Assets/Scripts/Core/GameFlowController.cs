// ─────────────────────────────────────────────────────────────
// GameFlowController.cs — the whole session's state machine.
// Attach to: the "Systems" object (next to GameManager).
//
//   [Tutorial] --flick to page 4, trigger "Start Game"--> [Starting: 5,4,3..] --> [Playing]
//        ^ scene loads here, arriving fresh from Intro every time                  |
//                              [Paused] <-----------B---------------------+
//                                 |                                        ^
//                                 +--------B--> [Resuming: 3,2,1] ---------+
//
//              [Playing] --time up--> [Results] --flick stick--> page turn
//              [Results] --end-of-round button--> load Intro (whole flow again)
//
// The scene opens on [Tutorial] (4 videos), not a Start menu: the player arrives here from
// the Intro scene after the pre-roll Timeline. Flicking the stick pages the videos; the last
// page confirms "Start Game" with the trigger (either hand). There's a single end-of-round
// button (labelled "New Player"/"Play Again" - same action) that always reloads Intro, so
// every round starts clean and watches the tutorial again; no separate quick-replay path.
//
// Controls: the trigger confirms Start Game on the last tutorial page AND grabs pizza / presses
// UI buttons in play (read as Start only on the last tutorial page, so no clash); B pauses/
// resumes; the thumbstick flicks through tutorial videos and results pages (shared StickFlickReader).
//
// Keyboard stand-ins for testing without a headset: Y = start/advance, B = pause/resume,
// U / I = previous / next page (tutorial and results).
//
// Stick flicking (an axis needs edge detection) lives in the shared StickFlickReader.
// B comes from an InputAction, which handles edge detection for us.
//
// Pausing itself lives in GameManager (Time.timeScale). Everything here that must keep
// running while paused - the countdowns, this Update - uses unscaled time, since seconds
// stop passing at timeScale 0 while frames keep coming.
//
// The ray has to stretch whenever a menu is up (RayLengthSwitcher): during play it's cut
// to 0.15m so the trigger grabs pizza rather than the room, which is far too short to
// reach a panel floating 1.5m away.
// ─────────────────────────────────────────────────────────────
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Pizzala.UI;

namespace Pizzala.Core
{
    // StartScreen is retired (the scene now opens on Tutorial). Kept in the enum only so any
    // old serialized data / logs referencing it don't break; the flow never enters it.
    public enum GameFlowState { StartScreen, Tutorial, Starting, Playing, Paused, Resuming, Results }

    public class GameFlowController : MonoBehaviour
    {
        [Header("References")]
        public GameManager gameManager;
        public ResultsScreenController resultsScreen;
        [Tooltip("Drives the 4 tutorial videos shown when the scene opens. Leave empty to skip the tutorial entirely (goes straight to the countdown).")]
        public TutorialController tutorialController;
        [Tooltip("Stretches the controller ray while menus are up. Leave empty to skip (the Start button then won't be reachable in VR).")]
        public RayLengthSwitcher rayLengthSwitcher;

        [Header("Scenes")]
        [Tooltip("Scene loaded at the end of every round - back to the title + pre-roll + tutorial for the next round/participant. There's only one end-of-round path now (no separate quick-replay), so every round watches the tutorial again.")]
        public string introSceneName = "Intro";

        [Header("Panels (leave empty to skip)")]
        public GameObject pauseMenuPanel;
        [Tooltip("Shows the 5..1 pre-round count and the 3..1 resume count. Lives on its own Screen Space - Overlay canvas (the GameFlowUI approach), separate from the tutorial canvas.")]
        public TMP_Text countdownText;
        [Tooltip("DEBUG: freeze the countdown at its start number so you can size/position the text without it ticking away. Turn off for real play.")]
        public bool freezeCountdown = false;

        [Header("Dev")]
        [Tooltip("Editor-only convenience: when opening BackBone directly (not via Intro), skip the 4 tutorial videos and drop into the countdown. Ignored in builds.")]
        public bool skipTutorialInEditor = false;

        [Header("Countdowns")]
        [Tooltip("Seconds after pressing Start before the round actually begins.")]
        public float startCountdownSeconds = 5f;
        [Tooltip("Seconds counted down before play resumes, so you aren't thrown straight back in mid-throw.")]
        public float resumeCountdownSeconds = 3f;

        [Header("Debug")]
        [Tooltip("Logs every state change to the Console. Useful before the panels exist, since the state is otherwise invisible.")]
        public bool logStateChanges = true;

        // Keyboard stand-ins avoid W/A/S/D/E/R/X/Z and the arrow keys: those are bound in
        // XRI Default Input Actions for movement and turning, so they'd double-fire once the
        // XR rig is in the scene. Y/U/I are only claimed by the XR Device Simulator, which
        // this project doesn't use.
        [Header("Input - Start Game (trigger)")]
        [Tooltip("Right-hand trigger that confirms the tutorial's last-page Start Game. Only read while the tutorial is on its last page, so it doesn't clash with grabbing pizza in play.")]
        public string triggerVrPath = "<XRController>{RightHand}/trigger";
        [Tooltip("Left-hand trigger also accepted for Start Game so either hand works. Clear to disable.")]
        public string triggerVrPathLeft = "<XRController>{LeftHand}/trigger";
        [Tooltip("Keyboard stand-in for A (also starts from the tutorial's last page on a PC with no headset).")]
        public string startKeyboardPath = "<Keyboard>/y";

        [Header("Input - Pause/Resume (B)")]
        public string pauseVrPath = "<XRController>{RightHand}/secondaryButton";
        [Tooltip("Stand-in for B so the flow can be exercised on a PC with no headset.")]
        public string pauseKeyboardPath = "<Keyboard>/b";

        [Header("Input - Page flick (thumbstick; tutorial + results share this)")]
        public string stickVrPath = "<XRController>{RightHand}/thumbstick";
        [Tooltip("Stand-in for flicking the stick left (previous page).")]
        public string stickKeyboardPath = "<Keyboard>/u";
        [Tooltip("Stand-in for flicking the stick right (next page).")]
        public string stickKeyboardPath2 = "<Keyboard>/i";
        [Tooltip("How far the stick must go before a flick registers.")]
        [Range(0.3f, 0.95f)] public float stickFlickThreshold = 0.7f;
        [Tooltip("The stick must fall back inside this before another flick counts - stops one long hold from tearing through every page.")]
        [Range(0.05f, 0.5f)] public float stickReleaseThreshold = 0.3f;

        public GameFlowState State { get; private set; } = GameFlowState.Tutorial;

        InputAction startAction;
        InputAction pauseAction;
        // One shared flick reader for both tutorial and results paging. They're mutually
        // exclusive states, so a single instance polled from one Update site is safe.
        StickFlickReader flick;
        float countdownRemaining;

        void Awake()
        {
            if (gameManager == null) gameManager = GetComponent<GameManager>();

            // Built in code rather than added to the .inputactions asset: keeps this component
            // self-contained. The trigger confirms the tutorial's Start Game button; we only
            // read it while the tutorial is on its last page, so it can still double as the
            // pizza-grab trigger during play without conflict.
            startAction = new InputAction("StartGame", InputActionType.Button, triggerVrPath);
            if (!string.IsNullOrEmpty(triggerVrPathLeft)) startAction.AddBinding(triggerVrPathLeft);
            if (!string.IsNullOrEmpty(startKeyboardPath)) startAction.AddBinding(startKeyboardPath);

            pauseAction = new InputAction("Pause", InputActionType.Button, pauseVrPath);
            if (!string.IsNullOrEmpty(pauseKeyboardPath)) pauseAction.AddBinding(pauseKeyboardPath);

            flick = new StickFlickReader(stickVrPath, stickKeyboardPath, stickKeyboardPath2,
                                         stickFlickThreshold, stickReleaseThreshold);
        }

        void OnEnable()
        {
            startAction?.Enable();
            pauseAction?.Enable();
            flick?.Enable();

            // Every round arrives here fresh from Intro and runs the tutorial; the editor
            // convenience skip is only for opening BackBone directly while iterating.
            bool skip = (Application.isEditor && skipTutorialInEditor) || tutorialController == null;
            if (skip) BeginStarting();
            else EnterTutorial();
        }

        void OnDisable()
        {
            startAction?.Disable();
            pauseAction?.Disable();
            flick?.Disable();
        }

        void OnDestroy()
        {
            startAction?.Dispose();
            pauseAction?.Dispose();
            flick?.Dispose();
        }

        void Update()
        {
            // The round can end on its own (time up) with nobody pressing anything.
            if (State == GameFlowState.Playing && gameManager != null && !gameManager.RoundActive)
                EnterResults();

            if (State == GameFlowState.Starting || State == GameFlowState.Resuming) TickCountdown();

            if (State == GameFlowState.Tutorial) TickTutorial();

            if (Pressed(pauseAction))
            {
                if (State == GameFlowState.Playing) EnterPaused();
                else if (State == GameFlowState.Paused) BeginResuming();
                // Resuming/Starting: already counting down, ignore so a double-tap can't skip it.
            }

            if (State == GameFlowState.Results && resultsScreen != null)
            {
                int f = flick.Poll();
                if (f > 0) resultsScreen.NextPage();
                else if (f < 0) resultsScreen.PrevPage();
            }
        }

        // Tutorial: flick pages the 4 videos; only on the last page does the trigger confirm
        // "Start Game". Reading the trigger only here keeps it from clashing with pizza-grab.
        void TickTutorial()
        {
            if (tutorialController == null) { BeginStarting(); return; } // nothing to show -> just start

            int f = flick.Poll();
            if (f > 0) tutorialController.NextPage();
            else if (f < 0) tutorialController.PrevPage();

            // A only starts the game from the last page. Log a press that lands on the wrong
            // page so "A does nothing" is diagnosable (usually: not actually on the last page).
            if (Pressed(startAction))
            {
                if (tutorialController.IsOnLastPage) BeginStarting();
                else if (logStateChanges)
                    Debug.Log("[GameFlow] Start pressed but not on the last tutorial page yet - flick to the end first.");
            }
        }

        static bool Pressed(InputAction a) => a != null && a.WasPressedThisFrame();

        // ── States ──

        // Single funnel for every transition so the log can't miss one.
        void SetState(GameFlowState next, string why)
        {
            if (logStateChanges)
                Debug.Log($"[GameFlow] {State} -> {next}  ({why}) | RoundActive={gameManager?.RoundActive} IsPaused={gameManager?.IsPaused} timeScale={Time.timeScale}");
            State = next;
        }

        void EnterTutorial()
        {
            SetState(GameFlowState.Tutorial, "showing the tutorial videos");
            SetActive(pauseMenuPanel, false);
            SetActive(countdownText, false);
            rayLengthSwitcher?.UseUiRay(); // the Start Game button (last page) needs pointing at
            tutorialController?.Begin(); // reset to page 1
        }

        /// <summary>Hook a "Start Game" UI button's OnClick to this if you want a pointer-click
        /// path in addition to the trigger. Only valid on the tutorial's last page.</summary>
        public void OnStartPressed()
        {
            if (State != GameFlowState.Tutorial) return;
            if (tutorialController != null && !tutorialController.IsOnLastPage) return;
            BeginStarting();
        }

        void BeginStarting()
        {
            SetState(GameFlowState.Starting, $"Start Game pressed - counting {startCountdownSeconds}s");
            countdownRemaining = startCountdownSeconds;
            tutorialController?.HideCanvas();    // clear the videos (but keep the canvas, the count sits on it)
            SetActive(countdownText, true);
            UpdateCountdownText();
            rayLengthSwitcher?.UsePlayRay(); // back to grab length before they can reach for a pizza
        }

        void BeginPlaying()
        {
            SetState(GameFlowState.Playing, "countdown finished");
            SetActive(pauseMenuPanel, false);
            SetActive(countdownText, false);
            if (gameManager != null) gameManager.StartRound();

            if (logStateChanges && gameManager != null && !gameManager.RoundActive)
                Debug.LogWarning("[GameFlow] StartRound() didn't take - RoundActive is still false. " +
                                 "GameManager bails out if tuning or SessionLogger.Instance is missing, " +
                                 "and the next frame will drop straight to Results.");
        }

        void EnterPaused()
        {
            SetState(GameFlowState.Paused, "B pressed");
            if (gameManager != null) gameManager.PauseRound();
            SetActive(pauseMenuPanel, true);
            SetActive(countdownText, false);
            rayLengthSwitcher?.UseUiRay();
        }

        void BeginResuming()
        {
            SetState(GameFlowState.Resuming, $"B pressed - counting {resumeCountdownSeconds}s");
            countdownRemaining = resumeCountdownSeconds;
            SetActive(pauseMenuPanel, false); // the number replaces the menu rather than stacking on it
            SetActive(countdownText, true);
            UpdateCountdownText();
            rayLengthSwitcher?.UsePlayRay();
            // Deliberately still paused - unpausing now would launch play before the
            // player has actually seen "3 2 1".
        }

        // Serves both counts: Starting (pre-round) and Resuming (post-pause). They differ
        // only in what happens at zero, so the ticking itself stays in one place.
        void TickCountdown()
        {
            // Debug: hold the count on-screen at its start number so the text can be sized and
            // positioned without it ticking to zero and vanishing. Never advances to play.
            if (freezeCountdown) { UpdateCountdownText(); return; }

            // unscaledDeltaTime: during Resuming the game is frozen, so scaled time isn't
            // moving at all; Starting runs before the round, where it makes no difference.
            countdownRemaining -= Time.unscaledDeltaTime;
            if (countdownRemaining > 0f) { UpdateCountdownText(); return; }

            SetActive(countdownText, false);

            if (State == GameFlowState.Starting)
            {
                BeginPlaying();
                return;
            }

            SetActive(pauseMenuPanel, false);
            SetState(GameFlowState.Playing, "countdown finished");
            if (gameManager != null) gameManager.ResumeRound();
        }

        void UpdateCountdownText()
        {
            if (countdownText != null)
                countdownText.text = Mathf.CeilToInt(countdownRemaining).ToString();
        }

        void EnterResults()
        {
            SetState(GameFlowState.Results, "round is no longer active");
            rayLengthSwitcher?.UseUiRay(); // the share button lives here and needs pointing at
            SetActive(pauseMenuPanel, false);
            SetActive(countdownText, false);
            // The results screen itself is already up: GameManager.EndRound() calls Show().
        }

        /// <summary>Hook the results screen's single end-of-round button OnClick to this
        /// (labelled "New Player" / "Play Again" - same action either way, no split needed).
        /// Loads the Intro scene so the next round always gets the whole flow again (title,
        /// pre-roll, tutorial). Everything - sauce splats, dropped pizzas, dirtied customers,
        /// session state - clears with the scene unload; nothing needs resetting here.</summary>
        public void OnNewPlayerPressed()
        {
            if (State != GameFlowState.Results) return;
            resultsScreen?.Hide();
            SceneManager.LoadScene(introSceneName);
        }

        static void SetActive(GameObject go, bool on) { if (go != null) go.SetActive(on); }
        static void SetActive(TMP_Text t, bool on) { if (t != null) t.gameObject.SetActive(on); }
    }
}
