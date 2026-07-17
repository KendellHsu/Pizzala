// ─────────────────────────────────────────────────────────────
// BoothStatusScreen.cs — a small world-space screen that rides the DJ booth's outer
// ring, sliding around to stay in front of the player as they turn, showing live
// hit count + time left. World-anchored (not head-locked) on purpose: a HUD glued to
// the camera is a classic VR comfort/occlusion problem, whereas a diegetic screen on
// the booth reads as part of the world and is far easier on the eyes.
//
// Decoupled from GameManager by design (division of labor - Kendell wires the data in
// BackBone): whatever owns the round state just calls SetHits()/SetTimeRemaining().
//
// A single flat panel, deliberately: a curved face built from fanned segments was tried
// and read worse than one clean panel at this size. It also doesn't sit out on the rim -
// pulling it in toward the centre (smaller orbitRadius) puts it at a comfortable reading
// distance instead of stuck out at arm's length.
// ─────────────────────────────────────────────────────────────
using TMPro;
using UnityEngine;

namespace Pizzala.UI
{
    // ExecuteAlways so the panel previews at its real computed spot while you tune the
    // numbers - LateUpdate skips the chase logic at edit time (see below).
    [ExecuteAlways]
    public class BoothStatusScreen : MonoBehaviour
    {
        [Header("Follow Target")]
        public Transform playerHead;   // Main Camera - its horizontal facing drives where the screen sits
        public Transform boothCenter;  // center of the booth ring the screen orbits

        [Header("Placement")]
        [Tooltip("Distance out from the booth centre (metres). The player stands at the centre, so this is also how far the screen is from their face - much under ~0.4 gets uncomfortable to focus on in VR.")]
        public float orbitRadius = 0.5f;
        public float height = 1.0f;        // height above boothCenter (meters)
        public float tiltUpDegrees = 20f;  // lean the screen's top back so its face points up toward the eyes

        [Header("Follow Feel")]
        [Tooltip("Where the screen parks relative to where you're looking. 0 = dead ahead (blocks the view); positive = off to your right.")]
        public float angleOffsetDegrees = 25f;
        [Tooltip("The screen sits still until it's this far off its parking spot - stops it twitching under every small head turn. Once past it, the screen catches up all the way, not just back to the edge of the deadzone.")]
        public float deadzoneDegrees = 25f;
        [Tooltip("Seconds to keep still after you turn before the screen follows. A turn that snaps back before this elapses is ignored, so glancing around doesn't drag it along.")]
        public float followDelaySeconds = 1.75f;
        [Tooltip("How quickly the screen catches up once it starts moving (higher = snappier).")]
        public float followSpeed = 3f;
        [Tooltip("Once this close to its parking spot, the screen settles and the deadzone re-arms.")]
        public float settleDegrees = 1.5f;

        [Header("Display")]
        public TMP_Text hitsText;
        public TMP_Text timeText;

        float currentAngleDeg; // angle around booth center (degrees) the screen currently sits at
        bool initialized;
        // Latches on when the deadzone is broken and stays on until the screen has caught
        // up. Without this the "outside the deadzone?" test would go false the moment the
        // gap shrank back to the deadzone, leaving the screen permanently parked that many
        // degrees off to one side instead of coming back in front of you.
        bool chasing;
        float delayTimer; // counts up while you're off-target but the delay hasn't elapsed yet

        void OnEnable() => SnapToPlayer();

        void SnapToPlayer()
        {
            if (playerHead == null || boothCenter == null) return;
            currentAngleDeg = TargetAngle();
            chasing = false;
            delayTimer = 0f;
            ApplyTransform();
            initialized = true;
        }

        // Where the screen wants to sit: off to one side of where you're looking, so it
        // isn't parked in the middle of the view you're trying to throw pizza through.
        float TargetAngle() => PlayerFacingAngle() + angleOffsetDegrees;

        void LateUpdate()
        {
            if (playerHead == null || boothCenter == null) return;

            if (!Application.isPlaying)
            {
                // Edit mode: place it correctly but never chase, so tuning orbitRadius /
                // height / offset previews truthfully without the screen running away from
                // you while you work.
                currentAngleDeg = TargetAngle();
                ApplyTransform();
                return;
            }

            if (!initialized) { SnapToPlayer(); return; }

            float target = TargetAngle();
            float gap = Mathf.Abs(Mathf.DeltaAngle(currentAngleDeg, target));

            if (!chasing)
            {
                if (gap > deadzoneDegrees)
                {
                    // Sit out the delay first - a glance that comes back before it elapses
                    // resets the timer, so the screen ignores quick looks around.
                    delayTimer += Time.deltaTime;
                    if (delayTimer >= followDelaySeconds) { chasing = true; delayTimer = 0f; }
                }
                else delayTimer = 0f;
            }
            else if (gap <= settleDegrees) chasing = false; // caught up; deadzone re-arms

            // Frame-rate-independent easing via 1 - e^(-k·dt).
            if (chasing)
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

            // A world-space Canvas reads correctly from the side its forward points AWAY
            // from (same as a default camera at -Z reading a canvas whose forward is +Z).
            // The player stands at the centre, so forward has to point outward, along dir -
            // pointing it inward at them shows the back of the canvas, i.e. mirrored text.
            // Tilt then leans the face up toward their eyes, since the screen sits at
            // counter height and they're looking down at it.
            transform.rotation = Quaternion.LookRotation(dir, Vector3.up) * Quaternion.Euler(tiltUpDegrees, 0f, 0f);
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
