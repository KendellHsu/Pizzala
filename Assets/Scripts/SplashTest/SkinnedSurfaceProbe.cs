using System.Collections.Generic;
using UnityEngine;
using Unity.Profiling;

/// <summary>
/// Editor test helper: bakes the current pose of all character skinned meshes,
/// then performs an exact raycast against those temporary meshes.
/// </summary>
public class SkinnedSurfaceProbe : MonoBehaviour
{
    [Header("Sources")]
    [Tooltip("Optional explicit renderers. If empty, all SkinnedMeshRenderers below this object are found automatically.")]
    [SerializeField] SkinnedMeshRenderer[] sourceRenderers;

    [SerializeField, Min(0.05f)] float castDistance = 0.8f;
    [SerializeField, Min(0.1f)] float normalDisplayLength = 0.25f;
    [SerializeField] bool includeInactiveRenderers;

    readonly List<ProbeTarget> targets = new List<ProbeTarget>();
    SkinnedSauceMeshGenerator skinnedSauceGenerator;

    sealed class ProbeTarget
    {
        public SkinnedMeshRenderer renderer;
        public Mesh bakedMesh;
        public GameObject colliderObject;
        public MeshCollider collider;
        public int[] sourceTriangles;
        public BoneWeight[] sourceWeights;
        public Matrix4x4[] bindposes;
    }

    public struct SurfaceHit
    {
        public Vector3 point;
        public Vector3 normal;
        public int triangleIndex;
        public Vector3 barycentricCoordinate;
        public SkinnedMeshRenderer renderer;
        public Mesh bakedMesh;
        public int[] sourceTriangles;
        public BoneWeight[] sourceWeights;
        public Matrix4x4[] bindposes;
    }

    static readonly ProfilerMarker TryProbeMarker = new ProfilerMarker("Splash.TryProbe");
    static readonly ProfilerMarker BakeMeshMarker = new ProfilerMarker("Splash.BakeMeshAndCookCollider");
    static readonly ProfilerMarker NearestFallbackMarker = new ProfilerMarker("Splash.NearestSurfaceFallback");

    void Awake()
    {
        BuildTargets();
        skinnedSauceGenerator = GetComponent<SkinnedSauceMeshGenerator>();
        if (skinnedSauceGenerator == null)
            skinnedSauceGenerator = gameObject.AddComponent<SkinnedSauceMeshGenerator>();
    }

    void BuildTargets()
    {
        CleanupTargets();
        var renderers = sourceRenderers != null && sourceRenderers.Length > 0
            ? sourceRenderers
            : GetComponentsInChildren<SkinnedMeshRenderer>(includeInactiveRenderers);
        foreach (var skinnedRenderer in renderers)
        {
            if (skinnedRenderer == null || skinnedRenderer.sharedMesh == null) continue;

            var colliderObject = new GameObject($"__BakedCollider_{skinnedRenderer.name}");
            colliderObject.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave;
            colliderObject.layer = LayerMask.NameToLayer("Ignore Raycast");

            var meshCollider = colliderObject.AddComponent<MeshCollider>();
            meshCollider.convex = false;
            meshCollider.enabled = false;

            targets.Add(new ProbeTarget
            {
                renderer = skinnedRenderer,
                bakedMesh = new Mesh { name = $"Baked_{skinnedRenderer.name}" },
                colliderObject = colliderObject,
                collider = meshCollider,
                sourceTriangles = skinnedRenderer.sharedMesh.triangles,
                sourceWeights = skinnedRenderer.sharedMesh.boneWeights,
                bindposes = skinnedRenderer.sharedMesh.bindposes,
            });
        }

        Debug.Log($"[SplashTest] SkinnedSurfaceProbe found {targets.Count} skinned renderer(s) under {name}.");
    }

