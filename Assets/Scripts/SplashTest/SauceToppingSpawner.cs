using System;
using System.Collections.Generic;
using UnityEngine;

public enum SauceToppingTheme
{
    None = -1,
    Margherita = 0,
    CosmicPinkMarshmallow = 1,
    Pepperoni = 2,
}

public enum SauceToppingPlaceholderKind
{
    Leaf = 0,
    Cheese = 1,
    Chili = 2,
    Mushroom = 3,
    Blueberry = 4,
    Pepperoni = 5,
}

[Serializable]
public class SauceToppingProfile
{
    public SauceToppingTheme theme;
    [Tooltip("美術資產到位後，把配料 Prefab 直接拖到這裡。Prefab 的本地 +Y 軸應朝向醬汁表面外側。")]
    public GameObject[] toppingPrefabs;
    [Range(0, 12)] public int minimumCount = 3;
    [Range(0, 12)] public int maximumCount = 6;
    [Range(0.3f, 2f)] public float minimumScale = 0.65f;
    [Range(0.3f, 2f)] public float maximumScale = 1.15f;
    [Min(0f)] public float surfaceOffset = 0.008f;
    [Range(0.1f, 1f)] public float usableSauceRadius = 0.82f;
    [Tooltip("尚未指派 Prefab 時使用的臨時幾何。Prefab 陣列有任何物件後，便會優先只使用那些 Prefab。")]
    public SauceToppingPlaceholderKind[] placeholderKinds;
}

/// <summary>
/// Creates rigid topping prefabs on a generated sauce surface. Each topping is parented to
/// the strongest bone at its sampled sauce vertex, so it follows the same body region as the sauce.
/// </summary>
public class SauceToppingSpawner : MonoBehaviour
{
    [Header("Pizza Topping Profiles")]
    [SerializeField] SauceToppingProfile[] toppingProfiles =
    {
        new SauceToppingProfile
        {
            theme = SauceToppingTheme.Margherita,
            placeholderKinds = new[] { SauceToppingPlaceholderKind.Leaf, SauceToppingPlaceholderKind.Cheese },
        },
        new SauceToppingProfile
        {
            theme = SauceToppingTheme.CosmicPinkMarshmallow,
            placeholderKinds = new[]
            {
                SauceToppingPlaceholderKind.Chili,
                SauceToppingPlaceholderKind.Mushroom,
                SauceToppingPlaceholderKind.Blueberry,
            },
        },
        new SauceToppingProfile
        {
            theme = SauceToppingTheme.Pepperoni,
            placeholderKinds = new[] { SauceToppingPlaceholderKind.Pepperoni },
        },
    };

    readonly List<int> candidateIndices = new List<int>(96);
    readonly Dictionary<SauceToppingPlaceholderKind, Material> placeholderMaterials =
        new Dictionary<SauceToppingPlaceholderKind, Material>();

