// ─────────────────────────────────────────────────────────────
// FaceSplatOverlay.cs — 玩家被砸中臉時,視野糊一片醬汁
// 掛載:一個 World Space 以外的 Canvas 上(Render Mode 選
//   Screen Space - Camera,Camera 拖 Main Camera,Plane Distance 0.35,
//   這樣在 VR 裡會像糊在臉上)。
// Inspector:
//   splatImage   — Canvas 底下一張全螢幕的 Image,放美術組的醬汁濺灑圖(預設圖)
//   flavorSplats — 依口味切換用,順序對應 PizzaFlavor 列舉
//                  (0=Margherita 1=Pepperoni 2=CosmicPinkMarshmallow)。
//                  某個口味留空,該口味被砸中時就沿用 splatImage 目前的圖。
// ─────────────────────────────────────────────────────────────
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Pizzala.Data;

namespace Pizzala.UI
{
    public class FaceSplatOverlay : MonoBehaviour
    {
        public Image splatImage;
        public Sprite[] flavorSplats;
        public float holdSeconds = 1.2f;
        public float fadeSeconds = 2f;

        Coroutine routine;

        void Start()
        {
            if (splatImage != null) SetAlpha(0f);
        }

        public void Show(PizzaFlavor? flavor = null)
        {
            if (splatImage == null) return;

            if (flavor.HasValue && flavorSplats != null
                && (int)flavor.Value < flavorSplats.Length
                && flavorSplats[(int)flavor.Value] != null)
                splatImage.sprite = flavorSplats[(int)flavor.Value];

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