    public bool TryProbe(
        Vector3 roughContactPoint,
        Vector3 incomingVelocity,
        Vector3 roughContactNormal,
        Vector3 roughColliderCenter,
        Material sauceMaterial,
        SauceToppingTheme toppingTheme,
        out SurfaceHit result)
    {
        using (TryProbeMarker.Auto())
            return TryProbeInternal(
                roughContactPoint,
                incomingVelocity,
                roughContactNormal,
                roughColliderCenter,
                sauceMaterial,
                toppingTheme,
                out result);
    }

    bool TryProbeInternal(
        Vector3 roughContactPoint,
        Vector3 incomingVelocity,
        Vector3 roughContactNormal,
        Vector3 roughColliderCenter,
        Material sauceMaterial,
        SauceToppingTheme toppingTheme,
        out SurfaceHit result)
    {
        result = default;
        if (targets.Count == 0) BuildTargets();
        if (targets.Count == 0)
        {
            Debug.LogWarning($"[SplashTest] No SkinnedMeshRenderer found below {name}.");
            return false;
        }

        // Contact normals point from the character collider toward the projectile,
        // therefore the impact travels in the opposite direction. Depending on
        // which Rigidbody receives the callback, Collision.relativeVelocity can
        // have the opposite sign, which previously made us hit the character's back.
        Vector3 expectedIncoming = -roughContactNormal.normalized;
        Vector3 incoming = incomingVelocity.sqrMagnitude > 0.0001f
            ? incomingVelocity.normalized
            : expectedIncoming;
        if (Vector3.Dot(incoming, expectedIncoming) < 0f)
            incoming = -incoming;

        // Start behind the rough collider contact and cast along the projectile path.
        Vector3 origin = roughContactPoint - incoming * (castDistance * 0.5f);
        var ray = new Ray(origin, incoming);
        bool found = false;
        float closestDistance = float.MaxValue;

        foreach (var target in targets)
        {
            using (BakeMeshMarker.Auto()) BakeTarget(target);
            target.collider.enabled = true;
            if (target.collider.Raycast(ray, out RaycastHit hit, castDistance)
                && hit.distance < closestDistance)
            {
                closestDistance = hit.distance;
                result = new SurfaceHit
                {
                    point = hit.point,
                    normal = hit.normal,
                    triangleIndex = hit.triangleIndex,
                    barycentricCoordinate = hit.barycentricCoordinate,
                    renderer = target.renderer,
                    bakedMesh = target.bakedMesh,
                    sourceTriangles = target.sourceTriangles,
                    sourceWeights = target.sourceWeights,
                    bindposes = target.bindposes,
                };
                found = true;
            }
            target.collider.enabled = false;
        }

        // Capsule contacts can be noticeably outside narrow limbs. Try a normal cast
        // as a fallback when the original flight line narrowly misses the visual mesh.
        if (!found)
        {
            Vector3 normal = roughContactNormal.normalized;
            ray = new Ray(roughContactPoint + normal * (castDistance * 0.5f), -normal);
            foreach (var target in targets)
            {
                target.collider.enabled = true;
                if (target.collider.Raycast(ray, out RaycastHit hit, castDistance)
                    && hit.distance < closestDistance)
                {
                    closestDistance = hit.distance;
                    result = new SurfaceHit
                    {
                        point = hit.point,
                        normal = hit.normal,
                        triangleIndex = hit.triangleIndex,
                        barycentricCoordinate = hit.barycentricCoordinate,
                        renderer = target.renderer,
                        bakedMesh = target.bakedMesh,
                        sourceTriangles = target.sourceTriangles,
                        sourceWeights = target.sourceWeights,
                        bindposes = target.bindposes,
                    };
                    found = true;
                }
                target.collider.enabled = false;
            }
        }

        // Cheap third attempt requested for broad capsule/cylinder hit zones:
        // cast radially from the contact point toward that collider's center.
        // This often finds the torso immediately, while the exact closest-triangle
        // search below remains the final guarantee for gaps and unusual poses.
        if (!found)
        {
            Vector3 towardCenter = roughColliderCenter - roughContactPoint;
            float centerDistance = towardCenter.magnitude;
            if (centerDistance > 0.0001f)
            {
                towardCenter /= centerDistance;
                Vector3 centerCastOrigin = roughContactPoint - towardCenter * 0.01f;
                ray = new Ray(centerCastOrigin, towardCenter);
                float centerCastDistance = centerDistance + castDistance * 0.5f;

                foreach (var target in targets)
                {
                    target.collider.enabled = true;
                    if (target.collider.Raycast(ray, out RaycastHit hit, centerCastDistance)
                        && hit.distance < closestDistance)
                    {
                        closestDistance = hit.distance;
                        result = new SurfaceHit
                        {
                            point = hit.point,
                            normal = hit.normal,
                            triangleIndex = hit.triangleIndex,
                            barycentricCoordinate = hit.barycentricCoordinate,
                            renderer = target.renderer,
                            bakedMesh = target.bakedMesh,
                            sourceTriangles = target.sourceTriangles,
                            sourceWeights = target.sourceWeights,
                            bindposes = target.bindposes,
                        };
                        found = true;
                    }
                    target.collider.enabled = false;
                }

                if (found)
                    Debug.Log("[SplashTest] Used BodyZone-center ray to find the visual mesh.");
            }
        }

        // Design fallback: the broad body collider may be intentionally simple.
        // If the projectile path passes through empty space inside that collider,
        // attach the sauce to the closest point of the current visible character mesh.
        if (!found)
        {
            float closestSurfaceDistanceSq = float.MaxValue;
            using (NearestFallbackMarker.Auto())
            {
                foreach (var target in targets)
                {
                    if (!TryFindClosestSurface(
                            target, roughContactPoint,
                            out Vector3 point, out Vector3 surfaceNormal,
                            out int triangleIndex, out Vector3 barycentric,
                            out float distanceSq)
                        || distanceSq >= closestSurfaceDistanceSq)
                        continue;

                    closestSurfaceDistanceSq = distanceSq;
                    result = new SurfaceHit
                    {
                        point = point,
                        normal = surfaceNormal,
                        triangleIndex = triangleIndex,
                        barycentricCoordinate = barycentric,
                        renderer = target.renderer,
                        bakedMesh = target.bakedMesh,
                        sourceTriangles = target.sourceTriangles,
                        sourceWeights = target.sourceWeights,
                        bindposes = target.bindposes,
                    };
                    found = true;
                }
            }

            if (found)
                Debug.Log($"[SplashTest] Flight ray missed visual mesh; using nearest surface point ({Mathf.Sqrt(closestSurfaceDistanceSq):F3} m away).");
        }

        if (!found)
        {
            Debug.DrawRay(origin, incoming * castDistance, Color.red, 5f);
            Debug.LogWarning(
                $"[SplashTest] Baked mesh raycast missed {name}. " +
                "Increase Cast Distance or tighten the BodyZone collider.");
            return false;
        }

        Debug.DrawRay(result.point, result.normal * normalDisplayLength, Color.green, 8f);
        string dominantBone = GetDominantBoneName(result);
        Debug.Log(
            $"[SplashTest] Exact skinned surface hit\n" +
            $"Renderer: {result.renderer.name}\n" +
            $"Dominant bone: {dominantBone}\n" +
            $"Point: {result.point}\n" +
            $"Normal: {result.normal}\n" +
            $"Triangle: {result.triangleIndex}\n" +
            $"Barycentric: {result.barycentricCoordinate}");

        SkinnedMeshRenderer hitRenderer = result.renderer;
        ProbeTarget sauceTarget = targets.Find(t => t.renderer == hitRenderer);
        if (sauceTarget != null && skinnedSauceGenerator != null)
        {
            sauceTarget.collider.enabled = true;
            GameObject createdSauce = skinnedSauceGenerator.Generate(
                result, sauceTarget.collider, sauceMaterial, toppingTheme);
            sauceTarget.collider.enabled = false;
            if (createdSauce != null) return true;

            Debug.LogWarning(
                $"[SurfaceSauce] Found {hitRenderer.name}, but its 3D sauce mesh was not created.");
            return false;
        }

        Debug.LogWarning("[SurfaceSauce] Exact surface was found, but no matching sauce generator target exists.");
        return false;
    }

