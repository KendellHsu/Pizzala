// ─────────────────────────────────────────────────────────────
// TweakResultsLayoutPass2.cs — second layout pass on PZ_ResultsCanvas from playtest
// feedback (2026-07-16):
//  Middle group: stat columns nudged right, font x0.8 (30->24), boss portrait
//    x1.7 (220x260 -> 374x442) and raised
//  Experimental: boss note ~3x its drawn size (the sprite is 1.25:1, so a 900x720
//    rect draws it at full rect size instead of the tiny 325x260 it was getting),
//    moved up to center; comment text area enlarged to match; caption heading
//    switched to white (the white BackgroundPanel is now hidden for Experimental -
//    wired here - so the heading sits on the dark scene)
// Run from Unity: Tools > Pizzala > Tweak Results Layout Pass 2. Safe to re-run.
// ─────────────────────────────────────────────────────────────
using UnityEditor;
using UnityEngine;
using TMPro;
using Pizzala.UI;

namespace Pizzala.EditorTools
{
    public static class TweakResultsLayoutPass2
    {
        const string PrefabPath = "Assets/Prefabs/UI/PZ_ResultsCanvas.prefab";

        [MenuItem("Tools/Pizzala/Tweak Results Layout Pass 2")]
        public static void Run()
        {
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);

            // ── Middle group ──
            var middle = root.transform.Find("MiddlePanel");
            if (middle != null)
            {
                var labels = middle.Find("LabelsText") as RectTransform;
                var values = middle.Find("ValuesText") as RectTransform;
                if (labels != null)
                {
                    labels.anchoredPosition = new Vector2(0, 0); // right of the bigger portrait
                    SetFontSize(labels, 24);                     // 30 x 0.8
                }
                if (values != null)
                {
                    values.anchoredPosition = new Vector2(260, 0);
                    SetFontSize(values, 24);
                }
                var portrait = middle.Find("BossPortraitImage") as RectTransform;
                if (portrait != null)
                {
                    portrait.sizeDelta = new Vector2(374, 442);        // 220x260 x1.7
                    portrait.anchoredPosition = new Vector2(-300, 35); // raised, kept on-canvas
                }
            }
            else Debug.LogWarning("TweakResultsLayoutPass2: MiddlePanel not found - run Build Middle Panel first.");

            // ── Experimental group ──
            var experimental = root.transform.Find("ExperimentalPanel");
            if (experimental != null)
            {
                var note = experimental.Find("BossNotePanel") as RectTransform;
                if (note != null)
                {
                    // 900x720 matches the note sprite's 1.25:1 aspect exactly, so
                    // PreserveAspect no longer shrinks it - ~3x the previous drawn size.
                    note.sizeDelta = new Vector2(900, 720);
                    note.anchoredPosition = new Vector2(0, 10);

                    var comment = note.Find("BossCommentText") as RectTransform;
                    if (comment != null)
                    {
                        comment.sizeDelta = new Vector2(600, 340);
                        comment.anchoredPosition = new Vector2(0, -20);
                        var tmp = comment.GetComponent<TMP_Text>();
                        if (tmp != null) { tmp.enableAutoSizing = true; tmp.fontSizeMin = 18; tmp.fontSizeMax = 44; }
                    }
                }

                var caption = experimental.Find("CaptionText") as RectTransform;
                if (caption != null)
                {
                    caption.anchoredPosition = new Vector2(0, 370); // clear of the bigger note
                    var tmp = caption.GetComponent<TMP_Text>();
                    if (tmp != null) tmp.color = Color.white; // background hidden for Experimental -> dark scene behind
                }
            }

            // ── Wire the background panel so the controller can hide it for Experimental ──
            var controller = root.GetComponent<ResultsScreenController>();
            var background = root.transform.Find("BackgroundPanel");
            if (controller != null && background != null)
                controller.backgroundPanel = background.gameObject;
            else
                Debug.LogWarning("TweakResultsLayoutPass2: controller or BackgroundPanel missing - backgroundPanel not wired.");

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            PrefabUtility.UnloadPrefabContents(root);
            Debug.Log("TweakResultsLayoutPass2: middle text/portrait, boss note size/position, caption color, and background wiring updated.");
        }

        static void SetFontSize(RectTransform textRect, float size)
        {
            var tmp = textRect.GetComponent<TMP_Text>();
            if (tmp == null) return;
            tmp.enableAutoSizing = false;
            tmp.fontSize = size;
        }
    }
}
