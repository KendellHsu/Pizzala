// ─────────────────────────────────────────────────────────────
// CustomerController.cs — 客人狀態機:等餐 → 開心 / 生氣(+丟回)
// 掛載:客人 Prefab 根物件。
// Inspector:
//   customerId    — 場上每個客人給不同編號(0,1,2...)
//   sector        — 這個客人在玩家的左/中/右(對應方位統計)
//   faceRenderer  — 舊表情 Quad(備援用,模型不在時才會被換貼圖)
//   faceNormal/Happy/Angry/Dirty — 四張「整身貼圖」(texture_0 的表情
//                   變體,美術組產出);表情=整張角色貼圖直接換
//   flavorIcon    — 頭上口味圖示的 SpriteRenderer
//   flavorSprites — 口味圖示陣列,順序對應 PizzaFlavor 列舉
//                   (0=Margherita 1=Pepperoni 2=CosmicPinkMarshmallow)
//   faceAnchor    — 臉部位置的空物件(截圖相機對準用)
//   throwOrigin   — 丟回披薩的出發點(手或胸前的空物件)
//   requiredThrowType — 進階玩法:要求特定投擲方式,Unknown=不限
//   canWander     — 等餐時是否遊走(情緒加速機制),關掉=站樁
//   idle/walkAnimatorController — 站立/走路的 Animator Controller,
//                   移動時切換;留空 walk = 不切動畫(照樣會滑動)
// 情緒加速:等餐時間切三段(耐心→不耐煩→暴躁),越久遊走越快,
// 門檻與速度由 GameManager 從 ThrowTuning 推入(ApplyTuning)。
// ─────────────────────────────────────────────────────────────
using System.Collections;
using UnityEngine;
using Pizzala.Data;
using Pizzala.Throwing;

namespace Pizzala.Customers
{
    public enum CustomerState { Idle, Waiting, Happy, Angry }

    // 等餐情緒三段:耐心(站著)→ 不耐煩(慢速遊走)→ 暴躁(快速遊走)
    public enum WaitMood { Patient, Impatient, Urgent }

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

        [Header("移動(情緒加速)")]
        public bool canWander = true;

        [Header("走路動畫(可留空=不切動畫)")]
        public RuntimeAnimatorController idleAnimatorController;
        public RuntimeAnimatorController walkAnimatorController;

        public CustomerState State { get; private set; } = CustomerState.Idle;
        public WaitMood Mood { get; private set; } = WaitMood.Patient;
        public bool HasActiveOrder { get; private set; }
        public PizzaFlavor CurrentOrder { get; private set; }
        public float OrderStartTime { get; private set; }
        public bool IsDirty { get; private set; }

        // 站位點:遊走永遠繞著這裡,不會漂進隔壁客人的位置
        public Vector3 HomePosition { get; private set; }

        // 由 CustomerSpawner 標記:準備離場,不再接新訂單
        public bool IsLeaving { get; set; }

        // 由 GameManager 標記:丟回披薩中(預警→出手),期間定住不遊走
        public bool IsThrowingBack { get; set; }

        // 情緒門檻與速度,預設值可用,正式值由 GameManager 從 ThrowTuning 推入
        float impatientAt = 5f;
        float urgentAt = 10f;
        float impatientSpeed = 0.4f;
        float urgentSpeed = 0.9f;
        float wanderRadius = 0.7f;
        float pauseChance = 0.7f;
        float pauseMinSeconds = 1f;
        float pauseMaxSeconds = 2.5f;
        float pauseUntil; // 停頓到這個時間點才繼續走

        Transform lookTarget;   // 沒在走路時面向這裡(玩家頭部)
        Animator animator;
        Renderer[] modelRenderers; // 模型本體(士兵 SkinnedMesh),丟回預警閃紅用
        Vector3 wanderTarget;
        bool isWalking;

        Coroutine patienceRoutine;
        Coroutine faceResetRoutine;

        public event System.Action<CustomerController> OnOrderTimeout;
        public event System.Action<CustomerController, bool> OnOrderResolved; // bool = 是否滿意

        void Awake()
        {
            animator = GetComponentInChildren<Animator>();
            if (idleAnimatorController == null && animator != null)
                idleAnimatorController = animator.runtimeAnimatorController;
            modelRenderers = GetComponentsInChildren<SkinnedMeshRenderer>();
        }

        void Start()
        {
            SetFace(faceNormal);
            if (flavorIcon != null) flavorIcon.enabled = false;
            HomePosition = transform.position;
            wanderTarget = HomePosition;
        }

        // GameManager 在註冊客人時呼叫,把 ThrowTuning 的移動參數推進來
        public void ApplyTuning(ThrowTuning tuning, Transform look)
        {
            if (tuning != null)
            {
                impatientAt = tuning.customerImpatientAt;
                urgentAt = tuning.customerUrgentAt;
                impatientSpeed = tuning.customerImpatientMoveSpeed;
                urgentSpeed = tuning.customerUrgentMoveSpeed;
                wanderRadius = tuning.customerWanderRadius;
                pauseChance = tuning.customerWanderPauseChance;
                pauseMinSeconds = tuning.customerWanderPauseMinSeconds;
                pauseMaxSeconds = tuning.customerWanderPauseMaxSeconds;
            }
            lookTarget = look;
        }

