// ─────────────────────────────────────────────────────────────
// FixExperimentalPanelLayout.cs — HISTORICAL, superseded by Build Photo Wall Slots
// (the photo wall no longer uses PhotoGrid/GridLayoutGroup - it's pre-placed slots
// now, see ResultsScreenController's customerPhotoSlots/environmentPhotoSlots). Kept
// for reference on the CaptionText setup. Originally repaired the Experimental
// photo-wall screen:
//  1. deleted the leftover purple/green/yellow placeholder blocks under PhotoGrid
//     (they were stand-ins from before PZ_PhotoEntry existed - they're why the wall
//     showed colored squares instead of polaroids)
//  2. creates the CaptionText heading and wires it (also null - why no text appeared)
//  3. lays out PhotoGrid (upper area, room for 4x2 polaroids) and BossNotePanel
//     (lower area) so they don't overlap in the 1000x800 canvas
// Run from Unity: Tools > Pizzala > Fix Experimental Panel Layout. Safe to re-run.
// Positions are a first guess - nudge in the Inspector if anything looks off.
// ─────────────────────────────────────────────────────────────
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Pizzala.UI;

namespace Pizzala.EditorTools
{
    public static class FixExperimentalPanelLayout
    {
        const string CanvasPrefabPath = "Assets/Prefabs/UI/PZ_ResultsCanvas.prefab";
        const string PhotoEntryPrefabPath = "Assets/Prefabs/UI/PZ_PhotoEntry.prefab";
        const string FontAssetPath = "Assets/Prefabs/UI/LoRes9OTWide-Bold SDF.asset";

        [MenuItem("Tools/Pizzala/Fix Experimental Panel Layout")]
        public static void Run()
        {
            var photoEntryPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PhotoEntryPrefabPath);
            if (photoEntryPrefab == null)
            {
                Debug.LogError($"FixExperimentalPanelLayout: photo entry prefab not found at {PhotoEntryPrefabPath}");
                return;
            }

            var root = PrefabUtility.LoadPrefabContents(CanvasPrefabPath);
            var experimentalPanel = root.transform.Find("ExperimentalPanel");
            if (experimentalPanel == null)
            {
                Debug.LogError("FixExperimentalPanelLayout: ExperimentalPanel not found.");
                PrefabUtility.UnloadPrefabContents(root);
                return;
            }

            // 1. Clear the placeholder color blocks - at runtime the grid is populated
            //    from PZ_PhotoEntry instances, so nothing should live here in the prefab.
            var photoGrid = experimentalPanel.Find("PhotoGrid");
            if (photoGrid != null)
            {
                for (int i = photoGrid.childCount - 1; i >= 0; i--)
                    Object.DestroyImmediate(photoGrid.GetChild(i).gameObject);

                // Upper area of the canvas: room for a 4-column x 2-row polaroid wall
                // (cells 200x240, spacing 20 -> 860x500 content).
                var gridRect = (RectTransform)photoGrid;
                gridRect.anchoredPosition = new Vector2(0, 60);
                gridRect.sizeDelta = new Vector2(860, 520);
            }

            // 2+4. Boss note moves to the lower area so the wall and note don't overlap.
            var bossNotePanel = experimentalPanel.Find("BossNotePanel");
            if (bossNotePanel != null)
            {
                var noteRect = (RectTransform)bossNotePanel;
                noteRect.anchoredPosition = new Vector2(0, -265);
                noteRect.sizeDelta = new Vector2(620, 260);

                var commentText = bossNotePanel.Find("BossCommentText");
                if (commentText != null)
                {
                    var textRect = (RectTransform)commentText;
                    textRect.anchoredPosition = Vector2.zero;
                    textRect.sizeDelta = new Vector2(520, 200);
                    var tmp = commentText.GetComponent<TMP_Text>();
                    if (tmp != null)
                    {
                        tmp.enableAutoSizing = true; // comment length varies - let it shrink to fit the note
                        tmp.fontSizeMin = 16;
                        tmp.fontSizeMax = 36;
                    }
                }
            }

            // 3. Heading text across the top ("Tonight's Damage Report ..." gets set at runtime).
            var captionTr = experimentalPanel.Find("CaptionText");
            GameObject captionGO;
            if (captionTr != null)
            {
                captionGO = captionTr.gameObject;
            }
            else
            {
                captionGO = new GameObject("CaptionText", typeof(RectTransform), typeof(TextMeshProUGUI));
                captionGO.layer = experimentalPanel.gameObject.layer;
                captionGO.transform.SetParent(experimentalPanel, false);
            }
            var captionRect = (RectTransform)captionGO.transform;
            captionRect.anchorMin = new Vector2(0.5f, 0.5f);
            captionRect.anchorMax = new Vector2(0.5f, 0.5f);
            captionRect.pivot = new Vector2(0.5f, 0.5f);
            captionRect.anchoredPosition = new Vector2(0, 350);
            captionRect.sizeDelta = new Vector2(920, 110);
            var captionTmp = captionGO.GetComponent<TextMeshProUGUI>();
            captionTmp.color = Color.black;
            captionTmp.fontSize = 28;
            captionTmp.alignment = TextAlignmentOptions.Center;
            captionTmp.text = "Tonight's Damage Report";
            var pixelFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            if (pixelFont != null) captionTmp.font = pixelFont;

            // Wire everything into the controller.
            var controller = root.GetComponent<ResultsScreenController>();
            if (controller != null)
            {
                controller.captionText = captionTmp;
            }
            else
            {
                Debug.LogWarning("FixExperimentalPanelLayout: ResultsScreenController missing on prefab root - wire captionText manually.");
            }

            PrefabUtility.SaveAsPrefabAsset(root, CanvasPrefabPath);
            PrefabUtility.UnloadPrefabContents(root);
            Debug.Log("FixExperimentalPanelLayout: cleared placeholder blocks, wired captionText, laid out photo wall and boss note.");
        }
    }
}
