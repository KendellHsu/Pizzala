// ─────────────────────────────────────────────────────────────
// AddResultsPostNoteButtons.cs — adds ShareButton (bottom-right corner) and PlayAgainButton
// (just to its left) as
// children of BossNotePanel in PZ_ResultsCanvas.prefab, and wires them into
// ResultsScreenController.shareButton/playAgainButton. Both start inactive - the
// controller reveals them itself postNoteButtonDelay seconds after SetBossComment() lands
// (see ResultsScreenController.cs). Being children of BossNotePanel means they only ever
// render while that page is showing, for free, via normal Unity active-state cascading.
//
// Same visual language as the rest of the UI (white rounded panel, black pixel-font text).
// Anchored to the note's bottom-right corner (anchor+pivot (1,0)) and pulled inward by
// ButtonMargin so they sit just inside the paper. Click behaviour is NOT wired up here -
// what Share/Play Again actually do hasn't been decided yet.
//
// Run: Tools > Pizzala > Add Results Post-Note Buttons. Safe to re-run (replaces them).
// ─────────────────────────────────────────────────────────────
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Pizzala.UI;

namespace Pizzala.EditorTools
{
    public static class AddResultsPostNoteButtons
    {
        const string PrefabPath = "Assets/Prefabs/UI/PZ_ResultsCanvas.prefab";
        const string FontAssetPath = "Assets/Prefabs/UI/LoRes9OTWide-Bold SDF.asset";
        const string BossNotePanelName = "BossNotePanel";

        static readonly Color PanelColor = new Color(1f, 1f, 1f, 0.9f);
        const float CornerSizeMultiplier = 0.4f;
        static readonly Color TextColor = Color.black;

        const float ButtonWidth = 480f;
        const float ButtonHeight = 160f;
        // Anchored to the note's bottom-right corner (anchor+pivot (1,0)). These are the
        // inward margins from that corner: positive X pulls left off the right edge,
        // positive Y lifts up off the bottom edge, so the button sits just inside the note.
        const float ButtonMarginX = 60f;
        const float ButtonMarginY = 60f;
        // Play Again sits to the left of Share, one button-width + gap further in.
        const float ButtonGap = 40f;

        [MenuItem("Tools/Pizzala/Add Results Post-Note Buttons")]
        public static void Run()
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);

            var bossNotePanel = root.transform.Find(BossNotePanelName);
            if (bossNotePanel == null)
            {
                Debug.LogError($"AddResultsPostNoteButtons: {BossNotePanelName} not found under the prefab root.");
                PrefabUtility.UnloadPrefabContents(root);
                return;
            }

            var existingShare = bossNotePanel.Find("ShareButton");
            if (existingShare != null) Object.DestroyImmediate(existingShare.gameObject);
            var existingPlayAgain = bossNotePanel.Find("PlayAgainButton");
            if (existingPlayAgain != null) Object.DestroyImmediate(existingPlayAgain.gameObject);

            // Bottom-right corner of the note: Share hugs the corner, Play Again to its left.
            var shareAnchored = new Vector2(-ButtonMarginX, ButtonMarginY);
            var playAgainAnchored = new Vector2(-ButtonMarginX - ButtonWidth - ButtonGap, ButtonMarginY);
            var shareButton = BuildButton("ShareButton", bossNotePanel, font, "SHARE", shareAnchored);
            var playAgainButton = BuildButton("PlayAgainButton", bossNotePanel, font, "PLAY AGAIN", playAgainAnchored);

            shareButton.SetActive(false);
            playAgainButton.SetActive(false);

            var resultsScreen = root.GetComponent<ResultsScreenController>();
            if (resultsScreen != null)
            {
                resultsScreen.shareButton = shareButton;
                resultsScreen.playAgainButton = playAgainButton;
            }
            else
            {
                Debug.LogWarning("AddResultsPostNoteButtons: no ResultsScreenController on the prefab root - buttons built but not wired.");
            }

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            PrefabUtility.UnloadPrefabContents(root);
            Debug.Log("AddResultsPostNoteButtons: added Share (left) / Play Again (right) under BossNotePanel, wired into ResultsScreenController. Click actions still need deciding.");
        }

        static GameObject BuildButton(string name, Transform parent, TMP_FontAsset font, string label, Vector2 anchoredPosition)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(1f, 0f); // bottom-right corner of the note
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);     // button's own bottom-right corner sits at the anchor
            rect.sizeDelta = new Vector2(ButtonWidth, ButtonHeight);
            rect.anchoredPosition = anchoredPosition;

            var img = go.GetComponent<Image>();
            img.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
            img.type = Image.Type.Sliced;
            img.color = PanelColor;
            img.pixelsPerUnitMultiplier = CornerSizeMultiplier;

            var button = go.GetComponent<Button>();
            button.targetGraphic = img;

            var text = new GameObject("Label", typeof(RectTransform));
            text.layer = parent.gameObject.layer;
            text.transform.SetParent(go.transform, false);
            var textRect = (RectTransform)text.transform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var tmp = text.AddComponent<TextMeshProUGUI>();
            if (font != null) tmp.font = font;
            tmp.text = label;
            tmp.color = TextColor;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 18;
            tmp.fontSizeMax = 56;

            return go;
        }
    }
}
