// ─────────────────────────────────────────────────────────────
// SetupHistoryScene.cs — builds/resets Assets/Scenes/History.unity: the memorial-hall
// prototype where each pizza box opens one recorded round's three-page review
// (box click → sauce splat covers the view → review swaps in under it → sauce fades).
//
// Builds: camera + light + floor, the results canvas, a FaceSplatOverlay canvas
// (Screen Space - Camera at 0.35m so the sauce reads as "on your face" in VR, same
// recipe as BackBone's), a Demo object (loader/boss service/paging input), and one
// pizza box per entry in Boxes[] - each with a pixel-font date+time label parsed from
// its session file name, a collider + XRSimpleInteractable + PhotoBoxTrigger wired to
// its own PhotoBoxSequence.
//
// The box model comes from pizzaBox.fbx normalised to a real-world ~0.35m footprint
// (measured from renderer bounds at build time, so the fbx's own units don't matter);
// if the fbx is missing a plain cube stands in. No lid animation wired yet - the
// Animator slot on PhotoBoxSequence stays empty until the fbx's clip name is known.
//
// PC test: P cycles the boxes (DemoResultsLoader), U/I page, Space next page.
// Run: Tools > Pizzala > Setup History Scene. Safe to re-run (rebuilds those objects).
// ─────────────────────────────────────────────────────────────
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using TMPro;
using Pizzala.UI;
using Pizzala.LLM;
using Pizzala.DevTools;

namespace Pizzala.EditorTools
{
    public static class SetupHistoryScene
    {
        const string ScenePath = "Assets/Scenes/History.unity";
        const string CanvasPrefabPath = "Assets/Prefabs/UI/PZ_ResultsCanvas.prefab";
        const string BoxModelPath = "Assets/Art/Pizza/pizzaBox.fbx";
        const string FontAssetPath = "Assets/Prefabs/UI/LoRes9OTWide-Bold SDF.asset";
        const string SplatSpritePath = "Assets/Art/Splats/Margherita_splats_01.png";

        // Photo-rich samples from Data/sessions (most of the 20 only captured 1 photo).
        static readonly string[] Boxes =
        {
            "session_20260716_105321_Control.json", // 13 photos
            "session_20260716_104956_Control.json", // 11 photos
            "session_20260716_103828_Control.json", // 5 photos
        };

        const float BoxWidthMeters = 0.35f;
        const float BoxSpacing = 0.8f;
        const float BoxDistance = 1.5f;  // from the camera at the origin
        const float BoxHeight = 0.9f;    // counter-ish height so the label is readable

        [MenuItem("Tools/Pizzala/Setup History Scene")]
        public static void Run()
        {
            var canvasPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CanvasPrefabPath);
            if (canvasPrefab == null)
            {
                Debug.LogError($"SetupHistoryScene: prefab not found at {CanvasPrefabPath}");
                return;
            }
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);

