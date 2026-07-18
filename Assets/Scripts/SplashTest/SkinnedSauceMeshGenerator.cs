using System.Collections.Generic;
using UnityEngine;
using Unity.Profiling;

public class SkinnedSauceMeshGenerator : MonoBehaviour
{
    [Header("Sheet Shape")]
    [SerializeField, Min(0.01f)] float minimumRadius = 0.06f;
    [SerializeField, Min(0.01f)] float maximumRadius = 0.09f;
    [SerializeField, Min(0.001f)] float thickness = 0.012f;
    [SerializeField, Min(0.0005f)] float edgeThickness = 0.003f;
    [SerializeField, Min(0f)] float surfaceOffset = 0.003f;
    [SerializeField, Range(2, 8)] int rings = 4;
    [SerializeField, Range(8, 40)] int segments = 20;

    [Header("Impact-Driven Size")]
    [Tooltip("When enabled, faster pizza impacts create larger sauce sheets, always clamped by Minimum/Maximum Radius above.")]
    [SerializeField] bool useImpactDrivenRadius = true;
    [Tooltip("Impact speed (m/s) that maps to Minimum Radius.")]
    [SerializeField, Min(0f)] float minimumImpactSpeed = 1.5f;
    [Tooltip("Impact speed (m/s) that maps to Maximum Radius.")]
    [SerializeField, Min(0.01f)] float maximumImpactSpeed = 9f;
    [Tooltip("How much the hand speed at release affects the result. Impact speed remains the primary input.")]
    [SerializeField, Range(0f, 1f)] float releaseSpeedInfluence = 0.2f;
    [Tooltip("Hand release speed (m/s) that maps to Maximum Radius when release-speed influence is used.")]
    [SerializeField, Min(0.01f)] float maximumReleaseSpeed = 9f;
    [Tooltip("Small variation kept after physical sizing. 0.08 means plus/minus 8% across the allowed radius range.")]
    [SerializeField, Range(0f, 0.3f)] float radiusRandomVariation = 0.08f;

    [Header("Organic Outline")]
    [SerializeField, Range(0f, 0.35f)] float outlineIrregularity = 0.14f;
    [SerializeField, Range(0f, 0.3f)] float ellipseVariation = 0.10f;
    [SerializeField, Range(0, 8)] int roundedLobeCount = 3;
    [SerializeField, Range(0f, 0.5f)] float roundedLobeLength = 0.16f;
    [SerializeField, Range(0.1f, 1f)] float roundedLobeWidth = 0.38f;

    [Header("Instant Shrink Wrap")]
    [SerializeField, Range(4, 40)] int wrapIterations = 16;
    [SerializeField, Range(1, 6)] int constraintIterations = 2;
    [SerializeField, Range(0.05f, 1f)] float wrapStrength = 0.65f;

    [Header("Performance")]
    [Tooltip("Only searches triangles close to the impact. The legacy full-mesh search remains as a safety fallback.")]
    [SerializeField] bool useLocalSurfaceCandidates = true;
    [Tooltip("Extra radius used only while collecting safe candidate triangles. It does not change the visible sauce radius.")]
    [SerializeField, Range(1f, 2f)] float candidateRadiusMultiplier = 1.35f;
    [SerializeField] bool fallbackToFullMeshSearch = true;

    [Header("Appearance")]
    [SerializeField] Material[] sauceMaterials;
    [SerializeField] Color fallbackColor = new Color(0.65f, 0.08f, 0.025f, 1f);

    Material runtimeMaterial;
    SauceToppingSpawner toppingSpawner;
    int cachedTopologyRings = -1;
    int cachedTopologySegments = -1;
    List<int> cachedTopTriangles;
    List<Vector2Int> cachedEdges;
    List<int> cachedClosedTriangles;
    readonly List<int> candidateTriangleStarts = new List<int>(512);

    static readonly ProfilerMarker GenerateMarker = new ProfilerMarker("Splash.Generate");
    static readonly ProfilerMarker CandidateMarker = new ProfilerMarker("Splash.BuildCandidateTriangles");
    static readonly ProfilerMarker ShrinkWrapMarker = new ProfilerMarker("Splash.ShrinkWrap");
    static readonly ProfilerMarker FinalSurfaceMarker = new ProfilerMarker("Splash.FinalSurfaceQuery");
    static readonly ProfilerMarker BuildMeshMarker = new ProfilerMarker("Splash.BuildMesh");

    void Awake()
    {
        toppingSpawner = GetComponent<SauceToppingSpawner>();
    }

