using UnityEditor;
using UnityEngine;

// Static dressing (floor, counter - and later whatever comes in from Blender). Its own prefab
// so environment art can be swapped in without editing Restaurant.unity directly.
public static class RestaurantEnvironmentPrefabBuilder
{
    public const string PrefabPath = "Assets/Prefabs/RestaurantEnvironment.prefab";

    [MenuItem("Tools/Pizza VR/Build Restaurant Environment Prefab")]
    public static GameObject BuildPrefab()
    {
        var root = new GameObject("RestaurantEnvironment");

        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "Floor";
        floor.transform.SetParent(root.transform);
        floor.transform.localPosition = new Vector3(0, -0.05f, 0);
        floor.transform.localScale = new Vector3(20f, 0.1f, 20f);
        SetColor(floor, new Color(0.75f, 0.72f, 0.65f));

        var counter = GameObject.CreatePrimitive(PrimitiveType.Cube);
        counter.name = "KitchenCounter";
        counter.transform.SetParent(root.transform);
        counter.transform.localPosition = new Vector3(0, 0.4f, 0);
        counter.transform.localScale = new Vector3(1.2f, 0.8f, 1.2f);
        SetColor(counter, new Color(0.6f, 0.3f, 0.1f));

        if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
            AssetDatabase.CreateFolder("Assets", "Prefabs");

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);

        Debug.Log("RestaurantEnvironmentPrefabBuilder: prefab created at " + PrefabPath);
        return prefab;
    }

    public static GameObject EnsurePrefab() => BuildPrefab();

    static void SetColor(GameObject go, Color color)
    {
        var renderer = go.GetComponent<Renderer>();
        var mat = new Material(Shader.Find("Standard"));
        mat.color = color;
        renderer.sharedMaterial = mat;
    }
}
