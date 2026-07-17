// ─────────────────────────────────────────────────────────────
// BoothStatusScreen.cs — a small world-space screen that rides the DJ booth's outer
// ring, sliding around to stay in front of the player as they turn, showing live
// hit count + time left. World-anchored (not head-locked) on purpose: a HUD glued to
// the camera is a classic VR comfort/occlusion problem, whereas a diegetic screen on
// the booth reads as part of the world and is far easier on the eyes.
//
// Decoupled from GameManager by design (division of labor - Kendell wires the data in
// BackBone): whatever owns the round state just calls SetHits()/SetTimeRemaining().
// ─────────────────────────────────────────────────────────────
using TMPro;
using UnityEngine;

namespace Pizzala.UI
{
    public class BoothStatusScreen : MonoBehaviour
    {
        [Header("Follow Target")]
        public Transform playerHead;   // Main Camera - its horizontal facing drives where the screen sits
        public Transform boothCenter;  // center of the booth ring the screen orbits

        [Header("Placement")]
        public float orbitRadius = 0.6f;   // how far out on the ring (meters)
        public float height = 1.0f;        // height above boothCenter (meters)
        public float tiltUpDegrees = 20f;  // lean the screen's top back so its face points up toward the eyes

        [Header("Follow Feel")]
        [Tooltip("The screen only starts sliding once your facing drifts past this many degrees - stops it jittering under every small head turn.")]
        public float deadzoneDegrees = 25f;
        [Tooltip("How quickly the screen catches up once it starts moving (higher = snappier).")]
        public float followSpeed = 3f;

        [Header("Display")]
        public TMP_Text hitsText;
        public TMP_Text timeText;

        float currentAngleDeg; // angle around booth center (degrees) the screen currently sits at
        bool initialized;

        void OnEnable() => SnapToPlayer();

        void SnapToPlayer()
        {
            if (playerHead == null || boothCenter == null) return;
            currentAngleDeg = PlayerFacingAngle();
            ApplyTransform();
            initialized = true;
        }

        void LateUpdate()
        {
            if (playerHead == null || boothCenter == null) return;
            if (!initialized) { SnapToPlayer(); return; }

            float target = PlayerFacingAngle();
            // Only chase once the facing has drifted outside the deadzone; then ease toward it.
            // Frame-rate-independent easing via 1 - e^(-k·dt).
            if (Mathf.Abs(Mathf.DeltaAngle(currentAngleDeg, target)) > deadzoneDegrees)
                currentAngleDeg = Mathf.LerpAngle(currentAngleDeg, target, 1f - Mathf.Exp(-followSpeed * Time.deltaTime));

            ApplyTransform();
        }

        // Player's horizontal facing as an angle around +Z (degrees).
        float PlayerFacingAngle()
        {
            Vector3 fwd = playerHead.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 1e-4f) return currentAngleDeg;
            return Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg;
        }

        void ApplyTransform()
        {
            float rad = currentAngleDeg * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad)); // outward direction on the ring
            transform.position = boothCenter.position + dir * orbitRadius + Vector3.up * height;

            // Face inward toward the player at the center, then tilt the top back toward their eyes.
            transform.rotation = Quaternion.LookRotation(-dir, Vector3.up) * Quaternion.Euler(-tiltUpDegrees, 0f, 0f);
        }

        public void SetHits(int hits)
        {
            if (hitsText != null) hitsText.text = $"HITS  {hits}";
        }

        public void SetTimeRemaining(float seconds)
        {
            if (timeText == null) return;
            seconds = Mathf.Max(0f, seconds);
            int m = (int)(seconds / 60f);
            int s = (int)(seconds % 60f);
            timeText.text = $"TIME  {m}:{s:00}";
        }

        void OnDrawGizmosSelected()
        {
            if (boothCenter == null) return;
            Gizmos.color = Color.cyan;
            Vector3 c = boothCenter.position + Vector3.up * height;
            Vector3 prev = c + new Vector3(0, 0, orbitRadius);
            for (int i = 1; i <= 48; i++)
            {
                float a = i / 48f * Mathf.PI * 2f;
                Vector3 p = c + new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)) * orbitRadius;
                Gizmos.DrawLine(prev, p);
                prev = p;
            }
        }
    }
}
