using UnityEngine;
using Pizzala.Data;

namespace Pizzala.Customers
{
    /// <summary>
    /// Connects a normal gameplay customer to the runtime shrink-wrap sauce system.
    /// The coarse CustomerHitZone decides whether a customer was hit; this component
    /// then finds the exact visible skinned-mesh surface and creates the 3D sauce.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SkinnedSurfaceProbe))]
    [RequireComponent(typeof(SkinnedSauceMeshGenerator))]
    [RequireComponent(typeof(SauceToppingSpawner))]
    public class CustomerSurfaceSauce : MonoBehaviour
    {
        [Header("Handled Customer Hit Zones")]
        [SerializeField] bool createOnFace = true;
        [SerializeField] bool createOnBody = true;

        [Header("Pizza Flavor Mapping")]
        [SerializeField] Material margheritaSauceMaterial;
        [SerializeField] Material pepperoniSauceMaterial;
        [SerializeField] Material cosmicPinkMarshmallowSauceMaterial;

        SkinnedSurfaceProbe surfaceProbe;

        void Awake()
        {
            surfaceProbe = GetComponent<SkinnedSurfaceProbe>();
        }

        /// <summary>
        /// Returns true when this customer is configured to replace the legacy
        /// character splat for the specified hit zone.
        /// </summary>
        public bool Handles(CustomerHitZone hitZone)
        {
            if (hitZone == null) return false;

            return hitZone.zone switch
            {
                HitZoneType.Face => createOnFace,
                HitZoneType.Body => createOnBody,
                _ => false,
            };
        }

        public bool TryCreate(
            CustomerHitZone hitZone,
            Vector3 roughContactPoint,
            Vector3 incomingVelocity,
            Vector3 roughContactNormal,
            PizzaFlavor flavor)
        {
            if (!Handles(hitZone) || surfaceProbe == null) return false;

            Collider hitCollider = hitZone.GetComponent<Collider>();
            Vector3 roughColliderCenter = hitCollider != null
                ? hitCollider.bounds.center
                : transform.position;

            return surfaceProbe.TryProbe(
                roughContactPoint,
                incomingVelocity,
                roughContactNormal,
                roughColliderCenter,
                GetSauceMaterial(flavor),
                GetToppingTheme(flavor),
                out _);
        }

        Material GetSauceMaterial(PizzaFlavor flavor)
        {
            return flavor switch
            {
                PizzaFlavor.Margherita => margheritaSauceMaterial,
                PizzaFlavor.Pepperoni => pepperoniSauceMaterial,
                PizzaFlavor.CosmicPinkMarshmallow => cosmicPinkMarshmallowSauceMaterial,
                _ => null,
            };
        }

        static SauceToppingTheme GetToppingTheme(PizzaFlavor flavor)
        {
            return flavor switch
            {
                PizzaFlavor.Margherita => SauceToppingTheme.Margherita,
                PizzaFlavor.Pepperoni => SauceToppingTheme.Pepperoni,
                PizzaFlavor.CosmicPinkMarshmallow => SauceToppingTheme.CosmicPinkMarshmallow,
                _ => SauceToppingTheme.None,
            };
        }
    }
}
