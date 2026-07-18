using UnityEngine;

// Added at runtime to a real PZ_ThrowbackPizza_* prefab by SauceMeshTestLauncher.
// The pizza keeps its original Rigidbody and Collider; this component only routes
// its collision into the shrink-wrap sauce test.
public class SauceMeshTestProjectile : MonoBehaviour
{
    Material sauceMaterial;
    RuntimeSauceMeshGenerator sauceGenerator;
    LayerMask sauceTargetMask = ~0;
    SauceToppingTheme toppingTheme;
    bool hasSplashed;

    public void Initialize(
        Material material,
        RuntimeSauceMeshGenerator generator,
        LayerMask targetMask,
        SauceToppingTheme selectedToppingTheme)
    {
        sauceMaterial = material;
        sauceGenerator = generator;
        sauceTargetMask = targetMask;
        toppingTheme = selectedToppingTheme;
        hasSplashed = false;
    }

    void OnCollisionEnter(Collision collision)
    {
        if (hasSplashed || collision.contactCount == 0) return;
        if ((sauceTargetMask.value & (1 << collision.gameObject.layer)) == 0) return;

        ContactPoint contact = collision.GetContact(0);
        Vector3 impactVelocity = collision.relativeVelocity;
        float impulse = collision.impulse.magnitude;

        Debug.Log(
            $"[SplashTest] Hit {collision.gameObject.name}\n" +
            $"Point: {contact.point}\n" +
            $"Normal: {contact.normal}\n" +
            $"Relative velocity: {impactVelocity.magnitude:F2} m/s\n" +
            $"Impulse: {impulse:F2} N*s");

        var surfaceProbe = collision.collider.GetComponentInParent<SkinnedSurfaceProbe>();
        if (surfaceProbe != null)
        {
            surfaceProbe.TryProbe(
                contact.point,
                impactVelocity,
                contact.normal,
                collision.collider.bounds.center,
                sauceMaterial,
                toppingTheme,
                out _);
            hasSplashed = true;
            return;
        }

        if (sauceGenerator == null)
            sauceGenerator = FindFirstObjectByType<RuntimeSauceMeshGenerator>();

        if (sauceGenerator != null)
        {
            sauceGenerator.Generate(
                collision.collider,
                contact.point,
                contact.normal,
                impactVelocity,
                impulse,
                sauceMaterial);
        }

        hasSplashed = true;
    }
}