    public void SpawnToppings(
        GameObject sauceOwner,
        SauceToppingTheme theme,
        SkinnedMeshRenderer source,
        Vector3[] surfacePoints,
        Vector3[] surfaceNormals,
        BoneWeight[] surfaceWeights,
        float[] radius01,
        int seed)
    {
        if (sauceOwner == null || source == null || surfacePoints == null || surfaceNormals == null
            || surfaceWeights == null || radius01 == null)
            return;

        SauceToppingProfile profile = FindProfile(theme);
        if (profile == null) return;

        int prefabCount = CountValid(profile.toppingPrefabs);
        int placeholderCount = profile.placeholderKinds != null ? profile.placeholderKinds.Length : 0;
        if (prefabCount == 0 && placeholderCount == 0) return;

        candidateIndices.Clear();
        float usableRadius = Mathf.Clamp01(profile.usableSauceRadius);
        int pointCount = Mathf.Min(surfacePoints.Length,
            Mathf.Min(surfaceNormals.Length, Mathf.Min(surfaceWeights.Length, radius01.Length)));
        for (int i = 0; i < pointCount; i++)
        {
            if (radius01[i] <= usableRadius)
                candidateIndices.Add(i);
        }
        if (candidateIndices.Count == 0) return;

        int minimum = Mathf.Min(profile.minimumCount, profile.maximumCount);
        int maximum = Mathf.Max(profile.minimumCount, profile.maximumCount);
        var random = new System.Random(unchecked(seed ^ ((int)theme + 1) * 92821));
        int toppingCount = random.Next(minimum, maximum + 1);
        GeneratedSauceToppingOwner owner = sauceOwner.GetComponent<GeneratedSauceToppingOwner>();
        if (owner == null) owner = sauceOwner.AddComponent<GeneratedSauceToppingOwner>();

        for (int i = 0; i < toppingCount && candidateIndices.Count > 0; i++)
        {
            int candidateListIndex = random.Next(candidateIndices.Count);
            int surfaceIndex = candidateIndices[candidateListIndex];
            candidateIndices.RemoveAt(candidateListIndex);

            GameObject topping = CreateTopping(profile, prefabCount, random);
            if (topping == null) continue;

            DisableToppingPhysics(topping);
            Vector3 normal = surfaceNormals[surfaceIndex].sqrMagnitude > 0.0001f
                ? surfaceNormals[surfaceIndex].normalized
                : Vector3.up;
            float scale = Mathf.Lerp(profile.minimumScale, profile.maximumScale, (float)random.NextDouble());
            Quaternion rotation = Quaternion.AngleAxis((float)random.NextDouble() * 360f, normal)
                                * Quaternion.FromToRotation(Vector3.up, normal);
            topping.transform.SetPositionAndRotation(surfacePoints[surfaceIndex] + normal * profile.surfaceOffset, rotation);

            Transform attachmentBone = GetDominantBone(source, surfaceWeights[surfaceIndex]);
            topping.transform.SetParent(attachmentBone != null ? attachmentBone : source.transform, true);
            topping.transform.localScale *= scale;
            topping.name = $"SauceTopping_{theme}_{i + 1}";
            owner.Register(topping);
        }
    }

    SauceToppingProfile FindProfile(SauceToppingTheme theme)
    {
        if (toppingProfiles == null) return null;
        for (int i = 0; i < toppingProfiles.Length; i++)
        {
            if (toppingProfiles[i] != null && toppingProfiles[i].theme == theme)
                return toppingProfiles[i];
        }
        return null;
    }

    static int CountValid(GameObject[] prefabs)
    {
        if (prefabs == null) return 0;
        int count = 0;
        for (int i = 0; i < prefabs.Length; i++)
        {
            if (prefabs[i] != null) count++;
        }
        return count;
    }

    GameObject CreateTopping(SauceToppingProfile profile, int prefabCount, System.Random random)
    {
        if (prefabCount > 0)
        {
            int start = random.Next(profile.toppingPrefabs.Length);
            for (int offset = 0; offset < profile.toppingPrefabs.Length; offset++)
            {
                GameObject prefab = profile.toppingPrefabs[(start + offset) % profile.toppingPrefabs.Length];
                if (prefab != null) return Instantiate(prefab);
            }
        }

        SauceToppingPlaceholderKind kind = profile.placeholderKinds[
            random.Next(profile.placeholderKinds.Length)];
        return CreatePlaceholder(kind);
    }

    GameObject CreatePlaceholder(SauceToppingPlaceholderKind kind)
    {
        if (kind == SauceToppingPlaceholderKind.Mushroom)
        {
            var root = new GameObject("Placeholder_Mushroom");
            CreatePrimitiveChild(root.transform, PrimitiveType.Cylinder, "Stem", new Vector3(0.024f, 0.030f, 0.024f),
                new Vector3(0f, 0.030f, 0f), SauceToppingPlaceholderKind.Mushroom);
            CreatePrimitiveChild(root.transform, PrimitiveType.Sphere, "Cap", new Vector3(0.075f, 0.030f, 0.075f),
                new Vector3(0f, 0.068f, 0f), SauceToppingPlaceholderKind.Mushroom);
            return root;
        }

        PrimitiveType primitive = kind == SauceToppingPlaceholderKind.Blueberry
            ? PrimitiveType.Sphere
            : kind == SauceToppingPlaceholderKind.Leaf || kind == SauceToppingPlaceholderKind.Chili
                ? PrimitiveType.Capsule
                : PrimitiveType.Cylinder;
        GameObject result = GameObject.CreatePrimitive(primitive);
        RemoveCollider(result);
        result.name = $"Placeholder_{kind}";
        result.GetComponent<Renderer>().sharedMaterial = GetPlaceholderMaterial(kind);

        if (kind == SauceToppingPlaceholderKind.Leaf)
            result.transform.localScale = new Vector3(0.028f, 0.010f, 0.065f);
        else if (kind == SauceToppingPlaceholderKind.Cheese)
            result.transform.localScale = new Vector3(0.045f, 0.013f, 0.045f);
        else if (kind == SauceToppingPlaceholderKind.Chili)
            result.transform.localScale = new Vector3(0.018f, 0.075f, 0.018f);
        else if (kind == SauceToppingPlaceholderKind.Blueberry)
            result.transform.localScale = Vector3.one * 0.030f;
        else
            result.transform.localScale = new Vector3(0.050f, 0.012f, 0.050f);

        return result;
    }

