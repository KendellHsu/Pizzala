// ─────────────────────────────────────────────────────────────
// BuildBoothStatusScreen.cs — builds PZ_BoothStatusScreen.prefab: a small world-space
// panel (dark rounded backing, ~0.36m wide) with a HITS line and a TIME line in the
// pixel font, plus the BoothStatusScreen follow component with its text refs wired.
//
// One flat panel on purpose - a 3-segment "curved" face hugging the rim was tried and
// looked worse than this at these dimensions.
//
// The prefab has no boothCenter/playerHead assigned (scene-specific) - assign those when
// you drop it into a scene, or use Setup Booth Preview (Current Scene) which wires a rig.
// Run from Unity: Tools > Pizzala > Build Booth Status Screen. Safe to re-run, but it
// REPLACES the prefab, so any values tuned on it by hand are reset to the ones below.
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

        // Tuned against the real booth - keep in sync if you re-tune in the scene, since
        // re-running this tool overwrites the prefab with these.
        const float OrbitRadius = 1.25f; // settled on by eye in the booth - comfortable reading distance
        const float Height = 0.34f;      // ~1.1m world, i.e. booth counter height
        const float TiltUp = 0f;         // 0 = panel exactly square to the radius (no lean back)

        const float Width = 624f;   // canvas units; 1000 units = 1m at the canvas's 0.001 scale
        const float PanelHeight = 384f;

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
            canvasRect.sizeDelta = new Vector2(Width, PanelHeight);
            canvasRect.localScale = Vector3.one * 0.001f; // 1000 canvas units -> 1 metre

            var panel = NewUIChild("BackPanel", root.transform, out RectTransform panelRect);
            Stretch(panelRect);
            var panelImg = panel.AddComponent<Image>();
            panelImg.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
            panelImg.type = Image.Type.Sliced;
            panelImg.color = new Color(0.09f, 0.11f, 0.13f, 0.95f);

            var hits = NewText("HitsText", root.transform, font, "HITS  0");
            var hitsRect = (RectTransform)hits.transform;
            hitsRect.anchorMin = new Vector2(0, 0.5f);
            hitsRect.anchorMax = new Vector2(1, 1f);
            hitsRect.offsetMin = new Vector2(24, 0);
            hitsRect.offsetMax = new Vector2(-24, -14);

            var time = NewText("TimeText", root.transform, font, "TIME  2:00");
            var timeRect = (RectTransform)time.transform;
            timeRect.anchorMin = new Vector2(0, 0f);
            timeRect.anchorMax = new Vector2(1, 0.5f);
            timeRect.offsetMin = new Vector2(24, 14);
            timeRect.offsetMax = new Vector2(-24, 0);

            var screen = root.GetComponent<BoothStatusScreen>();
            screen.hitsText = hits;
            screen.timeText = time;
            screen.orbitRadius = OrbitRadius;
            screen.height = Height;
            screen.tiltUpDegrees = TiltUp;

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            Debug.Log($"BuildBoothStatusScreen: created {PrefabPath}" + (font == null
                ? " - pixel font not found, texts use the TMP default; run Setup Pixel Font for the pixel look."
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

        static TMP_Text NewText(string name, Transform parent, TMP_FontAsset font, string text)
        {
            var go = NewUIChild(name, parent, out _);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            if (font != null) tmp.font = font;
            tmp.text = text;
            tmp.color = new Color(0.85f, 0.95f, 1f, 1f);
            tmp.alignment = TextAlignmentOptions.Left;
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