            Scene scene;
            bool exists = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null;
            if (exists)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                foreach (var name in new[] { "PZ_ResultsCanvas", "Demo", "FaceSplatCanvas", "Boxes", "Floor" })
                {
                    var old = GameObject.Find(name);
                    if (old != null) Object.DestroyImmediate(old);
                }
                var oldController = Object.FindFirstObjectByType<ResultsScreenController>(FindObjectsInactive.Include);
                if (oldController != null) Object.DestroyImmediate(oldController.gameObject);
            }
            else
            {
                scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

                var camera = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
                camera.tag = "MainCamera";
                camera.transform.position = new Vector3(0f, 1.6f, 0f);

                var light = new GameObject("Directional Light", typeof(Light));
                light.GetComponent<Light>().type = LightType.Directional;
                light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            }

            var cam = Camera.main;

            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";

            // Results canvas, prefab default spot (0, 1.6, 2) - in front of the camera.
            var canvasInstance = (GameObject)PrefabUtility.InstantiatePrefab(canvasPrefab, scene);
            var resultsScreen = canvasInstance.GetComponent<ResultsScreenController>();

            var splatOverlay = BuildSplatCanvas(cam);

            var demo = new GameObject("Demo");
            var bossService = demo.AddComponent<BossCommentService>();
            var loader = demo.AddComponent<DemoResultsLoader>();
            loader.resultsScreen = resultsScreen;
            loader.bossCommentService = bossService;
            var pageInput = demo.AddComponent<ResultsPageInput>();
            pageInput.resultsScreen = resultsScreen;

            var boxesRoot = new GameObject("Boxes");
            float startX = -(Boxes.Length - 1) * BoxSpacing / 2f;
            for (int i = 0; i < Boxes.Length; i++)
                BuildBox(Boxes[i], i, boxesRoot.transform,
                         new Vector3(startX + i * BoxSpacing, BoxHeight, BoxDistance),
                         loader, splatOverlay, font);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"SetupHistoryScene: {(exists ? "reset" : "created")} {ScenePath} with {Boxes.Length} boxes. " +
                      "P cycles boxes, U/I page, Space next page.");
        }

        static FaceSplatOverlay BuildSplatCanvas(Camera cam)
        {
            var root = new GameObject("FaceSplatCanvas",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(FaceSplatOverlay));
            root.layer = LayerMask.NameToLayer("UI");
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 0.35f; // hugs the face in VR - same recipe as BackBone

            var imgGo = new GameObject("SplatImage", typeof(RectTransform), typeof(Image));
            imgGo.layer = root.layer;
            imgGo.transform.SetParent(root.transform, false);
            var rect = (RectTransform)imgGo.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var img = imgGo.GetComponent<Image>();
            img.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SplatSpritePath);
            img.raycastTarget = false;
            if (img.sprite == null)
                Debug.LogWarning($"SetupHistoryScene: splat sprite not found at {SplatSpritePath} - assign one on SplatImage by hand.");

            var overlay = root.GetComponent<FaceSplatOverlay>();
            overlay.splatImage = img;
            return overlay;
        }

        static void BuildBox(string sessionFile, int index, Transform parent, Vector3 position,
                             DemoResultsLoader loader, FaceSplatOverlay splat, TMP_FontAsset font)
        {
            GameObject box;
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(BoxModelPath);
            if (model != null)
            {
                box = (GameObject)PrefabUtility.InstantiatePrefab(model, parent);
                NormalizeScale(box, BoxWidthMeters);
            }
            else
            {
                box = GameObject.CreatePrimitive(PrimitiveType.Cube);
                box.transform.SetParent(parent, false);
                box.transform.localScale = new Vector3(BoxWidthMeters, 0.1f, BoxWidthMeters);
                Debug.LogWarning($"SetupHistoryScene: {BoxModelPath} not found - using a cube stand-in.");
            }
            box.name = $"Box_{index + 1:00}";
            box.transform.position = position;

            // The fbx may carry no collider; the interactable needs one to be clickable.
            if (box.GetComponentInChildren<Collider>() == null)
            {
                var col = box.AddComponent<BoxCollider>();
                var bounds = RendererBounds(box);
                if (bounds.HasValue)
                {
                    col.center = box.transform.InverseTransformPoint(bounds.Value.center);
                    col.size = box.transform.InverseTransformVector(bounds.Value.size);
                }
            }

            box.AddComponent<XRSimpleInteractable>();
            var sequence = box.AddComponent<PhotoBoxSequence>();
            sequence.sessionFileName = sessionFile;
            sequence.loader = loader;
            sequence.splatOverlay = splat;

            var trigger = box.AddComponent<PhotoBoxTrigger>();
            UnityEventTools.AddPersistentListener(trigger.onActivated, sequence.Play);

            BuildLabel(sessionFile, index, box.transform, font);
        }

        // "session_20260716_105321_Control.json" -> "07/16" + "10:53" - the date and
        // running number the boxes wear, straight from the save's own file name.
        static void BuildLabel(string sessionFile, int index, Transform box, TMP_FontAsset font)
        {
            var m = Regex.Match(sessionFile, @"session_\d{4}(\d{2})(\d{2})_(\d{2})(\d{2})");
            string text = m.Success
                ? $"#{index + 1:00}\n{m.Groups[1].Value}/{m.Groups[2].Value} {m.Groups[3].Value}:{m.Groups[4].Value}"
                : $"#{index + 1:00}";

            var canvasGo = new GameObject("Label", typeof(RectTransform), typeof(Canvas));
            canvasGo.layer = LayerMask.NameToLayer("UI");
            canvasGo.transform.SetParent(box, false);
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            var rect = (RectTransform)canvasGo.transform;
            rect.sizeDelta = new Vector2(300f, 160f);
            // Counteract whatever scale the box got from normalisation so the label is a
            // predictable real-world size (300 units * this = 0.3m wide).
            float inv = 0.001f / box.localScale.x;
            rect.localScale = Vector3.one * inv;
            rect.localPosition = new Vector3(0f, 0.25f / box.localScale.y, 0f); // floats above the box

            var tmpGo = new GameObject("Text", typeof(RectTransform));
            tmpGo.layer = canvasGo.layer;
            tmpGo.transform.SetParent(canvasGo.transform, false);
            var tmpRect = (RectTransform)tmpGo.transform;
            tmpRect.anchorMin = Vector2.zero;
            tmpRect.anchorMax = Vector2.one;
            tmpRect.offsetMin = Vector2.zero;
            tmpRect.offsetMax = Vector2.zero;
            var tmp = tmpGo.AddComponent<TextMeshProUGUI>();
            if (font != null) tmp.font = font;
            tmp.text = text;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 18;
            tmp.fontSizeMax = 64;
            // Face the camera at the origin (canvas is readable from the side its forward
            // points away from, so forward = away from the camera).
            canvasGo.transform.rotation = Quaternion.LookRotation(box.position - new Vector3(0f, box.position.y, 0f));
        }

        // Uniform-scales the model so its widest horizontal side is `targetWidth` metres -
        // measured from real renderer bounds, so the fbx's own export units are irrelevant.
        static void NormalizeScale(GameObject go, float targetWidth)
        {
            var bounds = RendererBounds(go);
            if (!bounds.HasValue) return;
            float widest = Mathf.Max(bounds.Value.size.x, bounds.Value.size.z);
            if (widest < 1e-5f) return;
            go.transform.localScale *= targetWidth / widest;
        }

        static Bounds? RendererBounds(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return null;
            var b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            return b;
        }
    }
}