    static bool TryFindClosestSurface(
        ProbeTarget target,
        Vector3 worldQuery,
        out Vector3 worldPoint,
        out Vector3 worldNormal,
        out int closestTriangle,
        out Vector3 closestBarycentric,
        out float closestDistanceSq)
    {
        worldPoint = default;
        worldNormal = Vector3.up;
        closestTriangle = -1;
        closestBarycentric = default;
        closestDistanceSq = float.MaxValue;

        Mesh mesh = target.bakedMesh;
        if (mesh == null) return false;
        Vector3[] vertices = mesh.vertices;
        Vector3[] normals = mesh.normals;
        int[] triangles = mesh.triangles;
        if (vertices.Length == 0 || triangles.Length < 3) return false;

        Transform meshTransform = target.colliderObject.transform;
        Vector3 localQuery = meshTransform.InverseTransformPoint(worldQuery);
        Vector3 bestLocalPoint = default;
        Vector3 bestLocalNormal = Vector3.up;

        for (int start = 0; start + 2 < triangles.Length; start += 3)
        {
            int ia = triangles[start];
            int ib = triangles[start + 1];
            int ic = triangles[start + 2];
            Vector3 barycentric;
            Vector3 point = ClosestPointOnTriangle(
                localQuery, vertices[ia], vertices[ib], vertices[ic], out barycentric);
            float distanceSq = (point - localQuery).sqrMagnitude;
            if (distanceSq >= closestDistanceSq) continue;

            closestDistanceSq = distanceSq;
            closestTriangle = start / 3;
            closestBarycentric = barycentric;
            bestLocalPoint = point;
            bestLocalNormal = normals.Length == vertices.Length
                ? (normals[ia] * barycentric.x
                   + normals[ib] * barycentric.y
                   + normals[ic] * barycentric.z).normalized
                : Vector3.Cross(vertices[ib] - vertices[ia], vertices[ic] - vertices[ia]).normalized;
        }

        if (closestTriangle < 0) return false;
        worldPoint = meshTransform.TransformPoint(bestLocalPoint);
        worldNormal = meshTransform.TransformDirection(bestLocalNormal).normalized;
        closestDistanceSq = (worldPoint - worldQuery).sqrMagnitude;
        return true;
    }

