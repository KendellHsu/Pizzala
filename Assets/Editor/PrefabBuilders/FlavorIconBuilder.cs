using System.IO;
using UnityEditor;
using UnityEngine;
using Pizzala.Customers;

namespace Pizzala.EditorTools
{
    // Placeholder flavor icons for PZ_Customer's FlavorIcon (PREFABS.md section 4) - the art
    // team hasn't delivered dedicated 256x256 icon sprites yet, so reuse each flavor's own
    // pizza diffuse texture as a stand-in. Re-run after real icons arrive to just re-point
    // SourcePaths at the new files, or delete Assets/Art/Icons and unassign manually.
    public static class FlavorIconBuilder
    {
        const string IconFolder = "Assets/Art/Icons";

        static readonly (string flavorName, string sourceDiffuse, string iconName)[] Icons =
        {
            ("Margherita", "Assets/Art/Pizza/pizza1/meterial/texture_diffuse.png", "FlavorIcon_Margherita.png"),
            ("Pepperoni", "Assets/Art/Pizza/pizza3/meterial/texture_diffuse.png", "FlavorIcon_Pepperoni.png"),
            ("CosmicPinkMarshmallow", "Assets/Art/Pizza/pizza2/meterial/texture_diffuse.png", "FlavorIcon_CosmicPinkMarshmallow.png"),
        };

        [MenuItem("Tools/Pizzala/Build Flavor Icons From Pizza Textures")]
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
                    Debug.LogError($"FlavorIconBuilder: source texture not found for {flavorName} at {sourcePath}");
                    continue;
                }

                // Source is a full-res pizza diffuse map (multi-MB) - way more than a 256x256
                // icon needs (PREFABS.md spec), so actually downscale the file instead of just
                // relying on import settings (which wouldn't shrink what gets committed to git).
                // CPU-side resize via GetPixelBilinear/SetPixels - RenderTexture/Graphics.Blit
                // silently no-op under -nographics batchmode (no GPU device), which produced
                // identical blank icons for all three flavors the first time this ran.
                const int iconSize = 256;
                var srcBytes = File.ReadAllBytes(sourcePath);
                var srcTex = new Texture2D(2, 2);
                srcTex.LoadImage(srcBytes);

                var resizedTex = new Texture2D(iconSize, iconSize, TextureFormat.RGBA32, false);
                var pixels = new Color[iconSize * iconSize];
                for (int y = 0; y < iconSize; y++)
                {
                    float v = y / (float)(iconSize - 1);
                    for (int x = 0; x < iconSize; x++)
                    {
                        float u = x / (float)(iconSize - 1);
                        pixels[y * iconSize + x] = srcTex.GetPixelBilinear(u, v);
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
                importer.SaveAndReimport();

                sprites[i] = AssetDatabase.LoadAssetAtPath<Sprite>(destPath);
                if (sprites[i] == null)
                    Debug.LogError($"FlavorIconBuilder: failed to load sprite for {flavorName} at {destPath}");
            }

            var customerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PZ_Customer.prefab");
            if (customerPrefab == null)
            {
                Debug.LogError("FlavorIconBuilder: Assets/Prefabs/PZ_Customer.prefab not found.");
                return;
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
            PrefabUtility.SaveAsPrefabAsset(root, "Assets/Prefabs/PZ_Customer.prefab");
            PrefabUtility.UnloadPrefabContents(root);

            Debug.Log("FlavorIconBuilder: PZ_Customer.flavorSprites wired with 3 placeholder icons (reused pizza diffuse textures).");
        }
    }
}
