// ─────────────────────────────────────────────────────────────
// BuildMiddlePanel.cs — builds the Middle group's screen by duplicating ControlPanel's
// stats layout and adding a boss/chef portrait beside it (no art yet - solid color
// placeholder block; swap BossPortraitImage's sprite once real art exists). Wires the
// result into ResultsScreenController's middlePanel/middleLabelsText/middleValuesText/
// bossPortraitImage fields.
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

            var controller = root.GetComponent<ResultsScreenController>();
            if (controller != null)
            {
                controller.middlePanel = middlePanel;
                controller.middleLabelsText = labels != null ? labels.GetComponent<TMP_Text>() : null;
                controller.middleValuesText = values != null ? values.GetComponent<TMP_Text>() : null;
                controller.bossPortraitImage = portraitImage;
            }
            else
            {
                Debug.LogWarning("BuildMiddlePanel: ResultsScreenController not found on the prefab root - wire the new fields manually.");
            }

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            PrefabUtility.UnloadPrefabContents(root);
            Debug.Log("BuildMiddlePanel: created MiddlePanel (boss portrait placeholder + stats columns) and wired it to ResultsScreenController.");
        }
    }
}
