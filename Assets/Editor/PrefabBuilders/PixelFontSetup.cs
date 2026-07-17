// ─────────────────────────────────────────────────────────────
// PixelFontSetup.cs — one-time setup: build a TMP Font Asset from the pixel
// font and swap every TMP_Text on the results screen prefabs over to it.
// Run from Unity (Tools > Pizzala > Setup Pixel Font) - building a Font Asset
// needs the Editor's font engine, so this can't be done via batchmode/YAML.
// Safe to re-run: reuses the existing Font Asset if one is already there.
// ─────────────────────────────────────────────────────────────
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using TMPro;

namespace Pizzala.EditorTools
{
    public static class PixelFontSetup
    {
        const string FontPath = "Assets/Prefabs/UI/LoRes9OTWide-Bold.ttf";
        const string FontAssetPath = "Assets/Prefabs/UI/LoRes9OTWide-Bold SDF.asset";

        // Everything the results screen actually prints (labels/values/captions/boss note).
        // Static atlas mode only ever has these glyphs - add more here if new text needs them.
        const string CharacterSet =
            " !\"#$%&'()*+,-./0123456789:;<=>?@" +
            "ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`" +
            "abcdefghijklmnopqrstuvwxyz{|}~";

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
            bool broken = fontAsset != null && (fontAsset.atlasTextures == null
                || fontAsset.atlasTextures.Length == 0 || fontAsset.atlasTextures[0] == null);
            if (broken)
            {
                Debug.LogWarning("PixelFontSetup: existing font asset's atlas texture is missing - rebuilding it.");
                AssetDatabase.DeleteAsset(FontAssetPath);
                fontAsset = null;
            }

            if (fontAsset == null)
            {
                // Static (not Dynamic) atlas mode: bake the character set into the atlas
                // texture right now, at creation time, instead of leaving it to be filled
                // in lazily at runtime. The first attempt used Dynamic mode's default and
                // left the atlas in a state that didn't survive a domain reload - Static
                // mode is what the built-in Font Asset Creator window uses by default, and
                // TryAddCharacters renders real pixels into the atlas before we ever save,
                // so there's no empty/lazy texture for the reference to go stale on.
                fontAsset = TMP_FontAsset.CreateFontAsset(
                    font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Static);
                if (fontAsset == null)
                {
                    Debug.LogError("PixelFontSetup: TMP_FontAsset.CreateFontAsset failed.");
                    return;
                }

                if (!fontAsset.TryAddCharacters(CharacterSet, out string missing))
                    Debug.LogWarning($"PixelFontSetup: font is missing glyphs for: \"{missing}\"");

                AssetDatabase.CreateAsset(fontAsset, FontAssetPath);

                if (fontAsset.atlasTextures != null && fontAsset.atlasTextures.Length > 0 && fontAsset.atlasTextures[0] != null)
                {
                    fontAsset.atlasTextures[0].name = fontAsset.name + " Atlas";
                    AssetDatabase.AddObjectToAsset(fontAsset.atlasTextures[0], fontAsset);
                }
                if (fontAsset.material != null)
                {
                    fontAsset.material.name = fontAsset.name + " Material";
                    AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
                }

                EditorUtility.SetDirty(fontAsset);
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(FontAssetPath, ImportAssetOptions.ForceUpdate);
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