    public GameObject Generate(
        SkinnedSurfaceProbe.SurfaceHit hit,
        MeshCollider bakedCollider,
        Material overrideMaterial = null,
        SauceToppingTheme toppingTheme = SauceToppingTheme.None,
        float impactSpeed = 0f,
        float releaseSpeed = 0f)
    {
        using (GenerateMarker.Auto())
            return GenerateInternal(
                hit,
                bakedCollider,
                overrideMaterial,
                toppingTheme,
                impactSpeed,
                releaseSpeed);
    }

    GameObject GenerateInternal(
        SkinnedSurfaceProbe.SurfaceHit hit,
        MeshCollider bakedCollider,
        Material overrideMaterial,
        SauceToppingTheme toppingTheme,
        float impactSpeed,
        float releaseSpeed)
    {
        SkinnedMeshRenderer source = hit.renderer;
        Mesh sourceMesh = source != null ? source.sharedMesh : null;
        Mesh bakedMesh = hit.bakedMesh;
        if (source == null || sourceMesh == null || bakedMesh == null || bakedCollider == null)
            return null;

        int[] sourceTriangles = hit.sourceTriangles ?? sourceMesh.triangles;
        BoneWeight[] sourceWeights = hit.sourceWeights ?? sourceMesh.boneWeights;
        if (sourceWeights.Length != sourceMesh.vertexCount || bakedMesh.vertexCount != sourceMesh.vertexCount)
        {
            Debug.LogWarning("[SplashTest] Shrink Wrap requires matching readable source and baked meshes.");
            return null;
        }

        int seed = unchecked(hit.triangleIndex * 486187739 ^ Time.frameCount);
        var random = new System.Random(seed);
        float radius = CalculateRadius(random, impactSpeed, releaseSpeed);
        float phaseA = (float)random.NextDouble() * Mathf.PI * 2f;
        float phaseB = (float)random.NextDouble() * Mathf.PI * 2f;
        float ellipseAngle = (float)random.NextDouble() * Mathf.PI * 2f;
        float ellipseAmount = ellipseVariation * Mathf.Lerp(0.45f, 1f, (float)random.NextDouble());
        var lobeAngles = new float[roundedLobeCount];
        var lobeLengths = new float[roundedLobeCount];
        var lobeWidths = new float[roundedLobeCount];
        for (int i = 0; i < roundedLobeCount; i++)
        {
            lobeAngles[i] = (float)random.NextDouble() * Mathf.PI * 2f;
            lobeLengths[i] = roundedLobeLength * Mathf.Lerp(0.55f, 1f, (float)random.NextDouble());
            lobeWidths[i] = roundedLobeWidth * Mathf.Lerp(0.75f, 1.25f, (float)random.NextDouble());
        }

        Vector3 normal = hit.normal.normalized;
        Vector3 tangent = Vector3.Cross(normal, Vector3.up);
        if (tangent.sqrMagnitude < 0.001f) tangent = Vector3.Cross(normal, Vector3.right);
        tangent.Normalize();
        Vector3 bitangent = Vector3.Cross(normal, tangent).normalized;

        int topVertexCount = 1 + rings * segments;
        var positions = new Vector3[topVertexCount];
        var radius01 = new float[topVertexCount];
        var uvs = new List<Vector2>(topVertexCount * 2) { new Vector2(0.5f, 0.5f) };
        positions[0] = hit.point;

        for (int ring = 1; ring <= rings; ring++)
        {
            float ring01 = ring / (float)rings;
            for (int segment = 0; segment < segments; segment++)
            {
                float angle = segment / (float)segments * Mathf.PI * 2f;
                int index = 1 + (ring - 1) * segments + segment;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);
                float shapeFactor = EvaluateOutline(
                    angle, phaseA, phaseB,
                    ellipseAngle, ellipseAmount,
                    lobeAngles, lobeLengths, lobeWidths);
                float shapedRadius = radius * ring01 * shapeFactor;
                positions[index] = hit.point
                                 + tangent * (cos * shapedRadius)
                                 + bitangent * (sin * shapedRadius);
                radius01[index] = ring01;
                uvs.Add(new Vector2(0.5f + cos * ring01 * 0.5f, 0.5f + sin * ring01 * 0.5f));
            }
        }

        EnsureCachedTopology();
        List<int> topTriangles = cachedTopTriangles;
        List<Vector2Int> edges = cachedEdges;
        var restLengths = new float[edges.Count];
        for (int i = 0; i < edges.Count; i++)
            restLengths[i] = Vector3.Distance(positions[edges[i].x], positions[edges[i].y]);

        Vector3[] bakedVertices = bakedMesh.vertices;
        Vector3[] bakedNormals = bakedMesh.normals;
        Transform bakedTransform = bakedCollider.transform;
        float maximumSurfaceDistance = radius * 1.1f;
        SurfaceSearchContext searchContext = new SurfaceSearchContext(
            bakedTransform,
            bakedVertices,
            bakedNormals,
            sourceTriangles,
            hit.point,
            maximumSurfaceDistance);

