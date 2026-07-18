using System.Collections.Generic;
using UnityEngine;

public class RuntimeSauceMeshGenerator : MonoBehaviour
{
    [Header("Shape")]
    [SerializeField, Min(0.01f)] float radius = 0.3f;
    [Tooltip("Additional height at the center of the sauce patch.")]
    [SerializeField, Min(0.001f)] float thickness = 0.035f;
    [Tooltip("Minimum visible thickness around the outer edge.")]
    [SerializeField, Min(0.001f)] float edgeThickness = 0.008f;
    [SerializeField, Range(2, 16)] int rings = 6;
    [SerializeField, Range(8, 64)] int segments = 24;

    [Header("Organic Variation")]
    [Tooltip("Smooth variation of the outside silhouette.")]
    [SerializeField, Range(0f, 0.6f)] float edgeIrregularity = 0.22f;
    [Tooltip("Amplifies the difference between inward valleys and outward lobes without adding sharp noise.")]
    [SerializeField, Range(0.5f, 3f)] float edgeRadiusContrast = 1.6f;
    [SerializeField, Range(0, 8)] int edgeSmoothingIterations = 3;
    [Tooltip("Number of longer splash fingers around the edge.")]
    [SerializeField, Range(0, 12)] int splashFingerCount = 6;
    [SerializeField, Range(0f, 1.5f)] float splashFingerLength = 0.55f;
    [SerializeField, Range(0.04f, 0.5f)] float splashFingerWidth = 0.16f;
    [Tooltip("Maximum smooth bumps and dents on the top surface.")]
    [SerializeField, Min(0f)] float surfaceVariation = 0.012f;
    [SerializeField, Range(0.2f, 10f)] float surfaceNoiseScale = 3.5f;
    [Tooltip("How much of the old single center dome remains.")]
    [SerializeField, Range(0f, 1f)] float centerDomeStrength = 0.22f;
    [SerializeField, Range(0, 12)] int bumpCount = 6;
    [SerializeField, Min(0f)] float bumpHeight = 0.022f;
    [SerializeField, Range(0.05f, 0.6f)] float bumpRadius = 0.22f;
    [Tooltip("Use zero for a different shape on every impact.")]
    [SerializeField] int randomSeed;

    [Header("Projection")]
    [SerializeField, Min(0.01f)] float probeDistance = 0.35f;
    [SerializeField, Min(0f)] float surfaceOffset = 0.006f;

    [Header("Appearance")]
    [SerializeField] Material[] sauceMaterials;
    [SerializeField] Color fallbackColor = new Color(0.65f, 0.08f, 0.025f, 1f);

