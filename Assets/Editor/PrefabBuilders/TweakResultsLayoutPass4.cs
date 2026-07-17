// ─────────────────────────────────────────────────────────────
// TweakResultsLayoutPass4.cs — fourth layout pass (2026-07-16): boss note LLM text
// shrunk to 0.5x (Pass 3's fontSizeMin/Max 30/90 -> 15/45) and repositioned toward
// the lower portion of the (now much bigger) note image instead of dead center.
// Run from Unity: Tools > Pizzala > Tweak Results Layout Pass 4. Safe to re-run.
// Depends on Pass 3 having been run first.
// ─────────────────────────────────────────────────────────────
using UnityEditor;
using UnityEngine;
using TMPro;

namespace Pizzala.EditorTools
{
    public static class TweakResultsLayoutPass4
    {
        const string PrefabPath = "Assets/Prefabs/UI/PZ_ResultsCanvas.prefab";

        [MenuItem("Tools/Pizzala/Tweak Results Layout Pass 4")]
        public static void Run()
        {
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);

            var comment = root.transform.Find("ExperimentalPanel/BossNotePanel/BossCommentText") as RectTransform;
            if (comment == null)
            {
                Debug.LogError("TweakResultsLayoutPass4: ExperimentalPanel/BossNotePanel/BossCommentText not found.");
                PrefabUtility.UnloadPrefabContents(root);
                return;
            }

            comment.sizeDelta = new Vector2(1000, 400);          // smaller box to match the smaller text
            comment.anchoredPosition = new Vector2(0, -350);     // lower portion of the 1800x1440 note

            var tmp = comment.GetComponent<TMP_Text>();
            if (tmp != null)
            {
                tmp.enableAutoSizing = true;
                tmp.fontSizeMin = 15; // Pass 3's 30 x 0.5
                tmp.fontSizeMax = 45; // Pass 3's 90 x 0.5
            }

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            PrefabUtility.UnloadPrefabContents(root);
            Debug.Log("TweakResultsLayoutPass4: boss note text shrunk to 0.5x and moved to the lower portion of the note.");
        }
    }
}
