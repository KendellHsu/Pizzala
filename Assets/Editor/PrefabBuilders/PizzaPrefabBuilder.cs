using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using Pizzala.Data;
using Pizzala.Throwing;

namespace Pizzala.EditorTools
{
    // Builds PZ_Pizza_Base (Docs/PREFABS.md section 1) from the art team's pizza1.fbx,
    // then three flavor Prefab Variants on top of it. Re-run after the art team delivers
    // separate flavor textures to replace the placeholder tint-per-variant approach.
    public static class PizzaPrefabBuilder
    {
        const string ModelPath = "Assets/Art/Pizza/pizza1/pizza1.fbx";
        const string PrefabFolder = "Assets/Prefabs";
        const string BasePrefabPath = PrefabFolder + "/PZ_Pizza_Base.prefab";

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
            model.transform.localRotation = Quaternion.identity;

            var rb = root.AddComponent<Rigidbody>();
            rb.mass = 0.3f;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            var bounds = ComputeLocalBounds(model, root.transform);
            var box = root.AddComponent<BoxCollider>();
            box.center = bounds.center;
            // Squash to a flat disc regardless of the raw model bounds - PREFABS.md just wants
            // "壓扁的 Box", not a collider that matches the pizza's actual (thin) mesh height.
            box.size = new Vector3(bounds.size.x, Mathf.Min(bounds.size.y, 0.04f), bounds.size.z);

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

        static Bounds ComputeLocalBounds(GameObject model, Transform relativeTo)
        {
            var renderers = model.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
                return new Bounds(Vector3.zero, new Vector3(0.3f, 0.04f, 0.3f));

            var worldBounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                worldBounds.Encapsulate(renderers[i].bounds);

            var center = relativeTo.InverseTransformPoint(worldBounds.center);
            return new Bounds(center, worldBounds.size);
        }
    }
}
