// ─────────────────────────────────────────────────────────────
// TweakResultsLayoutPass3.cs — third layout pass from playtest feedback (2026-07-16):
//  Middle group: portrait nudged right, both text columns nudged right further and
//    down slightly so their top edge lines up with the portrait's top edge
//  Experimental: boss note doubled again (now 4x the original drawn size) - note
//    this is now bigger than the 1000x800 canvas itself; since the World Space
//    canvas has no mask, it will render past the canvas edges rather than clip,
//    but double-check in Unity that it doesn't visually collide with anything else
// Run from Unity: Tools > Pizzala > Tweak Results Layout Pass 3. Safe to re-run.
// Depends on Pass 2 having been run first (reads/adjusts its output positions).
// ─────────────────────────────────────────────────────────────
using UnityEditor;
using UnityEngine;
using TMPro;

namespace Pizzala.EditorTools
{
    public static class TweakResultsLayoutPass3
    {
        const string PrefabPath = "Assets/Prefabs/UI/PZ_ResultsCanvas.prefab";

        [MenuItem("Tools/Pizzala/Tweak Results Layout Pass 3")]
        public static void Run()
        {
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);

            // ── Middle group ──
            var middle = root.transform.Find("MiddlePanel");
            if (middle != null)
            {
                var portrait = middle.Find("BossPortraitImage") as RectTransform;
                if (portrait != null)
                    portrait.anchoredPosition = new Vector2(-250, 35); // right a bit from Pass 2's -300

                // Photo top edge = 35 + 442/2 = 256 - push text down to match, and further right.
                var labels = middle.Find("LabelsText") as RectTransform;
                if (labels != null) labels.anchoredPosition = new Vector2(60, -20);

                var values = middle.Find("ValuesText") as RectTransform;
                if (values != null) values.anchoredPosition = new Vector2(320, -20);
            }
            else Debug.LogWarning("TweakResultsLayoutPass3: MiddlePanel not found.");

            // ── Experimental group: boss note x2 again (900x720 -> 1800x1440) ──
            var experimental = root.transform.Find("ExperimentalPanel");
            if (experimental != null)
            {
                var note = experimental.Find("BossNotePanel") as RectTransform;
                if (note != null)
                {
                    note.sizeDelta = new Vector2(1800, 1440);
                    note.anchoredPosition = new Vector2(0, 10);

                    var comment = note.Find("BossCommentText") as RectTransform;
                    if (comment != null)
                    {
                        comment.sizeDelta = new Vector2(1200, 680);
                        comment.anchoredPosition = new Vector2(0, -40);
                        var tmp = comment.GetComponent<TMP_Text>();
                        if (tmp != null) { tmp.enableAutoSizing = true; tmp.fontSizeMin = 30; tmp.fontSizeMax = 90; }
                    }
                }
                else Debug.LogWarning("TweakResultsLayoutPass3: BossNotePanel not found.");
            }
            else Debug.LogWarning("TweakResultsLayoutPass3: ExperimentalPanel not found.");

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            PrefabUtility.UnloadPrefabContents(root);
            Debug.Log("TweakResultsLayoutPass3: middle portrait/text repositioned, boss note doubled again.");
        }
    }
}
