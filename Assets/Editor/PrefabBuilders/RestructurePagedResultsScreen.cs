// ─────────────────────────────────────────────────────────────
// RestructurePagedResultsScreen.cs — migrates PZ_ResultsCanvas.prefab to the paged
// design confirmed 2026-07-16 (Control=1 page; Middle=2 pages P1 portrait+data / P2
// photo wall; Experimental=3 pages, same P1+P2 plus P3 boss note):
//   - MiddlePanel -> DataPortraitPanel (P1, now shared by Middle & Experimental):
//     BossPortraitImage (a static color-block Image) is swapped for a RawImage
//     named PlayerPortraitImage, since P1 shows a REAL captured player-face photo,
//     not placeholder art - LabelsText/ValuesText renamed for clarity.
//   - ExperimentalPanel -> PhotoWallPanel (P2, now shared by Middle & Experimental).
//   - BossNotePanel is pulled OUT of ExperimentalPanel/PhotoWallPanel and reparented
//     as its own sibling page (P3) directly under the canvas - it has to be
//     independent from the photo wall panel, or ShowBossNote() setting it active
//     would do nothing while ShowPhotoWall()'s HideAllPanels() call keeps its
//     (now former) parent inactive.
//   - UI_Polaroid_png.png gets its TextureImporter switched to Sprite (it imported
//     as a plain Default texture - Image.sprite needs an actual Sprite, not a
//     bare Texture2D - so it wouldn't have been assignable as-is).
// All of this then gets wired into ResultsScreenController's renamed fields
// (dataPortraitPanel/playerPortraitImage/portraitLabelsText/portraitValuesText/
// photoWallPanel/bossNotePanel).
// Run from Unity: Tools > Pizzala > Restructure Paged Results Screen.
// Depends on Build Middle Panel having been run already. Safe to re-run.
// ─────────────────────────────────────────────────────────────
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Pizzala.UI;

namespace Pizzala.EditorTools
{
    public static class RestructurePagedResultsScreen
    {
        const string PrefabPath = "Assets/Prefabs/UI/PZ_ResultsCanvas.prefab";
        const string PolaroidTexturePath = "Assets/Prefabs/UI/UI_Polaroid_png.png";

        [MenuItem("Tools/Pizzala/Restructure Paged Results Screen")]
        public static void Run()
        {
            FixPolaroidSpriteImport();

            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            var controller = root.GetComponent<ResultsScreenController>();
            if (controller == null)
            {
                Debug.LogError("RestructurePagedResultsScreen: ResultsScreenController not found on prefab root.");
                PrefabUtility.UnloadPrefabContents(root);
                return;
            }

            // ── P1: MiddlePanel becomes the shared player-photo + data panel ──
            var p1 = root.transform.Find("MiddlePanel");
            if (p1 != null)
            {
                p1.name = "DataPortraitPanel";

                var oldPortrait = p1.Find("BossPortraitImage");
                RawImage playerPortrait = null;
                if (oldPortrait != null)
                {
                    oldPortrait.name = "PlayerPortraitImage";
                    var rt = (RectTransform)oldPortrait;
                    Vector2 pos = rt.anchoredPosition, size = rt.sizeDelta;
                    var oldImage = oldPortrait.GetComponent<Image>();
                    if (oldImage != null) Object.DestroyImmediate(oldImage);
                    playerPortrait = oldPortrait.gameObject.AddComponent<RawImage>();
                    rt.anchoredPosition = pos; // AddComponent can reset these on some Unity versions - reassert
                    rt.sizeDelta = size;
                }

                var labels = p1.Find("LabelsText");
                if (labels != null) labels.name = "PortraitLabelsText";
                var values = p1.Find("ValuesText");
                if (values != null) values.name = "PortraitValuesText";

                controller.dataPortraitPanel = p1.gameObject;
                controller.playerPortraitImage = playerPortrait;
                controller.portraitLabelsText = labels != null ? labels.GetComponent<TMP_Text>() : null;
                controller.portraitValuesText = values != null ? values.GetComponent<TMP_Text>() : null;
            }
            else Debug.LogWarning("RestructurePagedResultsScreen: MiddlePanel not found - run Build Middle Panel first.");

            // ── P2/P3 split: BossNotePanel becomes its own sibling page instead of a
            // child of the photo wall panel (see header comment for why). ──
            var p2 = root.transform.Find("ExperimentalPanel");
            if (p2 != null)
            {
                p2.name = "PhotoWallPanel";
                var bossNote = p2.Find("BossNotePanel");
                if (bossNote != null)
                {
                    bossNote.SetParent(root.transform, worldPositionStays: false);
                    controller.bossNotePanel = bossNote.gameObject;
                }

                controller.photoWallPanel = p2.gameObject;
            }
            else Debug.LogWarning("RestructurePagedResultsScreen: ExperimentalPanel not found.");

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            PrefabUtility.UnloadPrefabContents(root);
            Debug.Log("RestructurePagedResultsScreen: P1 (portrait+data), P2 (photo wall), " +
                      "P3 (boss note, now an independent sibling page) rewired.");
        }

        static void FixPolaroidSpriteImport()
        {
            var importer = AssetImporter.GetAtPath(PolaroidTexturePath) as TextureImporter;
            if (importer == null)
            {
                Debug.LogWarning($"RestructurePagedResultsScreen: no TextureImporter at {PolaroidTexturePath}");
                return;
            }
            if (importer.textureType == TextureImporterType.Sprite) return; // already fixed
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
            Debug.Log("RestructurePagedResultsScreen: UI_Polaroid_png.png reimported as a Sprite.");
        }
    }
}