    // Returns the closest point and barycentric coordinate on triangle ABC.
    static Vector3 ClosestPointOnTriangle(
        Vector3 p, Vector3 a, Vector3 b, Vector3 c, out Vector3 barycentric)
    {
        Vector3 ab = b - a;
        Vector3 ac = c - a;
        Vector3 ap = p - a;
        float d1 = Vector3.Dot(ab, ap);
        float d2 = Vector3.Dot(ac, ap);
        if (d1 <= 0f && d2 <= 0f)
        {
            barycentric = new Vector3(1f, 0f, 0f);
            return a;
        }

        Vector3 bp = p - b;
        float d3 = Vector3.Dot(ab, bp);
        float d4 = Vector3.Dot(ac, bp);
        if (d3 >= 0f && d4 <= d3)
        {
            barycentric = new Vector3(0f, 1f, 0f);
            return b;
        }

        float vc = d1 * d4 - d3 * d2;
        if (vc <= 0f && d1 >= 0f && d3 <= 0f)
        {
            float v = d1 / (d1 - d3);
            barycentric = new Vector3(1f - v, v, 0f);
            return a + ab * v;
        }

        Vector3 cp = p - c;
        float d5 = Vector3.Dot(ab, cp);
        float d6 = Vector3.Dot(ac, cp);
        if (d6 >= 0f && d5 <= d6)
        {
            barycentric = new Vector3(0f, 0f, 1f);
            return c;
        }

        float vb = d5 * d2 - d1 * d6;
        if (vb <= 0f && d2 >= 0f && d6 <= 0f)
        {
            float w = d2 / (d2 - d6);
            barycentric = new Vector3(1f - w, 0f, w);
            return a + ac * w;
        }

        float va = d3 * d6 - d5 * d4;
        if (va <= 0f && (d4 - d3) >= 0f && (d5 - d6) >= 0f)
        {
            float w = (d4 - d3) / ((d4 - d3) + (d5 - d6));
            barycentric = new Vector3(0f, 1f - w, w);
            return b + (c - b) * w;
        }

        float denominator = 1f / (va + vb + vc);
        float faceV = vb * denominator;
        float faceW = vc * denominator;
        barycentric = new Vector3(1f - faceV - faceW, faceV, faceW);
        return a + ab * faceV + ac * faceW;
    }

