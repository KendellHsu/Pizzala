using UnityEngine;
using UnityEngine.InputSystem;

namespace PizzaVR.XR
{
    // Drives this transform from Input System XR actions (position/rotation) and exposes
    // trigger/grip/button state plus velocity computed from position deltas.
    //
    // Works identically whether the underlying device is a real Meta Quest controller or the
    // XR Device Simulator (Assets/Samples/XR Interaction Toolkit/2.6.4/XR Device Simulator),
    // since both expose the same generic <XRController>/<XRHMD> Input System device layout.
    // Bind the action fields to the matching actions in "XRI Default Input Actions.inputactions"
    // (Assets/Samples/XR Interaction Toolkit/2.6.4/Starter Assets/).
    public class XRControllerInput : MonoBehaviour
    {
        [Header("Pose (XRI Head / XRI LeftHand / XRI RightHand)")]
        public InputActionReference positionAction;
        public InputActionReference rotationAction;

        [Header("Buttons - leave unassigned for the head")]
        public InputActionReference gripValueAction;     // "Select Value" - analog grip
        public InputActionReference triggerValueAction;  // "Activate Value" - analog trigger
        public InputActionReference primaryButtonAction; // "Primary Button"

        public Vector3 Velocity { get; private set; }
        public Vector3 AngularVelocity { get; private set; }

        // Power and aim are read from different moments of the swing, matching how a real
        // frisbee throw works: you sweep through an arc (often still moving sideways at the
        // exact instant of peak speed) and only settle into the intended aim direction right
        // as you release. PeakSpeed (+ the angular velocity paired with that fastest moment,
        // for spin) comes from the whole recent window; RecentDirection is an average of just
        // the last few frames, so a fast-but-diagonal mid-swing moment doesn't steer the throw.
        public float PeakSpeed { get; private set; }
        public Vector3 PeakAngularVelocity { get; private set; }
        public Vector3 RecentDirection { get; private set; }
        public int velocitySampleWindow = 6;
        public int recentDirectionFrames = 3;
        [Tooltip("If the average speed over the last recentDirectionFrames falls below this, the hand has basically stopped moving right before release (e.g. it settled before letting go) and that average direction can't be trusted - fall back to the fastest frame's direction instead of dropping to zero.")]
        public float minRecentSpeedForDirection = 0.15f;

        public float Grip { get; private set; }
        public float Trigger { get; private set; }
        public bool GripHeld => Grip > 0.6f;
        public bool PrimaryButton { get; private set; }
        public bool PrimaryButtonDown { get; private set; }
        public bool GripButtonDown { get; private set; }
        public bool GripButtonUp { get; private set; }

        Vector3 prevPosition;
        Quaternion prevRotation = Quaternion.identity;
        bool hasPrevPose;
        bool prevPrimary;
        bool prevGripHeld;

        Vector3[] velocityHistory;
        Vector3[] angularVelocityHistory;
        int historyIndex;
        int historyCount;

        void OnEnable()
        {
            positionAction?.action.Enable();
            rotationAction?.action.Enable();
            gripValueAction?.action.Enable();
            triggerValueAction?.action.Enable();
            primaryButtonAction?.action.Enable();
        }

        void OnDisable()
        {
            positionAction?.action.Disable();
            rotationAction?.action.Disable();
            gripValueAction?.action.Disable();
            triggerValueAction?.action.Disable();
            primaryButtonAction?.action.Disable();
        }

