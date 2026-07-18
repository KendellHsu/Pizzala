using UnityEngine;
using UnityEngine.InputSystem;
using Pizzala.Customers;
using Pizzala.Data;
using Pizzala.Dirt;
using Pizzala.Throwing;

// TestProjectile is now only a movable spawn point. It creates an actual pizza
// prefab and lets that prefab's own Rigidbody and Collider perform the collision.
public class SauceMeshTestLauncher : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] float moveSpeed = 2f;
    [SerializeField] float launchSpeed = 8f;

    [Header("Pizza Prefabs And Matching Sauce")]
    [SerializeField] GameObject[] pizzaPrefabs;
    [SerializeField] Material[] sauceMaterials;
    [Tooltip("必須與 Pizza Prefabs 陣列順序相同，決定這顆披薩撞擊後要生成哪一組配料。")]
    [SerializeField] SauceToppingTheme[] toppingThemes;

    [Header("Sauce Test")]
    [SerializeField] RuntimeSauceMeshGenerator sauceGenerator;
    [SerializeField] LayerMask sauceTargetMask = ~0;

    GameObject currentPizza;
    int currentIndex = -1;
    PizzaFlavor currentFlavor = PizzaFlavor.Margherita;

    void Awake()
    {
        Renderer markerRenderer = GetComponent<Renderer>();
        if (markerRenderer != null) markerRenderer.enabled = false;

        Collider markerCollider = GetComponent<Collider>();
        if (markerCollider != null) markerCollider.enabled = false;

        Rigidbody markerBody = GetComponent<Rigidbody>();
        if (markerBody != null)
        {
            markerBody.isKinematic = true;
            markerBody.detectCollisions = false;
        }

        CreatePreviewPizza();
    }

    void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        Vector3 movement = Vector3.zero;
        if (keyboard.aKey.isPressed) movement += Vector3.left;
        if (keyboard.dKey.isPressed) movement += Vector3.right;
        if (keyboard.wKey.isPressed) movement += Vector3.forward;
        if (keyboard.sKey.isPressed) movement += Vector3.back;
        if (keyboard.eKey.isPressed) movement += Vector3.up;
        if (keyboard.cKey.isPressed) movement += Vector3.down;
        transform.position += movement.normalized * moveSpeed * Time.deltaTime;

        if (keyboard.qKey.wasPressedThisFrame) LaunchCurrentPizza();
        if (keyboard.rKey.wasPressedThisFrame) CreatePreviewPizza();
    }

    void CreatePreviewPizza()
    {
        if (pizzaPrefabs == null || pizzaPrefabs.Length == 0) return;

        if (currentPizza != null) Destroy(currentPizza);

        int startIndex = Random.Range(0, pizzaPrefabs.Length);
        GameObject prefab = null;
        for (int offset = 0; offset < pizzaPrefabs.Length; offset++)
        {
            currentIndex = (startIndex + offset) % pizzaPrefabs.Length;
            prefab = pizzaPrefabs[currentIndex];
            if (prefab != null) break;
        }
        if (prefab == null) return;

        currentFlavor = ReadPizzaFlavor(prefab);

        currentPizza = Instantiate(prefab, transform.position, transform.rotation, transform);
        currentPizza.name = $"Test_{prefab.name}";

        Rigidbody pizzaBody = currentPizza.GetComponent<Rigidbody>();
        if (pizzaBody != null)
        {
            pizzaBody.linearVelocity = Vector3.zero;
            pizzaBody.angularVelocity = Vector3.zero;
            pizzaBody.isKinematic = true;
            pizzaBody.detectCollisions = false;
        }

        // Prevent the normal customer-to-player throw resolution from also firing.
        ThrowbackProjectile normalThrow = currentPizza.GetComponent<ThrowbackProjectile>();
        if (normalThrow != null) normalThrow.enabled = false;
        SauceSpray normalSpray = currentPizza.GetComponent<SauceSpray>();
        if (normalSpray != null) normalSpray.enabled = false;
    }

    void LaunchCurrentPizza()
    {
        if (currentPizza == null) CreatePreviewPizza();
        if (currentPizza == null) return;

        Rigidbody pizzaBody = currentPizza.GetComponent<Rigidbody>();
        if (pizzaBody == null)
        {
            Debug.LogError($"[SplashTest] {currentPizza.name} has no Rigidbody.");
            return;
        }

        int flavorIndex = (int)currentFlavor;
        Material material = sauceMaterials != null && flavorIndex >= 0 && flavorIndex < sauceMaterials.Length
            ? sauceMaterials[flavorIndex]
            : null;
        SauceToppingTheme toppingTheme = currentFlavor switch
        {
            PizzaFlavor.Margherita => SauceToppingTheme.Margherita,
            PizzaFlavor.Pepperoni => SauceToppingTheme.Pepperoni,
            PizzaFlavor.CosmicPinkMarshmallow => SauceToppingTheme.CosmicPinkMarshmallow,
            _ => SauceToppingTheme.None,
        };
        var collisionRouter = currentPizza.AddComponent<SauceMeshTestProjectile>();
        collisionRouter.Initialize(material, sauceGenerator, sauceTargetMask, toppingTheme);

        currentPizza.transform.SetParent(null, true);
        pizzaBody.isKinematic = false;
        pizzaBody.detectCollisions = true;
        pizzaBody.linearVelocity = transform.forward * launchSpeed;
        pizzaBody.angularVelocity = new Vector3(0f, 10f, 0f);
        currentPizza = null;
    }

    static PizzaFlavor ReadPizzaFlavor(GameObject prefab)
    {
        var gameplayPizza = prefab.GetComponent<PizzaProjectile>();
        if (gameplayPizza != null) return gameplayPizza.flavor;

        var throwbackPizza = prefab.GetComponent<ThrowbackProjectile>();
        return throwbackPizza != null ? throwbackPizza.flavor : PizzaFlavor.Margherita;
    }
}
