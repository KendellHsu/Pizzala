// ─────────────────────────────────────────────────────────────
// SaucePaintable.cs — texture-space 醬料染色
// 把醬料直接畫進角色 albedo 的專屬 RenderTexture(UV 空間),
// 醬料像真的沾在衣服上,跟著蒙皮動畫變形,不會像 Decal 滑動。
// 材質維持 URP Lit 不用改(只是 _BaseMap 換成可寫入的 RT)。
// 只染模型子樹(Animator 底下),不會動到臉部換貼圖用的 Quad/圖示;
// SkinnedMeshRenderer 和一般 MeshRenderer(剛體部件)都支援。
// 由 DirtManager 在第一次砸中該客人時動態 AddComponent。
// 需求:Assets/Resources/Shaders/SauceUnwrapPaint.shader
// ─────────────────────────────────────────────────────────────
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Pizzala.Dirt
{
    public class SaucePaintable : MonoBehaviour
    {
        const int MaxTextureSize = 1024; // 角色貼圖更大時,染色 RT 降到這個上限省記憶體

        static Material paintMaterial;
        static readonly int PaintPosId = Shader.PropertyToID("_PaintPos");
        static readonly int PaintNormalId = Shader.PropertyToID("_PaintNormal");
        static readonly int PaintTangentId = Shader.PropertyToID("_PaintTangent");
        static readonly int PaintSizeId = Shader.PropertyToID("_PaintSize");
        static readonly int PaintDepthId = Shader.PropertyToID("_PaintDepth");
        static readonly int SplatTexId = Shader.PropertyToID("_SplatTex");
        static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        struct PaintTarget
        {
            public Renderer renderer;
            public int subMesh;
            public RenderTexture rt; // 所有權在 ownedRTs,這裡只是引用
        }

        readonly List<PaintTarget> targets = new List<PaintTarget>();
        readonly List<RenderTexture> ownedRTs = new List<RenderTexture>();
        readonly List<Material> ownedMats = new List<Material>();
        bool initialized;

        public static bool ShaderAvailable => GetPaintMaterial() != null;

        static Material GetPaintMaterial()
        {
            if (paintMaterial == null)
            {
                var shader = Resources.Load<Shader>("Shaders/SauceUnwrapPaint");
                if (shader != null && shader.isSupported) // 編譯失敗的 shader 視為不可用
                    paintMaterial = new Material(shader);
            }
            return paintMaterial;
        }

        // 把一塊醬料畫到命中點附近的表面;回傳是否具備染色條件
        public bool Paint(Vector3 point, Vector3 normal, Texture splatTex,
                          float size, float wrapDepth)
        {
            var mat = GetPaintMaterial();
            if (mat == null || splatTex == null) return false;

            EnsureTargets();
            Debug.Log($"[SaucePaintable] Paint 執行:target 數={targets.Count}");
            if (targets.Count == 0) return false;

            Vector3 n = normal.normalized;
            Vector3 t = Vector3.Cross(n, Random.onUnitSphere); // 隨機切線 = 隨機旋轉圖樣
            if (t.sqrMagnitude < 1e-4f) t = Vector3.Cross(n, Vector3.up);

            mat.SetVector(PaintPosId, point);
            mat.SetVector(PaintNormalId, n);
            mat.SetVector(PaintTangentId, t.normalized);
            mat.SetFloat(PaintSizeId, size);
            mat.SetFloat(PaintDepthId, wrapDepth);
            mat.SetTexture(SplatTexId, splatTex);

            var cmd = CommandBufferPool.Get("SaucePaint");
            foreach (var target in targets)
            {
                if (target.rt == null || target.renderer == null) continue;
                cmd.SetRenderTarget(target.rt);
                cmd.DrawRenderer(target.renderer, mat, target.subMesh, 0);
            }
            Graphics.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
            return true;
        }

        // 第一次染色時:把模型每顆材質的 albedo 換成可寫入的 RT。
        // 同一顆原始材質(多部件共用圖集)共用同一張 RT 與材質實例。
        void EnsureTargets()
        {
            if (initialized) return;
            initialized = true;

            // 只染模型子樹,避免動到臉部換貼圖的 Quad、口味圖示等 UI 面片
            var animator = GetComponentInChildren<Animator>();
            var modelRoot = animator != null ? animator.transform : transform;

            var allRenderers = modelRoot.GetComponentsInChildren<Renderer>(true);
            Debug.Log($"[SaucePaintable] {name}:animator={(animator != null ? animator.name : "無")}, modelRoot={modelRoot.name}, 底下 Renderer 數={allRenderers.Length}");

            var bySource = new Dictionary<Material, (Material inst, RenderTexture rt)>();

            foreach (var r in allRenderers)
            {
                var mesh = GetMesh(r);
                if (mesh == null)
                {
                    Debug.Log($"[SaucePaintable] 跳過 {r.name}({r.GetType().Name}):抓不到 mesh");
                    continue;
                }
                Debug.Log($"[SaucePaintable] 染 {r.name}({r.GetType().Name}), 材質數={r.sharedMaterials.Length}, subMesh={mesh.subMeshCount}");

                var mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    var src = mats[i];
                    if (src == null) continue;

                    if (!bySource.TryGetValue(src, out var entry))
                    {
                        entry.inst = new Material(src);
                        entry.rt = CreateAlbedoRT(src, entry.inst);
                        bySource[src] = entry;
                        ownedMats.Add(entry.inst);
                        ownedRTs.Add(entry.rt);
                    }

                    mats[i] = entry.inst;
                    changed = true;
                    if (i < mesh.subMeshCount)
                        targets.Add(new PaintTarget { renderer = r, subMesh = i, rt = entry.rt });
                }
                if (changed) r.sharedMaterials = mats;
            }
        }

        static Mesh GetMesh(Renderer r)
        {
            if (r is SkinnedMeshRenderer smr) return smr.sharedMesh;
            var mf = r.GetComponent<MeshFilter>();
            return mf != null ? mf.sharedMesh : null;
        }

        RenderTexture CreateAlbedoRT(Material src, Material inst)
        {
            var baseTex = src.HasProperty(BaseMapId) ? src.GetTexture(BaseMapId) : src.mainTexture;
            int w = baseTex != null ? Mathf.Min(baseTex.width, MaxTextureSize) : 512;
            int h = baseTex != null ? Mathf.Min(baseTex.height, MaxTextureSize) : 512;

            var rt = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32,
                                       RenderTextureReadWrite.sRGB)
            {
                name = $"SauceAlbedo_{src.name}",
                useMipMap = true,
                autoGenerateMips = true,
            };
            rt.Create();

            if (baseTex != null)
            {
                Graphics.Blit(baseTex, rt);
            }
            else
            {
                // 純色材質:把底色烘進 RT,材質實例改回白色免得乘兩次
                var baseColor = src.HasProperty(BaseColorId) ? src.GetColor(BaseColorId) : Color.white;
                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                GL.Clear(false, true, baseColor);
                RenderTexture.active = prev;
                if (inst.HasProperty(BaseColorId)) inst.SetColor(BaseColorId, Color.white);
            }

            if (inst.HasProperty(BaseMapId)) inst.SetTexture(BaseMapId, rt);
            else inst.mainTexture = rt;
            return rt;
        }

        void OnDestroy()
        {
            foreach (var rt in ownedRTs)
                if (rt != null) { rt.Release(); Destroy(rt); }
            foreach (var m in ownedMats)
                if (m != null) Destroy(m);
            ownedRTs.Clear();
            ownedMats.Clear();
            targets.Clear();
        }
    }
}
