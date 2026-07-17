// ─────────────────────────────────────────────────────────────
// BuildGameFlowUI.cs — builds PZ_GameFlowUI.prefab: the start screen, the pause menu and
// the resume countdown, all on one world-space canvas that lazily follows the player
// (PlayerFacingPanel). Same white rounded panel + pixel font as the results screen.
//
// One prefab rather than three because all three are the same surface at different
// moments, and they'd otherwise each need their own follow rig fighting for the same spot
// in front of the player. GameFlowController shows/hides the three children.
//
// The countdown is a sibling of the pause panel, not a child: resuming hides the menu but
// must keep showing the number.
//
// Run from Unity: Tools > Pizzala > Build Game Flow UI. Safe to re-run (replaces it).
// ─────────────────────────────────────────────────────────────
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Pizzala.UI;

namespace Pizzala.EditorTools
{
    public static class BuildGameFlowUI
    {
        const string PrefabPath = "Assets/Prefabs/UI/PZ_GameFlowUI.prefab";
        const string FontAssetPath = "Assets/Prefabs/UI/LoRes9OTWide-Bold SDF.asset";

        // Matches the results screen's backdrop so the game speaks one visual language.
        static readonly Color PanelColor = new Color(1f, 1f, 1f, 0.5f);
        const float CornerSizeMultiplier = 0.4f;
        static readonly Color TextColor = Color.black;

        const float CanvasWidth = 900f;   // canvas units; 1000 units = 1m at the 0.001 scale
        const float CanvasHeight = 600f;

        [MenuItem("Tools/Pizzala/Build Game Flow UI")]
        public static void Run()
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);

            var root = new GameObject("PZ_GameFlowUI",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(PlayerFacingPanel));
            root.layer = LayerMask.NameToLayer("UI");

            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var canvasRect = (RectTransform)root.transform;
            canvasRect.sizeDelta = new Vector2(CanvasWidth, CanvasHeight);
            canvasRect.localScale = Vector3.one * 0.001f;

            var start = BuildPanel("StartScreenPanel", root.transform, font,
                                   "PIZZALAB", "PRESS  A  TO  START");
            var pause = BuildPanel("PauseMenuPanel", root.transform, font,
                                   "PAUSED", "PRESS  B  TO  RESUME");

            // Big bare number, no panel behind it - the menu it replaces is already gone.
            var countdown = NewText("CountdownText", root.transform, font, "3", 320f);
            var countdownRect = (RectTransform)countdown.transform;
            countdownRect.anchorMin = Vector2.zero;
            countdownRect.anchorMax = Vector2.one;
            countdownRect.offsetMin = Vector2.zero;
            countdownRect.offsetMax = Vector2.zero;

            // Off by default: GameFlowController turns on whichever one the state needs.
            start.SetActive(false);
            pause.SetActive(false);
            countdown.gameObject.SetActive(false);

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            Debug.Log($"BuildGameFlowUI: created {PrefabPath}" + (font == null
                ? " - pixel font not found, using the TMP default; run Setup Pixel Font for the pixel look."
                : ".") + " Drop it in the scene and wire the 3 children into GameFlowController.");
            Selection.activeObject = prefab;
        }

        static GameObject BuildPanel(string name, Transform parent, TMP_FontAsset font,
                                     string title, string subtitle)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var img = go.GetComponent<Image>();
            img.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
            img.type = Image.Type.Sliced;
            img.color = PanelColor;
            img.pixelsPerUnitMultiplier = CornerSizeMultiplier;
            img.raycastTarget = false;

            var titleText = NewText("Title", go.transform, font, title, 110f);
            var titleRect = (RectTransform)titleText.transform;
            titleRect.anchorMin = new Vector2(0.05f, 0.5f);
            titleRect.anchorMax = new Vector2(0.95f, 0.9f);
            titleRect.offsetMin = Vector2.zero;
            titleRect.offsetMax = Vector2.zero;

            var subText = NewText("Subtitle", go.transform, font, subtitle, 48f);
            var subRect = (RectTransform)subText.transform;
            subRect.anchorMin = new Vector2(0.05f, 0.15f);
            subRect.anchorMax = new Vector2(0.95f, 0.45f);
            subRect.offsetMin = Vector2.zero;
            subRect.offsetMax = Vector2.zero;

            return go;
        }

        static TMP_Text NewText(string name, Transform parent, TMP_FontAsset font, string text, float size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);

            var tmp = go.AddComponent<TextMeshProUGUI>();
            if (font != null) tmp.font = font;
            tmp.text = text;
            tmp.color = TextColor;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 12f;
            tmp.fontSizeMax = size;
            return tmp;
        }
    }
}
