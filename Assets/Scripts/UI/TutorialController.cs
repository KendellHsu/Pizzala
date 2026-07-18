// ─────────────────────────────────────────────────────────────
// TutorialController.cs — the 4 how-to-play videos shown when the game scene opens.
// Attach to: the PZ_TutorialCanvas root (a World Space Canvas in front of the player).
//
// GameFlowController owns the flow and the stick: it calls Begin() on entering the Tutorial
// state, NextPage()/PrevPage() as the player flicks the thumbstick, and reads IsOnLastPage
// to know when the trigger may confirm "Start Game". This component just swaps the clip and
// shows/hides the last-page Start Game prompt - it does no input reading of its own, so it
// can never fight GameFlowController over the same stick or trigger.
//
// Playback: one VideoPlayer rendering into a RenderTexture that a RawImage displays. Videos
// don't obey Time.timeScale, but the tutorial never runs while the game is paused, so that
// doesn't matter here. Each page loops its clip until the player flicks on.
// ─────────────────────────────────────────────────────────────
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace Pizzala.UI
{
    public class TutorialController : MonoBehaviour
    {
        [Header("Canvas root (toggled on/off)")]
        [Tooltip("The whole tutorial canvas/panel. Leave empty to toggle this GameObject itself.")]
        public GameObject canvasRoot;

        [Header("Playback")]
        public VideoPlayer videoPlayer;
        [Tooltip("Where the video shows up. If set, the controller wires the VideoPlayer to render into it - you don't have to set up a RenderTexture by hand.")]
        public RawImage videoImage;
        [Tooltip("The 4 how-to-play clips, in page order.")]
        public VideoClip[] pages;

        [Header("Last-page Start Game prompt")]
        [Tooltip("Shown only on the last page: the 'flick done, press trigger to start' button/label.")]
        public GameObject startGamePrompt;

        int currentPage;
        RenderTexture autoTexture; // created here if none is wired, freed in OnDestroy

        public int PageCount => pages != null ? pages.Length : 0;
        public bool IsOnLastPage => PageCount == 0 || currentPage >= PageCount - 1;

        void Awake() => EnsureVideoPipeline();

        // The #1 reason the tutorial shows nothing is a half-wired VideoPlayer: no RenderTexture,
        // or the RawImage pointing at a different texture. Rather than make that a manual setup
        // step everyone gets wrong, force the whole chain here: VideoPlayer -> RenderTexture ->
        // RawImage. Any piece already set by hand is respected; only the missing links are built.
        void EnsureVideoPipeline()
        {
            if (videoPlayer == null) videoPlayer = GetComponentInChildren<VideoPlayer>(true);
            if (videoPlayer == null)
            {
                Debug.LogError("[TutorialController] No VideoPlayer found - the tutorial can't play. " +
                               "Add a VideoPlayer under the canvas and assign it.");
                return;
            }

            videoPlayer.renderMode = VideoRenderMode.RenderTexture;
            videoPlayer.playOnAwake = false;
            videoPlayer.waitForFirstFrame = true;

            if (videoPlayer.targetTexture == null)
            {
                // 1080p is plenty for a UI panel; matches the RawImage regardless of its rect.
                autoTexture = new RenderTexture(1920, 1080, 0);
                videoPlayer.targetTexture = autoTexture;
            }

            if (videoImage == null) videoImage = GetComponentInChildren<RawImage>(true);
            if (videoImage != null) videoImage.texture = videoPlayer.targetTexture;
            else Debug.LogWarning("[TutorialController] No RawImage to show the video on - assign videoImage.");

            if (pages == null || pages.Length == 0)
                Debug.LogWarning("[TutorialController] pages is empty - assign the 4 tutorial VideoClips.");
        }

        // Reset to page 1 and show the canvas. Called by GameFlowController on entering Tutorial.
        public void Begin()
        {
            EnsureVideoPipeline(); // in case it wasn't ready at Awake (e.g. clips assigned late)
            SetCanvasActive(true);
            currentPage = 0;
            ShowPage(0);

            // One-shot diagnostic: makes "the tutorial isn't showing" answerable from the
            // Console instead of hunting in the Scene view - is the canvas actually on, where
            // is it in the world, and did the video pipeline come up?
            var root = canvasRoot != null ? canvasRoot : gameObject;
            Debug.Log($"[TutorialController] Begin: canvas '{root.name}' activeInHierarchy={root.activeInHierarchy} " +
                      $"worldPos={root.transform.position} scale={root.transform.lossyScale} | " +
                      $"videoPlayer={(videoPlayer != null ? videoPlayer.name : "NULL")} " +
                      $"targetTexture={(videoPlayer != null && videoPlayer.targetTexture != null ? "set" : "NULL")} " +
                      $"videoImage={(videoImage != null ? videoImage.name : "NULL")} pages={PageCount}");
        }

        public void HideCanvas() => SetCanvasActive(false);

        // Both no-op at the ends (not a carousel) - the last page is the gate to Start Game,
        // so wrapping around would let the player skip past the "you've seen it all" point.
        public void NextPage()
        {
            if (currentPage + 1 >= PageCount) return;
            ShowPage(currentPage + 1);
        }

        public void PrevPage()
        {
            if (currentPage <= 0) return;
            ShowPage(currentPage - 1);
        }

        void ShowPage(int page)
        {
            currentPage = page;

            if (videoPlayer != null && pages != null && page < pages.Length && pages[page] != null)
            {
                videoPlayer.Stop();
                videoPlayer.clip = pages[page];
                videoPlayer.isLooping = true;
                videoPlayer.Play();
            }
            else if (pages == null || page >= pages.Length || pages[page] == null)
            {
                Debug.LogWarning($"[TutorialController] page {page} has no clip assigned - screen will be blank.");
            }

            // The Start Game prompt only exists on the final page.
            if (startGamePrompt != null) startGamePrompt.SetActive(IsOnLastPage);
        }

        void SetCanvasActive(bool on)
        {
            var go = canvasRoot != null ? canvasRoot : gameObject;
            go.SetActive(on);
            if (!on && videoPlayer != null) videoPlayer.Stop();
        }

        void OnDestroy()
        {
            if (autoTexture != null) { autoTexture.Release(); Destroy(autoTexture); }
        }
    }
}
