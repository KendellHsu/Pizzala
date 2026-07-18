// ─────────────────────────────────────────────────────────────
// StickFlickReader.cs — turns a thumbstick's x-axis into discrete left/right "flicks".
//
// The stick is an axis: held to one side it reports "pushed" every single frame, so a
// naive read would tear through a whole page list in one shove. This reads a flick only
// when the stick crosses flickThreshold, then locks out until it falls back inside
// releaseThreshold (re-centres) - one physical flick = exactly one page.
//
// Pulled out of GameFlowController so the results screen and the tutorial can page with
// identical feel from one place. IMPORTANT: give each caller its OWN instance and Poll()
// it from exactly one place per frame - two callers sharing an instance, or polling twice
// in a frame, would double-consume or steal each other's flicks. Because the states that
// use it (Results, Tutorial) are mutually exclusive in the flow, one instance per owner
// with a single Poll() site is enough; the armed state self-heals when the stick re-centres.
//
// Not a MonoBehaviour: it's a plain helper owned by whoever needs it. The owner is
// responsible for Enable()/Disable()/Dispose() around its own lifecycle.
// ─────────────────────────────────────────────────────────────
using UnityEngine;
using UnityEngine.InputSystem;

namespace Pizzala.UI
{
    public class StickFlickReader
    {
        readonly InputAction action;
        readonly float flickThreshold;
        readonly float releaseThreshold;
        bool armed = true; // false while the stick is still pushed past flickThreshold

        /// <param name="vrPath">e.g. "&lt;XRController&gt;{RightHand}/thumbstick" (Vector2).</param>
        /// <param name="keyboardLeftPath">keyboard stand-in for a left flick, or null to skip.</param>
        /// <param name="keyboardRightPath">keyboard stand-in for a right flick, or null to skip.</param>
        public StickFlickReader(string vrPath, string keyboardLeftPath, string keyboardRightPath,
                                float flickThreshold, float releaseThreshold)
        {
            this.flickThreshold = flickThreshold;
            this.releaseThreshold = releaseThreshold;

            // Value/Vector2 (not Button): we want the raw position every frame to run our own
            // flick + re-centre detection. The keyboard stand-in must be a 2DVector composite
            // too, or the action's value type would be ambiguous and ReadValue<Vector2>() throws.
            action = new InputAction("StickFlick", InputActionType.Value, vrPath, expectedControlType: "Vector2");
            if (!string.IsNullOrEmpty(keyboardLeftPath) && !string.IsNullOrEmpty(keyboardRightPath))
                action.AddCompositeBinding("2DVector")
                    .With("Left", keyboardLeftPath)
                    .With("Right", keyboardRightPath);
        }

        public void Enable() => action.Enable();
        public void Disable() => action.Disable();
        public void Dispose() => action.Dispose();

        /// <summary>Call exactly once per frame from a single site. Returns +1 (right), -1
        /// (left), or 0. Fires at most once per physical flick.</summary>
        public int Poll()
        {
            float x = action.ReadValue<Vector2>().x;
            if (Mathf.Abs(x) < releaseThreshold) armed = true;
            if (!armed || Mathf.Abs(x) < flickThreshold) return 0;

            armed = false;
            return x > 0f ? 1 : -1;
        }
    }
}
