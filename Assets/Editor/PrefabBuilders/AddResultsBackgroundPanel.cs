// ─────────────────────────────────────────────────────────────
// AddResultsBackgroundPanel.cs — adds one shared, semi-transparent rounded-rect
// background behind everything on the results screen. Sits as the first child
// of the canvas (renders behind ControlPanel/ExperimentalPanel and everything
// under them), sized to match the canvas itself so it backs all 3 conditions.
// Run from Unity: Tools > Pizzala > Add Results Background Panel.
// Safe to re-run: if BackgroundPanel already exists, just refreshes its color/sprite
// instead of skipping, so re-running after tweaking BackgroundColor below updates it.
// ─────────────────────────────────────────────────────────────
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Pizzala.EditorTools
{
    public static class AddResultsBackgroundPanel
    {
        const string PrefabPath = "Assets/Prefabs/UI/PZ_ResultsCanvas.prefab";

        // White, more transparent than the first pass - tweak here and re-run, or just
        // adjust the color directly on BackgroundPanel's Image component in the Inspector.
        static readonly Color BackgroundColor = new Color(1f, 1f, 1f, 0.5f);

        // Sliced-image border size is (sprite border px) / (spritePixelsPerUnit * this) -
        // a smaller multiplier stretches less of the corner texture over more screen
        // space, which is what makes the rounded corner read as bigger.
        const float CornerSizeMultiplier = 0.4f;

        [MenuItem("Tools/Pizzala/Add Results Background Panel")]
        public static void Run()
        {
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            var canvasRect = root.GetComponent<RectTransform>();

            var existing = root.transform.Find("BackgroundPanel");
            GameObject go;
            if (existing != null)
            {
                go = existing.gameObject;
            }
            else
            {
                go = new GameObject("BackgroundPanel", typeof(RectTransform), typeof(Image));
                go.layer = root.layer;
                go.transform.SetParent(root.transform, false);
                go.transform.SetAsFirstSibling(); // renders behind everything else on the canvas
            }

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = canvasRect.sizeDelta; // same size as the results screen

            var image = go.GetComponent<Image>();
            image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
            image.type = Image.Type.Sliced;
            image.color = BackgroundColor;
            image.pixelsPerUnitMultiplier = CornerSizeMultiplier;
            image.raycastTarget = false;

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            PrefabUtility.UnloadPrefabContents(root);
            Debug.Log("AddResultsBackgroundPanel: background panel updated.");
        }
    }
}
