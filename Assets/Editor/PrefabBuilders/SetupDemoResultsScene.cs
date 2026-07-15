// ─────────────────────────────────────────────────────────────
// SetupDemoResultsScene.cs — resets Test_reslut.unity into a clean demo of the
// results screen. The scene had two problems that made the photo wall look dead:
//  1. the PZ_ResultsCanvas instance carried old manual overrides (PhotoGrid was
//     disabled at the scene level, BossNotePanel stretched to 4167x3334, TMP
//     materials pointed at broken font copies embedded in the scene file)
//  2. DemoResultsLoader was never added to any scene, so pressing 1/2/3 did
//     nothing and no data/photos were ever loaded
// This deletes the stale canvas instance (dropping all those overrides), spawns a
// fresh one from the fixed prefab, adds a Demo object with DemoResultsLoader +
// BossCommentService wired up, and clears the orphaned font material/atlas objects
// that were bloating the scene file. Run: Tools > Pizzala > Setup Demo Results Scene.
// Safe to re-run.
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
    public static class SetupDemoResultsScene
    {
        const string ScenePath = "Assets/Scenes/Test_reslut.unity";
        const string CanvasPrefabPath = "Assets/Prefabs/UI/PZ_ResultsCanvas.prefab";

        [MenuItem("Tools/Pizzala/Setup Demo Results Scene")]
        public static void Run()
        {
            var canvasPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CanvasPrefabPath);
            if (canvasPrefab == null)
            {
                Debug.LogError($"SetupDemoResultsScene: prefab not found at {CanvasPrefabPath}");
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            // 1. Drop the old canvas instance - this is what discards the stale scene
            //    overrides (disabled PhotoGrid, giant BossNotePanel, broken materials).
            foreach (var controller in Object.FindObjectsByType<ResultsScreenController>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Object.DestroyImmediate(controller.gameObject);
            }

            // 2. Clear the orphaned font material/atlas copies embedded in the scene file
            //    (left behind by the earlier broken font asset - they're scene objects,
            //    not assets, so destroying them here only slims down this scene).
            foreach (var obj in Resources.FindObjectsOfTypeAll<Object>())
            {
                if (obj == null || EditorUtility.IsPersistent(obj)) continue;
                if (!(obj is Material) && !(obj is Texture2D)) continue;
                if (obj.name.StartsWith("LoRes 9 OT"))
                    Object.DestroyImmediate(obj);
            }

            // 3. Fresh canvas straight from the fixed prefab - its default transform
            //    (0, 1.6, 2) sits right in front of the scene camera at (0, 1.6, 0).
            var canvasInstance = (GameObject)PrefabUtility.InstantiatePrefab(canvasPrefab, scene);
            var resultsScreen = canvasInstance.GetComponent<ResultsScreenController>();

            // 4. Demo driver: press 1 = Control, 2 = Middle, 3 = Experimental in Play mode.
            var demo = GameObject.Find("Demo");
            if (demo != null) Object.DestroyImmediate(demo);
            demo = new GameObject("Demo");
            var bossService = demo.AddComponent<BossCommentService>();
            var loader = demo.AddComponent<DemoResultsLoader>();
            loader.resultsScreen = resultsScreen;
            loader.bossCommentService = bossService;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("SetupDemoResultsScene: scene reset - fresh PZ_ResultsCanvas + Demo loader (keys 1/2/3 in Play mode).");
        }
    }
}