        void Update()
        {
            if (positionAction == null || rotationAction == null)
                return;

            // If no device currently satisfies this action (e.g. the XR Device Simulator was
            // switched to hand-tracking mode with 'H', or a controller was briefly lost),
            // ReadValue would silently return zero and snap the hand to the local origin -
            // right at the player's head. Hold the last known pose instead of vanishing there.
            if (positionAction.action.controls.Count == 0 || rotationAction.action.controls.Count == 0)
                return;

            var pos = positionAction.action.ReadValue<Vector3>();
            var rot = rotationAction.action.ReadValue<Quaternion>();
            transform.localPosition = pos;
            transform.localRotation = rot;

            if (hasPrevPose && Time.deltaTime > 0f)
            {
                Velocity = (pos - prevPosition) / Time.deltaTime;

                var deltaRot = rot * Quaternion.Inverse(prevRotation);
                deltaRot.ToAngleAxis(out var angleDeg, out var axis);
                if (angleDeg > 180f) angleDeg -= 360f;
                if (!float.IsNaN(axis.x) && !float.IsInfinity(axis.x))
                    AngularVelocity = axis * (angleDeg * Mathf.Deg2Rad / Time.deltaTime);

                RecordVelocitySample(Velocity, AngularVelocity);
            }
            prevPosition = pos;
            prevRotation = rot;
            hasPrevPose = true;

            Grip = gripValueAction != null ? gripValueAction.action.ReadValue<float>() : 0f;
            Trigger = triggerValueAction != null ? triggerValueAction.action.ReadValue<float>() : 0f;

            bool primary = primaryButtonAction != null && primaryButtonAction.action.IsPressed();
            PrimaryButtonDown = primary && !prevPrimary;
            PrimaryButton = primary;
            prevPrimary = primary;

            bool gripHeld = GripHeld;
            GripButtonDown = gripHeld && !prevGripHeld;
            GripButtonUp = !gripHeld && prevGripHeld;
            prevGripHeld = gripHeld;
        }

        // Public (rather than tucked away in Update) so the peak-picking logic can be
        // exercised directly in a test without needing Play Mode to tick real frames.
        public void RecordVelocitySample(Vector3 velocity, Vector3 angularVelocity)
        {
            int window = Mathf.Max(1, velocitySampleWindow);
            if (velocityHistory == null || velocityHistory.Length != window)
            {
                velocityHistory = new Vector3[window];
                angularVelocityHistory = new Vector3[window];
                historyIndex = 0;
                historyCount = 0;
            }

            velocityHistory[historyIndex] = velocity;
            angularVelocityHistory[historyIndex] = angularVelocity;
            historyIndex = (historyIndex + 1) % window;
            historyCount = Mathf.Min(historyCount + 1, window);

            // Power (+ paired spin + direction): whichever single frame in the window was fastest.
            var peakVelocity = Vector3.zero;
            var peakAngular = Vector3.zero;
            float peakMagSq = -1f;
            for (int i = 0; i < historyCount; i++)
            {
                float magSq = velocityHistory[i].sqrMagnitude;
                if (magSq > peakMagSq)
                {
                    peakMagSq = magSq;
                    peakVelocity = velocityHistory[i];
                    peakAngular = angularVelocityHistory[i];
                }
            }
            PeakSpeed = Mathf.Sqrt(Mathf.Max(0f, peakMagSq));
            PeakAngularVelocity = peakAngular;

            // Aim: average direction of just the most recent frames (closest to release). The
            // hand is often nearly stationary for a frame or two right as it lets go (settling
            // into the release), so if that average is too weak to trust as a direction, fall
            // back to the fastest frame's direction instead of collapsing to zero - otherwise
            // every throw ends up with no horizontal velocity at all, i.e. it just drops
            // straight down under gravity no matter which way the swing was aimed.
            int dirFrames = Mathf.Clamp(recentDirectionFrames, 1, historyCount);
            var sum = Vector3.zero;
            int idx = historyIndex;
            for (int i = 0; i < dirFrames; i++)
            {
                idx = (idx - 1 + window) % window;
                sum += velocityHistory[idx];
            }

            float avgSpeed = sum.magnitude / dirFrames;
            if (avgSpeed >= minRecentSpeedForDirection)
                RecentDirection = (sum / dirFrames).normalized;
            else if (PeakSpeed > 0.0001f)
                RecentDirection = peakVelocity.normalized;
            else
                RecentDirection = Vector3.zero;
        }
    }
}
