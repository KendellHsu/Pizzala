using UnityEditor;
using UnityEngine;
using PizzaVR.XR;
using PizzaVR.Gameplay;
using PizzaVR.Core;

// Everything the player is: head + hand tracking, hit detection, and the throw logic. Kept as
// its own prefab (rather than built straight into the scene) so a teammate can open and tweak
// the rig without touching Restaurant.unity - one less way for scene-file merges to collide.
public static class XRRigPrefabBuilder
{
    public const string PrefabPath = "Assets/Prefabs/XRRig.prefab";

    [MenuItem("Tools/Pizza VR/Build XR Rig Prefab")]
    public static GameObject BuildPrefab()
    {
        var config = GameBalanceConfigBuilder.EnsureConfig();
        var pizzaPrefab = PizzaPrefabBuilder.EnsurePrefab();

        var xrOrigin = new GameObject("XR Origin");
        var bootstrap = xrOrigin.AddComponent<XRBootstrap>();

        var cameraOffset = new GameObject("Camera Offset");
        cameraOffset.transform.SetParent(xrOrigin.transform);
        bootstrap.rigRoot = cameraOffset.transform;

        var mainCamGO = new GameObject("Main Camera");
        mainCamGO.transform.SetParent(cameraOffset.transform);
        mainCamGO.tag = "MainCamera";
        mainCamGO.AddComponent<Camera>();
        mainCamGO.AddComponent<AudioListener>();

        var headTracker = mainCamGO.AddComponent<XRControllerInput>();
        headTracker.positionAction = XRActionFinder.FindAction("XRI Head", "Position");
        headTracker.rotationAction = XRActionFinder.FindAction("XRI Head", "Rotation");

        var headHitbox = mainCamGO.AddComponent<SphereCollider>();
        headHitbox.isTrigger = true;
        headHitbox.radius = 0.15f;
        var hitDetector = mainCamGO.AddComponent<PlayerHitDetector>();
        hitDetector.config = config;

        var leftHandInput = CreateControllerCube("LeftHand Controller", cameraOffset.transform, "Left", new Color(0.2f, 0.6f, 1f));
        var rightHandInput = CreateControllerCube("RightHand Controller", cameraOffset.transform, "Right", new Color(1f, 0.4f, 0.2f));

        var thrower = xrOrigin.AddComponent<PizzaThrower>();
        thrower.leftHand = leftHandInput;
        thrower.rightHand = rightHandInput;
        thrower.pizzaPrefab = pizzaPrefab;
        thrower.config = config;

        if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
            AssetDatabase.CreateFolder("Assets", "Prefabs");

        var prefab = PrefabUtility.SaveAsPrefabAsset(xrOrigin, PrefabPath);
        Object.DestroyImmediate(xrOrigin);

        Debug.Log("XRRigPrefabBuilder: XR Rig prefab created at " + PrefabPath);
        return prefab;
    }

    // Code-generated placeholder prefab - always rebuild to stay in sync with the scripts.
    public static GameObject EnsurePrefab() => BuildPrefab();

    // hand: "Left" or "Right" - matches the "XRI Left"/"XRI Right" map names in XRI 3.x's
    // default input actions (renamed from "XRI LeftHand"/"XRI RightHand" in XRI 2.x).
    static XRControllerInput CreateControllerCube(string name, Transform parent, string hand, Color color)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent);
        go.transform.localScale = new Vector3(0.05f, 0.05f, 0.12f);
        Object.DestroyImmediate(go.GetComponent<Collider>());
        var tracker = go.AddComponent<XRControllerInput>();
        string map = $"XRI {hand}";
        tracker.positionAction = XRActionFinder.FindAction(map, "Position");
        tracker.rotationAction = XRActionFinder.FindAction(map, "Rotation");
        tracker.gripValueAction = XRActionFinder.FindAction($"{map} Interaction", "Select Value");
        tracker.triggerValueAction = XRActionFinder.FindAction($"{map} Interaction", "Activate Value");
        tracker.primaryButtonAction = XRActionFinder.FindAction($"{map} Interaction", "Primary Button");
        SetColor(go, color);
        return tracker;
    }

    static void SetColor(GameObject go, Color color)
    {
        var renderer = go.GetComponent<Renderer>();
        var mat = new Material(Shader.Find("Standard"));
        mat.color = color;
        renderer.sharedMaterial = mat;
    }
}
