using UnityEngine;
using PizzaVR.Core;
using PizzaVR.XR;

namespace PizzaVR.Gameplay
{
    // Left hand's Primary Button cycles the pizza flavor; the pizza sits parented to the
    // right hand until the player releases Grip, at which point the hand's tracked
    // velocity/angular velocity (from XRControllerInput) becomes the throw.
    //
    // While Grip is held, an aim line previews the flight path - but not from instantaneous
    // swing speed (which is near zero during slow aiming and only spikes right at the fast
    // release swing, making the line look like it "appears" only at the last instant).
    // Instead it previews from how far the hand has drawn back from where Grip was first
    // pressed (a power proxy) and the hand's current facing (aim direction), so the preview
    // grows and updates continuously through the whole wind-up. The actual throw on release
    // still uses the real swing velocity (ComputeThrowVelocity) - the preview is a guide, not
    // a guarantee, since the real result depends on how fast you actually swing at release.
    public class PizzaThrower : MonoBehaviour
    {
        public XRControllerInput leftHand;
        public XRControllerInput rightHand;
        public GameObject pizzaPrefab;
        public GameBalanceConfig config;

        static readonly PizzaFlavor[] Flavors = (PizzaFlavor[])System.Enum.GetValues(typeof(PizzaFlavor));
        int flavorIndex;
        PizzaProjectile heldPizza;
        LineRenderer aimLine;
        Vector3 drawAnchor;
        bool hasDrawAnchor;

        public PizzaFlavor CurrentFlavor => Flavors[flavorIndex];

        void Awake()
        {
            aimLine = gameObject.AddComponent<LineRenderer>();
            aimLine.positionCount = 0;
            aimLine.useWorldSpace = true;
            aimLine.material = new Material(Shader.Find("Sprites/Default"));
            aimLine.startColor = new Color(1f, 1f, 1f, 0.9f);
            aimLine.endColor = new Color(1f, 1f, 1f, 0.05f);
        }

        void Start()
        {
            SpawnHeldPizza();
        }

        void Update()
        {
            if (leftHand != null && leftHand.PrimaryButtonDown)
                CycleFlavor();

            if (heldPizza == null)
                SpawnHeldPizza();

            if (rightHand != null && rightHand.GripButtonDown)
            {
                drawAnchor = rightHand.transform.position;
                hasDrawAnchor = true;
            }

            if (rightHand != null && rightHand.GripButtonUp)
                ThrowHeldPizza();

            UpdateAimLine();
        }

        void CycleFlavor()
        {
            flavorIndex = (flavorIndex + 1) % Flavors.Length;
            if (heldPizza != null)
                heldPizza.SetFlavor(CurrentFlavor);
            Debug.Log($"PizzaThrower: flavor switched to {CurrentFlavor}");
        }

        void SpawnHeldPizza()
        {
            var go = Instantiate(pizzaPrefab, rightHand.transform);
            go.transform.localPosition = config.handHoldOffset;
            go.transform.localRotation = Quaternion.identity;
            heldPizza = go.GetComponent<PizzaProjectile>();
            heldPizza.config = config;
            heldPizza.SetFlavor(CurrentFlavor);
        }

        void ThrowHeldPizza()
        {
            var swingVelocity = ComputeThrowVelocity(rightHand.RecentDirection, rightHand.PeakSpeed,
                config.throwVelocityMultiplier, config.throwYawCorrectionDegrees);

            float drawDistance = hasDrawAnchor ? Vector3.Distance(rightHand.transform.position, drawAnchor) : 0f;
            var drawVelocity = ComputeDrawPreviewVelocity(rightHand.transform.forward, drawDistance,
                config.aimDrawSpeedPerMeter, config.aimMaxPreviewSpeed, config.throwYawCorrectionDegrees);

            var velocity = ComputeReleaseVelocity(swingVelocity, drawVelocity);

            // Spin only ever comes from the real swing - it's what drives lift, and there's no
            // equivalent "draw back" proxy for it. Pulling the hand back a long way without
            // any wrist rotation will still throw far (from the line above) but glide less,
            // same as a real frisbee released without a wrist snap.
            var angularVelocity = rightHand.PeakAngularVelocity * config.throwAngularVelocityMultiplier;

            Debug.Log($"PizzaThrower: throw velocity={velocity} (speed={velocity.magnitude:F2}, swing={swingVelocity.magnitude:F2}, draw={drawVelocity.magnitude:F2}) angularVelocity={angularVelocity}");
            heldPizza.Throw(velocity, angularVelocity);
            heldPizza = null;
            hasDrawAnchor = false;
        }

