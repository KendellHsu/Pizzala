using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Pizzala.Customers;
using Pizzala.DevTools;

namespace Pizzala.EditorTools
{
    // Persistent test scene for PREFABS.md item 4 (PZ_Customer) - verifying the flavor icon and
    // waiting/timeout behavior, plus keeping items 1-2's grab/throw and throwback testing
    // available alongside it. Full hand-catch/wrong-flavor resolution still needs GameManager
    // (not built yet), so that part isn't wired here - see CustomerOrderTestTrigger's header.
    public static class CustomerTestSceneBuilder
    {
        const string ScenePath = "Assets/Scenes/_Test_04_Customer.unity";
        const string CustomerPath = "Assets/Prefabs/PZ_Customer.prefab";

        static readonly (string prefab, Vector3 pos)[] Pizzas =
        {
            ("Assets/Prefabs/Pizza/PZ_Pizza_Margherita.prefab", new Vector3(-0.3f, 0.9f, 0.5f)),
            ("Assets/Prefabs/Pizza/PZ_Pizza_Pepperoni.prefab", new Vector3(0f, 0.9f, 0.5f)),
            ("Assets/Prefabs/Pizza/PZ_Pizza_CosmicPinkMarshmallow.prefab", new Vector3(0.3f, 0.9f, 0.5f)),
        };

        [MenuItem("Tools/Pizzala/Build Customer Test Scene")]
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var defaultCamera = GameObject.Find("Main Camera");
            if (defaultCamera != null)
                Object.DestroyImmediate(defaultCamera);

            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.position = Vector3.zero;
            floor.transform.localScale = new Vector3(2f, 1f, 2f);
            floor.isStatic = false;

            var backWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            backWall.name = "BackWall";
            backWall.transform.position = new Vector3(0f, 1.5f, -3f);
            backWall.transform.localScale = new Vector3(6f, 3f, 0.2f);
            backWall.isStatic = false;

            var table = GameObject.CreatePrimitive(PrimitiveType.Cube);
            table.name = "TestTable";
            table.transform.position = new Vector3(0f, 0.45f, 0.5f);
            table.transform.localScale = new Vector3(1.2f, 0.9f, 0.6f);
            table.isStatic = false;

            var xrOrigin = TestSceneHelpers.InstantiatePrefab(TestSceneHelpers.XROriginPath, Vector3.zero);
            TestSceneHelpers.InstantiatePrefab(TestSceneHelpers.SimulatorPath, Vector3.zero);

            foreach (var (prefabPath, pos) in Pizzas)
                TestSceneHelpers.InstantiatePrefab(prefabPath, pos);

            var customer = TestSceneHelpers.InstantiatePrefab(CustomerPath, new Vector3(0f, 0f, 2.2f));
            if (customer != null)
                customer.transform.rotation = Quaternion.Euler(0f, 180f, 0f); // face the player

            var headTransform = TestSceneHelpers.BuildHeadHitbox(xrOrigin);
            BuildThrowbackTrigger(headTransform);
            BuildOrderTrigger(customer);
            TestSceneHelpers.BuildDirtManager();

            TestSceneHelpers.EnsureScenesFolder();
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"CustomerTestSceneBuilder: test scene saved at {ScenePath}");
        }

        // Press F: same throwback test as the Pizza/Throwback scene, aimed from roughly where
        // the customer stands.
        static void BuildThrowbackTrigger(Transform headTransform)
        {
            var spawner = new GameObject("ThrowbackTestSpawner");
            spawner.transform.position = new Vector3(0f, 1.3f, 2f);

            var trigger = spawner.AddComponent<ThrowbackTestTrigger>();
            trigger.throwbackPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Pizza/PZ_ThrowbackPizza_Margherita.prefab");
            trigger.targetHead = headTransform;
            trigger.speed = 6f;
        }

        // Press O: give the customer a random flavor order so its head icon can be checked
        // against CustomerController.flavorSprites.
        static void BuildOrderTrigger(GameObject customer)
        {
            if (customer == null)
                return;

            var controller = customer.GetComponent<CustomerController>();
            if (controller == null)
            {
                Debug.LogError("CustomerTestSceneBuilder: PZ_Customer instance has no CustomerController.");
                return;
            }

            var triggerGO = new GameObject("CustomerOrderTestTrigger");
            var trigger = triggerGO.AddComponent<CustomerOrderTestTrigger>();
            trigger.customer = controller;
            trigger.patienceSeconds = 8f;
        }
    }
}
