using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using Pizzala.Data;
using Pizzala.Throwing;

namespace Pizzala.EditorTools
{
    // Builds PZ_Pizza_Base (Docs/PREFABS.md section 1) from the art team's pizza1.fbx,
    // then three flavor Prefab Variants on top of it. The model's exported scale doesn't
    // match a real-world pizza, so BuildBase normalizes it to TargetDiameter first.
    public static class PizzaPrefabBuilder
    {
        const string ModelPath = "Assets/Art/Pizza/pizza1/pizza1.fbx";
        const string PrefabFolder = "Assets/Prefabs";
        const string BasePrefabPath = PrefabFolder + "/PZ_Pizza_Base.prefab";
        const float TargetDiameter = 0.22f; // ~personal-size pizza, small enough to grab comfortably
        const float ColliderHeight = 0.03f;
        // pizza1.fbx's round face comes in lying in the model's local X/Y plane (thin along Z)
        // instead of flat on the ground (X/Z plane, thin along Y) - rotate it onto its back.
        static readonly Quaternion ModelCorrectiveRotation = Quaternion.Euler(90f, 0f, 0f);

        [MenuItem("Tools/Pizzala/Build Pizza Prefabs")]
        public static void BuildAll()
        {
            var basePrefab = BuildBase();
            if (basePrefab == null)
                return;

            BuildVariant(basePrefab, "PZ_Pizza_Margherita", PizzaFlavor.Margherita, new Color(0.95f, 0.85f, 0.55f));
            BuildVariant(basePrefab, "PZ_Pizza_Pepperoni", PizzaFlavor.Pepperoni, new Color(0.85f, 0.25f, 0.2f));
            BuildVariant(basePrefab, "PZ_Pizza_Hawaiian", PizzaFlavor.Hawaiian, new Color(0.95f, 0.95f, 0.6f));

            Debug.Log("PizzaPrefabBuilder: PZ_Pizza_Base + 3 flavor variants built.");
        }

        static GameObject BuildBase()
        {
            var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (modelAsset == null)
            {
                Debug.LogError($"PizzaPrefabBuilder: model not found at {ModelPath}");
                return null;
            }

            var root = new GameObject("PZ_Pizza_Base");
            var model = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset, root.transform);
            model.name = "Model";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = ModelCorrectiveRotation;
            model.transform.localScale = Vector3.one;

            // Normalize scale: measure the model at scale 1, then scale it so its widest
            // horizontal dimension matches TargetDiameter - the fbx's exported scale is
            // whatever the source Blender file used and isn't a reliable "1 unit = 1 meter".
            var rawBounds = GetRendererBounds(model);
            float rawDiameter = Mathf.Max(rawBounds.size.x, rawBounds.size.z);
            float scale = rawDiameter > 0.0001f ? TargetDiameter / rawDiameter : 1f;
            model.transform.localScale = Vector3.one * scale;

            var rb = root.AddComponent<Rigidbody>();
            rb.mass = 0.3f;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            var scaledBounds = GetRendererBounds(model);
            var box = root.AddComponent<BoxCollider>();
            box.center = root.transform.InverseTransformPoint(scaledBounds.center);
            box.size = new Vector3(scaledBounds.size.x, ColliderHeight, scaledBounds.size.z);

            var grab = root.AddComponent<XRGrabInteractable>();
            grab.movementType = XRBaseInteractable.MovementType.VelocityTracking;
            grab.throwOnDetach = true;
            grab.throwVelocityScale = 1.5f;
            grab.smoothPosition = true;

            root.AddComponent<PizzaProjectile>();

            if (!AssetDatabase.IsValidFolder(PrefabFolder))
                AssetDatabase.CreateFolder("Assets", "Prefabs");

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, BasePrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        static Bounds GetRendererBounds(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
                return new Bounds(go.transform.position, Vector3.one * TargetDiameter);

            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        static void BuildVariant(GameObject basePrefab, string variantName, PizzaFlavor flavor, Color tint)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab);

            var projectile = instance.GetComponent<PizzaProjectile>();
            projectile.flavor = flavor;

            var renderer = instance.GetComponentInChildren<Renderer>();
            if (renderer != null)
            {
                var mat = new Material(renderer.sharedMaterial);
                mat.color = tint;
                renderer.sharedMaterial = mat;
            }

            var path = $"{PrefabFolder}/{variantName}.prefab";
            PrefabUtility.SaveAsPrefabAsset(instance, path);
            Object.DestroyImmediate(instance);
        }
    }
}
