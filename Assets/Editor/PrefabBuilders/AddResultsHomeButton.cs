// ─────────────────────────────────────────────────────────────
// AddResultsHomeButton.cs — clones the (hand-tuned) ShareButton under BossNotePanel into
// a HomeButton wearing UI_backtohome_button, places them side by side (home LEFT, share
// right), wires ResultsScreenController.homeButton, and makes sure BOTH start inactive so
// the post-note reveal (which flips all three on in the same frame) is what shows them.
//
// Cloning rather than rebuilding on purpose: the user restyled ShareButton by hand
// (sprite art, sizing) and the old AddResultsPostNoteButtons tool would stomp that.
// Also flips UI_backtohome_button.png's importer to Sprite (2D and UI) - it shipped as
// Default, which can't be assigned to a UI Image (same trap as the share art).
//
// Run: Tools > Pizzala > Add Results Home Button. Safe to re-run (replaces HomeButton).
// ─────────────────────────────────────────────────────────────
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Pizzala.UI;

namespace Pizzala.EditorTools
{
    public static class AddResultsHomeButton
    {
        const string PrefabPath = "Assets/Prefabs/UI/PZ_ResultsCanvas.prefab";
        const string HomeSpritePath = "Assets/Prefabs/UI/UI_backtohome_button.png";
        const float Gap = 60f; // canvas units between the two buttons

        [MenuItem("Tools/Pizzala/Add Results Home Button")]
        public static void Run()
        {
            var homeSprite = EnsureSprite(HomeSpritePath);

            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            var bossNotePanel = root.transform.Find("BossNotePanel");
            var share = bossNotePanel != null ? bossNotePanel.Find("ShareButton") : null;
            if (share == null)
            {
                Debug.LogError("AddResultsHomeButton: BossNotePanel/ShareButton not found.");
                PrefabUtility.UnloadPrefabContents(root);
                return;
            }

            var old = bossNotePanel.Find("HomeButton");
            if (old != null) Object.DestroyImmediate(old.gameObject);

            var home = Object.Instantiate(share.gameObject, bossNotePanel);
            home.name = "HomeButton";
            // Just under ShareButton in the hierarchy, so the Inspector reads sensibly.
            home.transform.SetSiblingIndex(share.GetSiblingIndex() + 1);

            var homeImg = home.GetComponent<Image>();
            if (homeImg != null && homeSprite != null) homeImg.sprite = homeSprite;

            // Side by side around the share button's hand-tuned spot: home left, share right.
            var shareRect = (RectTransform)share.transform;
            var homeRect = (RectTransform)home.transform;
            float centerX = shareRect.anchoredPosition.x;
            float halfStep = (shareRect.sizeDelta.x + Gap) / 2f;
            homeRect.anchoredPosition = new Vector2(centerX - halfStep, shareRect.anchoredPosition.y);
            shareRect.anchoredPosition = new Vector2(centerX + halfStep, shareRect.anchoredPosition.y);

            // Both hidden until the post-note reveal turns them on together.
            home.SetActive(false);
            share.gameObject.SetActive(false);

            var controller = root.GetComponent<ResultsScreenController>();
            if (controller != null) controller.homeButton = home;
            else Debug.LogWarning("AddResultsHomeButton: no ResultsScreenController on the prefab root - homeButton not wired.");

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            PrefabUtility.UnloadPrefabContents(root);
            Debug.Log("AddResultsHomeButton: HomeButton (left) + ShareButton (right) placed, both start hidden and reveal together after the note.");
        }

        // The png shipped with a Default importer; a UI Image can only take a Sprite.
        static Sprite EnsureSprite(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogWarning($"AddResultsHomeButton: {path} not found - HomeButton will keep the share sprite.");
                return null;
            }
            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
