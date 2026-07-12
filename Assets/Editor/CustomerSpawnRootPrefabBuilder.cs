using UnityEditor;
using UnityEngine;
using PizzaVR.Customers;

// The customer-spawning system as its own prefab, so whoever is tuning spawn/wander/scoring
// behavior can work in this prefab without touching Restaurant.unity.
public static class CustomerSpawnRootPrefabBuilder
{
    public const string PrefabPath = "Assets/Prefabs/CustomerSpawnRoot.prefab";

    [MenuItem("Tools/Pizza VR/Build Customer Spawn Root Prefab")]
    public static GameObject BuildPrefab()
    {
        var config = GameBalanceConfigBuilder.EnsureConfig();
        var customerPrefab = CustomerPrefabBuilder.EnsurePrefab();
        var pizzaPrefab = PizzaPrefabBuilder.EnsurePrefab();

        var root = new GameObject("CustomerSpawnRoot");
        var spawner = root.AddComponent<CustomerSpawner>();
        spawner.customerPrefab = customerPrefab;
        spawner.pizzaPrefab = pizzaPrefab;
        spawner.config = config;

        if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
            AssetDatabase.CreateFolder("Assets", "Prefabs");

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);

        Debug.Log("CustomerSpawnRootPrefabBuilder: prefab created at " + PrefabPath);
        return prefab;
    }

    public static GameObject EnsurePrefab() => BuildPrefab();
}
