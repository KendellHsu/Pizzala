using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Pizzala.Core;
using Pizzala.DevTools;

namespace Pizzala.EditorTools
{
    // Minimal, disposable scene for verifying the three flavor pizza prefabs' grab/throw in
    // isolation, away from BackBone.unity's Global Volume / renderer setup (which was showing
    // an unrelated visual artifact). No Global Volume added here on purpose.
    public static class PizzaTestSceneBuilder
    {
        const string ScenePath = "Assets/Scenes/_PizzaGrabTest.unity";
        const string XROriginPath = "Assets/Samples/XR Interaction Toolkit/3.3.2/Starter Assets/Prefabs/XR Origin (XR Rig).prefab";
        const string SimulatorPath = "Assets/Samples/XR Interaction Toolkit/3.3.2/XR Device Simulator/XR Device Simulator.prefab";

        static readonly (string prefab, Vector3 pos)[] Pizzas =
        {
            ("Assets/Prefabs/PZ_Pizza_Margherita.prefab", new Vector3(-0.3f, 0.9f, 0.5f)),
            ("Assets/Prefabs/PZ_Pizza_Pepperoni.prefab", new Vector3(0f, 0.9f, 0.5f)),
            ("Assets/Prefabs/PZ_Pizza_CosmicPinkMarshmallow.prefab", new Vector3(0.3f, 0.9f, 0.5f)),
        };

        [MenuItem("Tools/Pizzala/Build Pizza Grab Test Scene")]
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

            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Wall";
            wall.transform.position = new Vector3(0f, 1.5f, 3f);
            wall.transform.localScale = new Vector3(6f, 3f, 0.2f);
            wall.isStatic = false;

            var table = GameObject.CreatePrimitive(PrimitiveType.Cube);
            table.name = "TestTable";
            table.transform.position = new Vector3(0f, 0.45f, 0.5f);
            table.transform.localScale = new Vector3(1.2f, 0.9f, 0.6f);
            table.isStatic = false;

            var xrOrigin = InstantiatePrefab(XROriginPath, Vector3.zero);
            InstantiatePrefab(SimulatorPath, Vector3.zero);

            foreach (var (prefabPath, pos) in Pizzas)
                InstantiatePrefab(prefabPath, pos);

            var headTransform = BuildHeadHitbox(xrOrigin);
            BuildThrowbackTrigger(headTransform);

            if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
                AssetDatabase.CreateFolder("Assets", "Scenes");

            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"PizzaTestSceneBuilder: test scene saved at {ScenePath}");
        }

        // Per SETUP.md 2-2 / BUILD_STEPS.md §2: ThrowbackProjectile only recognizes a hit on the
        // player by finding PlayerHeadHitbox on the collider it touched.
        static Transform BuildHeadHitbox(GameObject xrOrigin)
        {
            if (xrOrigin == null)
                return null;

            var camera = xrOrigin.GetComponentInChildren<Camera>();
            if (camera == null)
            {
                Debug.LogError("PizzaTestSceneBuilder: no Camera found under XR Origin, can't build HeadHitbox.");
                return null;
            }

            var headHitbox = new GameObject("HeadHitbox");
            headHitbox.transform.SetParent(camera.transform, false);
            headHitbox.AddComponent<PlayerHeadHitbox>();

            var sphere = headHitbox.AddComponent<SphereCollider>();
            sphere.isTrigger = true;
            sphere.radius = 0.22f;

            var rb = headHitbox.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            return camera.transform;
        }

        // Stands in for a Customer until PZ_Customer/GameManager exist to launch throwbacks for
        // real - press F in Play Mode to fire a PZ_ThrowbackPizza_Margherita at wherever the
        // head currently is (locked in at that instant, so moving afterwards is what dodges it).
        static void BuildThrowbackTrigger(Transform headTransform)
        {
            var spawner = new GameObject("ThrowbackTestSpawner");
            spawner.transform.position = new Vector3(0f, 1.3f, 2.6f);

            var trigger = spawner.AddComponent<ThrowbackTestTrigger>();
            trigger.throwbackPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PZ_ThrowbackPizza_Margherita.prefab");
            trigger.targetHead = headTransform;
            trigger.speed = 6f;
        }

        static GameObject InstantiatePrefab(string path, Vector3 position)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null)
            {
                Debug.LogError($"PizzaTestSceneBuilder: prefab not found at {path}");
                return null;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            instance.transform.position = position;
            return instance;
        }
    }
}
