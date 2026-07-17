// ─────────────────────────────────────────────────────────────
// CustomerController.cs — 客人狀態機:點餐前 → 等餐 → 拿到(滿意) / 生氣(+丟回)
// 掛載:客人 Prefab 根物件。
// Inspector:
//   customerId    — 場上每個客人給不同編號(0,1,2...)
//   sector        — 這個客人在玩家的左/中/右(對應方位統計)
//   flavorIcon    — 頭上口味圖示的 SpriteRenderer(點餐後才顯示)
//   flavorSprites — 口味圖示陣列,順序對應 PizzaFlavor 列舉
//                   (0=Margherita 1=Pepperoni 2=CosmicPinkMarshmallow)
//   faceAnchor    — 臉部位置的空物件(截圖相機對準用)
//   throwOrigin   — 丟回披薩的出發點(手或胸前的空物件)
//   requiredThrowType — 進階玩法:要求特定投擲方式,Unknown=不限
//   canWander     — 等餐時是否遊走(情緒加速機制),關掉=站樁
//   idle/walk/throwAnimatorController — 站立/走路/丟回的 Animator Controller,
//                   對應狀態切換;留空則該狀態不換動畫
// 情緒:等餐耐心切三段(耐心→不耐煩→暴躁),越久遊走越快;
//   剩餘耐心改由頭上倒數圈(FlavorCountDown)呈現,不再換整身表情貼圖。
// 門檻與速度由 GameManager 從 ThrowTuning 推入(ApplyTuning)。
// ─────────────────────────────────────────────────────────────
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Pizzala.Data;
using Pizzala.Throwing;

namespace Pizzala.Customers
{
    // 客人生命週期(僅內部/除錯用;菜單顯示看 HasActiveOrder,不看這個):
    //   PreOrder 點餐前(無菜單) → Waiting 等餐 → Served 拿到滿意 / Angry 超時或被砸
    public enum CustomerState { PreOrder, Waiting, Served, Angry }

    // 等餐情緒三段:耐心(站著)→ 不耐煩(慢速遊走)→ 暴躁(快速遊走)
    public enum WaitMood { Patient, Impatient, Urgent }

    public class CustomerController : MonoBehaviour
    {
        [Header("身分")]
        public int customerId;
        public TargetSector sector;

        [Header("訂單顯示")]
        public SpriteRenderer flavorIcon;
        public Sprite[] flavorSprites; // 順序 = PizzaFlavor 列舉順序

        [Tooltip("頭上耐心倒數圈:UI Image(Image Type=Filled、Fill Method=Vertical、Origin=Bottom)," +
                 "fillAmount 隨剩餘耐心從 1 降到 0(水位下降)。留空則不顯示倒數")]
        public Image flavorCountDown;

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

        [Tooltip("丟回披薩時的出手動畫控制器;留空=丟回時不換動畫")]
        public RuntimeAnimatorController throwAnimatorController;

        public CustomerState State { get; private set; } = CustomerState.PreOrder;
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
        float walkAnimBaseSpeed = 0.5f; // 走路 clip 播 1x 時對應的移動速度 m/s;移動越快動畫越快
        float orderPatience;            // 目前訂單的總耐心秒數,用來算倒數圈比例

        Transform lookTarget;   // 沒在走路時面向這裡(玩家頭部)
        Animator animator;
        Renderer[] modelRenderers; // 模型本體(士兵 SkinnedMesh),丟回預警閃紅用
        Vector3 wanderTarget;
        bool isWalking;
        bool isThrowingAnim; // 播丟回出手動畫中,期間鎖住 idle/walk 切換,不被遊走狀態機搶回控制器

