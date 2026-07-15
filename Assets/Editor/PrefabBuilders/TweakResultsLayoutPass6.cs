// ─────────────────────────────────────────────────────────────
// TweakResultsLayoutPass6.cs — sixth layout pass (2026-07-16): boss note text
// shrunk another 0.7x (Pass 5's 12/28 -> ~8/20), box width halved (1400 -> 700,
// taller wrap instead of wide), nudged further down from Pass 5's -80.
// Run from Unity: Tools > Pizzala > Tweak Results Layout Pass 6. Safe to re-run.
// ─────────────────────────────────────────────────────────────
using UnityEditor;
using UnityEngine;
using TMPro;

namespace Pizzala.EditorTools
{
    public static class TweakResultsLayoutPass6
    {
        const string PrefabPath = "Assets/Prefabs/UI/PZ_ResultsCanvas.prefab";

        [MenuItem("Tools/Pizzala/Tweak Results Layout Pass 6")]
        public static void Run()
        {
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);

            var comment = root.transform.Find("ExperimentalPanel/BossNotePanel/BossCommentText") as RectTransform;
            if (comment == null)
            {
                Debug.LogError("TweakResultsLayoutPass6: ExperimentalPanel/BossNotePanel/BossCommentText not found.");
                PrefabUtility.UnloadPrefabContents(root);
                return;
            }

            comment.sizeDelta = new Vector2(700, 400);        // half of Pass 5's 1400 width
            comment.anchoredPosition = new Vector2(60, -170); // a bit further down from Pass 5's -80, nudged right

            var tmp = comment.GetComponent<TMP_Text>();
            if (tmp != null)
            {
                tmp.enableAutoSizing = true;
                tmp.fontSizeMin = 8;  // Pass 5's 12 x 0.7
                tmp.fontSizeMax = 20; // Pass 5's 28 x 0.7
            }

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            PrefabUtility.UnloadPrefabContents(root);
            Debug.Log("TweakResultsLayoutPass6: boss note text shrunk 0.7x, box width halved, moved further down.");
        }
    }
}
