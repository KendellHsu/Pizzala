using System.IO;
using UnityEditor;
using UnityEngine;
using Pizzala.Customers;

namespace Pizzala.EditorTools
{
    // Flavor icons for PZ_Customer's FlavorIcon (PREFABS.md section 4). The art team delivered
    // real top-down 2D pizza icons plus a speech-bubble frame in Assets/Art/Pizza/2D/ - resize
    // them down and wire into PZ_Customer: the bubble sits behind as a static "FlavorIconFrame"
    // sibling of FlavorIcon, always visible, with the flavor icon layered on top inside it (still
    // toggled on/off by CustomerController itself, unchanged). Re-run after the art changes to
    // re-pull from the same source files.
    public static class FlavorIconBuilder
    {
        const string IconFolder = "Assets/Art/Icons";
        const int IconSize = 256;
        const int FrameSize = 512;
        const string FrameSourcePath = "Assets/Art/Pizza/2D/pizza_Dialogue_2D-08.png";
        const string FrameDestPath = IconFolder + "/FlavorIconFrame.png";

        static readonly (string flavorName, string sourcePath, string iconName)[] Icons =
        {
            ("Margherita", "Assets/Art/Pizza/2D/pizza_Margherita_2D.png", "FlavorIcon_Margherita.png"),
            ("Pepperoni", "Assets/Art/Pizza/2D/pizza_Pepperoni_2D.png", "FlavorIcon_Pepperoni.png"),
            ("CosmicPinkMarshmallow", "Assets/Art/Pizza/2D/pizza_pinkMM_2D.png", "FlavorIcon_CosmicPinkMarshmallow.png"),
        };

        [MenuItem("Tools/Pizzala/Build Flavor Icons From 2D Pizza Art")]
        public static void Build()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Art"))
            {
                Debug.LogError("FlavorIconBuilder: Assets/Art folder not found.");
                return;
            }
            if (!AssetDatabase.IsValidFolder(IconFolder))
                AssetDatabase.CreateFolder("Assets/Art", "Icons");

            var sprites = new Sprite[Icons.Length];
            for (int i = 0; i < Icons.Length; i++)
            {
                var (flavorName, sourcePath, iconName) = Icons[i];
                var destPath = $"{IconFolder}/{iconName}";
                sprites[i] = ImportResizedSprite(sourcePath, destPath, IconSize, 700, flavorName);
            }

            // Frame's own circle window is ~65% of its canvas width, so at the same 700 PPU as
            // the icon, a 512px frame (~0.73m across) gives a ~0.47m clear opening - comfortably
            // larger than the 256px/700PPU icon (~0.37m) sitting inside it.
            var frameSprite = ImportResizedSprite(FrameSourcePath, FrameDestPath, FrameSize, 700, "dialogue frame");

            var root = PrefabUtility.LoadPrefabContents("Assets/Prefabs/PZ_Customer.prefab");
            var controller = root.GetComponent<CustomerController>();
            if (controller == null)
            {
                Debug.LogError("FlavorIconBuilder: PZ_Customer.prefab has no CustomerController.");
                PrefabUtility.UnloadPrefabContents(root);
                return;
            }

            // PizzaFlavor enum order: 0=Margherita, 1=Pepperoni, 2=CosmicPinkMarshmallow -
            // matches the Icons array order above, so no reordering needed.
            controller.flavorSprites = sprites;

            // FlavorIcon's own default sprite was a dangling reference (guid not present
            // anywhere in the project) - shows as a solid white square in Scene view before
            // Play, since CustomerController.Start() is what disables the renderer. Clear it so
            // there's nothing to see until GiveOrder() actually assigns a real icon.
            if (controller.flavorIcon != null)
            {
                controller.flavorIcon.sprite = null;
                controller.flavorIcon.sortingOrder = 1; // draw in front of the frame
                BuildOrUpdateFrame(controller.flavorIcon.transform, frameSprite);
            }

            PrefabUtility.SaveAsPrefabAsset(root, "Assets/Prefabs/PZ_Customer.prefab");
            PrefabUtility.UnloadPrefabContents(root);

            Debug.Log("FlavorIconBuilder: PZ_Customer.flavorSprites + FlavorIconFrame wired with the real 2D pizza art.");
        }

        // FlavorIconFrame is a sibling of FlavorIcon (same parent, same local position) so it
        // isn't affected by CustomerController toggling FlavorIcon's SpriteRenderer.enabled - the
        // bubble stays visible, only the pizza icon inside it appears/disappears with orders.
        static void BuildOrUpdateFrame(Transform flavorIconTransform, Sprite frameSprite)
        {
            var parent = flavorIconTransform.parent;
            var existing = parent.Find("FlavorIconFrame");
            GameObject frameGO;
            if (existing != null)
            {
                frameGO = existing.gameObject;
            }
            else
            {
                frameGO = new GameObject("FlavorIconFrame");
                frameGO.transform.SetParent(parent, false);
                frameGO.transform.localPosition = flavorIconTransform.localPosition;
                frameGO.transform.SetSiblingIndex(flavorIconTransform.GetSiblingIndex()); // frame before icon
            }

            var renderer = frameGO.GetComponent<SpriteRenderer>();
            if (renderer == null)
                renderer = frameGO.AddComponent<SpriteRenderer>();
            renderer.sprite = frameSprite;
            renderer.sortingOrder = 0; // behind FlavorIcon's sortingOrder 1
        }

        // Downscales sourcePath to targetSize x targetSize and imports it as a Sprite at the
        // given pixels-per-unit. CPU-side GetPixelBilinear/SetPixels rather than
        // RenderTexture/Graphics.Blit, which silently no-ops under -nographics batchmode (bit us
        // once already - see git history on this file).
        static Sprite ImportResizedSprite(string sourcePath, string destPath, int targetSize, float pixelsPerUnit, string label)
        {
            if (!File.Exists(sourcePath))
            {
                Debug.LogError($"FlavorIconBuilder: source art not found for {label} at {sourcePath}");
                return null;
            }

            var srcBytes = File.ReadAllBytes(sourcePath);
            var srcTex = new Texture2D(2, 2);
            srcTex.LoadImage(srcBytes);

            var resizedTex = new Texture2D(targetSize, targetSize, TextureFormat.RGBA32, false);
            var pixels = new Color[targetSize * targetSize];
            for (int y = 0; y < targetSize; y++)
            {
                float v = y / (float)(targetSize - 1);
                for (int x = 0; x < targetSize; x++)
                {
                    float u = x / (float)(targetSize - 1);
                    pixels[y * targetSize + x] = srcTex.GetPixelBilinear(u, v);
                }
            }
            resizedTex.SetPixels(pixels);
            resizedTex.Apply();

            File.WriteAllBytes(destPath, resizedTex.EncodeToPNG());
            Object.DestroyImmediate(srcTex);
            Object.DestroyImmediate(resizedTex);

            AssetDatabase.ImportAsset(destPath, ImportAssetOptions.ForceSynchronousImport);

            var importer = (TextureImporter)AssetImporter.GetAtPath(destPath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.spritePixelsPerUnit = pixelsPerUnit;
            importer.SaveAndReimport();

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(destPath);
            if (sprite == null)
                Debug.LogError($"FlavorIconBuilder: failed to load sprite for {label} at {destPath}");
            return sprite;
        }
    }
}
