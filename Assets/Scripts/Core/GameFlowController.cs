// ─────────────────────────────────────────────────────────────
// GameFlowController.cs — the whole session's state machine.
// Attach to: the "Systems" object (next to GameManager).
//
//   [StartScreen] --trigger the Start button--> [Starting: 5,4,3..] --> [Playing]
//                                                                          |
//                              [Paused] <-----------B---------------------+
//                                 |                                        ^
//                                 +--------B--> [Resuming: 3,2,1] ---------+
//
//              [Playing] --time up--> [Results] --flick right stick--> page turn
//
// Controls: the trigger picks up pizza AND presses UI buttons (so Start is a real button
// you point at, not a face button); B pauses/resumes; the right thumbstick flicks through
// the results pages.
//
// Keyboard stand-ins for testing without a headset: Y = start, B = pause/resume,
// U / I = previous / next results page.
//
// The stick is an axis, not a button, so it needs its own edge detection - see
// ReadStickFlick(). B comes from an InputAction, which handles that for us.
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
using Pizzala.UI;

namespace Pizzala.Core
{
    public enum GameFlowState { StartScreen, Starting, Playing, Paused, Resuming, Results }

    public class GameFlowController : MonoBehaviour
    {
        [Header("References")]
        public GameManager gameManager;
        public ResultsScreenController resultsScreen;
        [Tooltip("Stretches the controller ray while menus are up. Leave empty to skip (the Start button then won't be reachable in VR).")]
        public RayLengthSwitcher rayLengthSwitcher;

        [Header("Panels (leave empty to skip)")]
        public GameObject startScreenPanel;
        public GameObject pauseMenuPanel;
        [Tooltip("Shows the 5..1 pre-round count and the 3..1 resume count.")]
        public TMP_Text countdownText;

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
        [Header("Input - Start (keyboard only; VR uses the Start button)")]
        [Tooltip("The real Start is a UI button pressed with the trigger - this is only so the flow can be started on a PC with no ray to point with. Clear it to disable.")]
        public string startKeyboardPath = "<Keyboard>/y";

        [Header("Input - Pause/Resume (B)")]
        public string pauseVrPath = "<XRController>{RightHand}/secondaryButton";
        [Tooltip("Stand-in for B so the flow can be exercised on a PC with no headset.")]
        public string pauseKeyboardPath = "<Keyboard>/b";

        [Header("Input - Results Page (right thumbstick)")]
        public string stickVrPath = "<XRController>{RightHand}/thumbstick";
        [Tooltip("Stand-in for flicking the stick left (previous page).")]
        public string stickKeyboardPath = "<Keyboard>/u";
        [Tooltip("Stand-in for flicking the stick right (next page).")]
        public string stickKeyboardPath2 = "<Keyboard>/i";
        [Tooltip("How far the stick must go before a flick registers.")]
        [Range(0.3f, 0.95f)] public float stickFlickThreshold = 0.7f;
        [Tooltip("The stick must fall back inside this before another flick counts - stops one long hold from tearing through every page.")]
        [Range(0.05f, 0.5f)] public float stickReleaseThreshold = 0.3f;

        public GameFlowState State { get; private set; } = GameFlowState.StartScreen;

        InputAction startAction;
        InputAction pauseAction;
        InputAction stickAction;
        float countdownRemaining;
        bool stickArmed = true; // false while the stick is still pushed past the threshold

        void Awake()
        {
            if (gameManager == null) gameManager = GetComponent<GameManager>();

            // Built in code rather than added to the .inputactions asset: it keeps this
            // component self-contained, and the asset has no face-button bindings to extend.
            // Keyboard only: in VR, Start is a UI button pressed with the trigger, and the
            // trigger can't double as a "start" hotkey because it's also how you grab pizza.
            if (!string.IsNullOrEmpty(startKeyboardPath))
                startAction = new InputAction("Start", InputActionType.Button, startKeyboardPath);

            pauseAction = new InputAction("Pause", InputActionType.Button, pauseVrPath);
            if (!string.IsNullOrEmpty(pauseKeyboardPath)) pauseAction.AddBinding(pauseKeyboardPath);

            // Value, not Button: we want the stick's position every frame so we can do our
            // own flick + re-centre detection. Pinned to Vector2 so the keyboard stand-in
            // has to be a 2DVector composite too - mixing an axis in would leave the
            // action's value type ambiguous and ReadValue<Vector2>() would throw.
            stickAction = new InputAction("ResultsPage", InputActionType.Value, stickVrPath,
                                          expectedControlType: "Vector2");
            if (!string.IsNullOrEmpty(stickKeyboardPath))
                stickAction.AddCompositeBinding("2DVector")
                    .With("Left", stickKeyboardPath)
                    .With("Right", stickKeyboardPath2);
        }