    public GameObject Generate(
        Collider target,
        Vector3 impactPoint,
        Vector3 impactNormal,
        Vector3 impactVelocity,
        float impulse,
        Material overrideMaterial = null)
    {
        if (target == null) return null;

        Vector3 normal = impactNormal.normalized;
        Vector3 tangentVelocity = Vector3.ProjectOnPlane(impactVelocity, normal);
        Vector3 tangent = tangentVelocity.sqrMagnitude > 0.0001f
            ? tangentVelocity.normalized
            : Vector3.Cross(normal, Vector3.up);

        if (tangent.sqrMagnitude < 0.0001f)
            tangent = Vector3.Cross(normal, Vector3.right);
        tangent.Normalize();
        Vector3 bitangent = Vector3.Cross(normal, tangent).normalized;

        int seed = randomSeed == 0
            ? unchecked(System.Environment.TickCount ^ Time.frameCount * 486187739)
            : randomSeed;
        var random = new System.Random(seed);
        float noiseOffsetX = (float)random.NextDouble() * 1000f;
        float noiseOffsetY = (float)random.NextDouble() * 1000f;

        // A sum of circular waves keeps segment 0 and the last segment continuous.
        float[] harmonicPhase = new float[4];
        float[] harmonicAmplitude = new float[4];
        for (int i = 0; i < harmonicPhase.Length; i++)
        {
            harmonicPhase[i] = (float)random.NextDouble() * Mathf.PI * 2f;
            harmonicAmplitude[i] = edgeIrregularity
                                   * Mathf.Lerp(0.18f, 0.42f, (float)random.NextDouble())
                                   / (i + 1f);
        }

        float[] fingerAngles = new float[splashFingerCount];
        float[] fingerLengths = new float[splashFingerCount];
        float[] fingerWidths = new float[splashFingerCount];
        for (int i = 0; i < splashFingerCount; i++)
        {
            fingerAngles[i] = (float)random.NextDouble() * Mathf.PI * 2f;
            fingerLengths[i] = splashFingerLength * Mathf.Lerp(0.45f, 1f, (float)random.NextDouble());
            fingerWidths[i] = splashFingerWidth * Mathf.Lerp(0.7f, 1.3f, (float)random.NextDouble());
        }

        float[] edgeFactors = new float[segments];
        for (int segment = 0; segment < segments; segment++)
        {
            float angle = segment / (float)segments * Mathf.PI * 2f;
            edgeFactors[segment] = EvaluateEdgeFactor(
                angle, harmonicPhase, harmonicAmplitude,
                fingerAngles, fingerLengths, fingerWidths);
            edgeFactors[segment] = Mathf.Max(
                0.35f,
                1f + (edgeFactors[segment] - 1f) * edgeRadiusContrast);
        }
        SmoothCircular(edgeFactors, edgeSmoothingIterations);

        var bumpCenters = new Vector2[bumpCount];
        var bumpHeights = new float[bumpCount];
        var bumpRadii = new float[bumpCount];
        for (int i = 0; i < bumpCount; i++)
        {
            float a = (float)random.NextDouble() * Mathf.PI * 2f;
            float d = Mathf.Sqrt((float)random.NextDouble()) * 0.68f;
            bumpCenters[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * d;
            // Most features rise; a smaller portion form shallow dents.
            float sign = random.NextDouble() < 0.25 ? -0.45f : 1f;
            bumpHeights[i] = bumpHeight * sign * Mathf.Lerp(0.55f, 1f, (float)random.NextDouble());
            bumpRadii[i] = bumpRadius * Mathf.Lerp(0.75f, 1.35f, (float)random.NextDouble());
        }

        var worldVertices = new List<Vector3>(1 + rings * segments + segments);
        var surfaceNormals = new List<Vector3>(1 + rings * segments);
        var triangles = new List<int>();
        var uvs = new List<Vector2>(1 + rings * segments + segments);

        float centerHeight = EvaluateSurfaceHeight(
            Vector2.zero, 0f, edgeThickness, thickness, centerDomeStrength,
            bumpCenters, bumpHeights, bumpRadii, 0f);
        AddProjectedVertex(target, impactPoint, impactPoint, normal,
                           centerHeight, worldVertices, surfaceNormals);
        uvs.Add(new Vector2(0.5f, 0.5f));

        for (int ring = 1; ring <= rings; ring++)
        {
            float ring01 = ring / (float)rings;
            float ringRadius = radius * ring01;
            float height = edgeThickness
                           + thickness * Mathf.Pow(1f - ring01, 2f);

            for (int segment = 0; segment < segments; segment++)
            {
                float angle = segment / (float)segments * Mathf.PI * 2f;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);
                float edgeFactor = edgeFactors[segment];
                // Keep the center regular and introduce the silhouette mainly near the edge.
                float shapedRadius = ringRadius
                    * Mathf.Lerp(1f, edgeFactor, Mathf.Pow(ring01, 1.6f));

                float smoothNoise = Mathf.PerlinNoise(
                    noiseOffsetX + cos * ring01 * surfaceNoiseScale,
                    noiseOffsetY + sin * ring01 * surfaceNoiseScale) * 2f - 1f;
                float variationMask = Mathf.SmoothStep(0.2f, 1f, 1f - ring01);
                height = EvaluateSurfaceHeight(
                    new Vector2(cos, sin) * ring01,
                    ring01, edgeThickness, thickness, centerDomeStrength,
                    bumpCenters, bumpHeights, bumpRadii,
                    smoothNoise * surfaceVariation * variationMask);

                Vector3 sample = impactPoint
                                 + tangent * (cos * shapedRadius)
                                 + bitangent * (sin * shapedRadius);

                AddProjectedVertex(target, sample, impactPoint, normal, height,
                                   worldVertices, surfaceNormals);
                uvs.Add(new Vector2(0.5f + cos * ring01 * 0.5f,
                                    0.5f + sin * ring01 * 0.5f));
            }
        }

        // Center fan.
        for (int segment = 0; segment < segments; segment++)
        {
            int next = (segment + 1) % segments;
            triangles.Add(0);
            triangles.Add(1 + segment);
            triangles.Add(1 + next);
        }

        // Connect each pair of rings.
        for (int ring = 1; ring < rings; ring++)
        {
            int innerStart = 1 + (ring - 1) * segments;
            int outerStart = 1 + ring * segments;
            for (int segment = 0; segment < segments; segment++)
            {
                int next = (segment + 1) % segments;
                int a = innerStart + segment;
                int b = innerStart + next;
                int c = outerStart + segment;
                int d = outerStart + next;
                triangles.Add(a); triangles.Add(c); triangles.Add(b);
                triangles.Add(b); triangles.Add(c); triangles.Add(d);
            }
        }

        // Duplicate the outer ring close to the target and connect it as a side wall.
        int outerTopStart = 1 + (rings - 1) * segments;
        int outerBottomStart = worldVertices.Count;
        for (int segment = 0; segment < segments; segment++)
        {
            Vector3 top = worldVertices[outerTopStart + segment];
            Vector3 localSurfaceNormal = surfaceNormals[outerTopStart + segment];
            worldVertices.Add(top - localSurfaceNormal * edgeThickness);
            uvs.Add(new Vector2(segment / (float)segments, 0f));
        }

        for (int segment = 0; segment < segments; segment++)
        {
            int next = (segment + 1) % segments;
            int topA = outerTopStart + segment;
            int topB = outerTopStart + next;
            int bottomA = outerBottomStart + segment;
            int bottomB = outerBottomStart + next;
            triangles.Add(topA); triangles.Add(bottomA); triangles.Add(topB);
            triangles.Add(topB); triangles.Add(bottomA); triangles.Add(bottomB);
        }

        var sauceObject = new GameObject($"SauceMesh_{Time.frameCount}");
        sauceObject.transform.SetParent(target.transform, false);

        var localVertices = new List<Vector3>(worldVertices.Count);
        foreach (Vector3 vertex in worldVertices)
            localVertices.Add(target.transform.InverseTransformPoint(vertex));

        var mesh = new Mesh { name = "Runtime Sauce Mesh" };
        mesh.SetVertices(localVertices);
        mesh.SetTriangles(triangles, 0);
        mesh.SetUVs(0, uvs);
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();

        sauceObject.AddComponent<MeshFilter>().sharedMesh = mesh;
        sauceObject.AddComponent<MeshRenderer>().sharedMaterial =
            overrideMaterial != null ? overrideMaterial : GetMaterial();
        return sauceObject;
    }

