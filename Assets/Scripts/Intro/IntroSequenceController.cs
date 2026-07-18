// ─────────────────────────────────────────────────────────────
// IntroSequenceController.cs — drives the Intro scene: title -> pre-roll Timeline -> load game.
// Attach to: a controller object in the Intro scene, alongside the PlayableDirector.
//
// Flow:
//   1. Scene opens on the title with a "Start Game" button (PlayableDirector Play On Awake OFF).
//      Hook that button's OnClick to StartTimeline() (or press the keyboard stand-in).
//   2. The Timeline plays the pre-roll story. Where the art wants the player to read a line,
//      a Signal Emitter on the Timeline calls OnDialoguePause() -> director.Pause() and shows
//      a "press trigger to continue" hint.
//   3. The player presses the trigger -> director.Resume() plays on to the next Signal.
//   4. When the Timeline finishes, director.stopped fires -> load the game scene.
//
// Trigger handling: the trigger is only read while we're actually paused-and-waiting. That
// stops the same trigger press that clicked "Start Game" from immediately being eaten as a
// "continue past the first dialogue" - the classic double-fire that skips line one.
//
// Timeline uses scaled time (DirectorUpdateMode.GameTime) by default; the Intro scene has no
// pause system so timeScale stays 1. Awake forces it to 1 anyway as insurance against a
// previous scene that quit while paused.
// ─────────────────────────────────────────────────────────────
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;

namespace Pizzala.Intro
{
    public class IntroSequenceController : MonoBehaviour
    {
        [Header("References")]
        public PlayableDirector director;
        [Tooltip("Shown while the Timeline is paused waiting for the player to press the trigger.")]
        public GameObject continueHint;
        [Tooltip("Title screen shown before the pre-roll starts. Hidden once StartTimeline() runs.")]
        public GameObject titlePanel;

        [Header("Scene to load when the pre-roll finishes")]
        public string gameSceneName = "BackBone";

        [Header("Input - continue / start (trigger)")]
        public string triggerVrPath = "<XRController>{RightHand}/trigger";
        [Tooltip("Keyboard stand-in for the trigger.")]
        public string triggerKeyboardPath = "<Keyboard>/y";

        InputAction triggerAction;
        bool waitingForContinue; // true only while paused at a Signal, so the trigger is read only then
        bool started;

        void Awake()
        {
            Time.timeScale = 1f; // insurance: a prior scene may have quit while paused

            triggerAction = new InputAction("IntroTrigger", InputActionType.Button, triggerVrPath);
            if (!string.IsNullOrEmpty(triggerKeyboardPath)) triggerAction.AddBinding(triggerKeyboardPath);

            if (director != null && director.playableAsset != null)
                director.playOnAwake = false; // we start it on the Start Game button, not automatically
        }

        void OnEnable()
        {
            triggerAction?.Enable();
            if (director != null) director.stopped += OnDirectorStopped;
            SetActive(titlePanel, true);
            SetActive(continueHint, false);
        }

        void OnDisable()
        {
            triggerAction?.Disable();
            if (director != null) director.stopped -= OnDirectorStopped;
        }

        void OnDestroy() => triggerAction?.Dispose();

        void Update()
        {
            if (waitingForContinue && triggerAction != null && triggerAction.WasPressedThisFrame())
                Resume();
        }

        /// <summary>Hook the title's Start Game button OnClick here (or press the keyboard stand-in).</summary>
        public void StartTimeline()
        {
            if (started || director == null) return;
            started = true;
            SetActive(titlePanel, false);
            SetActive(continueHint, false);
            director.Play();
        }

        /// <summary>Called by a Timeline Signal Emitter where the story should wait for the
        /// player. Set up a Signal Receiver on the director that invokes this.</summary>
        public void OnDialoguePause()
        {
            if (director == null) return;
            director.Pause();
            waitingForContinue = true;
            SetActive(continueHint, true);
        }

        void Resume()
        {
            waitingForContinue = false;
            SetActive(continueHint, false);
            director?.Resume();
        }

        void OnDirectorStopped(PlayableDirector d)
        {
            // Only the real end of the Timeline reaches here (Pause() doesn't fire stopped).
            SceneManager.LoadScene(gameSceneName);
        }

        static void SetActive(GameObject go, bool on) { if (go != null) go.SetActive(on); }
    }
}
