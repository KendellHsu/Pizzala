// ─────────────────────────────────────────────────────────────
// DiagnoseResultsScene.cs — the photo wall has been reported "still not showing"
// twice now despite the prefab wiring checking out. This inspects the actual scene
// state directly (no Play mode needed) so we stop guessing blind: field wiring,
// active states, sizes, and - critically - whether the scene's PrefabInstance has
// stale property overrides from before the prefab was fixed (an override on an
// instance persists even after the source prefab changes, and won't show up just
// by reading the prefab asset).
// Run from Unity: Tools > Pizzala > Diagnose Results Scene. Paste the whole
// Console output back - read-only, makes no changes.
// ─────────────────────────────────────────────────────────────
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Pizzala.UI;

namespace Pizzala.EditorTools
{
    public static class DiagnoseResultsScene
    {
        const string ScenePath = "Assets/Scenes/Test_reslut.unity";

        [MenuItem("Tools/Pizzala/Diagnose Results Scene")]
        public static void Run()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            var controller = Object.FindFirstObjectByType<ResultsScreenController>(FindObjectsInactive.Include);
            if (controller == null)
            {
                Debug.LogError("DIAGNOSE: no ResultsScreenController in the scene - run Setup Demo Results Scene first.");
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine("=== DIAGNOSE Results Scene ===");
            sb.AppendLine($"controller GameObject: {controller.gameObject.name} (active: {controller.gameObject.activeInHierarchy})");

            var go = controller.gameObject;
            bool isInstance = PrefabUtility.IsPartOfPrefabInstance(go);
            sb.AppendLine($"is prefab instance: {isInstance}");
            if (isInstance)
            {
                var root = PrefabUtility.GetOutermostPrefabInstanceRoot(go);
                var mods = PrefabUtility.GetPropertyModifications(root);
                sb.AppendLine($"property overrides on this instance: {(mods == null ? 0 : mods.Length)}");
                if (mods != null)
                    foreach (var m in mods)
                        sb.AppendLine($"  OVERRIDE: {m.target?.name}.{m.propertyPath} = {m.value} (obj ref: {m.objectReference})");
            }

            void Log(string label, GameObject g)
            {
                if (g == null) { sb.AppendLine($"{label}: NULL"); return; }
                var rt = g.GetComponent<RectTransform>();
                sb.AppendLine($"{label}: '{g.name}' active(self)={g.activeSelf} active(hierarchy)={g.activeInHierarchy}" +
                               (rt != null ? $" pos={rt.anchoredPosition} size={rt.sizeDelta}" : ""));
            }

            Log("controlPanel", controller.controlPanel);
            Log("dataPortraitPanel", controller.dataPortraitPanel);
            Log("photoWallPanel", controller.photoWallPanel);
            void LogSlots(string label, GameObject[] slots)
            {
                if (slots == null) { sb.AppendLine($"{label}: NULL"); return; }
                sb.AppendLine($"{label}: {slots.Length} slot(s)");
                for (int i = 0; i < slots.Length; i++)
                {
                    var g = slots[i];
                    sb.AppendLine(g == null
                        ? $"  [{i}]: NULL"
                        : $"  [{i}] '{g.name}' active(self)={g.activeSelf} active(hierarchy)={g.activeInHierarchy}");
                }
            }
            LogSlots("photoWallSlots", controller.photoWallSlots);
            Log("bossNotePanel", controller.bossNotePanel);
            sb.AppendLine($"backgroundPanel: {(controller.backgroundPanel == null ? "NULL (field not wired!)" : controller.backgroundPanel.name)}");
            sb.AppendLine($"captionText: {(controller.captionText == null ? "NULL" : "OK")}");
            sb.AppendLine($"bossCommentText: {(controller.bossCommentText == null ? "NULL" : "OK")}");

            var demo = Object.FindFirstObjectByType<Pizzala.DevTools.DemoResultsLoader>(FindObjectsInactive.Include);
            sb.AppendLine($"DemoResultsLoader in scene: {(demo == null ? "NOT FOUND" : "found on " + demo.gameObject.name)}");
            if (demo != null)
                sb.AppendLine($"  resultsScreen wired: {demo.resultsScreen != null}, bossCommentService wired: {demo.bossCommentService != null}");

            Debug.Log(sb.ToString());
        }
    }
}
