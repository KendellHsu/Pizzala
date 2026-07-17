// ─────────────────────────────────────────────────────────────
// SetupBoothScreenDemo.cs — drops a self-contained test rig for the booth status screen
// into a scratch scene so you can preview the follow behaviour without a headset:
//   - BoothCenter (empty at origin) = the ring the screen orbits
//   - FakeHead (empty) = stand-in for the VR camera; BoothScreenDemo rotates it with A/D
//   - a PZ_BoothStatusScreen instance, its playerHead/boothCenter wired to the above
//   - a plain Camera looking at the booth so you can watch it in Game view
//   - a Demo object with BoothScreenDemo driving it (A/D turn, Space = +1 hit, time ticks)
// Run from Unity: Tools > Pizzala > Setup Booth Screen Demo. Safe to re-run.
// ─────────────────────────────────────────────────────────────
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
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

            var fakeHead = new GameObject("FakeHead");
            fakeHead.transform.position = new Vector3(0, 1.5f, 0);

            var screenInstance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            var screen = screenInstance.GetComponent<BoothStatusScreen>();
            screen.boothCenter = boothCenter.transform;
            screen.playerHead = fakeHead.transform;

            // A camera behind the player looking forward, so the Game view shows the screen
            // as it slides around the ring.
            var cam = GameObject.FindFirstObjectByType<Camera>();
            if (cam == null)
            {
                var camGO = new GameObject("Demo Camera", typeof(Camera));
                cam = camGO.GetComponent<Camera>();
            }
            cam.transform.position = new Vector3(0, 1.6f, -1.4f);
            cam.transform.rotation = Quaternion.Euler(8f, 0f, 0f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.15f, 0.15f, 0.18f);

            var demoGO = new GameObject("Demo");
            var demo = demoGO.AddComponent<BoothScreenDemo>();
            demo.screen = screen;
            demo.fakeHead = fakeHead.transform;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("SetupBoothScreenDemo: scene ready at " + ScenePath +
                      ". Press Play, then A/D to turn (watch the screen follow), Space to add a hit.");
        }
    }
}
