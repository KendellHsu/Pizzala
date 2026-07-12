using UnityEngine;

namespace PizzaVR.Core
{
    // All gameplay tuning numbers live here instead of as hardcoded constants, so the team
    // can experiment with pacing/difficulty by editing this one asset in the Inspector -
    // including live, while the game is running in Play Mode - without touching code or
    // triggering a scene/prefab rebuild.
    [CreateAssetMenu(fileName = "GameBalanceConfig", menuName = "Pizza VR/Game Balance Config")]
    public class GameBalanceConfig : ScriptableObject
    {
        [Header("Customer Spawning")]
        public int initialCustomerCount = 6;
        public float minSpawnInterval = 3f;
        public float maxSpawnInterval = 5f;
        public int sectorCount = 8;
        [Range(0f, 0.5f), Tooltip("Fraction of a sector's angular width a customer may be jittered from the sector's center line.")]
        public float sectorJitter = 0.35f;
        public DistanceTier[] distanceTiers =
        {
            new DistanceTier { minRadius = 1.8f, maxRadius = 2.4f }, // near
            new DistanceTier { minRadius = 3.0f, maxRadius = 3.8f }, // mid
            new DistanceTier { minRadius = 4.6f, maxRadius = 5.6f }, // far
        };

        [Header("Customer Waiting (seconds since spawn)")]
        public float customerImpatientAt = 10f;
        public float customerUrgentAt = 25f;
        public float customerLeaveAt = 40f;

        [Header("Customer Movement")]
        public float customerImpatientMoveSpeed = 0.3f;
        public float customerUrgentMoveSpeed = 0.8f;
        public float customerWanderRadius = 0.4f;

        [Header("Pizza Throwing")]
        public float throwVelocityMultiplier = 1.5f;
        [Tooltip("Hand-tracked spin is usually a weak wrist flick; amplified so the disc reaches frisbee-like spin rates.")]
        public float throwAngularVelocityMultiplier = 4f;
        public Vector3 handHoldOffset = new Vector3(0f, 0f, 0.08f);
        [Tooltip("Rotates the throw direction around the vertical axis to compensate for a consistent aim bias. Negative curves the throw left, positive curves it right.")]
        public float throwYawCorrectionDegrees = -10f;

        [Header("Pizza Flight (simplified frisbee glide)")]
        [Tooltip("Upward force per (speed^2 * flatness * spin stability). Higher = more glide.")]
        public float frisbeeLiftCoefficient = 0.15f;
        [Tooltip("How strongly the disc resists tumbling away from flat while spinning fast, per second.")]
        public float frisbeeSpinStabilization = 4f;
        [Tooltip("Spin rate (rad/s) around the disc's own face-normal below which it starts losing lift and stability, like a real disc running out of spin.")]
        public float frisbeeMinStableSpin = 5f;

        [Header("Delivery Scoring")]
        [Tooltip("Correct flavor landed on the plate, indexed by distance tier (near/mid/far) - farther is worth more.")]
        public int[] correctHitScoreByDistance = { 100, 150, 200 };
        [Tooltip("Wrong flavor, but it landed on the plate.")]
        public int wrongFlavorPenalty = -30;
        [Tooltip("Pizza hit the customer directly instead of the plate (bad aim), regardless of flavor.")]
        public int missedPlatePenalty = -50;
        [Tooltip("How many failed deliveries (wrong flavor or missed plate) a customer tolerates before storming off.")]
        public int customerMaxAttempts = 3;

        [Header("Customer Throwback / Dodge")]
        [Tooltip("Wrong flavor landed on the plate -> customer throws it back at the player's head; speed of that return throw.")]
        public float throwBackSpeed = 4f;
        [Tooltip("Height above the customer's feet the return throw launches from.")]
        public float throwBackSpawnHeight = 1f;
        [Tooltip("Extra penalty if the player doesn't dodge the returned pizza.")]
        public int dodgeFailPenalty = -40;

        [Header("Aim Line Preview (shown while gripping)")]
        [Tooltip("How many steps forward the aim line simulates.")]
        public int aimLineSegments = 30;
        [Tooltip("Simulated seconds per step; segments * this = how far into the future the line previews.")]
        public float aimLineSegmentDuration = 0.05f;
        public float aimLineWidth = 0.015f;
        [Tooltip("While winding up (Grip held, before the release swing), predicted preview speed added per meter the hand has drawn back from where Grip was first pressed - so the line already shows a growing prediction before you actually swing and let go.")]
        public float aimDrawSpeedPerMeter = 8f;
        [Tooltip("Clamp for the wind-up preview's predicted speed.")]
        public float aimMaxPreviewSpeed = 10f;
    }
}
