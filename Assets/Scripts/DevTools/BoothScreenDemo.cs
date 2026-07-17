// DEMO ONLY - preview the booth status screen without a headset. A/D (or left/right
// arrows) rotate a stand-in "head" so you can watch the screen slide around the booth
// ring; Space adds a hit; time counts down from demoRoundSeconds. Feeds the exact same
// SetHits/SetTimeRemaining the real GameManager will call in BackBone, so what you see
// here is what the wired-up version does.
using UnityEngine;
using UnityEngine.InputSystem;
using Pizzala.UI;

namespace Pizzala.DevTools
{
    public class BoothScreenDemo : MonoBehaviour
    {
        public BoothStatusScreen screen;
        public Transform fakeHead;      // assign this as the screen's playerHead for the demo
        public float turnSpeed = 90f;   // deg/sec
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
            if (kb == null || fakeHead == null) return;

            float turn = 0f;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) turn -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) turn += 1f;
            fakeHead.Rotate(0f, turn * turnSpeed * Time.deltaTime, 0f);

            if (kb.spaceKey.wasPressedThisFrame) hits++;

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
