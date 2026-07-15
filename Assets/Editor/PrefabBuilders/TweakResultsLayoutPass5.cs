// ─────────────────────────────────────────────────────────────
// TweakResultsLayoutPass5.cs — fifth layout pass (2026-07-16). The screenshot from
// Pass 4 showed the LLM text overflowing past the note's visible torn-paper area
// into the background below it - the 4167x3334 PNG has transparent padding around
// the actual paper shape, so a text box sized/positioned off the full texture
// bounds (which is what Pass 3/4 assumed) runs past the real paper edges. This
// pulls the box back toward center (safely on the visible paper, still biased low
// per the "偏下方" request), widens it for more characters per line, and shrinks
// the font further.
// Run from Unity: Tools > Pizzala > Tweak Results Layout Pass 5. Safe to re-run.
// The exact box bounds are still a best guess without a live preview of the PNG's
// alpha channel - check in Unity and nudge BossCommentText's RectTransform if the
// text still clips the paper's edge.
// ─────────────────────────────────────────────────────────────
using UnityEditor;
using UnityEngine;
using TMPro;

namespace Pizzala.EditorTools
{
    public static class TweakResultsLayoutPass5
    {
        const string PrefabPath = "Assets/Prefabs/UI/PZ_ResultsCanvas.prefab";

        [MenuItem("Tools/Pizzala/Tweak Results Layout Pass 5")]
        public static void Run()
        {
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);

            var comment = root.transform.Find("ExperimentalPanel/BossNotePanel/BossCommentText") as RectTransform;
            if (comment == null)
            {
                Debug.LogError("TweakResultsLayoutPass5: ExperimentalPanel/BossNotePanel/BossCommentText not found.");
                PrefabUtility.UnloadPrefabContents(root);
                return;
            }

            // Wider (more characters per line) and shorter, pulled back toward the note's
            // center so it stays on the visible paper instead of the transparent padding
            // beyond it - still offset downward from dead-center per "偏下方".
            comment.sizeDelta = new Vector2(1400, 400);
            comment.anchoredPosition = new Vector2(0, -80);

            var tmp = comment.GetComponent<TMP_Text>();
            if (tmp != null)
            {
                tmp.enableAutoSizing = true;
                tmp.fontSizeMin = 12;
                tmp.fontSizeMax = 28; // down from Pass 4's 45
            }

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            PrefabUtility.UnloadPrefabContents(root);
            Debug.Log("TweakResultsLayoutPass5: boss note text widened, shrunk further, and pulled back onto the visible paper area.");
        }
    }
}
