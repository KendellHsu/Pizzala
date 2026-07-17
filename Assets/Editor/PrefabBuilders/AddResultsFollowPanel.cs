// ─────────────────────────────────────────────────────────────
// AddResultsFollowPanel.cs — gives PZ_ResultsCanvas the same lazy player-follow as the
// pause menu (PlayerFacingPanel), instead of sitting fixed at its prefab-baked spot.
// Values copied exactly from PZ_GameFlowUI's pause/start panel so all three menus (start,
// pause, results) drift back into view the same way.
// Run: Tools > Pizzala > Add Results Follow Panel. Safe to re-run (replaces the component).
// ─────────────────────────────────────────────────────────────
using UnityEditor;
using UnityEngine;
using Pizzala.UI;

namespace Pizzala.EditorTools
{
    public static class AddResultsFollowPanel
    {
        const string PrefabPath = "Assets/Prefabs/UI/PZ_ResultsCanvas.prefab";

        // Matches PZ_GameFlowUI's PlayerFacingPanel exactly (see BuildGameFlowUI.cs / the
        // pause menu) - same follow feel across every menu the player sees.
        const float Distance = 1.5f;
        const float HeightOffset = -0.1f;
        const float DeadzoneDegrees = 10f;
        const float FollowDelaySeconds = 0.3f;
        const float FollowSpeed = 6f;
        const float SettleDegrees = 1f;

        [MenuItem("Tools/Pizzala/Add Results Follow Panel")]
        public static void Run()
        {
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);

            var panel = root.GetComponent<PlayerFacingPanel>();
            if (panel == null) panel = root.AddComponent<PlayerFacingPanel>();

            panel.distance = Distance;
            panel.heightOffset = HeightOffset;
            panel.deadzoneDegrees = DeadzoneDegrees;
            panel.followDelaySeconds = FollowDelaySeconds;
            panel.followSpeed = FollowSpeed;
            panel.settleDegrees = SettleDegrees;

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            PrefabUtility.UnloadPrefabContents(root);
            Debug.Log("AddResultsFollowPanel: PZ_ResultsCanvas now follows the player the same way the pause menu does.");
        }
    }
}
