// ─────────────────────────────────────────────────────────────
// BuildPhotoWallSlots.cs — replaces the auto-arranged GridLayoutGroup photo wall with
// 8 pre-placed PZ_PhotoEntry instances that the user can freely drag into a scattered
// "messy pile" look in the Editor - positions/rotations are hand-set and persist in the
// prefab. This is the MAX (8-photo) layout; ResultsScreenController fills however many
// slots it actually has photos for (min 2, max 8) and scales the active ones up/inward
// toward center as the count drops, so it never needs a separate layout per count.
// Removes PhotoGrid's GridLayoutGroup (it would otherwise force children back into a
// grid every layout pass, undoing any manual dragging), nudges PhotoGrid itself down
// for more clearance from CaptionText (the title sits at y=370, PhotoGrid moves from
// y=60 to y=-40 here), and wires the 8 slots into ResultsScreenController's
// photoWallSlots array.
// IMPORTANT: at low photo counts each slot scales UP (see ResultsScreenController's
// sizeScaleAtMin) - a slot whose position shrinks toward center can still grow tall
// enough to reach higher than its own un-scaled position, so "stays clear of the
// title" needs real vertical margin below y=0, not just being below the title at the
// slots' original (unscaled) position. The layout below keeps every slot's Y between
// -190 and +70 for this reason.
// Run from Unity: Tools > Pizzala > Build Photo Wall Slots.
// Depends on Apply Polaroid Frame having been run (so PZ_PhotoEntry already has the
// polaroid look). Safe to re-run - replaces any previously-built slots, but if you've
// already hand-positioned them, re-running will reset those positions back to the
// scattered defaults below, so only re-run this before you start arranging.
// ─────────────────────────────────────────────────────────────
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Pizzala.UI;

namespace Pizzala.EditorTools
{
    public static class BuildPhotoWallSlots
    {
        const string ResultsCanvasPath = "Assets/Prefabs/UI/PZ_ResultsCanvas.prefab";
        const string PhotoEntryPath = "Assets/Prefabs/UI/PZ_PhotoEntry.prefab";

        // Scattered starting positions/rotations, Y kept within -190..70 (see header comment
        // on why that margin matters) - PhotoSlot1/2 (the two shown at the lowest count) are
        // pushed far apart left/right so they don't overlap even after scaling up. Just a
        // starting point, drag them wherever looks right.
        static readonly (string name, Vector2 pos, float rot)[] SlotLayout =
        {
            ("PhotoSlot1", new Vector2(-260, -40), -8f),
            ("PhotoSlot2", new Vector2(260, -20), 6f),
            ("PhotoSlot3", new Vector2(-140, 60), 5f),
            ("PhotoSlot4", new Vector2(140, 70), -6f),
            ("PhotoSlot5", new Vector2(-80, -180), -4f),
            ("PhotoSlot6", new Vector2(80, -190), 9f),
            ("PhotoSlot7", new Vector2(-320, -170), 3f),
            ("PhotoSlot8", new Vector2(320, -160), -3f),
        };

        static readonly Vector2 PhotoGridPosition = new Vector2(0, -40); // was (0, 60) - moved down for title clearance

        [MenuItem("Tools/Pizzala/Build Photo Wall Slots")]
        public static void Run()
        {
            var photoEntryPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PhotoEntryPath);
            if (photoEntryPrefab == null)
            {
                Debug.LogError($"BuildPhotoWallSlots: no prefab at {PhotoEntryPath}");
                return;
            }

            var root = PrefabUtility.LoadPrefabContents(ResultsCanvasPath);
            var grid = root.transform.Find("PhotoWallPanel/PhotoGrid")
                       ?? root.transform.Find("ExperimentalPanel/PhotoGrid"); // fallback if run before the rename
            if (grid == null)
            {
                Debug.LogError("BuildPhotoWallSlots: PhotoGrid not found under PhotoWallPanel/ExperimentalPanel.");
                PrefabUtility.UnloadPrefabContents(root);
                return;
            }

            var layoutGroup = grid.GetComponent<GridLayoutGroup>();
            if (layoutGroup != null) Object.DestroyImmediate(layoutGroup); // would fight manual dragging otherwise

            ((RectTransform)grid).anchoredPosition = PhotoGridPosition;

            // Clear any previously-built slots (this tool's own, or the old split naming) before rebuilding.
            for (int i = grid.childCount - 1; i >= 0; i--)
            {
                var child = grid.GetChild(i);
                if (child.name.StartsWith("PhotoSlot") || child.name.StartsWith("CustomerSlot") || child.name.StartsWith("EnvironmentSlot"))
                    Object.DestroyImmediate(child.gameObject);
            }

            var slots = new GameObject[SlotLayout.Length];
            for (int i = 0; i < SlotLayout.Length; i++)
                slots[i] = SpawnSlot(photoEntryPrefab, grid, SlotLayout[i]);

            var controller = root.GetComponent<ResultsScreenController>();
            if (controller != null)
            {
                controller.photoWallSlots = slots;
            }
            else
            {
                Debug.LogWarning("BuildPhotoWallSlots: ResultsScreenController not found - wire photoWallSlots manually.");
            }

            PrefabUtility.SaveAsPrefabAsset(root, ResultsCanvasPath);
            PrefabUtility.UnloadPrefabContents(root);
            Debug.Log($"BuildPhotoWallSlots: created {SlotLayout.Length} photo slots and wired them to " +
                      "ResultsScreenController.photoWallSlots. Drag them around in Prefab Mode to arrange the pile.");
        }

        static GameObject SpawnSlot(GameObject photoEntryPrefab, Transform parent, (string name, Vector2 pos, float rot) layout)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(photoEntryPrefab, parent);
            instance.name = layout.name;
            var rt = (RectTransform)instance.transform;
            rt.anchoredPosition = layout.pos;
            rt.localRotation = Quaternion.Euler(0f, 0f, layout.rot);
            return instance;
        }
    }
}