        void OnEnable()
        {
            startAction?.Enable();
            pauseAction?.Enable();
            stickAction?.Enable();
            EnterStartScreen();
        }

        void OnDisable()
        {
            startAction?.Disable();
            pauseAction?.Disable();
            stickAction?.Disable();
        }

        void OnDestroy()
        {
            startAction?.Dispose();
            pauseAction?.Dispose();
            stickAction?.Dispose();
        }

        void Update()
        {
            // The round can end on its own (time up) with nobody pressing anything.
            if (State == GameFlowState.Playing && gameManager != null && !gameManager.RoundActive)
                EnterResults();

            if (State == GameFlowState.Starting || State == GameFlowState.Resuming) TickCountdown();

            if (State == GameFlowState.StartScreen && Pressed(startAction)) OnStartPressed();

            if (Pressed(pauseAction))
            {
                if (State == GameFlowState.Playing) EnterPaused();
                else if (State == GameFlowState.Paused) BeginResuming();
                // Resuming/Starting: already counting down, ignore so a double-tap can't skip it.
            }

            if (State == GameFlowState.Results && resultsScreen != null)
            {
                int flick = ReadStickFlick();
                if (flick > 0) resultsScreen.NextPage();
                else if (flick < 0) resultsScreen.PrevPage();
            }
        }

        static bool Pressed(InputAction a) => a != null && a.WasPressedThisFrame();

        // The stick is an axis, so it reports "pushed" every frame it's held - one flick
        // would otherwise chew through every page at once. Only fire when it crosses the
        // threshold, then stay locked out until it falls back near centre.
        int ReadStickFlick()
        {
            if (stickAction == null) return 0;

            float x = stickAction.ReadValue<Vector2>().x;
            if (Mathf.Abs(x) < stickReleaseThreshold) stickArmed = true;
            if (!stickArmed || Mathf.Abs(x) < stickFlickThreshold) return 0;

            stickArmed = false;
            return x > 0f ? 1 : -1;
        }

        // ── States ──

        // Single funnel for every transition so the log can't miss one.
        void SetState(GameFlowState next, string why)
        {
            if (logStateChanges)
                Debug.Log($"[GameFlow] {State} -> {next}  ({why}) | RoundActive={gameManager?.RoundActive} IsPaused={gameManager?.IsPaused} timeScale={Time.timeScale}");
            State = next;
        }

        void EnterStartScreen()
        {
            SetState(GameFlowState.StartScreen, "waiting for the Start button");
            SetActive(startScreenPanel, true);
            SetActive(pauseMenuPanel, false);
            SetActive(countdownText, false);
            rayLengthSwitcher?.UseUiRay(); // the Start button is unreachable with the grab-length ray
        }

        /// <summary>Hook the Start button's OnClick to this. Also fired by the keyboard stand-in.</summary>
        public void OnStartPressed()
        {
            if (State != GameFlowState.StartScreen) return;
            BeginStarting();
        }

        void BeginStarting()
        {
            SetState(GameFlowState.Starting, $"Start pressed - counting {startCountdownSeconds}s");
            countdownRemaining = startCountdownSeconds;
            SetActive(startScreenPanel, false); // the number replaces the menu rather than stacking on it
            SetActive(countdownText, true);
            UpdateCountdownText();
            rayLengthSwitcher?.UsePlayRay(); // back to grab length before they can reach for a pizza
        }

        void BeginPlaying()
        {
            SetState(GameFlowState.Playing, "countdown finished");
            SetActive(startScreenPanel, false);
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
            SetActive(startScreenPanel, false);
            SetActive(pauseMenuPanel, false);
            SetActive(countdownText, false);
            // The results screen itself is already up: GameManager.EndRound() calls Show().
        }

        static void SetActive(GameObject go, bool on) { if (go != null) go.SetActive(on); }
        static void SetActive(TMP_Text t, bool on) { if (t != null) t.gameObject.SetActive(on); }
    }
}
