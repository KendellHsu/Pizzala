// ─────────────────────────────────────────────────────────────
// PhotoBoxSequence.cs — the History scene's click-a-pizza-box reveal:
//   (optional lid animation) → sauce splats over the whole view →
//   while the screen is covered, the three-page review swaps in behind it →
//   sauce fades out and the review is just... there.
//
// The splat doubles as the scene transition: revealDelay must stay under
// FaceSplatOverlay.holdSeconds (1.2s) so the review swap happens while the player
// literally cannot see it happen - that's what makes it read as "fades in from
// under the sauce" instead of "UI popped".
//
// Attach to: each pizza box, next to its PhotoBoxTrigger; wire the trigger's
// OnActivated to Play(). One sequence per box because the box owns its session file
// (the memorial-hall idea: many boxes, each opening its own round's review).
//
// The DemoResultsLoader dependency is the prototype's session source (repo Data/
// sample files); the real hall will read persistentDataPath saves instead, at which
// point the loader reference swaps for whatever that loader ends up being.
// ─────────────────────────────────────────────────────────────
using System.Collections;
using UnityEngine;
using Pizzala.DevTools;

namespace Pizzala.UI
{
    public class PhotoBoxSequence : MonoBehaviour
    {
        [Tooltip("This box's session - filename only, looked up under <repo root>/Data/sessions/.")]
        public string sessionFileName;

        [Header("References")]
        public DemoResultsLoader loader;
        public FaceSplatOverlay splatOverlay;

        [Header("Optional box-open animation (leave Animator empty to skip)")]
        public Animator boxAnimator;
        public string openTriggerName = "Open";
        [Tooltip("Seconds to let the lid animation play before the sauce hits.")]
        public float openSeconds = 0.6f;

        [Header("Timing")]
        [Tooltip("Seconds after the sauce lands before the review swaps in behind it. Keep UNDER the overlay's holdSeconds (1.2) so the swap is hidden.")]
        public float revealDelay = 0.8f;

        bool playing;

        /// <summary>Hook the box's PhotoBoxTrigger.OnActivated to this.</summary>
        public void Play()
        {
            if (playing) return; // mid-sequence re-clicks do nothing
            StartCoroutine(Sequence());
        }

        IEnumerator Sequence()
        {
            playing = true;

            if (boxAnimator != null)
            {
                boxAnimator.SetTrigger(openTriggerName);
                yield return new WaitForSeconds(openSeconds);
            }

            if (splatOverlay != null) splatOverlay.Show();
            yield return new WaitForSeconds(revealDelay);

            if (loader != null) loader.ShowRecordedSession(sessionFileName);
            else Debug.LogError("PhotoBoxSequence: loader not assigned.");

            playing = false;
        }
    }
}
