using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Pizzala.EditorTools
{
    // Minimal, disposable scene for verifying PZ_Pizza_Base grab/throw in isolation, away from
    // BackBone.unity's Global Volume / renderer setup (which was showing an unrelated visual
    // artifact). No Global Volume added here on purpose - just a floor, a wall and the rig.
    public static class PizzaTestSceneBuilder
    {
        const string ScenePath = "Assets/Scenes/_PizzaGrabTest.unity";
        const string XROriginPath = "Assets/Samples/XR Interaction Toolkit/3.3.2/Starter Assets/Prefabs/XR Origin (XR Rig).prefab";
        const string SimulatorPath = "Assets/Samples/XR Interaction Toolkit/3.3.2/XR Device Simulator/XR Device Simulator.prefab";

        static readonly (string prefab, Vector3 pos)[] Pizzas =
        {
            ("Assets/Prefabs/PZ_Pizza_Base.prefab", new Vector3(-0.45f, 0.9f, 0.5f)),
            ("Assets/Prefabs/PZ_Pizza_Margherita.prefab", new Vector3(-0.15f, 0.9f, 0.5f)),
            ("Assets/Prefabs/PZ_Pizza_Pepperoni.prefab", new Vector3(0.15f, 0.9f, 0.5f)),
            ("Assets/Prefabs/PZ_Pizza_Hawaiian.prefab", new Vector3(0.45f, 0.9f, 0.5f)),
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

            InstantiatePrefab(XROriginPath, Vector3.zero);
            InstantiatePrefab(SimulatorPath, Vector3.zero);

            foreach (var (prefabPath, pos) in Pizzas)
                InstantiatePrefab(prefabPath, pos);

            if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
                AssetDatabase.CreateFolder("Assets", "Scenes");

            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"PizzaTestSceneBuilder: test scene saved at {ScenePath}");
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