        void Update()
        {
            UpdateWander();
        }

        void UpdateWander()
        {
            float speed = 0f;
            if (HasActiveOrder && canWander && !IsLeaving && !IsThrowingBack)
            {
                Mood = ComputeMood(Time.time - OrderStartTime, impatientAt, urgentAt);
                speed = Mood switch
                {
                    WaitMood.Urgent => urgentSpeed,
                    WaitMood.Impatient => impatientSpeed,
                    _ => 0f
                };
            }
            else
            {
                Mood = WaitMood.Patient;
            }

            if (speed <= 0f || Time.time < pauseUntil)
            {
                SetWalking(false);
                FaceLookTarget();
                return;
            }

            if (Vector3.Distance(transform.position, wanderTarget) < 0.05f)
            {
                var offset = Random.insideUnitCircle * wanderRadius;
                wanderTarget = HomePosition + new Vector3(offset.x, 0f, offset.y);

                // 到點:依機率停頓一下再走,不會一直走個不停
                if (Random.value < pauseChance)
                {
                    pauseUntil = Time.time + Random.Range(pauseMinSeconds, pauseMaxSeconds);
                    SetWalking(false);
                    FaceLookTarget();
                    return;
                }
            }

            Vector3 before = transform.position;
            transform.position = Vector3.MoveTowards(before, wanderTarget, speed * Time.deltaTime);

            Vector3 moveDir = transform.position - before;
            moveDir.y = 0f;
            if (moveDir.sqrMagnitude > 1e-8f)
                transform.rotation = Quaternion.Slerp(
                    transform.rotation, Quaternion.LookRotation(moveDir), Time.deltaTime * 8f);

            SetWalking(true);
        }

        void FaceLookTarget()
        {
            if (lookTarget == null) return;
            Vector3 to = lookTarget.position - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude < 0.0001f) return;
            transform.rotation = Quaternion.Slerp(
                transform.rotation, Quaternion.LookRotation(to), Time.deltaTime * 5f);
        }

        void SetWalking(bool walking)
        {
            if (walking == isWalking) return;
            isWalking = walking;
            if (animator == null) return;
            var target = walking ? walkAnimatorController : idleAnimatorController;
            if (target != null) animator.runtimeAnimatorController = target;
        }

        // 純函式,方便不進 Play Mode 驗證門檻
        public static WaitMood ComputeMood(float waitTime, float impatientAt, float urgentAt)
        {
            if (waitTime >= urgentAt) return WaitMood.Urgent;
            if (waitTime >= impatientAt) return WaitMood.Impatient;
            return WaitMood.Patient;
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
            OnOrderResolved?.Invoke(this, satisfied);
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

        // 表情 = 換掉整張角色貼圖(texture_0 的表情變體),
        // 直接換士兵模型材質的主貼圖;沒有模型才退回舊的表情 Quad。
        void SetFace(Texture tex)
        {
            if (tex == null) return;

            if (modelRenderers != null && modelRenderers.Length > 0)
            {
                foreach (var r in modelRenderers)
                    foreach (var m in r.materials)
                        m.mainTexture = tex;
                return;
            }

            if (faceRenderer != null)
                faceRenderer.material.mainTexture = tex;
        }

        // 丟回預警:身體閃紅,結束後由 GameManager 發射投射物。
        // 指定 bodyRenderer 就閃它;沒指定就閃模型本體(SkinnedMesh),
        // 再沒有才 fallback 到 faceRenderer(舊的表情 Quad,可能不可見)。
        public IEnumerator Telegraph(float seconds, Renderer bodyRenderer)
        {
            Renderer[] targets;
            if (bodyRenderer != null)
                targets = new[] { bodyRenderer };
            else if (modelRenderers != null && modelRenderers.Length > 0)
                targets = modelRenderers;
            else if (faceRenderer != null)
                targets = new Renderer[] { faceRenderer };
            else
            {
                yield return new WaitForSeconds(seconds);
                yield break;
            }

            // 收集所有材質並記下原色(renderer.materials 會自動實例化,不汙染共用材質)
            var mats = new System.Collections.Generic.List<Material>();
            foreach (var r in targets)
                mats.AddRange(r.materials);
            var originals = new Color[mats.Count];
            for (int i = 0; i < mats.Count; i++)
                originals[i] = mats[i].color;

            float t = 0f;
            while (t < seconds)
            {
                t += Time.deltaTime;
                float blink = Mathf.PingPong(t * 6f, 1f);
                for (int i = 0; i < mats.Count; i++)
                    mats[i].color = Color.Lerp(originals[i], Color.red, blink);
                yield return null;
            }

            for (int i = 0; i < mats.Count; i++)
                mats[i].color = originals[i];
        }
    }
}
