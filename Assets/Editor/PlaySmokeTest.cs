using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using PizzaVR.Customers;

// Headless smoke test: enters Play Mode in batch mode, waits for the scene to run for a
// moment, logs what got spawned, then exits the process. Run with (no -quit flag):
//   Unity.exe -batchmode -projectPath <path> -executeMethod PlaySmokeTest.RunSpawnSmokeTest -logFile <path>
public static class PlaySmokeTest
{
    static bool loggedResults;
    static double enterTime;

    [MenuItem("Tools/Pizza VR/Run Spawn Smoke Test")]
    public static void RunSpawnSmokeTest()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Restaurant.unity");
        loggedResults = false;
        EditorApplication.update += OnUpdate;
        EditorApplication.isPlaying = true;
    }

    static void OnUpdate()
    {
        if (!EditorApplication.isPlaying || EditorApplication.isPaused)
            return;

        if (enterTime == 0)
            enterTime = EditorApplication.timeSinceStartup;

        if (loggedResults)
            return;

        if (Time.time > 1.5f)
        {
            loggedResults = true;
            var customers = Object.FindObjectsOfType<Customer>();
            Debug.Log($"SMOKE_TEST_COUNT={customers.Length}");
            foreach (var c in customers)
            {
                var p = c.HomePosition;
                float radius = new Vector2(p.x, p.z).magnitude;
                float angle = Mathf.Atan2(p.x, p.z) * Mathf.Rad2Deg;
                if (angle < 0) angle += 360f;
                Debug.Log($"SMOKE_TEST_CUSTOMER sector={c.Sector} distIdx={c.DistanceIndex} flavor={c.RequiredFlavor} angle={angle:F1} radius={radius:F2} pos=({p.x:F2},{p.y:F2},{p.z:F2})");
            }

            EditorApplication.update -= OnUpdate;
            EditorApplication.isPlaying = false;
            EditorApplication.delayCall += () => EditorApplication.Exit(0);
        }
    }
}
