// ─────────────────────────────────────────────────────────────
// SetupBoothScreenDemo.cs — builds a bare scratch scene with just the booth status screen
// and a FreeCamera, for checking the follow behaviour in isolation. There's no booth model
// here, so it shows the motion but not how it sits on the real booth - for that, copy
// BackBone to a throwaway scene and use Setup Booth Preview (Current Scene) instead.
//
// Preview camera uses URP's FreeCamera: MOVE THE MOUSE to look around (= turning your
// head), WASD to move. That rotation is what the screen's follow logic reads.
// Run from Unity: Tools > Pizzala > Setup Booth Screen Demo. Safe to re-run.
// ─────────────────────────────────────────────────────────────
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering; // FreeCamera
using Pizzala.UI;
using Pizzala.DevTools;

namespace Pizzala.EditorTools
{
    public static class SetupBoothScreenDemo
    {
        const string ScenePath = "Assets/Scenes/BoothScreenDemo.unity";
        const string PrefabPath = "Assets/Prefabs/UI/PZ_BoothStatusScreen.prefab";

        [MenuItem("Tools/Pizzala/Setup Booth Screen Demo")]
        public static void Run()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                Debug.LogError($"SetupBoothScreenDemo: prefab not found at {PrefabPath} - run Build Booth Status Screen first.");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var boothCenter = new GameObject("BoothCenter");
            boothCenter.transform.position = Vector3.zero;

            var screenInstance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            var screen = screenInstance.GetComponent<BoothStatusScreen>();
            screen.boothCenter = boothCenter.transform;

            // The camera IS the head here - FreeCamera turns it with the mouse, and the
            // screen's follow logic reads that rotation.
            var cam = Object.FindFirstObjectByType<Camera>();
            if (cam == null) cam = new GameObject("PreviewHead", typeof(Camera)).GetComponent<Camera>();
            cam.gameObject.name = "PreviewHead";
            if (cam.GetComponent<FreeCamera>() == null) cam.gameObject.AddComponent<FreeCamera>();
            cam.transform.position = boothCenter.transform.position + Vector3.up * 1.6f;
            cam.transform.rotation = Quaternion.identity;
            cam.nearClipPlane = 0.05f; // the screen sits <1m away - default 0.3 would clip it
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.15f, 0.15f, 0.18f);

            screen.playerHead = cam.transform;

            var demoGO = new GameObject("BoothDemo");
            var demo = demoGO.AddComponent<BoothScreenDemo>();
            demo.screen = screen;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("SetupBoothScreenDemo: scene ready at " + ScenePath +
                      ". Press Play, then MOVE THE MOUSE to look around (Space = +1 hit).");
        }
    }
}
