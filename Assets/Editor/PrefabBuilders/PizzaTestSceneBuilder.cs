using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Pizzala.DevTools;

namespace Pizzala.EditorTools
{
    // Persistent test scene covering PREFABS.md items 1 (PZ_Pizza_*) and 2 (PZ_ThrowbackPizza_*),
    // kept separate from BackBone.unity's Global Volume / renderer setup (which was showing an
    // unrelated visual artifact). No Global Volume added here on purpose. Don't delete or
    // overwrite this for a different item - future PREFABS.md items get their own
    // _Test_NN_Name.unity scene instead.
    public static class PizzaTestSceneBuilder
    {
        const string ScenePath = "Assets/Scenes/_Test_01_02_Pizza_Throwback.unity";

        static readonly (string prefab, Vector3 pos)[] Pizzas =
        {
            ("Assets/Prefabs/Pizza/PZ_Pizza_Margherita.prefab", new Vector3(-0.3f, 0.9f, 0.5f)),
            ("Assets/Prefabs/Pizza/PZ_Pizza_Pepperoni.prefab", new Vector3(0f, 0.9f, 0.5f)),
            ("Assets/Prefabs/Pizza/PZ_Pizza_CosmicPinkMarshmallow.prefab", new Vector3(0.3f, 0.9f, 0.5f)),
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

            // ThrowbackTestSpawner throws from +Z toward the player at the origin - a dodge
            // sends the pizza continuing into -Z, past the player, so the "hits the wall behind
            // you" acceptance point (PREFABS.md item 2) needs a wall back there too, not just
            // the one in front that the player's own throws are aimed at.
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

            var headTransform = TestSceneHelpers.BuildHeadHitbox(xrOrigin);
            BuildThrowbackTrigger(headTransform);
            TestSceneHelpers.BuildDirtManager();

            TestSceneHelpers.EnsureScenesFolder();
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"PizzaTestSceneBuilder: test scene saved at {ScenePath}");
        }

        // Stands in for a Customer until PZ_Customer/GameManager exist to launch throwbacks for
        // real - press F in Play Mode to fire a PZ_ThrowbackPizza_Margherita at wherever the
        // head currently is (locked in at that instant, so moving afterwards is what dodges it).
        static void BuildThrowbackTrigger(Transform headTransform)
        {
            var spawner = new GameObject("ThrowbackTestSpawner");
            spawner.transform.position = new Vector3(0f, 1.3f, 2.6f);

            var trigger = spawner.AddComponent<ThrowbackTestTrigger>();
            trigger.throwbackPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Pizza/PZ_ThrowbackPizza_Margherita.prefab");
            trigger.targetHead = headTransform;
            trigger.speed = 6f;
        }
    }
}
