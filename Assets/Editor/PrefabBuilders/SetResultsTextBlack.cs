// ─────────────────────────────────────────────────────────────
// SetResultsTextBlack.cs — switches every TMP_Text on the results screen prefabs
// to black, to read against the new white BackgroundPanel (see
// AddResultsBackgroundPanel.cs). Run from Unity: Tools > Pizzala > Set Results Text Black.
// ─────────────────────────────────────────────────────────────
using UnityEditor;
using UnityEngine;
using TMPro;

namespace Pizzala.EditorTools
{
    public static class SetResultsTextBlack
    {
        static readonly string[] PrefabPaths =
        {
            "Assets/Prefabs/UI/PZ_ResultsCanvas.prefab",
            "Assets/Prefabs/UI/PZ_PhotoEntry.prefab",
        };

        [MenuItem("Tools/Pizzala/Set Results Text Black")]
        public static void Run()
        {
            int changed = 0;
            foreach (var prefabPath in PrefabPaths)
            {
                var root = PrefabUtility.LoadPrefabContents(prefabPath);
                foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
                {
                    text.color = Color.black;
                    changed++;
                }
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                PrefabUtility.UnloadPrefabContents(root);
            }
            Debug.Log($"SetResultsTextBlack: set {changed} TMP_Text component(s) across {PrefabPaths.Length} prefab(s) to black.");
        }
    }
}
