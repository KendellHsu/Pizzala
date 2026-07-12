using UnityEditor;
using UnityEngine;
using PizzaVR.Core;

public static class GameBalanceConfigBuilder
{
    public const string AssetPath = "Assets/Config/GameBalanceConfig.asset";

    // Unlike the code-generated prefabs, this asset is NOT rebuilt if it already exists -
    // it's the team's persistent tuning data and must survive scene/prefab regeneration.
    public static GameBalanceConfig EnsureConfig()
    {
        var existing = AssetDatabase.LoadAssetAtPath<GameBalanceConfig>(AssetPath);
        if (existing != null)
            return existing;

        var config = ScriptableObject.CreateInstance<GameBalanceConfig>();
        if (!AssetDatabase.IsValidFolder("Assets/Config"))
            AssetDatabase.CreateFolder("Assets", "Config");
        AssetDatabase.CreateAsset(config, AssetPath);
        AssetDatabase.SaveAssets();
        Debug.Log("GameBalanceConfigBuilder: created " + AssetPath);
        return config;
    }
}
