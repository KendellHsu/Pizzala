// ─────────────────────────────────────────────────────────────
// AddBoothScoreboardShell.cs — drops scoreboard.fbx into PZ_BoothStatusScreen.prefab as a
// child "shell" wrapping the existing canvas, instead of rebuilding the prefab (which would
// wipe the hand-tuned BoothStatusScreen values - orbitRadius, angleOffsetDegrees, etc).
//
// Sizing: measured straight from the FBX's own vertex data (UnitScaleFactor 1 = cm,
// standard Y-up axes, so no axis-swap surprises) - X: -35..35, Y: 1.5841..47.5841,
// Z: -5..5 (cm), i.e. ~0.70 x 0.46 x 0.10m natural size. That's almost exactly our panel
// (0.624 x 0.384m) plus a uniform ~3.8cm border on every side (0.076m spare on both width
// AND height - not a coincidence, the art was sized to this panel), so it's used at its
// natural 1:1 scale, no resizing.
//
// The canvas root sits at localScale 0.001 (1000 canvas units = 1m - see
// BuildBoothStatusScreen.cs), so a child that wants to render at natural real-world size
// needs the inverse scale to cancel that out. The mesh's own pivot isn't centred (Y starts
// at 1.58, not 0 - modelled with a base-level pivot), so it also needs a vertical shift to
// land its visual centre on the canvas's local origin (which is the panel's centre).
//
// NOT verified here: which way the shell's front opening faces. Run from Unity:
// Tools > Pizzala > Add Booth Scoreboard Shell, then look at it in Scene view - if the
// frame faces the wrong way, rotate ScoreboardShell 180° on Y in the Inspector.
// ─────────────────────────────────────────────────────────────
using UnityEditor;
using UnityEngine;

namespace Pizzala.EditorTools
{
    public static class AddBoothScoreboardShell
    {
        const string PrefabPath = "Assets/Prefabs/UI/PZ_BoothStatusScreen.prefab";
        const string ModelPath = "Assets/Art/scene/scoreboard/scoreboard.fbx";
        const string ShellChildName = "ScoreboardShell";

        const float CanvasScale = 0.001f;       // matches the root Canvas's localScale
        const float MeshCenterYMeters = 0.245841f; // mesh's own Y bounding-box centre, off-pivot

        [MenuItem("Tools/Pizzala/Add Booth Scoreboard Shell")]
        public static void Run()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (model == null)
            {
                Debug.LogError($"AddBoothScoreboardShell: model not found at {ModelPath}");
                return;
            }

            var root = PrefabUtility.LoadPrefabContents(PrefabPath);

            var existing = root.transform.Find(ShellChildName);
            if (existing != null) Object.DestroyImmediate(existing.gameObject);

            var shell = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
            shell.name = ShellChildName;
            shell.transform.SetSiblingIndex(0);

            // Cancel the canvas's 0.001 so the shell renders at its natural ~0.70x0.46x0.10m.
            shell.transform.localScale = Vector3.one / CanvasScale;
            // Shift down by the mesh's own off-pivot Y centre, in the same (parent-scaled)
            // local space, so the shell's visual middle lands on the canvas's centre.
            shell.transform.localPosition = new Vector3(0f, -MeshCenterYMeters / CanvasScale, 0f);
            shell.transform.localRotation = Quaternion.identity;

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            PrefabUtility.UnloadPrefabContents(root);
            Debug.Log("AddBoothScoreboardShell: added scoreboard.fbx as a shell child of PZ_BoothStatusScreen. " +
                      "Facing direction not verified - check in Scene view, rotate 180 on Y if it's backwards.");
        }
    }
}
