using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Pizzala.Customers;
using Pizzala.UI;

namespace Pizzala.EditorTools
{
    // 產生/修好 PZ_Customer 頭上的耐心倒數圈(FlavorCountDown)。
    // UI.Image 的 fillAmount(Filled/Vertical/Bottom)是唯一原生支援「水位下降」
    // 效果的元件,SpriteRenderer 沒有這個功能,所以倒數圈得用 World Space
    // Canvas + Image,跟其他元素(SpriteRenderer)混在一起。
    // 一鍵完成:
    //   1. 找/建一個 World Space Canvas,掛在 FlavorIconFrame 底下
    //      (會自動繼承 FlavorIconFrame 既有的 Billboard 旋轉,不用自己再掛一個)
    //   2. Canvas 排序設跟 FlavorIconFrame 同層(order=0),
    //      蓋在框上但不會蓋過 FlavorIcon(order=1)——水位在 pizza 圖示背後
    //   3. 底下的 Image 設 Filled/Vertical/Bottom,顏色/sprite 沿用舊的
    //      Circle(SpriteRenderer)當初手動擺的外觀
    //   4. 接進 CustomerController.flavorCountDown
    //   5. 刪掉被取代的 Circle(SpriteRenderer)
    // 可重複執行:Canvas/Image 找不到就新建,找到就修正既有的設定。
    // 注意(需在 Unity 裡人工驗收):Canvas 的 RectTransform 大小/縮放只給了
    // 預設值,實際跟框的比例要在 Scene 視圖裡拖到跟原本 Circle 視覺上差不多大。
    public static class FlavorCountDownBuilder
    {
        const string BasePrefabPath = "Assets/Prefabs/PZ_Customer.prefab";

        [MenuItem("Tools/Pizzala/Build Flavor Count Down")]
        public static void Build()
        {
            var root = PrefabUtility.LoadPrefabContents(BasePrefabPath);
            try
            {
                var frame = root.transform.Find("FlavorIconFrame");
                if (frame == null)
                { Debug.LogError("FlavorCountDownBuilder: 找不到 FlavorIconFrame。"); return; }

                var cc = root.GetComponent<CustomerController>();
                if (cc == null)
                { Debug.LogError("FlavorCountDownBuilder: 根物件沒有 CustomerController。"); return; }

                // 從舊的 Circle(SpriteRenderer)接手 sprite/顏色當倒數圈外觀,稍後刪掉它
                var oldCircle = frame.Find("Circle");
                Sprite circleSprite = null;
                Color circleColor = Color.white;
                Vector3 circleLocalPos = Vector3.zero;
                if (oldCircle != null)
                {
                    var sr = oldCircle.GetComponent<SpriteRenderer>();
                    if (sr != null) { circleSprite = sr.sprite; circleColor = sr.color; }
                    circleLocalPos = oldCircle.localPosition;
                }

                // 找/建 Canvas(World Space),掛在 FlavorIconFrame 底下
                var canvas = root.GetComponentInChildren<Canvas>(true);
                GameObject canvasGO = canvas != null ? canvas.gameObject
                                     : new GameObject("FlavorCountDownCanvas", typeof(RectTransform));
                canvasGO.name = "FlavorCountDownCanvas";
                int uiLayer = LayerMask.NameToLayer("UI");
                if (uiLayer >= 0) canvasGO.layer = uiLayer;
                canvasGO.transform.SetParent(frame, false);

                if (canvas == null) canvas = canvasGO.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.overrideSorting = true;
                canvas.sortingLayerID = 0; // Default,跟 FlavorIconFrame/FlavorIcon 同一層
                canvas.sortingOrder = 0;   // 跟框同層,蓋不過 FlavorIcon(order=1)= 蓋在 pizza 圖示背後

                if (canvasGO.GetComponent<CanvasScaler>() == null) canvasGO.AddComponent<CanvasScaler>();
                if (canvasGO.GetComponent<GraphicRaycaster>() == null) canvasGO.AddComponent<GraphicRaycaster>();
                // 不用自己掛 Billboard:Canvas 掛在 FlavorIconFrame 底下,
                // 會直接繼承它的旋轉(FlavorIconFrame 本身已經有 Billboard 面向玩家)。
                var staleBillboard = canvasGO.GetComponent<Billboard>();
                if (staleBillboard != null) Object.DestroyImmediate(staleBillboard);

                var canvasRT = canvasGO.GetComponent<RectTransform>();
                canvasRT.localPosition = circleLocalPos;
                canvasRT.localRotation = Quaternion.identity;
                canvasRT.localScale = Vector3.one * 0.01f; // World Space Canvas 常見起始縮放,實際比例需在 Scene 視圖微調
                canvasRT.sizeDelta = new Vector2(100, 100);

                // 找/建 Image(倒數圈本體)
                var imgTr = canvasGO.transform.Find("FlavorCountDown");
                GameObject imgGO = imgTr != null ? imgTr.gameObject
                                  : new GameObject("FlavorCountDown", typeof(RectTransform));
                imgGO.name = "FlavorCountDown";
                imgGO.transform.SetParent(canvasGO.transform, false);

                var imgRT = imgGO.GetComponent<RectTransform>();
                imgRT.anchorMin = new Vector2(0.5f, 0.5f);
                imgRT.anchorMax = new Vector2(0.5f, 0.5f);
                imgRT.pivot = new Vector2(0.5f, 0.5f);
                imgRT.anchoredPosition = Vector2.zero;
                imgRT.sizeDelta = new Vector2(100, 100);

                var image = imgGO.GetComponent<Image>();
                if (image == null) image = imgGO.AddComponent<Image>();
                if (circleSprite != null) image.sprite = circleSprite;
                image.color = circleColor;
                image.type = Image.Type.Filled;
                image.fillMethod = Image.FillMethod.Vertical;
                image.fillOrigin = (int)Image.OriginVertical.Bottom;
                image.fillAmount = 1f;

                cc.flavorCountDown = image;

                if (oldCircle != null) Object.DestroyImmediate(oldCircle.gameObject);

                PrefabUtility.SaveAsPrefabAsset(root, BasePrefabPath);
                Debug.Log("FlavorCountDownBuilder: 已產生/修好 FlavorCountDown 並接進 CustomerController.flavorCountDown。"
                         + "記得在 Scene 視圖檢查 Canvas 的縮放/位置跟框的比例是否對。");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();
        }
    }
}