    static float EvaluateEdgeFactor(
        float angle,
        float[] phases,
        float[] amplitudes,
        float[] fingerAngles,
        float[] fingerLengths,
        float[] fingerWidths)
    {
        float factor = 1f;
        for (int i = 0; i < phases.Length; i++)
            factor += Mathf.Sin(angle * (i + 2) + phases[i]) * amplitudes[i];

        for (int i = 0; i < fingerAngles.Length; i++)
        {
            float delta = Mathf.Abs(Mathf.DeltaAngle(
                angle * Mathf.Rad2Deg,
                fingerAngles[i] * Mathf.Rad2Deg)) * Mathf.Deg2Rad;
            float width = Mathf.Max(0.01f, fingerWidths[i]);
            factor += fingerLengths[i] * Mathf.Exp(-(delta * delta) / (2f * width * width));
        }

        return Mathf.Max(0.45f, factor);
    }

    static void SmoothCircular(float[] values, int iterations)
    {
        if (values == null || values.Length < 3) return;
        var buffer = new float[values.Length];
        for (int pass = 0; pass < iterations; pass++)
        {
            for (int i = 0; i < values.Length; i++)
            {
                float previous = values[(i - 1 + values.Length) % values.Length];
                float next = values[(i + 1) % values.Length];
                buffer[i] = previous * 0.25f + values[i] * 0.5f + next * 0.25f;
            }
            System.Array.Copy(buffer, values, values.Length);
        }
    }

