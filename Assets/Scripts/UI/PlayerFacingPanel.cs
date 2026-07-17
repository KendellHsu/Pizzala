// ─────────────────────────────────────────────────────────────
// PlayerFacingPanel.cs — keeps a world-space menu in front of the player with a lazy
// follow: it sits still for small head movements, then eases into view once you've
// turned away far enough.
//
// Deliberately NOT head-locked (rigidly parented to the camera). A panel welded to the
// view never moves relative to your head, which is the classic VR discomfort trigger;
// letting it live in the world and drift back into view reads as "always in front of
// you" without the nausea.
//
// Everything here runs on UNSCALED time on purpose: these panels are shown while the
// game is paused (Time.timeScale = 0), where scaled time stops dead and a scaled follow
// would freeze exactly when the player needs the menu to come to them.
// ─────────────────────────────────────────────────────────────
using UnityEngine;

namespace Pizzala.UI
{
    public class PlayerFacingPanel : MonoBehaviour
    {
        [Header("Follow Target")]
        [Tooltip("Main Camera. Left empty, it grabs Camera.main on enable.")]
        public Transform playerHead;

        [Header("Placement")]
        [Tooltip("Metres in front of the player.")]
        public float distance = 1.5f;
        [Tooltip("Metres above the player's eye level (negative sits it lower).")]
        public float heightOffset = -0.1f;

        [Header("Follow Feel")]
        [Tooltip("Stays put until your facing drifts this far off. Smaller than the booth screen's - a menu should come to you.")]
        public float deadzoneDegrees = 10f;
        [Tooltip("Seconds off-target before it starts moving.")]
        public float followDelaySeconds = 0.3f;
        [Tooltip("How quickly it catches up once moving.")]
        public float followSpeed = 6f;
        [Tooltip("Once this close, it settles and the deadzone re-arms.")]
        public float settleDegrees = 1f;

        float currentAngleDeg;
        bool chasing;
        float delayTimer;

        void OnEnable()
        {
            if (playerHead == null && Camera.main != null) playerHead = Camera.main.transform;
            SnapToPlayer(); // shown mid-pause: appear where they're looking, don't fly in from the last spot
        }

        void SnapToPlayer()
        {
            if (playerHead == null) return;
            currentAngleDeg = PlayerFacingAngle();
            chasing = false;
            delayTimer = 0f;
            ApplyTransform();
        }

        void LateUpdate()
        {
            if (playerHead == null) return;

            float target = PlayerFacingAngle();
            float gap = Mathf.Abs(Mathf.DeltaAngle(currentAngleDeg, target));

            if (!chasing)
            {
                if (gap > deadzoneDegrees)
                {
                    delayTimer += Time.unscaledDeltaTime;
                    if (delayTimer >= followDelaySeconds) { chasing = true; delayTimer = 0f; }
                }
                else delayTimer = 0f;
            }
            else if (gap <= settleDegrees) chasing = false;

            if (chasing)
                currentAngleDeg = Mathf.LerpAngle(currentAngleDeg, target,
                                                  1f - Mathf.Exp(-followSpeed * Time.unscaledDeltaTime));

            ApplyTransform();
        }

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
            Vector3 dir = new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad));

            transform.position = playerHead.position + dir * distance + Vector3.up * heightOffset;
            // Forward points away from the player: a canvas is readable from the side its
            // forward points away from (same reason the booth screen faces outward).
            transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
        }
    }
}