        Coroutine patienceRoutine;

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
            if (flavorIcon != null) flavorIcon.enabled = false;
            if (flavorCountDown != null) flavorCountDown.enabled = false;
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
                walkAnimBaseSpeed = tuning.customerWalkAnimBaseSpeed;
            }
            lookTarget = look;
        }

        void Update()
        {
            UpdateWander();
            UpdateCountdown();
        }

        // 頭上耐心倒數圈:fillAmount 隨剩餘耐心從 1 掉到 0(水位下降)。
        // Mood 三段變速本來就跟這條同一個時鐘(elapsed / OrderStartTime),所以圈與速度天然同步。
        void UpdateCountdown()
        {
            if (flavorCountDown == null || !HasActiveOrder || orderPatience <= 0f) return;
            flavorCountDown.fillAmount =
                Mathf.Clamp01(1f - (Time.time - OrderStartTime) / orderPatience);
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
            // 移動越快走路動畫播越快(不耐煩→暴躁),避免快走時腳步打滑
            if (animator != null && !isThrowingAnim)
                animator.speed = Mathf.Max(0.1f, speed / Mathf.Max(walkAnimBaseSpeed, 0.01f));
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
            if (isThrowingAnim) return; // 出手動畫播放中,別搶回 idle/walk 控制器
            if (walking == isWalking) return;
            isWalking = walking;
            if (animator == null) return;
            if (!walking) animator.speed = 1f; // 回到站立:動畫用正常速度
            var target = walking ? walkAnimatorController : idleAnimatorController;
            if (target != null) animator.runtimeAnimatorController = target;
        }

        // 丟回披薩的出手動畫:切到 throw 控制器播一次(長度自動取 clip 長),
        // 播完切回 idle。播放期間 isThrowingAnim 鎖住 SetWalking。
        // 由 GameManager 在丟回發射的瞬間呼叫。
        public void PlayThrow()
        {
            if (throwAnimatorController == null || animator == null) return;
            StartCoroutine(ThrowAnimRoutine());
        }

        IEnumerator ThrowAnimRoutine()
        {
            isThrowingAnim = true;
            isWalking = false;
            animator.speed = 1f; // 出手動畫用正常速度,不受走路變速影響
            animator.runtimeAnimatorController = throwAnimatorController;
            yield return null; // 等 animator 進到新狀態,下一幀才讀得到 clip 長度

            float len = 1f;
            if (animator.runtimeAnimatorController == throwAnimatorController)
            {
                var info = animator.GetCurrentAnimatorStateInfo(0);
                if (info.length > 0.01f) len = info.length;
            }
            yield return new WaitForSeconds(len);

            isThrowingAnim = false;
            if (animator != null)
                animator.runtimeAnimatorController = idleAnimatorController; // 收回 idle,下一幀 SetWalking 重新評估
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
            orderPatience = patienceSeconds;
            State = CustomerState.Waiting;

            if (flavorIcon != null && flavorSprites != null && (int)flavor < flavorSprites.Length)
            {
                flavorIcon.sprite = flavorSprites[(int)flavor];
                flavorIcon.enabled = true;
            }

            if (flavorCountDown != null)
            {
                flavorCountDown.fillAmount = 1f; // 滿水位起跳
                flavorCountDown.enabled = true;
            }

            if (patienceRoutine != null) StopCoroutine(patienceRoutine);
            patienceRoutine = StartCoroutine(PatienceCountdown(patienceSeconds));
        }

        IEnumerator PatienceCountdown(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            if (!HasActiveOrder) yield break;
            ClearOrder();
            State = CustomerState.Angry; // 超時 → 生氣(耐心圈已歸零;後續由 GameManager 決定丟回/離場)
            OnOrderTimeout?.Invoke(this);
        }

        // 訂單被解決(成功或口味錯),由 GameManager 呼叫
        public void ResolveOrder(bool satisfied)
        {
            ClearOrder();
            State = satisfied ? CustomerState.Served : CustomerState.Angry;
            OnOrderResolved?.Invoke(this, satisfied);
        }

        void ClearOrder()
        {
            HasActiveOrder = false;
            if (patienceRoutine != null) { StopCoroutine(patienceRoutine); patienceRoutine = null; }
            if (flavorIcon != null) flavorIcon.enabled = false;
            if (flavorCountDown != null) flavorCountDown.enabled = false;
        }

        // 被披薩砸到(臉/身體):標記髒污。情緒改由倒數圈呈現,不再換表情貼圖;
        // 丟回與否由 GameManager 決定(砸臉機率丟回、身體只噴醬)。
        public void GetDirty()
        {
            IsDirty = true;
        }

        // 丟回預警:身體閃紅,結束後由 GameManager 發射投射物。
        // 指定 bodyRenderer 就閃它;沒指定就閃模型本體(SkinnedMesh)。
        public IEnumerator Telegraph(float seconds, Renderer bodyRenderer)
        {
            Renderer[] targets;
            if (bodyRenderer != null)
                targets = new[] { bodyRenderer };
            else if (modelRenderers != null && modelRenderers.Length > 0)
                targets = modelRenderers;
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
