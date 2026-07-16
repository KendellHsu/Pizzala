// ─────────────────────────────────────────────────────────────
// BuildMiddlePanel.cs — builds the panel that duplicates ControlPanel's stats layout
// with a portrait box beside it. HISTORICAL: superseded by RestructurePagedResultsScreen.cs,
// which renames this panel to DataPortraitPanel (shared P1 for Middle & Experimental) and
// swaps the portrait placeholder for a real player-face-photo RawImage - run that tool
// after this one, not instead of it, if starting from scratch.
// Run from Unity: Tools > Pizzala > Build Middle Panel. Safe to re-run - replaces any
// existing MiddlePanel first.
// The exact pixel positions below are a starting guess (no live Unity preview while
// writing this) - open the prefab afterward and nudge things if the layout looks off.
// ─────────────────────────────────────────────────────────────
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Pizzala.UI;

namespace Pizzala.EditorTools
{
    public static class BuildMiddlePanel
    {
        const string PrefabPath = "Assets/Prefabs/UI/PZ_ResultsCanvas.prefab";

        [MenuItem("Tools/Pizzala/Build Middle Panel")]
        public static void Run()
        {
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);

            var controlPanel = root.transform.Find("ControlPanel");
            if (controlPanel == null)
            {
                Debug.LogError("BuildMiddlePanel: ControlPanel not found - run this after the results canvas is set up.");
                PrefabUtility.UnloadPrefabContents(root);
                return;
            }

            var existing = root.transform.Find("MiddlePanel");
            if (existing != null) Object.DestroyImmediate(existing.gameObject);

            var middlePanel = Object.Instantiate(controlPanel.gameObject, root.transform);
            middlePanel.name = "MiddlePanel";
            middlePanel.SetActive(false);

            var labels = middlePanel.transform.Find("LabelsText");
            var values = middlePanel.transform.Find("ValuesText");

            // Shift the two text columns right to make room for the boss portrait on the left.
            if (labels != null)
            {
                var rt = (RectTransform)labels;
                rt.anchoredPosition = new Vector2(-70, 0);
                rt.sizeDelta = new Vector2(220, 550);
            }
            if (values != null)
            {
                var rt = (RectTransform)values;
                rt.anchoredPosition = new Vector2(200, 0);
                rt.sizeDelta = new Vector2(300, 550);
            }

            var portraitGO = new GameObject("BossPortraitImage", typeof(RectTransform), typeof(Image));
            portraitGO.layer = middlePanel.layer;
            portraitGO.transform.SetParent(middlePanel.transform, false);
            portraitGO.transform.SetAsFirstSibling();
            var portraitRect = (RectTransform)portraitGO.transform;
            portraitRect.anchorMin = new Vector2(0.5f, 0.5f);
            portraitRect.anchorMax = new Vector2(0.5f, 0.5f);
            portraitRect.pivot = new Vector2(0.5f, 0.5f);
            portraitRect.anchoredPosition = new Vector2(-370, 0);
            portraitRect.sizeDelta = new Vector2(220, 260);
            var portraitImage = portraitGO.GetComponent<Image>();
            portraitImage.color = new Color(0.6f, 0.6f, 0.6f, 1f); // placeholder gray block

            // Field wiring is handled by RestructurePagedResultsScreen.cs (under the
            // renamed dataPortraitPanel/playerPortraitImage/portraitLabelsText/
            // portraitValuesText fields) - run that tool next.

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            PrefabUtility.UnloadPrefabContents(root);
            Debug.Log("BuildMiddlePanel: created MiddlePanel (portrait placeholder + stats columns). " +
                      "Run Restructure Paged Results Screen next to rename/wire it.");
        }
    }
}
