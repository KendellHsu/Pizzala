// ─────────────────────────────────────────────────────────────
// SetupResultsScene.cs — builds/resets Assets/Scenes/Results.unity: a standalone scene
// that just shows the paged results screen and lets you flip through it. Meant to be the
// scene Kendell eventually SceneManager.LoadScene()s into once a round ends - we're only
// building the "what's in the scene" half here, not the transition itself (session data
// surviving the load, the async boss-comment callback, whether the XR rig follows across -
// see the chat for that discussion). Deliberately separate from Test_reslut.unity, which
// stays our own scratch scene for iterating on the results UI itself.
//
// Contents: a camera + light (this scene has nothing by default, unlike File > New Scene),
// a fresh PZ_ResultsCanvas instance at its prefab default (0, 1.6, 2) - right in front of
// the camera at (0, 1.6, 0) - and one "Demo" object carrying:
//   - DemoResultsLoader, set to auto-load Experimental on Start (autoLoadOnStart) - this
//     scene is dedicated to one condition, so the 1/2/3 keys stay available but aren't
//     needed to see anything
//   - ResultsPageInput (right thumbstick, keyboard U/I) - the real paging input the
//     finished flow will use
//
// Run: Tools > Pizzala > Setup Results Scene. Safe to re-run - drops and rebuilds the
// canvas + Demo object each time, same pattern as SetupDemoResultsScene.
// ─────────────────────────────────────────────────────────────
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Pizzala.UI;
using Pizzala.LLM;
using Pizzala.DevTools;

namespace Pizzala.EditorTools
{
    public static class SetupResultsScene
    {
        const string ScenePath = "Assets/Scenes/Results.unity";
        const string CanvasPrefabPath = "Assets/Prefabs/UI/PZ_ResultsCanvas.prefab";

        [MenuItem("Tools/Pizzala/Setup Results Scene")]
        public static void Run()
        {
            var canvasPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CanvasPrefabPath);
            if (canvasPrefab == null)
            {
                Debug.LogError($"SetupResultsScene: prefab not found at {CanvasPrefabPath}");
                return;
            }

            Scene scene;
            bool sceneAlreadyExists = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null;
            if (sceneAlreadyExists)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

                // Reset: drop whatever canvas/demo objects are already here so re-running
                // this is idempotent instead of piling up duplicates.
                foreach (var controller in Object.FindObjectsByType<ResultsScreenController>(
                             FindObjectsInactive.Include, FindObjectsSortMode.None))
                    Object.DestroyImmediate(controller.gameObject);
                var oldDemo = GameObject.Find("Demo");
                if (oldDemo != null) Object.DestroyImmediate(oldDemo);
            }
            else
            {
                scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

                var camera = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
                camera.tag = "MainCamera";
                camera.transform.position = new Vector3(0f, 1.6f, 0f);

                var light = new GameObject("Directional Light", typeof(Light));
                light.GetComponent<Light>().type = LightType.Directional;
                light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            }

            // Fresh canvas straight from the prefab - its default transform (0, 1.6, 2)
            // sits right in front of the camera at (0, 1.6, 0).
            var canvasInstance = (GameObject)PrefabUtility.InstantiatePrefab(canvasPrefab, scene);
            var resultsScreen = canvasInstance.GetComponent<ResultsScreenController>();

            var demo = new GameObject("Demo");
            var bossService = demo.AddComponent<BossCommentService>();
            var loader = demo.AddComponent<DemoResultsLoader>();
            loader.resultsScreen = resultsScreen;
            loader.bossCommentService = bossService;
            loader.autoLoadOnStart = true; // this scene is dedicated to Experimental - no 1/2/3 needed
            loader.autoLoadCondition = Pizzala.Data.ExperimentCondition.Experimental;
            var pageInput = demo.AddComponent<ResultsPageInput>();
            pageInput.resultsScreen = resultsScreen;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("SetupResultsScene: " + (sceneAlreadyExists ? "reset " : "created ") +
                      $"{ScenePath} - PZ_ResultsCanvas auto-loaded as Experimental, ResultsPageInput (thumbstick, U/I) to page through it.");
        }
    }
}
