// ─────────────────────────────────────────────────────────────
// ApplyPolaroidFrame.cs — wires the new UI_Polaroid_png (1488x1815, "PIZZALAB"
// branded) onto PZ_PhotoEntry.prefab: the Frame Image gets the polaroid sprite,
// and the Photo RawImage / CaptionText get stretch-anchored into the frame's
// actual black photo-window and white caption-strip areas (measured directly
// from the PNG's pixels - see the fractions below - since the visible art sits
// well inside the image's own transparent-padded bounds, not flush with the
// texture's edges). Also widens PhotoGrid's cell size in PZ_ResultsCanvas.prefab
// to match the frame's aspect ratio (harmless no-op once Build Photo Wall Slots has
// removed the GridLayoutGroup - run this tool BEFORE that one).
// Run from Unity: Tools > Pizzala > Apply Polaroid Frame. Safe to re-run.
// ─────────────────────────────────────────────────────────────
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Pizzala.EditorTools
{
    public static class ApplyPolaroidFrame
    {
        const string PhotoEntryPath = "Assets/Prefabs/UI/PZ_PhotoEntry.prefab";
        const string ResultsCanvasPath = "Assets/Prefabs/UI/PZ_ResultsCanvas.prefab";
        const string PolaroidTexturePath = "Assets/Prefabs/UI/UI_Polaroid_png.png";

        // Measured from the PNG (1488x1815): black photo window spans x 0.075-0.935,
        // y 0.107-0.693 from the top. Converted to Unity's bottom-up anchor Y below.
        static readonly Vector2 PhotoAnchorMin = new Vector2(0.075f, 0.307f);
        static readonly Vector2 PhotoAnchorMax = new Vector2(0.935f, 0.893f);
        static readonly Vector2 CaptionAnchorMin = new Vector2(0.08f, 0.04f);
        static readonly Vector2 CaptionAnchorMax = new Vector2(0.92f, 0.29f);

        const float EntryWidth = 220f;
        const float EntryHeight = 268f; // matches the PNG's 1488:1815 aspect at this width

        [MenuItem("Tools/Pizzala/Apply Polaroid Frame")]
        public static void Run()
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(PolaroidTexturePath);
            if (sprite == null)
            {
                Debug.LogError($"ApplyPolaroidFrame: no Sprite at {PolaroidTexturePath} - run Restructure Paged Results Screen first (it fixes the texture import type).");
                return;
            }

            var root = PrefabUtility.LoadPrefabContents(PhotoEntryPath);
            var rootRect = (RectTransform)root.transform;
            rootRect.sizeDelta = new Vector2(EntryWidth, EntryHeight);

            var frame = root.transform.Find("Frame")?.GetComponent<Image>();
            if (frame != null)
            {
                frame.sprite = sprite;
                frame.type = Image.Type.Simple;
            }
            else Debug.LogWarning("ApplyPolaroidFrame: Frame Image not found on PZ_PhotoEntry.");

            var photoRect = root.transform.Find("Photo") as RectTransform;
            if (photoRect != null)
            {
                photoRect.anchorMin = PhotoAnchorMin;
                photoRect.anchorMax = PhotoAnchorMax;
                photoRect.offsetMin = Vector2.zero;
                photoRect.offsetMax = Vector2.zero;
            }
            else Debug.LogWarning("ApplyPolaroidFrame: Photo RawImage not found on PZ_PhotoEntry.");

            var captionRect = root.transform.Find("CaptionText") as RectTransform;
            if (captionRect != null)
            {
                captionRect.anchorMin = CaptionAnchorMin;
                captionRect.anchorMax = CaptionAnchorMax;
                captionRect.offsetMin = Vector2.zero;
                captionRect.offsetMax = Vector2.zero;

                // Was a fixed size 22 with auto-sizing off - real captions ("Right in the
                // face!", "Health code violation"...) are much longer than the "Caption"
                // placeholder and don't fit, overflowing past the strip. Auto-size shrinks
                // to whatever actually fits instead.
                var tmp = captionRect.GetComponent<TMP_Text>();
                if (tmp != null)
                {
                    tmp.enableAutoSizing = true;
                    tmp.fontSizeMin = 8;
                    tmp.fontSizeMax = 16;
                }
            }
            else Debug.LogWarning("ApplyPolaroidFrame: CaptionText not found on PZ_PhotoEntry.");

            PrefabUtility.SaveAsPrefabAsset(root, PhotoEntryPath);
            PrefabUtility.UnloadPrefabContents(root);

            // Widen the photo wall's grid cells to match the new (wider) polaroid shape.
            var canvasRoot = PrefabUtility.LoadPrefabContents(ResultsCanvasPath);
            var grid = canvasRoot.transform.Find("PhotoWallPanel/PhotoGrid")
                       ?? canvasRoot.transform.Find("ExperimentalPanel/PhotoGrid"); // fallback if run before the rename
            var layout = grid != null ? grid.GetComponent<GridLayoutGroup>() : null;
            if (layout != null)
            {
                layout.cellSize = new Vector2(EntryWidth, EntryHeight);
                PrefabUtility.SaveAsPrefabAsset(canvasRoot, ResultsCanvasPath);
                Debug.Log("ApplyPolaroidFrame: PhotoGrid cell size updated to match the new polaroid frame.");
            }
            else Debug.LogWarning("ApplyPolaroidFrame: PhotoGrid/GridLayoutGroup not found - cell size left as-is.");
            PrefabUtility.UnloadPrefabContents(canvasRoot);

            Debug.Log("ApplyPolaroidFrame: PZ_PhotoEntry now uses the new polaroid frame with photo/caption positioned inside it.");
        }
    }
}
