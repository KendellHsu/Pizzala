// ─────────────────────────────────────────────────────────────
// CustomerController.cs — 客人狀態機:等餐 → 開心 / 生氣(+丟回)
// 掛載:客人 Prefab 根物件。
// Inspector:
//   customerId    — 場上每個客人給不同編號(0,1,2...)
//   sector        — 這個客人在玩家的左/中/右(對應方位統計)
//   faceRenderer  — 臉部的 Renderer(材質主貼圖會被換)
//   faceNormal/Happy/Angry/Dirty — 四張表情貼圖(美術組產出)
//   flavorIcon    — 頭上口味圖示的 SpriteRenderer
//   flavorSprites — 口味圖示陣列,順序對應 PizzaFlavor 列舉
//                   (0=Margherita 1=Pepperoni 2=CosmicPinkMarshmallow)
//   faceAnchor    — 臉部位置的空物件(截圖相機對準用)
//   throwOrigin   — 丟回披薩的出發點(手或胸前的空物件)
//   requiredThrowType — 進階玩法:要求特定投擲方式,Unknown=不限
// ─────────────────────────────────────────────────────────────
using System.Collections;
using UnityEngine;
using Pizzala.Data;

namespace Pizzala.Customers
{
    public enum CustomerState { Idle, Waiting, Happy, Angry }

    public class CustomerController : MonoBehaviour
    {
        [Header("身分")]
        public int customerId;
        public TargetSector sector;

        [Header("表情(換貼圖)")]
        public Renderer faceRenderer;
        public Texture faceNormal, faceHappy, faceAngry, faceDirty;

        [Header("訂單顯示")]
        public SpriteRenderer flavorIcon;
        public Sprite[] flavorSprites; // 順序 = PizzaFlavor 列舉順序

        [Header("定位點")]
        public Transform faceAnchor;
        public Transform throwOrigin;

        [Header("進階玩法(預設關閉)")]
        public ThrowType requiredThrowType = ThrowType.Unknown; // Unknown = 不限

        public CustomerState State { get; private set; } = CustomerState.Idle;
        public bool HasActiveOrder { get; private set; }
        public PizzaFlavor CurrentOrder { get; private set; }
        public float OrderStartTime { get; private set; }
        public bool IsDirty { get; private set; }

        Coroutine patienceRoutine;
        Coroutine faceResetRoutine;

        public event System.Action<CustomerController> OnOrderTimeout;

        void Start()
        {
            SetFace(faceNormal);
            if (flavorIcon != null) flavorIcon.enabled = false;
        }

        public void GiveOrder(PizzaFlavor flavor, float patienceSeconds)
        {
            CurrentOrder = flavor;
            HasActiveOrder = true;
            OrderStartTime = Time.time;
            State = CustomerState.Waiting;

            if (flavorIcon != null && flavorSprites != null && (int)flavor < flavorSprites.Length)
            {
                flavorIcon.sprite = flavorSprites[(int)flavor];
                flavorIcon.enabled = true;
            }

            if (patienceRoutine != null) StopCoroutine(patienceRoutine);
            patienceRoutine = StartCoroutine(PatienceCountdown(patienceSeconds));
        }

        IEnumerator PatienceCountdown(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            if (!HasActiveOrder) yield break;
            ClearOrder();
            SetMood(CustomerState.Angry);
            OnOrderTimeout?.Invoke(this);
        }

        // 訂單被解決(成功或口味錯),由 GameManager 呼叫
        public void ResolveOrder(bool satisfied)
        {
            ClearOrder();
            SetMood(satisfied ? CustomerState.Happy : CustomerState.Angry);
        }

        void ClearOrder()
        {
            HasActiveOrder = false;
            if (patienceRoutine != null) { StopCoroutine(patienceRoutine); patienceRoutine = null; }
            if (flavorIcon != null) flavorIcon.enabled = false;
        }

        public void GetDirty()
        {
            IsDirty = true;
            SetMood(CustomerState.Angry);
        }

        void SetMood(CustomerState mood)
        {
            State = mood;
            SetFace(mood == CustomerState.Happy ? faceHappy
                  : mood == CustomerState.Angry ? (IsDirty ? faceDirty : faceAngry)
                  : faceNormal);

            if (faceResetRoutine != null) StopCoroutine(faceResetRoutine);
            faceResetRoutine = StartCoroutine(ResetFaceLater(2.5f));
        }

        IEnumerator ResetFaceLater(float delay)
        {
            yield return new WaitForSeconds(delay);
            State = CustomerState.Idle;
            SetFace(IsDirty ? faceDirty : faceNormal); // 髒臉是永久的,直到回合結束
        }

        void SetFace(Texture tex)
        {
            if (faceRenderer != null && tex != null)
                faceRenderer.material.mainTexture = tex;
        }

        // 丟回預警:身體閃紅,結束後由 GameManager 發射投射物
        public IEnumerator Telegraph(float seconds, Renderer bodyRenderer)
        {
            var target = bodyRenderer != null ? bodyRenderer : faceRenderer;
            if (target == null) { yield return new WaitForSeconds(seconds); yield break; }

            Color original = target.material.color;
            float t = 0f;
            while (t < seconds)
            {
                t += Time.deltaTime;
                float blink = Mathf.PingPong(t * 6f, 1f);
                target.material.color = Color.Lerp(original, Color.red, blink);
                yield return null;
            }
            target.material.color = original;
        }
    }
}
