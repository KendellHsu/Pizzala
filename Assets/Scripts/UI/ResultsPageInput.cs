// ─────────────────────────────────────────────────────────────
// ResultsPageInput.cs — pages a ResultsScreenController with the right thumbstick.
// Attach to: any object in the Results scene (see SetupResultsScene.cs).
//
// Pulled out of GameFlowController rather than reused directly: that controller also
// owns the whole start/pause state machine, which lives in BackBone.unity and has no
// business existing in a standalone results scene. This is just the paging slice of it,
// with the exact same flick-detection logic and keyboard stand-ins (U/I) so behaviour
// matches whichever scene the player is actually in.
//
// The stick is an axis, not a button, so it reports "pushed" every frame it's held - one
// flick would otherwise chew through every page at once. Only fire when it crosses
// stickFlickThreshold, then stay locked out until it falls back under stickReleaseThreshold.
// ─────────────────────────────────────────────────────────────
using UnityEngine;
using UnityEngine.InputSystem;

namespace Pizzala.UI
{
    public class ResultsPageInput : MonoBehaviour
    {
        public ResultsScreenController resultsScreen;

        [Header("Input - Results Page (right thumbstick)")]
        public string stickVrPath = "<XRController>{RightHand}/thumbstick";
        [Tooltip("Stand-in for flicking the stick left (previous page).")]
        public string stickKeyboardPath = "<Keyboard>/u";
        [Tooltip("Stand-in for flicking the stick right (next page).")]
        public string stickKeyboardPath2 = "<Keyboard>/i";
        [Range(0.3f, 0.95f)] public float stickFlickThreshold = 0.7f;
        [Range(0.05f, 0.5f)] public float stickReleaseThreshold = 0.3f;

        InputAction stickAction;
        bool stickArmed = true; // false while the stick is still pushed past the threshold

        void Awake()
        {
            stickAction = new InputAction("ResultsPage", InputActionType.Value, stickVrPath,
                                          expectedControlType: "Vector2");
            if (!string.IsNullOrEmpty(stickKeyboardPath))
                stickAction.AddCompositeBinding("2DVector")
                    .With("Left", stickKeyboardPath)
                    .With("Right", stickKeyboardPath2);
        }

        void OnEnable() => stickAction?.Enable();
        void OnDisable() => stickAction?.Disable();
        void OnDestroy() => stickAction?.Dispose();

        void Update()
        {
            if (resultsScreen == null) return;
            int flick = ReadStickFlick();
            if (flick > 0) resultsScreen.NextPage();
            else if (flick < 0) resultsScreen.PrevPage();
        }

        int ReadStickFlick()
        {
            if (stickAction == null) return 0;

            float x = stickAction.ReadValue<Vector2>().x;
            if (Mathf.Abs(x) < stickReleaseThreshold) stickArmed = true;
            if (!stickArmed || Mathf.Abs(x) < stickFlickThreshold) return 0;

            stickArmed = false;
            return x > 0f ? 1 : -1;
        }
    }
}
