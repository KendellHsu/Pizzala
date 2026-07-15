// ─────────────────────────────────────────────────────────────
// PixelFontSetup.cs — one-time setup: build a TMP Font Asset from the pixel
// font and swap every TMP_Text on the results screen prefabs over to it.
// Run from Unity (Tools > Pizzala > Setup Pixel Font) - building a Font Asset
// needs the Editor's font engine, so this can't be done via batchmode/YAML.
// Safe to re-run: reuses the existing Font Asset if one is already there.
// ─────────────────────────────────────────────────────────────
using UnityEditor;
using UnityEngine;
using TMPro;

namespace Pizzala.EditorTools
{
    public static class PixelFontSetup
    {
        const string FontPath = "Assets/Prefabs/UI/LoRes9OTWide-Bold.ttf";
        const string FontAssetPath = "Assets/Prefabs/UI/LoRes9OTWide-Bold SDF.asset";

        static readonly string[] PrefabPaths =
        {
            "Assets/Prefabs/UI/PZ_ResultsCanvas.prefab",
            "Assets/Prefabs/UI/PZ_PhotoEntry.prefab",
        };

        [MenuItem("Tools/Pizzala/Setup Pixel Font")]
        public static void Setup()
        {
            var font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
            if (font == null)
            {
                Debug.LogError($"PixelFontSetup: font not found at {FontPath}");
                return;
            }

            var fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            if (fontAsset == null)
            {
                fontAsset = TMP_FontAsset.CreateFontAsset(font);
                if (fontAsset == null)
                {
                    Debug.LogError("PixelFontSetup: TMP_FontAsset.CreateFontAsset failed.");
                    return;
                }
                AssetDatabase.CreateAsset(fontAsset, FontAssetPath);
                Debug.Log($"PixelFontSetup: created {FontAssetPath}");
            }

            int swapped = 0;
            foreach (var prefabPath in PrefabPaths)
            {
                var root = PrefabUtility.LoadPrefabContents(prefabPath);
                foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
                {
                    text.font = fontAsset;
                    swapped++;
                }
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"PixelFontSetup: swapped {swapped} TMP_Text component(s) across {PrefabPaths.Length} prefab(s) to the pixel font.");
        }
    }
}
