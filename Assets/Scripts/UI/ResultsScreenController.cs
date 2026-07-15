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
        public TMP_Text statsText;

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

        // ── Control: plain performance stats ──
        void ShowControl(SessionData session)
        {
            if (controlPanel != null) controlPanel.SetActive(true);
            var s = session.summary;
            var sb = new StringBuilder();

            sb.AppendLine("<size=140%><b>本局表現</b></size>\n");
            sb.AppendLine($"準度:{s.accuracyPercent:F0}%({s.hits}/{s.totalThrows})");
            sb.AppendLine($"最快出手:{s.maxReleaseSpeed:F1} m/s");
            sb.AppendLine($"最快甩腕:{s.maxWristSnapDegPerSec:F0} °/s");
            sb.AppendLine($"出手方式:反手 ×{s.backhandCount} 正手 ×{s.forehandCount} 過頭 ×{s.overheadCount} 低手 ×{s.underhandCount}");
            sb.AppendLine($"各方位命中:左 {s.leftHits}/{s.leftThrows} 中 {s.centerHits}/{s.centerThrows} 右 {s.rightHits}/{s.rightThrows}");
            if (s.avgReactionTime > 0f)
                sb.AppendLine($"平均反應:{s.avgReactionTime:F2} 秒");
            if (s.dodgeTotal > 0)
                sb.AppendLine($"閃避:{s.dodgeSuccess}/{s.dodgeTotal}");
            sb.AppendLine($"\n身體活動:移動 {s.totalHeadDistance:F0} m/下蹲 {s.squatCount} 次/轉身 {s.turnDegreesTotal:F0}°");
            if (s.avgHeartRate > 0)
                sb.AppendLine($"心率:平均 {s.avgHeartRate}/最高 {s.maxHeartRate} bpm");
            sb.AppendLine($"估計消耗:{s.estimatedCalories:F0} kcal");

            if (statsText != null) statsText.text = sb.ToString();
        }

        // ── Experimental: the messy photo wall ──
        void ShowExperimental(SessionData session)
        {
            if (experimentalPanel != null) experimentalPanel.SetActive(true);
            var s = session.summary;

            if (captionText != null)
                captionText.text = $"<size=130%><b>今晚的災情紀錄</b></size>\n" +
                                   $"你把店弄髒了 <b>{s.dirtCount}</b> 處、" +
                                   $"砸中 <b>{session.customerFacePhotos.Count}</b> 張客人的臉" +
                                   (s.playerFaceHits > 0 ? $",自己也中了 <b>{s.playerFaceHits}</b> 發" : "") + "!";

            var all = new List<string>();
            all.AddRange(session.customerFacePhotos);
            all.AddRange(session.playerFacePhotos);
            all.AddRange(session.environmentPhotos);

            foreach (var path in all)
            {
                var tex = LoadPhoto(path);
                if (tex == null) continue;
                var entry = Instantiate(photoEntryPrefab, photoGrid);
                var raw = entry.GetComponentInChildren<RawImage>();
                if (raw != null) raw.texture = tex;
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