        if (useLocalSurfaceCandidates)
        {
            using (CandidateMarker.Auto())
                BuildCandidateTriangleStarts(searchContext, candidateRadiusMultiplier, candidateTriangleStarts);
            searchContext.candidateTriangleStarts = candidateTriangleStarts;
            searchContext.allowFullMeshFallback = fallbackToFullMeshSearch;
        }

        using (ShrinkWrapMarker.Auto())
        {
            for (int iteration = 0; iteration < wrapIterations; iteration++)
            {
                for (int i = 1; i < positions.Length; i++)
                {
                    if (TryClosestSurface(positions[i], searchContext,
                            out Vector3 target, out _, out _, out _))
                        positions[i] = Vector3.Lerp(positions[i], target, wrapStrength);
                }

                for (int pass = 0; pass < constraintIterations; pass++)
                {
                    for (int i = 0; i < edges.Count; i++)
                        SolveEdge(positions, edges[i], restLengths[i]);
                    positions[0] = hit.point;
                }
            }
        }

        var surfacePoints = new Vector3[topVertexCount];
        var surfaceNormals = new Vector3[topVertexCount];
        var weights = new BoneWeight[topVertexCount];
        bool reportedInvalidTriangleRemap = false;
        using (FinalSurfaceMarker.Auto())
        {
            for (int i = 0; i < topVertexCount; i++)
            {
                bool resolved = TryResolveFinalSurface(
                    positions[i],
                    hit.point,
                    searchContext,
                    out surfacePoints[i],
                    out surfaceNormals[i],
                    out int triangle,
                    out Vector3 barycentric);

                // A valid center hit is already guaranteed by SkinnedSurfaceProbe.
                // Never discard the entire splat merely because one random outer
                // lobe crosses a narrow limb, face edge, or mesh seam.
                if (!resolved)
                {
                    surfacePoints[i] = hit.point;
                    surfaceNormals[i] = hit.normal;
                    triangle = hit.triangleIndex;
                    barycentric = hit.barycentricCoordinate;
                }

                if (TryBlendTriangleWeights(
                        sourceTriangles,
                        sourceWeights,
                        triangle,
                        barycentric,
                        out weights[i]))
                    continue;

                // RaycastHit.triangleIndex belongs to the temporary baked mesh.
                // Most skinned meshes preserve topology, but a spawned/imported
                // character may not. Never use that index blindly against the
                // original mesh's bone-weight arrays.
                if (!reportedInvalidTriangleRemap)
                {
                    reportedInvalidTriangleRemap = true;
                    Debug.LogWarning(
                        $"[SplashTest] {source.name}: baked triangle {triangle} could not be used " +
                        "with the source mesh. Remapping sauce vertices to valid source triangles.");
                }

                if (!TryClosestSurfaceAnywhere(
                        surfacePoints[i],
                        searchContext,
                        out Vector3 remappedPoint,
                        out Vector3 remappedNormal,
                        out int remappedTriangle,
                        out Vector3 remappedBarycentric)
                    || !TryBlendTriangleWeights(
                        sourceTriangles,
                        sourceWeights,
                        remappedTriangle,
                        remappedBarycentric,
                        out weights[i]))
                {
                    Debug.LogWarning(
                        $"[SplashTest] {source.name}: could not recover a valid bone-weight triangle for sauce vertex {i}.");
                    return null;
                }

                surfacePoints[i] = remappedPoint;
                surfaceNormals[i] = remappedNormal;
            }
        }

        Matrix4x4[] bindposes = hit.bindposes ?? sourceMesh.bindposes;
        var bindVertices = new List<Vector3>(topVertexCount * 2);
        var outputWeights = new List<BoneWeight>(topVertexCount * 2);
        for (int copy = 0; copy < 2; copy++)
        {
            for (int i = 0; i < topVertexCount; i++)
            {
                float height = edgeThickness
                             + (thickness - edgeThickness) * Mathf.Pow(1f - radius01[i], 2f);
                float offset = copy == 0 ? surfaceOffset + height : surfaceOffset;
                Vector3 world = surfacePoints[i] + surfaceNormals[i] * offset;
                Vector3 currentLocal = source.transform.InverseTransformPoint(world);
                Matrix4x4 skin = BuildSkinMatrix(source, bindposes, weights[i]);
                bindVertices.Add(skin.inverse.MultiplyPoint3x4(currentLocal));
                outputWeights.Add(weights[i]);
                if (copy == 1) uvs.Add(uvs[i]);
            }
        }

