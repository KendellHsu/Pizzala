// ─────────────────────────────────────────────────────────────
// SetupBoothPreview.cs — one-click "no headset" preview of the booth status screen in
// whatever scene is currently open (intended for a throwaway copy of BackBone, e.g.
// BackBone_BoothTest - do NOT run this on the real BackBone.unity, it adds objects and
// disables the XR cameras).
//
// Sets up:
//   - spawns PZ_BoothStatusScreen if the scene doesn't have one
//   - wires boothCenter to DJ_booth (found by name) and playerHead to a preview camera
//   - creates "PreviewHead" - a camera with URP's FreeCamera at head height in the booth,
//     so moving the MOUSE turns the view = simulates turning your head
//   - disables any other cameras so the preview one is what you see
//   - adds BoothScreenDemo (Space = +1 hit, time ticks down)
//
// With a real headset you don't need any of this: just point the screen's playerHead at
// the XR camera and the follow works off real head tracking.
// Run from Unity: Tools > Pizzala > Setup Booth Preview (Current Scene). Safe to re-run.
// ─────────────────────────────────────────────────────────────
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering; // FreeCamera
using UnityEngine.SceneManagement;
using Pizzala.UI;
using Pizzala.DevTools;

namespace Pizzala.EditorTools
{
    public static class SetupBoothPreview
    {
        const string PrefabPath = "Assets/Prefabs/UI/PZ_BoothStatusScreen.prefab";
        const string RealSceneName = "BackBone"; // the one we must never touch

        [MenuItem("Tools/Pizzala/Setup Booth Preview (Current Scene)")]
        public static void Run()
        {
            var scene = SceneManager.GetActiveScene();

            // Guard: this tool mutates the scene, so refuse to run on Kendell's real BackBone.
            if (scene.name == RealSceneName)
            {
                EditorUtility.DisplayDialog("Wrong scene",
                    "This is the real BackBone scene (Kendell's). Make a copy first " +
                    "(e.g. BackBone_BoothTest) and run this there instead.", "OK");
                return;
            }

            var screen = Object.FindFirstObjectByType<BoothStatusScreen>(FindObjectsInactive.Include);
            if (screen == null)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                if (prefab == null)
                {
                    Debug.LogError($"SetupBoothPreview: no screen in scene and no prefab at {PrefabPath} - run Build Booth Status Screen first.");
                    return;
                }
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                screen = inst.GetComponent<BoothStatusScreen>();
            }

            // boothCenter -> DJ_booth if we can find it; otherwise fall back to an empty at origin.
            if (screen.boothCenter == null)
            {
                var booth = GameObject.Find("DJ_booth");
                if (booth != null)
                {
                    screen.boothCenter = booth.transform;
                }
                else
                {
                    var center = GameObject.Find("BoothCenter") ?? new GameObject("BoothCenter");
                    center.transform.position = Vector3.zero;
                    screen.boothCenter = center.transform;
                    Debug.LogWarning("SetupBoothPreview: no 'DJ_booth' in this scene - using an empty at the origin as boothCenter.");
                }
            }

            // Preview camera: FreeCamera means mouse movement rotates it, which is exactly
            // the "turn your head" input the follow logic reads.
            var previewGO = GameObject.Find("PreviewHead");
            if (previewGO == null)
                previewGO = new GameObject("PreviewHead", typeof(Camera));
            if (previewGO.GetComponent<Camera>() == null) previewGO.AddComponent<Camera>();
            if (previewGO.GetComponent<FreeCamera>() == null) previewGO.AddComponent<FreeCamera>();

            // Stand where the player stands: in the booth, at head height.
            previewGO.transform.position = screen.boothCenter.position + Vector3.up * 1.6f;
            previewGO.transform.rotation = Quaternion.identity;

            var previewCam = previewGO.GetComponent<Camera>();
            previewCam.nearClipPlane = 0.05f; // the screen sits <1m away - default 0.3 could clip it
            previewCam.depth = 100;           // render on top of any leftover scene camera

            // Only the preview camera should render, or the XR camera fights it for the Game view.
            foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (cam != previewCam) cam.enabled = false;

            screen.playerHead = previewGO.transform;

            var demoGO = GameObject.Find("BoothDemo") ?? new GameObject("BoothDemo");
            var demo = demoGO.GetComponent<BoothScreenDemo>() ?? demoGO.AddComponent<BoothScreenDemo>();
            demo.screen = screen;

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("SetupBoothPreview: ready. Press Play, then MOVE THE MOUSE to look around " +
                      "(WASD moves, Space = +1 hit). The screen should slide around the booth ring to stay in front of you.");
            Selection.activeGameObject = screen.gameObject;
        }
    }
}