    void CreatePrimitiveChild(
        Transform parent,
        PrimitiveType type,
        string name,
        Vector3 scale,
        Vector3 position,
        SauceToppingPlaceholderKind materialKind)
    {
        GameObject child = GameObject.CreatePrimitive(type);
        child.name = name;
        child.transform.SetParent(parent, false);
        child.transform.localPosition = position;
        child.transform.localScale = scale;
        RemoveCollider(child);
        child.GetComponent<Renderer>().sharedMaterial = GetPlaceholderMaterial(materialKind);
    }

    void DisableToppingPhysics(GameObject topping)
    {
        foreach (Collider collider in topping.GetComponentsInChildren<Collider>(true))
            collider.enabled = false;
        foreach (Rigidbody body in topping.GetComponentsInChildren<Rigidbody>(true))
        {
            body.isKinematic = true;
            body.detectCollisions = false;
        }
    }

    static void RemoveCollider(GameObject target)
    {
        Collider collider = target.GetComponent<Collider>();
        if (collider != null) Destroy(collider);
    }

    Transform GetDominantBone(SkinnedMeshRenderer source, BoneWeight weight)
    {
        int index = weight.boneIndex0;
        float strongest = weight.weight0;
        if (weight.weight1 > strongest) { strongest = weight.weight1; index = weight.boneIndex1; }
        if (weight.weight2 > strongest) { strongest = weight.weight2; index = weight.boneIndex2; }
        if (weight.weight3 > strongest) { index = weight.boneIndex3; }
        return index >= 0 && index < source.bones.Length ? source.bones[index] : source.rootBone;
    }

    Material GetPlaceholderMaterial(SauceToppingPlaceholderKind kind)
    {
        if (placeholderMaterials.TryGetValue(kind, out Material material) && material != null)
            return material;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        material = new Material(shader) { name = $"Runtime Topping Placeholder {kind}" };
        Color color = kind == SauceToppingPlaceholderKind.Leaf ? new Color(0.10f, 0.48f, 0.14f)
            : kind == SauceToppingPlaceholderKind.Cheese ? new Color(1f, 0.82f, 0.22f)
            : kind == SauceToppingPlaceholderKind.Chili ? new Color(0.82f, 0.08f, 0.05f)
            : kind == SauceToppingPlaceholderKind.Mushroom ? new Color(0.92f, 0.76f, 0.58f)
            : kind == SauceToppingPlaceholderKind.Blueberry ? new Color(0.16f, 0.24f, 0.80f)
            : new Color(0.68f, 0.12f, 0.07f);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.35f);
        placeholderMaterials[kind] = material;
        return material;
    }

    void OnDestroy()
    {
        foreach (Material material in placeholderMaterials.Values)
        {
            if (material != null) Destroy(material);
        }
        placeholderMaterials.Clear();
    }
}

public class GeneratedSauceToppingOwner : MonoBehaviour
{
    readonly List<GameObject> instances = new List<GameObject>();

    public void Register(GameObject instance)
    {
        if (instance != null) instances.Add(instance);
    }

    void OnDestroy()
    {
        foreach (GameObject instance in instances)
        {
            if (instance != null) Destroy(instance);
        }
        instances.Clear();
    }
}
