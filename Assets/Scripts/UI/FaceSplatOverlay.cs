// ─────────────────────────────────────────────────────────────
// FaceSplatOverlay.cs — 玩家被砸中臉時,視野糊一片醬汁
// 掛載:一個 World Space 以外的 Canvas 上(Render Mode 選
//   Screen Space - Camera,Camera 拖 Main Camera,Plane Distance 0.35,
//   這樣在 VR 裡會像糊在臉上)。
// Inspector:
//   splatImage — Canvas 底下一張全螢幕的 Image,放美術組的醬汁濺灑圖
// ─────────────────────────────────────────────────────────────
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Pizzala.UI
{
    public class FaceSplatOverlay : MonoBehaviour
    {
        public Image splatImage;
        public float holdSeconds = 1.2f;
        public float fadeSeconds = 2f;

        Coroutine routine;

        void Start()
        {
            if (splatImage != null) SetAlpha(0f);
        }

        public void Show()
        {
            if (splatImage == null) return;
            if (routine != null) StopCoroutine(routine);
            routine = StartCoroutine(SplatRoutine());
        }

        IEnumerator SplatRoutine()
        {
            SetAlpha(1f);
            yield return new WaitForSeconds(holdSeconds);
            float t = 0f;
            while (t < fadeSeconds)
            {
                t += Time.deltaTime;
                SetAlpha(1f - t / fadeSeconds);
                yield return null;
            }
            SetAlpha(0f);
        }

        void SetAlpha(float a)
        {
            var c = splatImage.color;
            c.a = a;
            splatImage.color = c;
        }
    }
}
