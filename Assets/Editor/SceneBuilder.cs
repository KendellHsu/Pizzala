using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using PizzaVR.Core;

// Assembles Restaurant.unity out of prefab instances - XRRig, RestaurantEnvironment, and
// CustomerSpawnRoot are each built (or reused) as their own prefab assets, so day-to-day
// tweaks happen inside a prefab instead of this shared scene file.
public static class SceneBuilder
{
    const string SimulatorPrefabPath = "Assets/Samples/XR Interaction Toolkit/3.3.2/XR Device Simulator/XR Device Simulator.prefab";
    public const string ScenePath = "Assets/Scenes/Restaurant.unity";

    [MenuItem("Tools/Pizza VR/Build Restaurant Scene")]
    public static void BuildRestaurantScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        // The default scene's own Main Camera is replaced by the one built into the XR Rig
        // prefab; keep the default Directional Light for basic lighting.
        var defaultCamera = GameObject.Find("Main Camera");
        if (defaultCamera != null)
            Object.DestroyImmediate(defaultCamera);

        var config = GameBalanceConfigBuilder.EnsureConfig();

        var scoreManagerGO = new GameObject("ScoreManager");
        scoreManagerGO.AddComponent<ScoreManager>();

        PrefabUtility.InstantiatePrefab(XRRigPrefabBuilder.EnsurePrefab());
        PrefabUtility.InstantiatePrefab(RestaurantEnvironmentPrefabBuilder.EnsurePrefab());
        PrefabUtility.InstantiatePrefab(CustomerSpawnRootPrefabBuilder.EnsurePrefab());

        var simulatorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SimulatorPrefabPath);
        if (simulatorPrefab != null)
            PrefabUtility.InstantiatePrefab(simulatorPrefab);
        else
            Debug.LogWarning("SceneBuilder: XR Device Simulator prefab not found - run Tools > Pizza VR > Import XRI Samples first.");

        if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
            AssetDatabase.CreateFolder("Assets", "Scenes");

        EditorSceneManager.SaveScene(scene, ScenePath);

        var scenes = EditorBuildSettings.scenes;
        bool alreadyIn = System.Array.Exists(scenes, s => s.path == ScenePath);
        if (!alreadyIn)
        {
            var list = new System.Collections.Generic.List<EditorBuildSettingsScene>(scenes);
            list.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = list.ToArray();
        }

        Debug.Log("SceneBuilder: Restaurant scene created.");
    }
}
