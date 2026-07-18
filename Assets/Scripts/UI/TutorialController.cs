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
        [Tooltip("The 4 how-to-play clips, in page order.")]
        public VideoClip[] pages;

        [Header("Last-page Start Game prompt")]
        [Tooltip("Shown only on the last page: the 'flick done, press trigger to start' button/label.")]
        public GameObject startGamePrompt;

        int currentPage;

        public int PageCount => pages != null ? pages.Length : 0;
        public bool IsOnLastPage => PageCount == 0 || currentPage >= PageCount - 1;

        // Reset to page 1 and show the canvas. Called by GameFlowController on entering Tutorial.
        public void Begin()
        {
            SetCanvasActive(true);
            currentPage = 0;
            ShowPage(0);
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

            // The Start Game prompt only exists on the final page.
            if (startGamePrompt != null) startGamePrompt.SetActive(IsOnLastPage);
        }

        void SetCanvasActive(bool on)
        {
            var go = canvasRoot != null ? canvasRoot : gameObject;
            go.SetActive(on);
            if (!on && videoPlayer != null) videoPlayer.Stop();
        }
    }
}
