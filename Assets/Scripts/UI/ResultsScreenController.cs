// ─────────────────────────────────────────────────────────────
// ResultsScreenController.cs — Results screen (the core of the experiment manipulation!)
// Attach to: one World Space Canvas (placed 2m in front of the player, ~1.6m up).
// Three conditions, shown as sequential PAGES within a condition (Show() jumps to page 0,
// NextPage() advances - hook NextPage() to whatever the real "next page" input ends up
// being; the Demo loader currently drives it with Space for testing):
//   Control (1 page):
//     P1 controlPanel      — plain stats (statsText/statsValuesText, two aligned columns)
//   Middle (2 pages):
//     P1 dataPortraitPanel — real captured player-face photo (playerPortraitImage) beside
//                            the same two-column stats (portraitLabelsText/ValuesText)
//     P2 photoWallPanel    — photoWallSlots: up to 8 pre-placed polaroid slots (hand-
//                            positioned/rotated in the prefab for a "messy pile" look, NOT
//                            auto-laid-out). Actual photo count varies a lot round to round
//                            (2-8ish), so only the first N slots activate - the active group
//                            is re-centered as a rigid body (translate only, spacing between
//                            cards never changes) and each card scales up as N drops, so a
//                            handful of photos still reads as "centered and full" rather than
//                            a few small cards adrift in empty space.
//                            (player-face photos live on P1 instead, not in this wall)
//   Experimental (3 pages): same P1 + P2 as Middle, plus:
//     P3 bossNotePanel     — LLM-generated boss comment (bossCommentText) - GameManager
//                            only calls BossCommentService for this condition. Once the
//                            real text lands (SetBossComment), shareButton/playAgainButton
//                            (children of bossNotePanel, so they're hidden/shown along with
//                            it for free) fade in after postNoteButtonDelay seconds - what
//                            they actually do on click isn't wired up yet.
// backgroundPanel (white rounded backdrop) is shown for Control/P1, hidden for the photo
// wall and boss note pages (sketch: 不用背景框).
// ─────────────────────────────────────────────────────────────
using System.Collections;
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
        [Header("Shared")]
        public GameObject backgroundPanel; // white rounded backdrop - hidden on the photo wall/boss note pages

        [Header("Control: Stats Only (1 page)")]
        public GameObject controlPanel;
        public TMP_Text statsText;       // fixed labels column
        public TMP_Text statsValuesText; // measured values column, aligned row-for-row with statsText

        [Header("P1: Player Photo + Data (Middle & Experimental)")]
        public GameObject dataPortraitPanel;
        public RawImage playerPortraitImage; // real captured player-face-hit photo, not static art
        public TMP_Text portraitLabelsText;
        public TMP_Text portraitValuesText;

        [Header("P2: Photo Wall (Middle & Experimental)")]
        public GameObject photoWallPanel;
        public TMP_Text captionText;
        // Pre-placed PZ_PhotoEntry instances, hand-positioned/rotated in the prefab for a
        // scattered "messy pile" look (max layout, i.e. what it looks like with all 8 full) -
        // not auto-laid-out. Combined customer+environment photos (sketch: min 2, max 8,
        // however many actually got captured) fill the first N slots in time order.
        public GameObject[] photoWallSlots;

        [Header("Photo Wall Scaling (fewer photos = bigger, group re-centered)")]
        [Tooltip("Below this many photos, sizing stops changing further.")]
        public int minPhotoCount = 2;
        [Tooltip("At or above this many photos, slots use their exact hand-set size.")]
        public int maxPhotoCount = 8;
        [Tooltip("Uniform scale multiplier applied at minPhotoCount (1 = unchanged). Spacing between cards never changes - only this size and the active group's centering do.")]
        public float sizeScaleAtMin = 1.0f;

        // Cached the first time the photo wall is shown - the design (hand-set) local
        // position/scale of each slot, so repeated Show() calls always scale from the same
        // baseline instead of compounding shrink/grow on top of the previous call's result.
        Vector2[] slotBaselinePositions;
        Vector3[] slotBaselineScales;
        // Slot array indices, ordered by spatial spread (computed once from the hand-set
        // positions - see ComputeSpreadOrder). displayOrder[0..N-1] is always the best N-card
        // spread available, regardless of what order the slots happen to sit in the array or
        // how they got dragged around - this is what makes "fewer photos, no more overlap"
        // work without the designer having to keep slot 1/2/3... in any particular priority.
        int[] displayOrder;

        [Header("P3: Boss Note (Experimental only)")]
        public GameObject bossNotePanel;    // torn-paper note container
        public TMP_Text bossCommentText;    // filled in later by BossCommentService

        [Header("P3: Post-Note Buttons (children of bossNotePanel, appear once the note is ready)")]
        public GameObject shareButton;
        public GameObject playAgainButton;
        [Tooltip("Seconds after the boss comment text arrives before the buttons appear.")]
        public float postNoteButtonDelay = 5f;

        SessionData currentSession;
        int currentPage;
        int pageCount;
        Coroutine postNoteButtonsRoutine;

        public bool HasNextPage => currentSession != null && currentPage + 1 < pageCount;
        public bool HasPrevPage => currentSession != null && currentPage > 0;

        void Start()
        {
            HideAllPanels();
        }

        // Fully clears the results screen - every panel including the background, unlike
        // HideAllPanels() which leaves the background on for the next page. Called by
        // GameFlowController when the player hits Play Again and we go back to the start
        // screen, so the results don't linger behind the start menu.
        public void Hide()
        {
            if (postNoteButtonsRoutine != null) { StopCoroutine(postNoteButtonsRoutine); postNoteButtonsRoutine = null; }
            if (shareButton != null) shareButton.SetActive(false);
            if (playAgainButton != null) playAgainButton.SetActive(false);
            HideAllPanels();
            if (backgroundPanel != null) backgroundPanel.SetActive(false);
            currentSession = null;
        }

        // Also called at the top of each page change - the demo loader can switch pages/
        // conditions repeatedly in one session, so every transition must start from a
        // blank slate (otherwise panels stack on top of each other and old polaroids pile up).
        void HideAllPanels()
        {
            if (controlPanel != null) controlPanel.SetActive(false);
            if (dataPortraitPanel != null) dataPortraitPanel.SetActive(false);
            if (photoWallPanel != null) photoWallPanel.SetActive(false);
            if (bossNotePanel != null) bossNotePanel.SetActive(false);
            if (backgroundPanel != null) backgroundPanel.SetActive(true); // default on; photo wall/boss note pages turn it off
            // Slots are permanent (hand-positioned in the prefab), not destroyed/recreated -
            // ShowPhotoWall() re-fills and shows/hides each one every time it runs.
        }

        public void Show(SessionData session)
        {
            currentSession = session;
            pageCount = session.condition switch
            {
                ExperimentCondition.Control => 1,
                ExperimentCondition.Middle => 2,
                _ => 3, // Experimental
            };
            // Reset here, not in HideAllPanels() - that also runs on every ordinary page
            // turn within the same session, which would cancel a reveal already in flight
            // (or already shown) just from paging P1 -> P2 -> P3.
            if (postNoteButtonsRoutine != null) { StopCoroutine(postNoteButtonsRoutine); postNoteButtonsRoutine = null; }
            if (shareButton != null) shareButton.SetActive(false);
            if (playAgainButton != null) playAgainButton.SetActive(false);

            ShowPage(0);
        }

        // Driven by GameFlowController off the right thumbstick (and by DemoResultsLoader
        // from the keyboard). Both no-op at the ends rather than wrapping around - the
        // pages are a report to read through, not a carousel.
        public void NextPage()
        {
            if (!HasNextPage) return;
            ShowPage(currentPage + 1);
        }

        public void PrevPage()
        {
            if (!HasPrevPage) return;
            ShowPage(currentPage - 1);
        }

        void ShowPage(int page)
        {
            currentPage = page;
            HideAllPanels();

            if (currentSession.condition == ExperimentCondition.Control)
            {
                ShowControl(currentSession);
                return;
            }

            // Middle and Experimental share P1 (portrait+data) and P2 (photo wall);
            // only Experimental reaches P3 (boss note) - pageCount already caps this.
            switch (page)
            {
                case 0: ShowDataPortrait(currentSession); break;
                case 1: ShowPhotoWall(currentSession); break;
                case 2: ShowBossNote(currentSession); break;
            }
        }

        // Called by GameManager once BossCommentService's async request resolves
        // (success or fallback) - the boss note page already put a "writing..." placeholder up.
        public void SetBossComment(string text)
        {
            if (bossCommentText != null) bossCommentText.text = text;

            if (postNoteButtonsRoutine != null) StopCoroutine(postNoteButtonsRoutine);
            postNoteButtonsRoutine = StartCoroutine(RevealPostNoteButtons());
        }

        // Buttons are children of bossNotePanel, so they only ever render while that page
        // is actually showing - no need to track which page the player is on here, Unity's
        // parent/child active-state cascading handles it for free.
        IEnumerator RevealPostNoteButtons()
        {
            yield return new WaitForSeconds(postNoteButtonDelay);
            if (shareButton != null) shareButton.SetActive(true);
            if (playAgainButton != null) playAgainButton.SetActive(true);
            postNoteButtonsRoutine = null;
        }

        // ── Control: plain performance stats, laid out as two aligned columns ──
        void ShowControl(SessionData session)
        {
            if (controlPanel != null) controlPanel.SetActive(true);
            BuildStatColumns(session.summary, out string labels, out string values);
            if (statsText != null) statsText.text = labels;
            if (statsValuesText != null) statsValuesText.text = values;
        }

        // ── P1: the same stats as Control, plus whatever player-face photo got captured ──
        void ShowDataPortrait(SessionData session)
        {
            if (dataPortraitPanel != null) dataPortraitPanel.SetActive(true);
            BuildStatColumns(session.summary, out string labels, out string values);
            if (portraitLabelsText != null) portraitLabelsText.text = labels;
            if (portraitValuesText != null) portraitValuesText.text = values;

            if (playerPortraitImage != null)
            {
                var photo = session.playerFacePhotos.Count > 0 ? session.playerFacePhotos[0] : null;
                var tex = photo != null ? LoadPhoto(photo.path) : null;
                playerPortraitImage.texture = tex;
                playerPortraitImage.gameObject.SetActive(tex != null); // no player-face capture this round -> hide, don't show a blank frame
            }
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

        // ── P2: photo wall - combined customer+environment photos fill the first N of up to
        // 8 pre-placed slots; fewer photos -> each slot scales up and the active group
        // re-centers as a whole (spacing between cards stays exactly as hand-placed).
        // Player-face photos live on P1 instead, so they're deliberately excluded here. ──
        void ShowPhotoWall(SessionData session)
        {
            if (photoWallPanel != null) photoWallPanel.SetActive(true);
            // The white background stays for this page - "不用背景框" was specifically
            // about the boss note (P3) sticky note, not the photo wall.
            if (backgroundPanel != null) backgroundPanel.SetActive(true);
            var s = session.summary;

            if (captionText != null)
                captionText.text = $"<size=130%><b>Tonight's Damage Report</b></size>\n" +
                                   $"You made a mess in <b>{s.dirtCount}</b> spots, " +
                                   $"hit <b>{session.customerFacePhotos.Count}</b> customers in the face!";

            CacheSlotBaselines();

            var combined = new List<PhotoRecord>();
            combined.AddRange(session.customerFacePhotos);
            combined.AddRange(session.environmentPhotos);
            var photos = CurateRepresentative(SortedByTime(combined), photoWallSlots?.Length ?? 0);

            int filled = ArrangeAndFillSlots(photos);
            Debug.Log($"ResultsScreen: photo wall filled {filled}/{photoWallSlots?.Length ?? 0} slot(s) " +
                      $"from {photos.Count} curated photo(s).");
        }

        // Slot Transforms get mutated every time the wall is shown (position/scale scaled
        // by count) - this records each slot's original hand-set values exactly once so
        // that scaling always starts from the same baseline instead of compounding.
        void CacheSlotBaselines()
        {
            if (slotBaselinePositions != null || photoWallSlots == null) return;
            slotBaselinePositions = new Vector2[photoWallSlots.Length];
            slotBaselineScales = new Vector3[photoWallSlots.Length];
            for (int i = 0; i < photoWallSlots.Length; i++)
            {
                if (photoWallSlots[i] == null) continue;
                var rt = (RectTransform)photoWallSlots[i].transform;
                slotBaselinePositions[i] = rt.anchoredPosition;
                slotBaselineScales[i] = rt.localScale;
            }
            displayOrder = ComputeSpreadOrder(slotBaselinePositions);
        }

        // Greedy farthest-point ordering: start from the slot closest to the overall
        // centroid, then repeatedly add whichever remaining slot is farthest from every
        // slot picked so far. Every prefix of the result (first 1, first 2, first 3...) is
        // therefore a well-spread subset, no matter what physical order the slots are in or
        // how they were dragged around in the Editor.
        static int[] ComputeSpreadOrder(Vector2[] positions)
        {
            int n = positions.Length;
            var order = new List<int>(n);
            var remaining = new List<int>(n);
            for (int i = 0; i < n; i++) remaining.Add(i);
            if (n == 0) return order.ToArray();

            Vector2 centroid = Vector2.zero;
            foreach (var p in positions) centroid += p;
            centroid /= n;

            int seed = remaining[0];
            float bestDist = float.MaxValue;
            foreach (var i in remaining)
            {
                float d = (positions[i] - centroid).sqrMagnitude;
                if (d < bestDist) { bestDist = d; seed = i; }
            }
            order.Add(seed);
            remaining.Remove(seed);

            while (remaining.Count > 0)
            {
                int best = remaining[0];
                float bestMinDist = -1f;
                foreach (var candidate in remaining)
                {
                    float minDist = float.MaxValue;
                    foreach (var chosen in order)
                        minDist = Mathf.Min(minDist, (positions[candidate] - positions[chosen]).sqrMagnitude);
                    if (minDist > bestMinDist) { bestMinDist = minDist; best = candidate; }
                }
                order.Add(best);
                remaining.Remove(best);
            }

            return order.ToArray();
        }

        // Fills the first `photos.Count` slots and hides the rest, growing each card's size
        // as the active count drops toward minPhotoCount, and re-centering the active GROUP
        // as a rigid body (translate only) so it sits in the middle of the screen. Crucially
        // this does NOT scale positions toward center - that would also shrink the gaps
        // between cards, which is the opposite of "keep their spacing, just center the set."
        // Spacing between any two active cards is always exactly what was hand-placed.
        int ArrangeAndFillSlots(List<PhotoRecord> photos)
        {
            if (photoWallSlots == null || displayOrder == null) return 0;

            int activeCount = photos.Count;
            int clamped = Mathf.Clamp(activeCount, minPhotoCount, maxPhotoCount);
            float t = maxPhotoCount > minPhotoCount ? (float)(clamped - minPhotoCount) / (maxPhotoCount - minPhotoCount) : 1f;
            float sizeScale = Mathf.Lerp(sizeScaleAtMin, 1f, t);

            // Centroid of the active subset's hand-set (baseline) positions - shifting every
            // active slot by this offset re-centers the whole group without touching the
            // distances between them. Uses displayOrder so the active subset is always the
            // best-spread one, not just whatever happens to be first in the array.
            int activeN = Mathf.Min(activeCount, displayOrder.Length);
            Vector2 centroid = Vector2.zero;
            for (int rank = 0; rank < activeN; rank++) centroid += slotBaselinePositions[displayOrder[rank]];
            if (activeN > 0) centroid /= activeN;

            int filled = 0;
            int photoCursor = 0;
            for (int rank = 0; rank < displayOrder.Length; rank++)
            {
                int idx = displayOrder[rank];
                var slot = photoWallSlots[idx];
                if (slot == null) continue;

                if (rank >= activeCount) { slot.SetActive(false); continue; }

                var rec = photos[photoCursor++];
                var tex = LoadPhoto(rec.path);
                if (tex == null)
                {
                    Debug.LogWarning($"ResultsScreen: photo failed to load: '{rec.path}' (exists: {File.Exists(rec.path ?? "")})");
                    slot.SetActive(false);
                    continue;
                }

                slot.SetActive(true);
                filled++;

                var rt = (RectTransform)slot.transform;
                rt.anchoredPosition = slotBaselinePositions[idx] - centroid; // rigid shift only - spacing unchanged
                rt.localScale = slotBaselineScales[idx] * sizeScale;

                var raw = slot.GetComponentInChildren<RawImage>();
                if (raw != null) raw.texture = tex;
                var caption = slot.GetComponentInChildren<TMP_Text>();
                if (caption != null)
                {
                    var ts = System.TimeSpan.FromSeconds(Mathf.Max(0f, rec.gameTime));
                    caption.text = string.IsNullOrEmpty(rec.caption)
                        ? $"{ts.Minutes}:{ts.Seconds:00}"
                        : $"{ts.Minutes}:{ts.Seconds:00} - {rec.caption}";
                }
            }
            return filled;
        }

        // ── P3: boss note + LLM comment (Experimental only) ──
        void ShowBossNote(SessionData session)
        {
            if (bossNotePanel != null) bossNotePanel.SetActive(true);
            if (backgroundPanel != null) backgroundPanel.SetActive(false);
            if (bossCommentText != null) bossCommentText.text = "The boss is writing a note...";
        }

        static List<PhotoRecord> SortedByTime(List<PhotoRecord> src)
        {
            var copy = new List<PhotoRecord>(src);
            copy.Sort((a, b) => a.gameTime.CompareTo(b.gameTime));
            return copy;
        }

        // Picks up to `max` entries spread evenly across the (already time-sorted) list,
        // so the selection represents the whole round rather than just whatever was taken first.
        static List<PhotoRecord> CurateRepresentative(List<PhotoRecord> sorted, int max)
        {
            if (max <= 0) return new List<PhotoRecord>();
            if (sorted.Count <= max) return sorted;
            if (max == 1) return new List<PhotoRecord> { sorted[sorted.Count / 2] };
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
