using System.IO;
using UnityEditor;
using UnityEngine;
using Pizzala.Customers;

namespace Pizzala.EditorTools
{
    // Placeholder flavor icons for PZ_Customer's FlavorIcon (PREFABS.md section 4) - the art
    // team hasn't delivered dedicated 256x256 icon sprites yet. Reusing each pizza's own UV
    // diffuse texture looked like a jumbled square (it's a 3D-model wrap, not a top-down photo),
    // so instead this draws a plain colored disc per flavor - round, transparent background,
    // matches the spec's silhouette even without real art. Re-run after real icons arrive to
    // just re-point Icons at the new files, or delete Assets/Art/Icons and unassign manually.
    public static class FlavorIconBuilder
    {
        const string IconFolder = "Assets/Art/Icons";
        const int IconSize = 256;

        static readonly (string flavorName, Color color, string iconName)[] Icons =
        {
            ("Margherita", new Color(0.95f, 0.85f, 0.55f), "FlavorIcon_Margherita.png"),
            ("Pepperoni", new Color(0.85f, 0.25f, 0.2f), "FlavorIcon_Pepperoni.png"),
            ("CosmicPinkMarshmallow", new Color(0.95f, 0.45f, 0.75f), "FlavorIcon_CosmicPinkMarshmallow.png"),
        };

        [MenuItem("Tools/Pizzala/Build Flavor Icons (Placeholder Discs)")]
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
                var (flavorName, color, iconName) = Icons[i];
                var destPath = $"{IconFolder}/{iconName}";

                File.WriteAllBytes(destPath, DrawDisc(color).EncodeToPNG());
                AssetDatabase.ImportAsset(destPath, ImportAssetOptions.ForceSynchronousImport);

                var importer = (TextureImporter)AssetImporter.GetAtPath(destPath);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
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

            Debug.Log("FlavorIconBuilder: PZ_Customer.flavorSprites wired with 3 round placeholder disc icons.");
        }

        // Plain filled circle, color inside / transparent outside, with a couple pixels of
        // antialiasing on the edge. Pure CPU pixel work - no RenderTexture/Blit/Camera needed,
        // so this runs fine under -nographics batchmode.
        static Texture2D DrawDisc(Color color)
        {
            var tex = new Texture2D(IconSize, IconSize, TextureFormat.RGBA32, false);
            var pixels = new Color[IconSize * IconSize];
            float center = (IconSize - 1) / 2f;
            float radius = IconSize * 0.47f;
            const float featherWidth = 2f;

            for (int y = 0; y < IconSize; y++)
            {
                for (int x = 0; x < IconSize; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                    float alpha = Mathf.Clamp01((radius - dist) / featherWidth + 0.5f);
                    var c = color;
                    c.a = alpha;
                    pixels[y * IconSize + x] = c;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }
    }
}
