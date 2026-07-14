using UnityEngine;
using PizzaVR.Core;

namespace PizzaVR.Gameplay
{
    [RequireComponent(typeof(Rigidbody))]
    public class PizzaProjectile : MonoBehaviour
    {
        public PizzaFlavor Flavor { get; private set; }
        public bool Thrown { get; private set; }
        public GameBalanceConfig config;
        // Set true for a customer's throw-back (wrong flavor), so PlayerHitDetector can tell
        // it apart from the player's own held/thrown pizza.
        public bool IsReturnThrow;

        Rigidbody rb;
        Renderer bodyRenderer;
        Material bodyMaterial;

        // Lazily resolved rather than cached only in Awake(), since Awake does not run for
        // objects touched outside Play Mode (e.g. editor tooling instantiating the prefab).
        Rigidbody Rb => rb != null ? rb : rb = GetComponent<Rigidbody>();
        Renderer BodyRenderer => bodyRenderer != null ? bodyRenderer : bodyRenderer = GetComponent<Renderer>();

        public void SetFlavor(PizzaFlavor flavor)
        {
            Flavor = flavor;
            if (BodyRenderer == null)
                return;

            if (bodyMaterial == null)
            {
                bodyMaterial = new Material(Shader.Find("Standard"));
                BodyRenderer.sharedMaterial = bodyMaterial;
            }
            bodyMaterial.color = PizzaFlavorInfo.GetColor(flavor);
        }

        public void Throw(Vector3 velocity, Vector3 angularVelocity)
        {
            transform.SetParent(null);
            Rb.isKinematic = false;
            Rb.linearVelocity = velocity;
            Rb.angularVelocity = angularVelocity;
            Thrown = true;
            Destroy(gameObject, 15f); // cleanup if it never gets scored / settles somewhere
        }

        void FixedUpdate()
        {
            if (!Thrown || Rb.isKinematic || config == null)
                return;

            // The disc's flat-face normal is its local up axis (matches the prefab's thin
            // Y-scale "pizza box" shape).
            var discNormal = transform.up;

            Rb.angularVelocity = ComputeStabilizedAngularVelocity(
                Rb.angularVelocity, discNormal, config.frisbeeMinStableSpin,
                config.frisbeeSpinStabilization, Time.fixedDeltaTime);

            var lift = ComputeLiftForce(
                Rb.linearVelocity, Rb.angularVelocity, discNormal,
                config.frisbeeLiftCoefficient, config.frisbeeMinStableSpin);
            Rb.AddForce(lift, ForceMode.Acceleration);
        }

        // A spinning disc gyroscopically resists tipping away from flat; as spin (the
        // component of angular velocity around the disc's own face-normal) falls below
        // minStableSpin, that resistance fades out and the disc is free to tumble - pure
        // function so the damping math can be sanity-checked without Play Mode.
        public static Vector3 ComputeStabilizedAngularVelocity(Vector3 angularVelocity, Vector3 discNormal,
            float minStableSpin, float stabilizationStrength, float deltaTime)
        {
            float spinRate = Vector3.Dot(angularVelocity, discNormal);
            float stability = Mathf.Clamp01(Mathf.Abs(spinRate) / Mathf.Max(minStableSpin, 0.0001f));
            var tumble = angularVelocity - discNormal * spinRate;
            return angularVelocity - tumble * (stability * stabilizationStrength * deltaTime);
        }

        // Upward lift scaling with speed^2 and how flat the disc is facing (a disc on its
        // edge generates no lift), gated by the same spin-stability so a tumbling/spent disc
        // just falls like a dropped object instead of gliding.
        public static Vector3 ComputeLiftForce(Vector3 velocity, Vector3 angularVelocity, Vector3 discNormal,
            float liftCoefficient, float minStableSpin)
        {
            float spinRate = Vector3.Dot(angularVelocity, discNormal);
            float stability = Mathf.Clamp01(Mathf.Abs(spinRate) / Mathf.Max(minStableSpin, 0.0001f));
            float speed = velocity.magnitude;
            float flatness = Mathf.Clamp01(Vector3.Dot(discNormal, Vector3.up));
            return Vector3.up * (liftCoefficient * speed * speed * flatness * stability);
        }
    }
}