        GameObject sauceObject;
        using (BuildMeshMarker.Auto())
        {
            var mesh = new Mesh { name = "Runtime Instant Shrink-Wrap Sauce Mesh" };
            mesh.SetVertices(bindVertices);
            mesh.SetTriangles(cachedClosedTriangles, 0);
            mesh.SetUVs(0, uvs);
            mesh.boneWeights = outputWeights.ToArray();
            mesh.bindposes = bindposes;
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();

            sauceObject = new GameObject($"ShrinkWrapSauce_{Time.frameCount}");
            sauceObject.transform.SetParent(source.transform.parent, false);
            sauceObject.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
            sauceObject.transform.localScale = source.transform.localScale;
            var renderer = sauceObject.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh = mesh;
            renderer.sharedMaterial = overrideMaterial != null ? overrideMaterial : GetMaterial();
            renderer.bones = source.bones;
            renderer.rootBone = source.rootBone;
            renderer.updateWhenOffscreen = true;
        }

        Debug.Log($"[SplashTest] Created instant shrink-wrap sauce. Radius: {radius:F3} m, vertices: {bindVertices.Count}, iterations: {wrapIterations}.");
        if (toppingSpawner == null) toppingSpawner = GetComponent<SauceToppingSpawner>();
        if (toppingSpawner != null)
        {
            toppingSpawner.SpawnToppings(
                sauceObject,
                toppingTheme,
                source,
                surfacePoints,
                surfaceNormals,
                weights,
                radius01,
                seed);
        }
        return sauceObject;
    }

    float CalculateRadius(System.Random random, float impactSpeed, float releaseSpeed)
    {
        float minRadius = Mathf.Min(minimumRadius, maximumRadius);
        float maxRadius = Mathf.Max(minimumRadius, maximumRadius);

        // Keep the old fully-random behaviour available for isolated tests that
        // do not provide an impact velocity, or when the feature is disabled.
        if (!useImpactDrivenRadius || impactSpeed <= 0.0001f)
        {
            return Mathf.Lerp(minRadius, maxRadius, (float)random.NextDouble());
        }

        float minImpact = Mathf.Min(minimumImpactSpeed, maximumImpactSpeed);
        float maxImpact = Mathf.Max(minimumImpactSpeed, maximumImpactSpeed);
        float size01 = Mathf.InverseLerp(minImpact, maxImpact, impactSpeed);

        // The real collision speed is the main source of truth. Release speed
        // adds a small hand-throw contribution only when gameplay recorded it.
        if (releaseSpeed > 0.0001f && releaseSpeedInfluence > 0f)
        {
            float release01 = Mathf.InverseLerp(0f, Mathf.Max(0.01f, maximumReleaseSpeed), releaseSpeed);
            size01 = Mathf.Lerp(size01, release01, releaseSpeedInfluence);
        }

        float randomOffset = ((float)random.NextDouble() * 2f - 1f) * radiusRandomVariation;
        size01 = Mathf.Clamp01(size01 + randomOffset);
        return Mathf.Lerp(minRadius, maxRadius, size01);
    }

    void EnsureCachedTopology()
    {
        if (cachedTopologyRings == rings && cachedTopologySegments == segments
            && cachedTopTriangles != null && cachedEdges != null && cachedClosedTriangles != null)
            return;

        cachedTopologyRings = rings;
        cachedTopologySegments = segments;
        cachedTopTriangles = BuildTopTriangles();
        cachedEdges = BuildEdges(cachedTopTriangles);
        cachedClosedTriangles = BuildClosedTriangles(cachedTopTriangles, 1 + rings * segments);
    }

    float EvaluateOutline(
        float angle,
        float phaseA,
        float phaseB,
        float ellipseAngle,
        float ellipseAmount,
        float[] lobeAngles,
        float[] lobeLengths,
        float[] lobeWidths)
    {
        float factor = 1f;
        factor += Mathf.Sin(angle * 3f + phaseA) * outlineIrregularity * 0.65f;
        factor += Mathf.Sin(angle * 5f + phaseB) * outlineIrregularity * 0.35f;
        factor += Mathf.Cos((angle - ellipseAngle) * 2f) * ellipseAmount;

        for (int i = 0; i < lobeAngles.Length; i++)
        {
            float delta = Mathf.Abs(Mathf.DeltaAngle(
                angle * Mathf.Rad2Deg,
                lobeAngles[i] * Mathf.Rad2Deg)) * Mathf.Deg2Rad;
            float width = Mathf.Max(0.01f, lobeWidths[i]);
            factor += lobeLengths[i] * Mathf.Exp(-(delta * delta) / (2f * width * width));
        }

        return Mathf.Clamp(factor, 0.55f, 1.55f);
    }

    List<int> BuildTopTriangles()
    {
        var triangles = new List<int>();
        for (int segment = 0; segment < segments; segment++)
        {
            int next = (segment + 1) % segments;
            triangles.Add(0); triangles.Add(1 + segment); triangles.Add(1 + next);
        }
        for (int ring = 1; ring < rings; ring++)
        {
            int inner = 1 + (ring - 1) * segments;
            int outer = 1 + ring * segments;
            for (int segment = 0; segment < segments; segment++)
            {
                int next = (segment + 1) % segments;
                triangles.Add(inner + segment); triangles.Add(outer + segment); triangles.Add(inner + next);
                triangles.Add(inner + next); triangles.Add(outer + segment); triangles.Add(outer + next);
            }
        }
        return triangles;
    }

    static List<Vector2Int> BuildEdges(List<int> triangles)
    {
        var edges = new List<Vector2Int>();
        var keys = new HashSet<ulong>();
        for (int i = 0; i < triangles.Count; i += 3)
        {
            AddEdge(triangles[i], triangles[i + 1], keys, edges);
            AddEdge(triangles[i + 1], triangles[i + 2], keys, edges);
            AddEdge(triangles[i + 2], triangles[i], keys, edges);
        }
        return edges;
    }

    static void AddEdge(int a, int b, HashSet<ulong> keys, List<Vector2Int> edges)
    {
        uint min = (uint)Mathf.Min(a, b);
        uint max = (uint)Mathf.Max(a, b);
        ulong key = ((ulong)min << 32) | max;
        if (keys.Add(key)) edges.Add(new Vector2Int(a, b));
    }

    static void SolveEdge(Vector3[] positions, Vector2Int edge, float restLength)
    {
        int a = edge.x;
        int b = edge.y;
        Vector3 delta = positions[b] - positions[a];
        float length = delta.magnitude;
        if (length < 0.000001f) return;
        Vector3 correction = delta * ((length - restLength) / length);
        if (a == 0) positions[b] -= correction;
        else if (b == 0) positions[a] += correction;
        else
        {
            positions[a] += correction * 0.5f;
            positions[b] -= correction * 0.5f;
        }
    }

    List<int> BuildClosedTriangles(List<int> top, int count)
    {
        var result = new List<int>(top.Count * 2 + segments * 6);
        result.AddRange(top);
        for (int i = 0; i < top.Count; i += 3)
        {
            result.Add(count + top[i + 2]);
            result.Add(count + top[i + 1]);
            result.Add(count + top[i]);
        }
        int outer = 1 + (rings - 1) * segments;
        for (int segment = 0; segment < segments; segment++)
        {
            int next = (segment + 1) % segments;
            int topA = outer + segment;
            int topB = outer + next;
            int bottomA = count + topA;
            int bottomB = count + topB;
            result.Add(topA); result.Add(bottomA); result.Add(topB);
            result.Add(topB); result.Add(bottomA); result.Add(bottomB);
        }
        return result;
    }

    struct SurfaceSearchContext
    {
        public Transform transform;
        public Vector3[] vertices;
        public Vector3[] normals;
        public int[] triangles;
        public Vector3 localAllowedCenter;
        public float localMaximumDistance;
        public float localMaximumDistanceSq;
        public List<int> candidateTriangleStarts;
        public bool allowFullMeshFallback;

        public SurfaceSearchContext(
            Transform transform,
            Vector3[] vertices,
            Vector3[] normals,
            int[] triangles,
            Vector3 allowedCenter,
            float maximumSurfaceDistance)
        {
            this.transform = transform;
            this.vertices = vertices;
            this.normals = normals;
            this.triangles = triangles;
            localAllowedCenter = transform.InverseTransformPoint(allowedCenter);
            Vector3 scale = transform.lossyScale;
            float minimumScale = Mathf.Max(0.000001f,
                Mathf.Min(Mathf.Abs(scale.x), Mathf.Min(Mathf.Abs(scale.y), Mathf.Abs(scale.z))));
            localMaximumDistance = maximumSurfaceDistance / minimumScale;
            localMaximumDistanceSq = localMaximumDistance * localMaximumDistance;
            candidateTriangleStarts = null;
            allowFullMeshFallback = false;
        }
    }

    static void BuildCandidateTriangleStarts(
        SurfaceSearchContext context,
        float radiusMultiplier,
        List<int> result)
    {
        result.Clear();
        float localRadius = context.localMaximumDistance * Mathf.Max(1f, radiusMultiplier);
        float localRadiusSq = localRadius * localRadius;

        for (int start = 0; start + 2 < context.triangles.Length; start += 3)
        {
            Vector3 a = context.vertices[context.triangles[start]];
            Vector3 b = context.vertices[context.triangles[start + 1]];
            Vector3 c = context.vertices[context.triangles[start + 2]];
            Vector3 min = Vector3.Min(a, Vector3.Min(b, c));
            Vector3 max = Vector3.Max(a, Vector3.Max(b, c));
            Vector3 closestOnBounds = new Vector3(
                Mathf.Clamp(context.localAllowedCenter.x, min.x, max.x),
                Mathf.Clamp(context.localAllowedCenter.y, min.y, max.y),
                Mathf.Clamp(context.localAllowedCenter.z, min.z, max.z));

            if ((closestOnBounds - context.localAllowedCenter).sqrMagnitude <= localRadiusSq)
                result.Add(start);
        }
    }

    static bool TryClosestSurface(
        Vector3 worldQuery,
        SurfaceSearchContext context,
        out Vector3 worldPoint,
        out Vector3 worldNormal,
        out int closestTriangle,
        out Vector3 closestBarycentric)
    {
        Vector3 query = context.transform.InverseTransformPoint(worldQuery);
        if (TryClosestSurfaceInSet(query, context, context.candidateTriangleStarts,
                out Vector3 localPoint, out Vector3 localNormal,
                out closestTriangle, out closestBarycentric)
            || (context.allowFullMeshFallback && context.candidateTriangleStarts != null
                && TryClosestSurfaceInSet(query, context, null,
                    out localPoint, out localNormal,
                    out closestTriangle, out closestBarycentric)))
        {
            worldPoint = context.transform.TransformPoint(localPoint);
            worldNormal = context.transform.TransformDirection(localNormal).normalized;
            return true;
        }

        worldPoint = default;
        worldNormal = Vector3.up;
        closestTriangle = -1;
        closestBarycentric = default;
        return false;
    }

    // Final mesh construction must not be all-or-nothing. First pull an edge
    // point toward the valid center (shrinking the sheet at a tight boundary),
    // then use the closest mesh point anywhere as a final safety net.
    static bool TryResolveFinalSurface(
        Vector3 query,
        Vector3 validCenter,
        SurfaceSearchContext context,
        out Vector3 worldPoint,
        out Vector3 worldNormal,
        out int closestTriangle,
        out Vector3 closestBarycentric)
    {
        if (TryClosestSurface(query, context,
                out worldPoint, out worldNormal, out closestTriangle, out closestBarycentric))
            return true;

        const int shrinkSteps = 4;
        for (int step = 1; step <= shrinkSteps; step++)
        {
            float towardCenter = step / (float)(shrinkSteps + 1);
            Vector3 shrunkenQuery = Vector3.Lerp(query, validCenter, towardCenter);
            if (TryClosestSurface(shrunkenQuery, context,
                    out worldPoint, out worldNormal, out closestTriangle, out closestBarycentric))
                return true;
        }

        return TryClosestSurfaceAnywhere(query, context,
            out worldPoint, out worldNormal, out closestTriangle, out closestBarycentric);
    }

    static bool TryClosestSurfaceAnywhere(
        Vector3 worldQuery,
        SurfaceSearchContext context,
        out Vector3 worldPoint,
        out Vector3 worldNormal,
        out int closestTriangle,
        out Vector3 closestBarycentric)
    {
        Vector3 query = context.transform.InverseTransformPoint(worldQuery);
        if (TryClosestSurfaceInSetAnywhere(query, context, context.candidateTriangleStarts,
                out Vector3 localPoint, out Vector3 localNormal,
                out closestTriangle, out closestBarycentric)
            || (context.candidateTriangleStarts != null
                && TryClosestSurfaceInSetAnywhere(query, context, null,
                    out localPoint, out localNormal,
                    out closestTriangle, out closestBarycentric)))
        {
            worldPoint = context.transform.TransformPoint(localPoint);
            worldNormal = context.transform.TransformDirection(localNormal).normalized;
            return true;
        }

        worldPoint = default;
        worldNormal = Vector3.up;
        closestTriangle = -1;
        closestBarycentric = default;
        return false;
    }

    static bool TryClosestSurfaceInSet(
        Vector3 query,
        SurfaceSearchContext context,
        List<int> triangleStarts,
        out Vector3 bestPoint,
        out Vector3 bestNormal,
        out int closestTriangle,
        out Vector3 closestBarycentric)
    {
        float bestDistance = float.MaxValue;
        bestPoint = default;
        bestNormal = Vector3.up;
        closestTriangle = -1;
        closestBarycentric = default;

        int count = triangleStarts != null ? triangleStarts.Count : context.triangles.Length / 3;
        for (int index = 0; index < count; index++)
        {
            int start = triangleStarts != null ? triangleStarts[index] : index * 3;
            int ia = context.triangles[start];
            int ib = context.triangles[start + 1];
            int ic = context.triangles[start + 2];
            Vector3 point = ClosestPointOnTriangle(
                query, context.vertices[ia], context.vertices[ib], context.vertices[ic], out Vector3 bary);
            if ((point - context.localAllowedCenter).sqrMagnitude > context.localMaximumDistanceSq) continue;
            float distance = (point - query).sqrMagnitude;
            if (distance >= bestDistance) continue;
            bestDistance = distance;
            closestTriangle = start / 3;
            closestBarycentric = bary;
            bestPoint = point;
            bestNormal = context.normals.Length == context.vertices.Length
                ? (context.normals[ia] * bary.x + context.normals[ib] * bary.y + context.normals[ic] * bary.z).normalized
                : Vector3.Cross(context.vertices[ib] - context.vertices[ia], context.vertices[ic] - context.vertices[ia]).normalized;
        }

        return closestTriangle >= 0;
    }

    static bool TryClosestSurfaceInSetAnywhere(
        Vector3 query,
        SurfaceSearchContext context,
        List<int> triangleStarts,
        out Vector3 bestPoint,
        out Vector3 bestNormal,
        out int closestTriangle,
        out Vector3 closestBarycentric)
    {
        float bestDistance = float.MaxValue;
        bestPoint = default;
        bestNormal = Vector3.up;
        closestTriangle = -1;
        closestBarycentric = default;

        int count = triangleStarts != null ? triangleStarts.Count : context.triangles.Length / 3;
        for (int index = 0; index < count; index++)
        {
            int start = triangleStarts != null ? triangleStarts[index] : index * 3;
            int ia = context.triangles[start];
            int ib = context.triangles[start + 1];
            int ic = context.triangles[start + 2];
            Vector3 point = ClosestPointOnTriangle(
                query, context.vertices[ia], context.vertices[ib], context.vertices[ic], out Vector3 bary);
            float distance = (point - query).sqrMagnitude;
            if (distance >= bestDistance) continue;

            bestDistance = distance;
            closestTriangle = start / 3;
            closestBarycentric = bary;
            bestPoint = point;
            bestNormal = context.normals.Length == context.vertices.Length
                ? (context.normals[ia] * bary.x + context.normals[ib] * bary.y + context.normals[ic] * bary.z).normalized
                : Vector3.Cross(context.vertices[ib] - context.vertices[ia], context.vertices[ic] - context.vertices[ia]).normalized;
        }

        return closestTriangle >= 0;
    }

    static Vector3 ClosestPointOnTriangle(Vector3 p, Vector3 a, Vector3 b, Vector3 c, out Vector3 bary)
    {
        Vector3 ab = b - a;
        Vector3 ac = c - a;
        Vector3 ap = p - a;
        float d1 = Vector3.Dot(ab, ap);
        float d2 = Vector3.Dot(ac, ap);
        if (d1 <= 0f && d2 <= 0f) { bary = new Vector3(1, 0, 0); return a; }
        Vector3 bp = p - b;
        float d3 = Vector3.Dot(ab, bp);
        float d4 = Vector3.Dot(ac, bp);
        if (d3 >= 0f && d4 <= d3) { bary = new Vector3(0, 1, 0); return b; }
        float vc = d1 * d4 - d3 * d2;
        if (vc <= 0f && d1 >= 0f && d3 <= 0f)
        {
            float v = d1 / (d1 - d3); bary = new Vector3(1f - v, v, 0); return a + ab * v;
        }
        Vector3 cp = p - c;
        float d5 = Vector3.Dot(ab, cp);
        float d6 = Vector3.Dot(ac, cp);
        if (d6 >= 0f && d5 <= d6) { bary = new Vector3(0, 0, 1); return c; }
        float vb = d5 * d2 - d1 * d6;
        if (vb <= 0f && d2 >= 0f && d6 <= 0f)
        {
            float w = d2 / (d2 - d6); bary = new Vector3(1f - w, 0, w); return a + ac * w;
        }
        float va = d3 * d6 - d5 * d4;
        if (va <= 0f && d4 - d3 >= 0f && d5 - d6 >= 0f)
        {
            float w = (d4 - d3) / ((d4 - d3) + (d5 - d6));
            bary = new Vector3(0, 1f - w, w); return b + (c - b) * w;
        }
        float denominator = 1f / (va + vb + vc);
        float faceV = vb * denominator;
        float faceW = vc * denominator;
        bary = new Vector3(1f - faceV - faceW, faceV, faceW);
        return a + ab * faceV + ac * faceW;
    }

    static bool TryBlendTriangleWeights(
        int[] triangles,
        BoneWeight[] sourceWeights,
        int triangle,
        Vector3 barycentric,
        out BoneWeight result)
    {
        result = default;
        if (triangles == null || sourceWeights == null || triangles.Length < 3 || triangle < 0)
            return false;

        // Check before multiplication so an unexpected large value cannot overflow.
        if (triangle > (triangles.Length - 3) / 3)
            return false;

        int start = triangle * 3;
        int a = triangles[start];
        int b = triangles[start + 1];
        int c = triangles[start + 2];
        if ((uint)a >= (uint)sourceWeights.Length
            || (uint)b >= (uint)sourceWeights.Length
            || (uint)c >= (uint)sourceWeights.Length)
            return false;

        result = BlendWeights(
            sourceWeights[a],
            sourceWeights[b],
            sourceWeights[c],
            barycentric);
        return true;
    }

    static BoneWeight BlendWeights(BoneWeight a, BoneWeight b, BoneWeight c, Vector3 bary)
    {
        var totals = new Dictionary<int, float>();
        AddWeights(totals, a, bary.x);
        AddWeights(totals, b, bary.y);
        AddWeights(totals, c, bary.z);
        var sorted = new List<KeyValuePair<int, float>>(totals);
        sorted.Sort((x, y) => y.Value.CompareTo(x.Value));
        float sum = 0f;
        for (int i = 0; i < Mathf.Min(4, sorted.Count); i++) sum += sorted[i].Value;
        var result = new BoneWeight();
        if (sum <= 0f) { result.boneIndex0 = a.boneIndex0; result.weight0 = 1f; return result; }
        if (sorted.Count > 0) { result.boneIndex0 = sorted[0].Key; result.weight0 = sorted[0].Value / sum; }
        if (sorted.Count > 1) { result.boneIndex1 = sorted[1].Key; result.weight1 = sorted[1].Value / sum; }
        if (sorted.Count > 2) { result.boneIndex2 = sorted[2].Key; result.weight2 = sorted[2].Value / sum; }
        if (sorted.Count > 3) { result.boneIndex3 = sorted[3].Key; result.weight3 = sorted[3].Value / sum; }
        return result;
    }

    static void AddWeights(Dictionary<int, float> totals, BoneWeight weight, float scale)
    {
        AddWeight(totals, weight.boneIndex0, weight.weight0 * scale);
        AddWeight(totals, weight.boneIndex1, weight.weight1 * scale);
        AddWeight(totals, weight.boneIndex2, weight.weight2 * scale);
        AddWeight(totals, weight.boneIndex3, weight.weight3 * scale);
    }

    static void AddWeight(Dictionary<int, float> totals, int index, float value)
    {
        if (value <= 0f) return;
        totals.TryGetValue(index, out float current);
        totals[index] = current + value;
    }

    static Matrix4x4 BuildSkinMatrix(SkinnedMeshRenderer renderer, Matrix4x4[] bindposes, BoneWeight weight)
    {
        Matrix4x4 result = ZeroMatrix();
        AddMatrix(ref result, GetBoneMatrix(renderer, bindposes, weight.boneIndex0), weight.weight0);
        AddMatrix(ref result, GetBoneMatrix(renderer, bindposes, weight.boneIndex1), weight.weight1);
        AddMatrix(ref result, GetBoneMatrix(renderer, bindposes, weight.boneIndex2), weight.weight2);
        AddMatrix(ref result, GetBoneMatrix(renderer, bindposes, weight.boneIndex3), weight.weight3);
        return result;
    }

    static Matrix4x4 ZeroMatrix()
    {
        var matrix = new Matrix4x4();
        for (int i = 0; i < 16; i++) matrix[i] = 0f;
        return matrix;
    }

    static Matrix4x4 GetBoneMatrix(SkinnedMeshRenderer renderer, Matrix4x4[] bindposes, int index)
    {
        if (index < 0 || index >= renderer.bones.Length || index >= bindposes.Length || renderer.bones[index] == null)
            return Matrix4x4.identity;
        return renderer.transform.worldToLocalMatrix * renderer.bones[index].localToWorldMatrix * bindposes[index];
    }

    static void AddMatrix(ref Matrix4x4 target, Matrix4x4 value, float weight)
    {
        for (int i = 0; i < 16; i++) target[i] += value[i] * weight;
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
        if (runtimeMaterial != null) return runtimeMaterial;
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        runtimeMaterial = new Material(shader) { name = "Runtime Instant Shrink-Wrap Sauce Material" };
        if (runtimeMaterial.HasProperty("_BaseColor")) runtimeMaterial.SetColor("_BaseColor", fallbackColor);
        if (runtimeMaterial.HasProperty("_Color")) runtimeMaterial.SetColor("_Color", fallbackColor);
        if (runtimeMaterial.HasProperty("_Smoothness")) runtimeMaterial.SetFloat("_Smoothness", 0.8f);
        return runtimeMaterial;
    }

    void OnDestroy()
    {
        if (runtimeMaterial != null) Destroy(runtimeMaterial);
    }
}