    static float EvaluateSurfaceHeight(
        Vector2 position,
        float radius01,
        float edgeHeight,
        float domeHeight,
        float domeStrength,
        Vector2[] bumpCenters,
        float[] bumpHeights,
        float[] bumpRadii,
        float noise)
    {
        float height = edgeHeight
                       + domeHeight * domeStrength * Mathf.Pow(1f - radius01, 2f)
                       + noise;

        for (int i = 0; i < bumpCenters.Length; i++)
        {
            float radius = Mathf.Max(0.01f, bumpRadii[i]);
            float distanceSq = (position - bumpCenters[i]).sqrMagnitude;
            height += bumpHeights[i] * Mathf.Exp(-distanceSq / (2f * radius * radius));
        }

        return Mathf.Max(edgeHeight * 0.7f, height);
    }

    void AddProjectedVertex(
        Collider target,
        Vector3 sample,
        Vector3 impactPoint,
        Vector3 projectionNormal,
        float height,
        List<Vector3> vertices,
        List<Vector3> normals)
    {
        Vector3 projectedSample = sample;
        RaycastHit hit = default;
        bool found = false;
        // If a long finger misses the curved target, shrink it toward the impact
        // point instead of leaving a flat vertex that creates a sharp folded triangle.
        for (int attempt = 0; attempt < 5; attempt++)
        {
            Vector3 origin = projectedSample + projectionNormal * probeDistance;
            var ray = new Ray(origin, -projectionNormal);
            if (target.Raycast(ray, out hit, probeDistance * 2f)
                && Vector3.Dot(hit.normal, projectionNormal) > 0.2f)
            {
                found = true;
                break;
            }
            projectedSample = Vector3.Lerp(projectedSample, impactPoint, 0.25f);
        }

        if (found)
        {
            vertices.Add(hit.point + hit.normal * (surfaceOffset + height));
            normals.Add(hit.normal.normalized);
        }
        else
        {
            vertices.Add(impactPoint + projectionNormal * (surfaceOffset + height));
            normals.Add(projectionNormal.normalized);
            Debug.LogWarning($"[SplashTest] Sauce projection collapsed toward the impact point on {target.name}.");
        }
    }

    Material GetMaterial()
    {
        if (sauceMaterials != null && sauceMaterials.Length > 0)
        {
            int startIndex = UnityEngine.Random.Range(0, sauceMaterials.Length);
            for (int offset = 0; offset < sauceMaterials.Length; offset++)
            {
                Material candidate = sauceMaterials[(startIndex + offset) % sauceMaterials.Length];
                if (candidate != null) return candidate;
            }
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        var material = new Material(shader) { name = "Runtime Sauce Material" };
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", fallbackColor);
        if (material.HasProperty("_Color")) material.SetColor("_Color", fallbackColor);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.75f);
        return material;
    }
}
