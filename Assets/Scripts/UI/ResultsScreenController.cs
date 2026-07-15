// ─────────────────────────────────────────────────────────────
// ResultsScreenController.cs — Results screen (the core of the experiment manipulation!)
// Attach to: one World Space Canvas (placed 2m in front of the player, ~1.6m up),
//   with two panels underneath:
//   controlPanel      — Control: a plain stats text block (drag a TMP_Text into statsText)
//   experimentalPanel — Experimental: photo wall + boss note
//     photoGrid        — container with a Grid Layout Group
//     photoEntryPrefab — a RawImage prefab (can add a polaroid frame)
//     captionText      — heading text (TMP_Text)
//     bossNotePanel    — torn-paper note container
//     bossCommentText  — the LLM-generated boss comment
// GameManager calls Show() automatically when the round ends.
// ─────────────────────────────────────────────────────────────
using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Pizzala.Data;

namespace Pizzala.UI
{
    public class ResultsScreenController : MonoBehaviour
    {
        [Header("Control: Stats Only")]
        public GameObject controlPanel;
        public TMP_Text statsText;       // fixed labels column
        public TMP_Text statsValuesText; // measured values column, aligned row-for-row with statsText

        [Header("Experimental: Photo Wall")]
        public GameObject experimentalPanel;
        public Transform photoGrid;
        public GameObject photoEntryPrefab; // contains a RawImage
        public TMP_Text captionText;

        [Header("Experimental: Boss Note")]
        public GameObject bossNotePanel;    // torn-paper note container, placeholder color block for now
        public TMP_Text bossCommentText;    // filled in later by BossCommentService

        void Start()
        {
            if (controlPanel != null) controlPanel.SetActive(false);
            if (experimentalPanel != null) experimentalPanel.SetActive(false);
        }

        public void Show(SessionData session)
        {
            if (session.condition == ExperimentCondition.Control)
                ShowControl(session);
            else
                ShowExperimental(session);
        }

        // ── Control: plain performance stats, laid out as two aligned columns ──
        // (labels never change; values are what got measured - keeping them in
        // separate text blocks built row-by-row is what keeps the columns aligned)
        void ShowControl(SessionData session)
        {
            if (controlPanel != null) controlPanel.SetActive(true);
            var s = session.summary;
            var labels = new StringBuilder();
            var values = new StringBuilder();

            void Row(string label, string value)
            {
                labels.AppendLine(label);
                values.AppendLine(value);
            }

            // "Today's Score" is a separate, static heading in the prefab (ScoreHeading) -
            // it doesn't have a matching value row, so it must never be a line in these
            // two builders or every row below it drifts out of alignment.
            Row("Accuracy", $"{s.accuracyPercent:F0}% ({s.hits}/{s.totalThrows})");
            Row("Top Speed", $"{s.maxReleaseSpeed:F1} m/s");
            Row("Max Wrist Snap", $"{s.maxWristSnapDegPerSec:F0} deg/s");
            Row("Throw Style", $"BH x{s.backhandCount}  FH x{s.forehandCount}  OH x{s.overheadCount}  UH x{s.underhandCount}");
            Row("Sector Hits", $"L {s.leftHits}/{s.leftThrows}  C {s.centerHits}/{s.centerThrows}  R {s.rightHits}/{s.rightThrows}");
            if (s.avgReactionTime > 0f) Row("Avg Reaction", $"{s.avgReactionTime:F2}s");
            if (s.dodgeTotal > 0) Row("Dodges", $"{s.dodgeSuccess}/{s.dodgeTotal}");
            Row("Movement", $"{s.totalHeadDistance:F0} m");
            Row("Squats", $"{s.squatCount}");
            Row("Turns", $"{s.turnDegreesTotal:F0} deg");
            if (s.avgHeartRate > 0) Row("Heart Rate", $"avg {s.avgHeartRate} / max {s.maxHeartRate} bpm");
            // Calories deliberately omitted - we can't actually measure it, only guess.

            if (statsText != null) statsText.text = labels.ToString();
            if (statsValuesText != null) statsValuesText.text = values.ToString();
        }

        // ── Experimental: the messy photo wall ──
        void ShowExperimental(SessionData session)
        {
            if (experimentalPanel != null) experimentalPanel.SetActive(true);
            var s = session.summary;

            if (captionText != null)
                captionText.text = $"<size=130%><b>Tonight's Damage Report</b></size>\n" +
                                   $"You made a mess in <b>{s.dirtCount}</b> spots, " +
                                   $"hit <b>{session.customerFacePhotos.Count}</b> customers in the face" +
                                   (s.playerFaceHits > 0 ? $", and took <b>{s.playerFaceHits}</b> hits yourself" : "") + "!";

            var all = new List<PhotoRecord>();
            all.AddRange(session.customerFacePhotos);
            all.AddRange(session.playerFacePhotos);
            all.AddRange(session.environmentPhotos);

            foreach (var rec in all)
            {
                var tex = LoadPhoto(rec.path);
                if (tex == null) continue;
                var entry = Instantiate(photoEntryPrefab, photoGrid);
                var raw = entry.GetComponentInChildren<RawImage>();
                if (raw != null) raw.texture = tex;
                var caption = entry.GetComponentInChildren<TMP_Text>();
                if (caption != null)
                {
                    var ts = System.TimeSpan.FromSeconds(Mathf.Max(0f, rec.gameTime));
                    caption.text = string.IsNullOrEmpty(rec.caption)
                        ? $"{ts.Minutes}:{ts.Seconds:00}"
                        : $"{ts.Minutes}:{ts.Seconds:00} - {rec.caption}";
                }
                entry.transform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-6f, 6f)); // 拍立得歪斜感
            }
        }

        Texture2D LoadPhoto(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
            var tex = new Texture2D(2, 2);
            return tex.LoadImage(File.ReadAllBytes(path)) ? tex : null;
        }
    }
}
