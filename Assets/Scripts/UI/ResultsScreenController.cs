// ─────────────────────────────────────────────────────────────
// ResultsScreenController.cs — Results screen (the core of the experiment manipulation!)
// Attach to: one World Space Canvas (placed 2m in front of the player, ~1.6m up),
//   with three panels underneath:
//   controlPanel      — Control: a plain stats text block (drag a TMP_Text into statsText)
//   middlePanel       — Middle: boss/chef portrait (art TBD, placeholder block for now)
//                        beside the same two-column stats as Control - no photo wall, no LLM
//   experimentalPanel — Experimental only: photo wall + LLM boss note
//     photoGrid        — container with a Grid Layout Group
//     photoEntryPrefab — a RawImage prefab (can add a polaroid frame)
//     captionText      — heading text (TMP_Text)
//     bossNotePanel    — torn-paper note container (no background frame behind it - the
//                        note graphic itself is the whole visual, nothing else needed)
//     bossCommentText  — the LLM-generated boss comment
// Three conditions: Control (stats only) / Middle (stats + boss portrait, no photos/LLM) /
// Experimental (photo wall + LLM boss note) - GameManager only calls BossCommentService
// for Experimental. GameManager calls Show() automatically when the round ends.
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
        // Photo wall aims for this many representative shots (sketch called for "5-7") -
        // picked evenly across the round's timeline rather than just the first N taken.
        const int MaxPhotoWallEntries = 6;

        [Header("Shared")]
        public GameObject backgroundPanel; // white rounded backdrop - hidden for Experimental (sketch: 不用背景框)

        [Header("Control: Stats Only")]
        public GameObject controlPanel;
        public TMP_Text statsText;       // fixed labels column
        public TMP_Text statsValuesText; // measured values column, aligned row-for-row with statsText

        [Header("Middle: Boss Portrait + Simple Data")]
        public GameObject middlePanel;
        public Image bossPortraitImage;  // no art yet - solid color placeholder, swap the sprite later
        public TMP_Text middleLabelsText;
        public TMP_Text middleValuesText;

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
            HideAll();
        }

        // Also called at the top of Show() - the demo loader can switch conditions
        // repeatedly in one session, so each Show() must start from a blank slate
        // (otherwise panels stack on top of each other and old polaroids pile up).
        void HideAll()
        {
            if (controlPanel != null) controlPanel.SetActive(false);
            if (middlePanel != null) middlePanel.SetActive(false);
            if (experimentalPanel != null) experimentalPanel.SetActive(false);
            if (backgroundPanel != null) backgroundPanel.SetActive(true); // default on; Experimental turns it off
            if (photoGrid != null)
                for (int i = photoGrid.childCount - 1; i >= 0; i--)
                    Destroy(photoGrid.GetChild(i).gameObject);
        }

        public void Show(SessionData session)
        {
            HideAll();
            switch (session.condition)
            {
                case ExperimentCondition.Control:
                    ShowControl(session);
                    break;
                case ExperimentCondition.Middle:
                    ShowMiddle(session);
                    break;
                default: // Experimental
                    ShowExperimental(session);
                    break;
            }
        }

        // Called by GameManager once BossCommentService's async request resolves
        // (success or fallback) - Show() already put a "writing..." placeholder up.
        public void SetBossComment(string text)
        {
            if (bossCommentText != null) bossCommentText.text = text;
        }

        // ── Control: plain performance stats, laid out as two aligned columns ──
        void ShowControl(SessionData session)
        {
            if (controlPanel != null) controlPanel.SetActive(true);
            BuildStatColumns(session.summary, out string labels, out string values);
            if (statsText != null) statsText.text = labels;
            if (statsValuesText != null) statsValuesText.text = values;
        }

        // ── Middle: same stats as Control, plus a boss/chef portrait - no photos, no LLM ──
        void ShowMiddle(SessionData session)
        {
            if (middlePanel != null) middlePanel.SetActive(true);
            BuildStatColumns(session.summary, out string labels, out string values);
            if (middleLabelsText != null) middleLabelsText.text = labels;
            if (middleValuesText != null) middleValuesText.text = values;
            // bossPortraitImage is a static placeholder set on the prefab - nothing to
            // update here until real boss/chef art exists.
        }

        // Shared by Control and Middle - keeping the row-building logic in one place is
        // what keeps both layouts' label/value columns aligned and in sync with each other.
        // (labels never change; values are what got measured - keeping them in separate
        // text blocks built row-by-row is what keeps each pair of columns aligned)
        static void BuildStatColumns(SessionSummary s, out string labels, out string values)
        {
            var labelsSb = new StringBuilder();
            var valuesSb = new StringBuilder();

            void Row(string label, string value)
            {
                labelsSb.AppendLine(label);
                valuesSb.AppendLine(value);
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

            labels = labelsSb.ToString();
            values = valuesSb.ToString();
        }

        // ── Experimental only: the messy photo wall + boss note ──
        void ShowExperimental(SessionData session)
        {
            if (experimentalPanel != null) experimentalPanel.SetActive(true);
            // Sketch note "不用背景框": the experimental screen floats directly over the
            // scene - polaroids and the boss note carry their own paper/frame visuals.
            if (backgroundPanel != null) backgroundPanel.SetActive(false);
            Debug.Log($"ResultsScreen: backgroundPanel after hide attempt - " +
                      $"{(backgroundPanel == null ? "field is NULL" : $"activeSelf={backgroundPanel.activeSelf}")}");
            var s = session.summary;

            if (bossNotePanel != null) bossNotePanel.SetActive(true);
            if (bossCommentText != null) bossCommentText.text = "The boss is writing a note...";

            if (captionText != null)
                captionText.text = $"<size=130%><b>Tonight's Damage Report</b></size>\n" +
                                   $"You made a mess in <b>{s.dirtCount}</b> spots, " +
                                   $"hit <b>{session.customerFacePhotos.Count}</b> customers in the face" +
                                   (s.playerFaceHits > 0 ? $", and took <b>{s.playerFaceHits}</b> hits yourself" : "") + "!";

            var all = new List<PhotoRecord>();
            all.AddRange(session.customerFacePhotos);
            all.AddRange(session.playerFacePhotos);
            all.AddRange(session.environmentPhotos);
            all.Sort((a, b) => a.gameTime.CompareTo(b.gameTime));

            int spawned = 0;
            foreach (var rec in CurateRepresentative(all, MaxPhotoWallEntries))
            {
                var tex = LoadPhoto(rec.path);
                if (tex == null)
                {
                    Debug.LogWarning($"ResultsScreen: photo failed to load: '{rec.path}' (exists: {File.Exists(rec.path ?? "")})");
                    continue;
                }
                spawned++;
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

            // Diagnostic breadcrumb while the "polaroids not showing" report is open -
            // this line tells us definitively whether the wall got populated.
            Debug.Log($"ResultsScreen: photo wall spawned {spawned}/{all.Count} polaroid(s) " +
                      $"(grid active: {photoGrid != null && photoGrid.gameObject.activeInHierarchy}, " +
                      $"entry prefab set: {photoEntryPrefab != null})");
        }

        // Picks up to `max` entries spread evenly across the (already time-sorted) list,
        // so the wall represents the whole round rather than just whatever was taken first.
        static List<PhotoRecord> CurateRepresentative(List<PhotoRecord> sorted, int max)
        {
            if (sorted.Count <= max) return sorted;
            var result = new List<PhotoRecord>(max);
            for (int i = 0; i < max; i++)
                result.Add(sorted[i * (sorted.Count - 1) / (max - 1)]);
            return result;
        }

        Texture2D LoadPhoto(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
            var tex = new Texture2D(2, 2);
            return tex.LoadImage(File.ReadAllBytes(path)) ? tex : null;
        }
    }
}