        void UpdateAimLine()
        {
            if (rightHand == null || !rightHand.GripHeld)
            {
                aimLine.positionCount = 0;
                hasDrawAnchor = false;
                return;
            }

            aimLine.widthMultiplier = config.aimLineWidth;

            var handTransform = rightHand.transform;
            var startPos = handTransform.TransformPoint(config.handHoldOffset);
            var discNormal = handTransform.up;

            float drawDistance = hasDrawAnchor ? Vector3.Distance(handTransform.position, drawAnchor) : 0f;
            var velocity = ComputeDrawPreviewVelocity(handTransform.forward, drawDistance,
                config.aimDrawSpeedPerMeter, config.aimMaxPreviewSpeed, config.throwYawCorrectionDegrees);
            // Current wrist rotation rate as a live spin proxy - winding up the wrist shows up
            // in the preview's glide immediately, same as the real throw would use at release.
            var angularVelocity = rightHand.AngularVelocity * config.throwAngularVelocityMultiplier;

            var points = SimulatePreview(startPos, velocity, angularVelocity, discNormal, config,
                config.aimLineSegments, config.aimLineSegmentDuration);
            aimLine.positionCount = points.Length;
            aimLine.SetPositions(points);
        }

        // Pure so the yaw-correction math can be sanity-checked without Play Mode.
        public static Vector3 ComputeThrowVelocity(Vector3 recentDirection, float peakSpeed,
            float velocityMultiplier, float yawCorrectionDegrees)
        {
            var correctedDirection = Quaternion.Euler(0f, yawCorrectionDegrees, 0f) * recentDirection;
            return correctedDirection * peakSpeed * velocityMultiplier;
        }

        // Whichever is stronger wins, so the throw never ends up weaker than what the aim line
        // just promised (a big draw-back with a gentle release still gets the drawn power; a
        // genuinely fast swing can still exceed it). Pure so it can be sanity-checked without
        // Play Mode.
        public static Vector3 ComputeReleaseVelocity(Vector3 swingVelocity, Vector3 drawVelocity)
        {
            return swingVelocity.sqrMagnitude >= drawVelocity.sqrMagnitude ? swingVelocity : drawVelocity;
        }

        // Wind-up preview: power grows with how far the hand has drawn back since Grip was
        // pressed, direction comes from where the hand currently faces - both available and
        // updating continuously well before the fast release swing happens. Pure so it can be
        // sanity-checked without Play Mode.
        public static Vector3 ComputeDrawPreviewVelocity(Vector3 handForward, float drawDistance,
            float speedPerMeter, float maxSpeed, float yawCorrectionDegrees)
        {
            float speed = Mathf.Min(drawDistance * speedPerMeter, maxSpeed);
            var direction = Quaternion.Euler(0f, yawCorrectionDegrees, 0f) * handForward.normalized;
            return direction * speed;
        }

        // Steps the same lift/spin-stabilization math PizzaProjectile.FixedUpdate uses, so the
        // preview matches what actually happens on release, instead of a generic dotted ray.
        // The disc's face-normal is assumed to hold roughly steady for the (short) preview
        // window rather than re-simulating full orientation drift.
        public static Vector3[] SimulatePreview(Vector3 startPos, Vector3 velocity, Vector3 angularVelocity,
            Vector3 discNormal, GameBalanceConfig config, int steps, float stepTime)
        {
            var points = new Vector3[steps + 1];
            var pos = startPos;
            var vel = velocity;
            var angVel = angularVelocity;
            points[0] = pos;

            int count = 1;
            for (int i = 0; i < steps; i++)
            {
                angVel = PizzaProjectile.ComputeStabilizedAngularVelocity(angVel, discNormal,
                    config.frisbeeMinStableSpin, config.frisbeeSpinStabilization, stepTime);
                var lift = PizzaProjectile.ComputeLiftForce(vel, angVel, discNormal,
                    config.frisbeeLiftCoefficient, config.frisbeeMinStableSpin);
                vel += (Physics.gravity + lift) * stepTime;
                pos += vel * stepTime;
                points[count++] = pos;

                if (pos.y < startPos.y - 5f)
                    break;
            }

            if (count != points.Length)
                System.Array.Resize(ref points, count);
            return points;
        }
    }
}
