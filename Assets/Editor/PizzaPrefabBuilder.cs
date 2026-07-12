using UnityEditor;
using UnityEngine;
using PizzaVR.Core;
using PizzaVR.Gameplay;

public static class PizzaPrefabBuilder
{
    public const string PrefabPath = "Assets/Prefabs/Pizza.prefab";

    [MenuItem("Tools/Pizza VR/Build Pizza Prefab")]
    public static GameObject BuildPrefab()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "Pizza";
        go.transform.localScale = new Vector3(0.22f, 0.04f, 0.22f);

        var mat = new Material(Shader.Find("Standard"));
        mat.color = PizzaFlavorInfo.GetColor(PizzaFlavor.Margherita);
        go.GetComponent<Renderer>().sharedMaterial = mat;

        var rb = go.AddComponent<Rigidbody>();
        rb.isKinematic = true;

        go.AddComponent<PizzaProjectile>();

        if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
            AssetDatabase.CreateFolder("Assets", "Prefabs");

        var prefab = PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
        Object.DestroyImmediate(go);

        Debug.Log("PizzaPrefabBuilder: Pizza prefab created at " + PrefabPath);
        return prefab;
    }

    // Code-generated placeholder prefab - always rebuild to stay in sync with PizzaProjectile.cs.
    public static GameObject EnsurePrefab()
    {
        return BuildPrefab();
    }
}
