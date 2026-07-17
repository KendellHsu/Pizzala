// ─────────────────────────────────────────────────────────────
// BuildBoothStatusScreen.cs — builds PZ_BoothStatusScreen.prefab: a small world-space
// "monitor" (dark rounded panel, ~0.36m wide) with a HITS line and a TIME line in the
// pixel font, plus the BoothStatusScreen follow component with its text refs wired.
// The prefab has no boothCenter/playerHead assigned (scene-specific) - assign those when
// you drop it into a scene, or use Setup Booth Screen Demo which wires a test rig.
// Run from Unity: Tools > Pizzala > Build Booth Status Screen. Safe to re-run.
// ─────────────────────────────────────────────────────────────
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Pizzala.UI;

namespace Pizzala.EditorTools
{
    public static class BuildBoothStatusScreen
    {
        const string PrefabPath = "Assets/Prefabs/UI/PZ_BoothStatusScreen.prefab";
        const string FontAssetPath = "Assets/Prefabs/UI/LoRes9OTWide-Bold SDF.asset";

        [MenuItem("Tools/Pizzala/Build Booth Status Screen")]
        public static void Run()
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);

            var root = new GameObject("PZ_BoothStatusScreen",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(BoothStatusScreen));
            root.layer = LayerMask.NameToLayer("UI");

            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var canvasRect = (RectTransform)root.transform;
            canvasRect.sizeDelta = new Vector2(360, 230);
            canvasRect.localScale = Vector3.one * 0.001f; // 360x230 "px" -> 0.36m x 0.23m

            // Dark rounded monitor panel filling the canvas.
            var panel = NewUIChild("BackPanel", root.transform, out RectTransform panelRect);
            Stretch(panelRect);
            var panelImg = panel.AddComponent<Image>();
            panelImg.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
            panelImg.type = Image.Type.Sliced;
            panelImg.color = new Color(0.09f, 0.11f, 0.13f, 0.95f);

            var hits = NewText("HitsText", root.transform, font, "HITS  0", TextAlignmentOptions.Left);
            var hitsRect = (RectTransform)hits.transform;
            hitsRect.anchorMin = new Vector2(0, 0.5f);
            hitsRect.anchorMax = new Vector2(1, 1f);
            hitsRect.offsetMin = new Vector2(24, 0);
            hitsRect.offsetMax = new Vector2(-24, -14);

            var time = NewText("TimeText", root.transform, font, "TIME  2:00", TextAlignmentOptions.Left);
            var timeRect = (RectTransform)time.transform;
            timeRect.anchorMin = new Vector2(0, 0f);
            timeRect.anchorMax = new Vector2(1, 0.5f);
            timeRect.offsetMin = new Vector2(24, 14);
            timeRect.offsetMax = new Vector2(-24, 0);

            var screen = root.GetComponent<BoothStatusScreen>();
            screen.hitsText = hits;
            screen.timeText = time;

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            Debug.Log($"BuildBoothStatusScreen: created {PrefabPath}" + (font == null
                ? " (pixel font not found - texts use TMP default; run Setup Pixel Font first for the pixel look)."
                : "."));
            Selection.activeObject = prefab;
        }

        static GameObject NewUIChild(string name, Transform parent, out RectTransform rect)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            rect = (RectTransform)go.transform;
            return go;
        }

        static TMP_Text NewText(string name, Transform parent, TMP_FontAsset font, string text, TextAlignmentOptions align)
        {
            var go = NewUIChild(name, parent, out _);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            if (font != null) tmp.font = font;
            tmp.text = text;
            tmp.color = new Color(0.85f, 0.95f, 1f, 1f); // light, glowy-monitor feel
            tmp.alignment = align;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 24;
            tmp.fontSizeMax = 72;
            return tmp;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
