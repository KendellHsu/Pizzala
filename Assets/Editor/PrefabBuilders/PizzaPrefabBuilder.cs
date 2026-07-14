using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using Pizzala.Data;
using Pizzala.Throwing;

namespace Pizzala.EditorTools
{
    // Builds PZ_Pizza_Base (Docs/PREFABS.md section 1) as a flattened cube placeholder,
    // then three flavor Prefab Variants on top of it. Swap BuildBase's "Model" child for the
    // art team's Assets/Art/Pizza/pizza1.fbx once its scale/pivot is fixed to work with grabbing.
    public static class PizzaPrefabBuilder
    {
        const string PrefabFolder = "Assets/Prefabs";
        const string BasePrefabPath = PrefabFolder + "/PZ_Pizza_Base.prefab";
        static readonly Vector3 PizzaSize = new Vector3(0.18f, 0.03f, 0.18f);

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
            var root = new GameObject("PZ_Pizza_Base");

            var model = GameObject.CreatePrimitive(PrimitiveType.Cube);
            model.name = "Model";
            model.transform.SetParent(root.transform);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = PizzaSize;
            Object.DestroyImmediate(model.GetComponent<BoxCollider>()); // root has its own collider below

            var rb = root.AddComponent<Rigidbody>();
            rb.mass = 0.3f;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            var box = root.AddComponent<BoxCollider>();
            box.size = PizzaSize;

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
    }
}
