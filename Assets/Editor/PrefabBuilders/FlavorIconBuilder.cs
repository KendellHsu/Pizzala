using System.IO;
using UnityEditor;
using UnityEngine;
using Pizzala.Customers;

namespace Pizzala.EditorTools
{
    // Flavor icons for PZ_Customer's FlavorIcon (PREFABS.md section 4). The art team delivered
    // real top-down 2D pizza icons in Assets/Art/Pizza/2D/ - resize them to the spec's 256x256
    // and wire into PZ_Customer.flavorSprites. Re-run after the art changes to re-pull from the
    // same source files.
    public static class FlavorIconBuilder
    {
        const string IconFolder = "Assets/Art/Icons";
        const int IconSize = 256;

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

                if (!File.Exists(sourcePath))
                {
                    Debug.LogError($"FlavorIconBuilder: source art not found for {flavorName} at {sourcePath}");
                    continue;
                }

                // Source art is 1067x1067 (700KB+) - more than a small hovering icon needs, so
                // resize down to the PREFABS.md spec's 256x256 before committing it. CPU-side
                // GetPixelBilinear/SetPixels rather than RenderTexture/Graphics.Blit, which
                // silently no-ops under -nographics batchmode (see FlavorIconBuilder history).
                var srcBytes = File.ReadAllBytes(sourcePath);
                var srcTex = new Texture2D(2, 2);
                srcTex.LoadImage(srcBytes);

                var resizedTex = new Texture2D(IconSize, IconSize, TextureFormat.RGBA32, false);
                var pixels = new Color[IconSize * IconSize];
                for (int y = 0; y < IconSize; y++)
                {
                    float v = y / (float)(IconSize - 1);
                    for (int x = 0; x < IconSize; x++)
                    {
                        float u = x / (float)(IconSize - 1);
                        pixels[y * IconSize + x] = srcTex.GetPixelBilinear(u, v);
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
                // 256px at 700 PPU renders as a ~0.37m icon - a plausible "hovering above the
                // customer's head" size (default 100 PPU would make it a 2.56m sphere).
                importer.spritePixelsPerUnit = 700;
                importer.SaveAndReimport();

                sprites[i] = AssetDatabase.LoadAssetAtPath<Sprite>(destPath);
                if (sprites[i] == null)
                    Debug.LogError($"FlavorIconBuilder: failed to load sprite for {flavorName} at {destPath}");
            }

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
                controller.flavorIcon.sprite = null;

            PrefabUtility.SaveAsPrefabAsset(root, "Assets/Prefabs/PZ_Customer.prefab");
            PrefabUtility.UnloadPrefabContents(root);

            Debug.Log("FlavorIconBuilder: PZ_Customer.flavorSprites wired with the real 2D pizza icons.");
        }
    }
}
