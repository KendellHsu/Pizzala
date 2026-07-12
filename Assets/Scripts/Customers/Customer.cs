using UnityEngine;
using PizzaVR.Core;
using PizzaVR.Gameplay;

namespace PizzaVR.Customers
{
    public enum CustomerState
    {
        Patient,   // 0-10s: stands still
        Impatient, // 10-25s: slow wander, slightly annoyed
        Urgent     // 25-40s: fast wander, annoyed; leaves once past this window
    }

    public enum DeliveryOutcome
    {
        Success,     // correct flavor, landed on the plate
        WrongFlavor, // landed on the plate, but the wrong flavor
        MissedPlate  // hit the customer directly instead of the plate (bad aim)
    }

    public class Customer : MonoBehaviour
    {
        public PizzaFlavor RequiredFlavor { get; private set; }
        public int Sector { get; private set; }
        public int DistanceIndex { get; private set; }
        // Spawn position; movement stays anchored around this point so a wandering
        // customer never drifts into a neighboring customer's slot.
        public Vector3 HomePosition { get; private set; }
        public CustomerState State { get; private set; }
        public float WaitTime { get; private set; }
        public int AttemptsRemaining { get; private set; }

        [Header("Visuals")]
        [SerializeField] Renderer bodyRenderer;
        [SerializeField] Renderer orderTicketRenderer;
        static readonly Color PatientColor = new Color(0.5f, 0.55f, 0.6f);
        static readonly Color ImpatientColor = new Color(0.85f, 0.75f, 0.25f);
        static readonly Color UrgentColor = new Color(0.8f, 0.2f, 0.2f);
        static readonly Color SuccessColor = new Color(0.25f, 0.9f, 0.35f);
        static readonly Color FailColor = new Color(0.95f, 0.1f, 0.1f);

        CustomerSpawner spawner;
        GameBalanceConfig config;
        GameObject pizzaPrefab;
        Vector3 wanderTarget;
        Material bodyMaterial;
        bool isLeaving;

        public void Init(CustomerSpawner owner, int sector, int distanceIndex, PizzaFlavor flavor)
        {
            spawner = owner;
            config = owner.config;
            pizzaPrefab = owner.pizzaPrefab;
            Sector = sector;
            DistanceIndex = distanceIndex;
            RequiredFlavor = flavor;
            HomePosition = transform.position;
            wanderTarget = HomePosition;
            State = CustomerState.Patient;
            WaitTime = 0f;
            AttemptsRemaining = config.customerMaxAttempts;

            if (orderTicketRenderer != null)
            {
                var mat = new Material(Shader.Find("Standard"));
                mat.color = PizzaFlavorInfo.GetColor(flavor);
                orderTicketRenderer.sharedMaterial = mat;
            }

            if (bodyRenderer != null)
            {
                bodyMaterial = new Material(Shader.Find("Standard"));
                bodyMaterial.color = PatientColor;
                bodyRenderer.sharedMaterial = bodyMaterial;
            }
        }

        void Update()
        {
            if (isLeaving)
                return;

            WaitTime += Time.deltaTime;

            if (WaitTime >= config.customerLeaveAt)
            {
                Leave();
                return;
            }

            State = ComputeState(WaitTime, config.customerImpatientAt, config.customerUrgentAt);

            UpdateMovement();
            UpdateVisual();
            FaceCenter();
        }

        void UpdateMovement()
        {
            float speed = State switch
            {
                CustomerState.Impatient => config.customerImpatientMoveSpeed,
                CustomerState.Urgent => config.customerUrgentMoveSpeed,
                _ => 0f
            };

            if (speed <= 0f)
                return;

            if (Vector3.Distance(transform.position, wanderTarget) < 0.05f)
            {
                var offset = Random.insideUnitCircle * config.customerWanderRadius;
                wanderTarget = HomePosition + new Vector3(offset.x, 0f, offset.y);
            }

            transform.position = Vector3.MoveTowards(transform.position, wanderTarget, speed * Time.deltaTime);
        }

        void UpdateVisual()
        {
            if (bodyMaterial == null)
                return;

            Color target = State switch
            {
                CustomerState.Impatient => ImpatientColor,
                CustomerState.Urgent => UrgentColor,
                _ => PatientColor
            };
            bodyMaterial.color = Color.Lerp(bodyMaterial.color, target, Time.deltaTime * 3f);
        }

        void FaceCenter()
        {
            var toCenter = new Vector3(0f, transform.position.y, 0f) - transform.position;
            if (toCenter.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(toCenter);
        }

        // Called by CustomerPlate (hitPlate: true) or CustomerBody (hitPlate: false) when a
        // thrown pizza reaches this customer.
        public void ReceiveDelivery(PizzaFlavor deliveredFlavor, bool hitPlate)
        {
            if (isLeaving)
                return;

            var outcome = EvaluateDelivery(deliveredFlavor, RequiredFlavor, hitPlate);

            if (outcome == DeliveryOutcome.Success)
            {
                ScoreManager.Instance?.AddScore(config.correctHitScoreByDistance[DistanceIndex]);
                FlashColor(SuccessColor);
                isLeaving = true;
                Invoke(nameof(Leave), 0.3f);
                return;
            }

            AttemptsRemaining--;
            int penalty = outcome == DeliveryOutcome.WrongFlavor ? config.wrongFlavorPenalty : config.missedPlatePenalty;
            ScoreManager.Instance?.AddScore(penalty);
            FlashColor(FailColor);

            if (outcome == DeliveryOutcome.WrongFlavor)
                ThrowBackPizza(deliveredFlavor);

            if (AttemptsRemaining <= 0)
            {
                isLeaving = true;
                Invoke(nameof(Leave), 0.3f);
            }
        }

        // Wrong flavor landed on the plate: the customer hurls it back at the player's head,
        // who must physically dodge (PlayerHitDetector) or take a penalty.
        void ThrowBackPizza(PizzaFlavor flavor)
        {
            if (pizzaPrefab == null || Camera.main == null)
                return;

            var spawnPos = transform.position + Vector3.up * config.throwBackSpawnHeight;
            var go = Instantiate(pizzaPrefab, spawnPos, Quaternion.identity);
            var projectile = go.GetComponent<PizzaProjectile>();
            projectile.config = config;
            projectile.SetFlavor(flavor);
            projectile.IsReturnThrow = true;

            var toPlayer = Camera.main.transform.position - spawnPos;
            var velocity = toPlayer.normalized * config.throwBackSpeed;
            projectile.Throw(velocity, Vector3.zero);
        }

        void FlashColor(Color color)
        {
            if (bodyMaterial != null)
                bodyMaterial.color = color;
        }

        public void Leave()
        {
            spawner.ReleaseSlot(Sector, DistanceIndex);
            Destroy(gameObject);
        }

        // Pure so the wait-time thresholds can be sanity-checked without Play Mode.
        public static CustomerState ComputeState(float waitTime, float impatientAt, float urgentAt)
        {
            if (waitTime >= urgentAt) return CustomerState.Urgent;
            if (waitTime >= impatientAt) return CustomerState.Impatient;
            return CustomerState.Patient;
        }

        // Pure so the hit/flavor/score branching can be sanity-checked without Play Mode.
        public static DeliveryOutcome EvaluateDelivery(PizzaFlavor delivered, PizzaFlavor required, bool hitPlate)
        {
            if (hitPlate && delivered == required) return DeliveryOutcome.Success;
            if (hitPlate) return DeliveryOutcome.WrongFlavor;
            return DeliveryOutcome.MissedPlate;
        }
    }
}
