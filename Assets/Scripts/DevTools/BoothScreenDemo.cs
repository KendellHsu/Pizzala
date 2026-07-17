// DEMO ONLY - feeds fake hits/time to the booth status screen so it can be previewed
// without a live round. Space = +1 hit, time counts down from demoRoundSeconds.
//
// This does NOT simulate head turning - that comes from the camera itself:
//   - no headset: put URP's FreeCamera on the preview camera and point the screen's
//     playerHead at it, then just move the mouse to look around (see Setup Booth Preview)
//   - with a headset: point playerHead at the real XR camera; nothing else needed
// Either way this component only supplies the numbers, matching what the real
// GameManager will call in BackBone (SetHits / SetTimeRemaining).
using UnityEngine;
using UnityEngine.InputSystem;
using Pizzala.UI;

namespace Pizzala.DevTools
{
    public class BoothScreenDemo : MonoBehaviour
    {
        public BoothStatusScreen screen;
        public float demoRoundSeconds = 120f;

        int hits;
        float timeLeft;

        void OnEnable()
        {
            hits = 0;
            timeLeft = demoRoundSeconds;
            Push();
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.spaceKey.wasPressedThisFrame) hits++;

            timeLeft = Mathf.Max(0f, timeLeft - Time.deltaTime);
            Push();
        }

        void Push()
        {
            if (screen == null) return;
            screen.SetHits(hits);
            screen.SetTimeRemaining(timeLeft);
        }
    }
}