    static string GetDominantBoneName(SurfaceHit hit)
    {
        Mesh source = hit.renderer != null ? hit.renderer.sharedMesh : null;
        if (source == null || hit.triangleIndex < 0) return "Unknown";
        int[] triangles = hit.sourceTriangles ?? source.triangles;
        BoneWeight[] weights = hit.sourceWeights ?? source.boneWeights;
        int triangleStart = hit.triangleIndex * 3;
        if (triangleStart + 2 >= triangles.Length || weights.Length != source.vertexCount)
            return "Unknown";

        var totals = new Dictionary<int, float>();
        int[] vertices =
        {
            triangles[triangleStart],
            triangles[triangleStart + 1],
            triangles[triangleStart + 2],
        };
        float baryX = hit.barycentricCoordinate.x;
        float baryY = hit.barycentricCoordinate.y;
        float baryZ = hit.barycentricCoordinate.z;
        for (int corner = 0; corner < 3; corner++)
        {
            BoneWeight weight = weights[vertices[corner]];
            float bary = corner == 0 ? baryX : corner == 1 ? baryY : baryZ;
            AddBone(totals, weight.boneIndex0, weight.weight0 * bary);
            AddBone(totals, weight.boneIndex1, weight.weight1 * bary);
            AddBone(totals, weight.boneIndex2, weight.weight2 * bary);
            AddBone(totals, weight.boneIndex3, weight.weight3 * bary);
        }

        int bestIndex = -1;
        float bestWeight = 0f;
        foreach (var pair in totals)
        {
            if (pair.Value <= bestWeight) continue;
            bestIndex = pair.Key;
            bestWeight = pair.Value;
        }
        return bestIndex >= 0 && bestIndex < hit.renderer.bones.Length && hit.renderer.bones[bestIndex] != null
            ? $"{hit.renderer.bones[bestIndex].name} ({bestWeight:P0})"
            : "Unknown";
    }

    static void AddBone(Dictionary<int, float> totals, int boneIndex, float weight)
    {
        if (weight <= 0f) return;
        totals.TryGetValue(boneIndex, out float current);
        totals[boneIndex] = current + weight;
    }

    void BakeTarget(ProbeTarget target)
    {
        target.renderer.BakeMesh(target.bakedMesh, true);
        Transform source = target.renderer.transform;
        target.colliderObject.transform.SetPositionAndRotation(source.position, source.rotation);
        target.colliderObject.transform.localScale = Vector3.one;
        target.collider.sharedMesh = null;
        target.collider.sharedMesh = target.bakedMesh;
    }

    void OnDestroy()
    {
        CleanupTargets();
    }

    void CleanupTargets()
    {
        foreach (var target in targets)
        {
            if (target.colliderObject != null) Destroy(target.colliderObject);
            if (target.bakedMesh != null) Destroy(target.bakedMesh);
        }
        targets.Clear();
    }
}
