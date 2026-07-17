using UnityEditor;
using UnityEngine;
using Pizzala.Core;

namespace Pizzala.EditorTools
{
    // Shared building blocks for the _Test_NN_Name.unity scenes - each PREFABS.md item gets its
    // own persistent scene (see PizzaTestSceneBuilder's header comment), but they all need the
    // same handful of supporting pieces (head hitbox, dirt manager, prefab instantiation).
    public static class TestSceneHelpers
    {
        public const string XROriginPath = "Assets/Samples/XR Interaction Toolkit/3.3.2/Starter Assets/Prefabs/XR Origin (XR Rig).prefab";
        public const string SimulatorPath = "Assets/Samples/XR Interaction Toolkit/3.3.2/XR Device Simulator/XR Device Simulator.prefab";

        public static GameObject InstantiatePrefab(string path, Vector3 position)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null)
            {
                Debug.LogError($"TestSceneHelpers: prefab not found at {path}");
                return null;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            instance.transform.position = position;
            return instance;
        }

        // Per SETUP.md 2-2 / BUILD_STEPS.md §2: ThrowbackProjectile only recognizes a hit on the
        // player by finding PlayerHeadHitbox on the collider it touched.
        public static Transform BuildHeadHitbox(GameObject xrOrigin)
        {
            if (xrOrigin == null)
                return null;

            var camera = xrOrigin.GetComponentInChildren<Camera>();
            if (camera == null)
            {
                Debug.LogError("TestSceneHelpers: no Camera found under XR Origin, can't build HeadHitbox.");
                return null;
            }

            var headHitbox = new GameObject("HeadHitbox");
            headHitbox.transform.SetParent(camera.transform, false);
            headHitbox.AddComponent<PlayerHeadHitbox>();

            var sphere = headHitbox.AddComponent<SphereCollider>();
            sphere.isTrigger = true;
            sphere.radius = 0.22f;

            var rb = headHitbox.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            return camera.transform;
        }

        // Wires all PZ_SauceSplat_* prefabs into a DirtManager under a "Systems" object - without
        // this, ThrowbackProjectile/PizzaProjectile's splat-spawn calls just no-op since
        // DirtManager.Instance is null.
        public static void BuildDirtManager()
        {
            var systems = new GameObject("Systems");
            var dirtManager = systems.AddComponent<Pizzala.Dirt.DirtManager>();

            var splatGuids = AssetDatabase.FindAssets("PZ_SauceSplat_ t:Prefab", new[] { "Assets/Prefabs" });
            var splats = new GameObject[splatGuids.Length];
            for (int i = 0; i < splatGuids.Length; i++)
                splats[i] = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(splatGuids[i]));
            dirtManager.splatPrefabs = splats;

            Debug.Log($"TestSceneHelpers: DirtManager wired with {splats.Length} sauce splat prefabs.");
        }

        public static void EnsureScenesFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
                AssetDatabase.CreateFolder("Assets", "Scenes");
        }
    }
}
