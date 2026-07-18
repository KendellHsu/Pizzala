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
//   animatorController — 單一 Animator Controller(統一 Armature 骨架),
//                   內含 Idle/Walk/Throw 三個 state:走動時 SetBool("Walking"),
//                   丟回時 SetTrigger("Throw")。留空則沿用 Animator 上原本的控制器。
// 情緒:等餐耐心切三段(耐心→不耐煩→暴躁),越久遊走越快;
//   剩餘耐心改由頭上倒數圈(FlavorCountDown)呈現,不再換整身表情貼圖。
// 門檻與速度由 GameManager 從 ThrowTuning 推入(ApplyTuning)。
// ─────────────────────────────────────────────────────────────
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Pizzala.Core;
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

        [Header("情緒圖示")]
        public Sprite smileSprite;
        public Sprite angrySprite;

        [Tooltip("頭上耐心倒數圈:UI Image(Image Type=Filled、Fill Method=Vertical、Origin=Bottom)," +
                 "fillAmount 隨剩餘耐心從 1 降到 0(水位下降)。留空則不顯示倒數")]
        public Image flavorCountDown;

        [Header("Pizza 盒(接住成功)")]
        [Tooltip("Pizza 盒物件(含模型與 HandZone);點餐前隱藏,接單時才出現。留空 = 一直顯示")]
        public GameObject pizzaBox;
        [Tooltip("Pizza 盒的 Animation 元件(舊版動畫);接住正確口味時播關盒 clip。留空 = 不播關盒")]
        public Animation pizzaBoxAnimation;
        [Tooltip("關盒動畫的 clip 名稱(要和 Animation 元件 clip 清單裡的名字一致)")]
        public string pizzaBoxCloseClip = "Close";
        [Tooltip("盒中生成 pizza 的位置(空物件,擺在盒子開口內)。留空 = 不生成盒中 pizza")]
        public Transform pizzaBoxSlot;
        [Tooltip("盒中展示用的 pizza prefab,順序對應 PizzaFlavor 列舉(建議用靜態、不可抓取的版本)")]
        public GameObject[] boxPizzaByFlavor;

        [Header("定位點")]
        public Transform faceAnchor;
        public Transform throwOrigin;

        [Header("進階玩法(預設關閉)")]
        public ThrowType requiredThrowType = ThrowType.Unknown; // Unknown = 不限

        [Header("移動(情緒加速)")]
        public bool canWander = true;

        [Header("角色動畫(單一 Controller,統一 Armature 骨架)")]
        [Tooltip("內含 Idle/Walk/Throw 三個 state 的 Animator Controller。" +
                 "走動切 Walking(bool)、丟回觸發 Throw(trigger)。" +
                 "留空 = 沿用 Animator 元件上原本掛的控制器,不覆寫。")]
        public RuntimeAnimatorController animatorController;

        // Animator 參數名(與各角色 controller 內的參數一致)
        static readonly int WalkingHash = Animator.StringToHash("Walking");
        static readonly int ThrowHash   = Animator.StringToHash("Throw");

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

        // 超時反擊中(走去撿地上 pizza→丟回),期間自己驅動移動,離場要等它結束
        public bool IsRetaliating { get; private set; }

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
        float pickupReach = 0.8f;       // 撿地上 pizza 反擊時,走到多近算撿到
        float walkAnimBaseSpeed = 0.5f; // 走路 clip 播 1x 時對應的移動速度 m/s;移動越快動畫越快
        float orderPatience;            // 目前訂單的總耐心秒數,用來算倒數圈比例
        GameObject currentBoxPizza;     // 盒中目前那顆展示 pizza(錯口味丟回時要清掉)
        bool boxClosing;                // 關盒動畫播放中:訂單結束也先別收盒,等動畫播完

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
            if (animator != null)
            {
                // 位移完全由本腳本的 MoveTowards 驅動;Root Motion 開著會跟它打架(原地踏步/抖動)。
                // 各角色 FBX 匯入的 Animator 預設值不一,這裡統一強制關閉,不依賴 prefab 手動設定。
                animator.applyRootMotion = false;
                // 有指定就覆寫;留空則沿用 Animator 上原本掛的控制器(prefab 已接好)。
                if (animatorController != null)
                    animator.runtimeAnimatorController = animatorController;
            }
            modelRenderers = GetComponentsInChildren<SkinnedMeshRenderer>();

            // 診斷:確認每隻客人開場抓到的 animator 與 idle 控制器狀態
            if (animator != null) StartCoroutine(DiagAnimator());
        }

        System.Collections.IEnumerator DiagAnimator()
        {
            yield return null; // 等 animator 初始化一幀
            var st0 = animator.GetCurrentAnimatorStateInfo(0);
            float t0 = st0.normalizedTime;
            int clips = animator.runtimeAnimatorController != null
                ? animator.runtimeAnimatorController.animationClips.Length : -1;
            yield return new WaitForSeconds(0.5f); // 過半秒再看動畫時間有沒有推進
            var st1 = animator.GetCurrentAnimatorStateInfo(0);
            Debug.Log($"[Diag] {name}:" +
                      $" enabled={animator.enabled} speed={animator.speed}" +
                      $" cullingMode={animator.cullingMode} updateMode={animator.updateMode}" +
                      $" hasController={(animator.runtimeAnimatorController != null)} clipCount={clips}" +
                      $" normTime {t0:F3}→{st1.normalizedTime:F3} (有推進表示動畫在播)" +
                      $" avatarValid={(animator.avatar != null && animator.avatar.isValid)}" +
                      $" isHuman={(animator.avatar != null && animator.avatar.isHuman)}");
        }

        void Start()
        {
            HideHeadIcon();
            if (flavorCountDown != null) flavorCountDown.enabled = false;
            if (pizzaBox != null) pizzaBox.SetActive(false); // 點餐前不出現盒子
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
                pickupReach = tuning.customerPickupReach;
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
            if (IsRetaliating) return; // 反擊中由 RetaliateRoutine 自己驅動移動,遊走狀態機讓開

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
                var candidate = HomePosition + new Vector3(offset.x, 0f, offset.y);
                // 排除區(notAllowToPick)同時是客人禁止進入區:
                // 抽到落在區內的點就丟掉,維持原目標(HomePosition 在區外,不會走進去)。
                if (!Pizzala.Throwing.PickupExclusionZone.Contains(candidate))
                    wanderTarget = candidate;

                // 到點:依機率停頓一下再走,不會一直走個不停
                if (Random.value < pauseChance)
                {
                    pauseUntil = Time.time + Random.Range(pauseMinSeconds, pauseMaxSeconds);
                    SetWalking(false);
                    FaceLookTarget();
                    return;
                }
            }

            Vector3 moveDir = StepTowards(wanderTarget, speed);
            if (moveDir.sqrMagnitude > 1e-8f)
                transform.rotation = Quaternion.Slerp(
                    transform.rotation, Quaternion.LookRotation(moveDir), Time.deltaTime * 8f);

            SetWalking(true);
            // 移動越快走路動畫播越快(不耐煩→暴躁),避免快走時腳步打滑
            if (animator != null && !isThrowingAnim)
                animator.speed = Mathf.Max(0.1f, speed / Mathf.Max(walkAnimBaseSpeed, 0.01f));
        }

        // 朝目標走一小步,但守住排除區(notAllowToPick=玩家禁止進入區):
        // 若這一步會踏進任一排除區就停在原地(不踏進去),避免直線路徑穿過玩家。
        // 回傳這一幀實際位移向量(給轉向用;沒動就回 zero)。
        Vector3 StepTowards(Vector3 target, float speed)
        {
            Vector3 before = transform.position;
            Vector3 next = Vector3.MoveTowards(before, target, speed * Time.deltaTime);
            if (Pizzala.Throwing.PickupExclusionZone.Contains(next))
                return Vector3.zero; // 下一步在禁區內:停在邊界外,這幀不移動
            transform.position = next;
            Vector3 moved = next - before;
            moved.y = 0f;
            return moved;
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
            if (isThrowingAnim) return; // 出手動畫播放中,別搶回 idle/walk 狀態
            if (walking == isWalking) return;
            isWalking = walking;
            if (animator == null) return;
            if (!walking) animator.speed = 1f; // 回到站立:動畫用正常速度
            animator.SetBool(WalkingHash, walking); // Idle ↔ Walk 由 controller 內 transition 切換
        }

        // Throw state 標名(controller 內丟回狀態的名字);播完自動 transition 回 Idle。
        const string ThrowStateName = "Throw";

        // 丟回披薩的出手動畫:SetTrigger("Throw") 播一次,播完回 Idle。
        // 播放期間 isThrowingAnim 鎖住 SetWalking。由 GameManager 在丟回發射的瞬間呼叫。
        public void PlayThrow()
        {
            if (animator == null) return;
            StartCoroutine(ThrowAnimRoutine());
        }

        IEnumerator ThrowAnimRoutine()
        {
            isThrowingAnim = true;
            isWalking = false;
            animator.speed = 1f;             // 出手動畫用正常速度,不受走路變速影響
            animator.SetBool(WalkingHash, false); // 先離開 Walk,免得 Walking 還開著搶回去
            animator.SetTrigger(ThrowHash);

            // 等 animator 進到 Throw 狀態(trigger 到實際切換有幾幀延遲),
            // 進到後再等它播完;沒有 Throw 狀態(controller 未接)就退回固定秒數。
            float timeout = Time.time + 0.5f;
            while (Time.time < timeout &&
                   !animator.GetCurrentAnimatorStateInfo(0).IsName(ThrowStateName))
                yield return null;

            if (animator.GetCurrentAnimatorStateInfo(0).IsName(ThrowStateName))
            {
                // 等到 normalizedTime 走完一輪(非 loop 的 Throw 播一次即 >=1)
                while (animator.GetCurrentAnimatorStateInfo(0).IsName(ThrowStateName) &&
                       animator.GetCurrentAnimatorStateInfo(0).normalizedTime < 0.98f)
                    yield return null;
            }
            else
            {
                yield return new WaitForSeconds(1f); // 保底:沒偵測到 Throw 狀態
            }

            isThrowingAnim = false; // 解鎖,下一幀 SetWalking 依遊走狀態重新評估
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
            if (!isActiveAndEnabled) return; // 停用中不接單(PatienceCountdown 的 StartCoroutine 會在 inactive 物件上炸)

            CurrentOrder = flavor;
            HasActiveOrder = true;
            OrderStartTime = Time.time;
            orderPatience = patienceSeconds;
            State = CustomerState.Waiting;

            ShowOrderIcon(flavor);

            if (flavorCountDown != null)
            {
                flavorCountDown.fillAmount = 1f; // 滿水位起跳
                flavorCountDown.enabled = true;
            }

            if (pizzaBox != null) pizzaBox.SetActive(true); // 接單 → 盒子出現

            if (patienceRoutine != null) StopCoroutine(patienceRoutine);
            patienceRoutine = StartCoroutine(PatienceCountdown(patienceSeconds));
        }

        IEnumerator PatienceCountdown(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            if (!HasActiveOrder) yield break;

            // 注意:不呼叫 ClearOrder(它會 StopCoroutine 砍掉本協程,底下 yield 就跑不到)。
            HasActiveOrder = false;
            patienceRoutine = null;
            HideOrderUI();
            State = CustomerState.Angry; // 超時 → 生氣(耐心圈已歸零)
            ShowEmotionIcon(angrySprite);

            // 超時反擊:去撿一顆地上可撿的 pizza 丟回玩家臉;撿不到就直接離場。
            // 開關 = GameManager.throwbackOnTimeout。
            var gm = GameManager.Instance;
            bool retaliate = gm != null && gm.throwbackOnTimeout;
            var ground = retaliate ? Pizzala.Throwing.GroundPizzaRegistry.FindNearestPickable(transform.position) : null;
            if (ground != null)
                yield return RetaliateRoutine(ground);

            OnOrderTimeout?.Invoke(this); // 反擊完(或沒得撿)才通知離場
        }

        // 超時反擊:走向地上那顆 pizza → 撿起(消掉它)→ 由 GameManager 走丟回流程 → 結束。
        // 期間 IsRetaliating=true 讓 UpdateWander 讓開;OnOrderTimeout 延到反擊後才發,
        // 所以 CustomerSpawner 的離場流程自然會等到反擊結束才開始。
        IEnumerator RetaliateRoutine(Pizzala.Throwing.PizzaProjectile ground)
        {
            IsRetaliating = true;
            var flavor = ground.flavor;

            // 走向目標披薩(用暴躁速度衝過去)。
            // 保底逾時:萬一直線路徑貼著排除區邊界、StepTowards 一直卡住不前進,
            // 也不要讓這個 while 空轉卡死客人(離場流程在等它),超時就放棄改直接離場。
            float walkDeadline = Time.time + 6f;
            while (ground != null && Time.time < walkDeadline)
            {
                Vector3 target = ground.transform.position;
                target.y = transform.position.y; // 只在水平面移動
                if (Vector3.Distance(transform.position, target) <= pickupReach) break;

                Vector3 dir = StepTowards(target, urgentSpeed); // 一樣守住排除區,不穿過玩家
                if (dir.sqrMagnitude > 1e-8f)
                    transform.rotation = Quaternion.Slerp(
                        transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * 8f);

                SetWalking(true);
                if (animator != null && !isThrowingAnim)
                    animator.speed = Mathf.Max(0.1f, urgentSpeed / Mathf.Max(walkAnimBaseSpeed, 0.01f));
                yield return null;
            }
            SetWalking(false);

            if (ground != null) Destroy(ground.gameObject); // 撿起來 = 地上那顆消失

            // 丟回玩家臉(走真正的丟回流程:預警→出手動畫→發射)
            if (GameManager.Instance != null)
                GameManager.Instance.ThrowBackFromCustomer(this, flavor);

            // 等丟回動作大致跑完(預警+出手約 telegraph+release+一點餘裕)再放行離場
            yield return new WaitForSeconds(2f);
            IsRetaliating = false;
        }

        // 盒中生成指定口味的展示 pizza(對、錯口味都會呈現丟進去的那顆)。
        // 由 GameManager 在 ResolveHandCatch 接到 Hand 時呼叫(丟中的 pizza 由 GameManager 另外銷毀)。
        public void ShowPizzaInBox(PizzaFlavor flavor)
        {
            if (pizzaBoxSlot == null)
            { Debug.LogWarning($"[CustomerController] {name}: pizzaBoxSlot 未接,盒中不會生成 pizza"); return; }
            if (boxPizzaByFlavor == null || (int)flavor >= boxPizzaByFlavor.Length)
            { Debug.LogWarning($"[CustomerController] {name}: boxPizzaByFlavor 沒有 {flavor}(index {(int)flavor})對應的格子"); return; }

            var prefab = boxPizzaByFlavor[(int)flavor];
            if (prefab == null)
            { Debug.LogWarning($"[CustomerController] {name}: boxPizzaByFlavor[{(int)flavor}] 是空的"); return; }

            if (currentBoxPizza != null) Destroy(currentBoxPizza); // 先清掉上一顆,避免疊
            currentBoxPizza = Instantiate(prefab, pizzaBoxSlot.position, pizzaBoxSlot.rotation, pizzaBoxSlot);
        }

        // 清掉盒中那顆展示 pizza(錯口味丟回時,由 GameManager 在丟回發射瞬間呼叫,
        // 讓盒中那顆剛好在披薩被丟出去的時間點消失)。
        public void ClearBoxPizza()
        {
            if (currentBoxPizza == null) return; // 沒有盒中 pizza(例:砸臉丟回)就別動盒子

            Destroy(currentBoxPizza);
            currentBoxPizza = null;

            // 錯口味丟回:訂單早就結束了,那顆飛出去的同時把盒子也收掉
            if (!HasActiveOrder && pizzaBox != null) pizzaBox.SetActive(false);
        }

        // 關盒動畫(只有接住正確口味才關);播完才把盒子收起來。
        public void CloseBox()
        {
            if (pizzaBoxAnimation == null || string.IsNullOrEmpty(pizzaBoxCloseClip))
            { Debug.LogWarning($"[CustomerController] {name}: pizzaBoxAnimation / pizzaBoxCloseClip 未接,不播關盒動畫"); return; }

            boxClosing = true; // 擋住 ClearOrder 提早收盒
            pizzaBoxAnimation.Play(pizzaBoxCloseClip);
            StartCoroutine(HideBoxAfterClose());
        }

        // 等關盒動畫整段播完,才把盒子(連同盒中 pizza)收起來
        IEnumerator HideBoxAfterClose()
        {
            float len = 0.5f;
            var state = pizzaBoxAnimation[pizzaBoxCloseClip];
            if (state != null && state.length > 0.01f) len = state.length;
            yield return new WaitForSeconds(len);
            if (pizzaBox != null) pizzaBox.SetActive(false);
            boxClosing = false;
        }

        // 訂單被解決(成功或口味錯),由 GameManager 呼叫
        public void ResolveOrder(bool satisfied)
        {
            ClearOrder();
            State = satisfied ? CustomerState.Served : CustomerState.Angry;
            ShowEmotionIcon(satisfied ? smileSprite : angrySprite);
            OnOrderResolved?.Invoke(this, satisfied);
        }

        // 頭頂圖示統一由這三個方法管理，避免各狀態直接操作 flavorIcon。
        void ShowOrderIcon(PizzaFlavor flavor)
        {
            int index = (int)flavor;
            if (flavorIcon == null || flavorSprites == null ||
                index < 0 || index >= flavorSprites.Length || flavorSprites[index] == null)
            {
                HideHeadIcon();
                return;
            }

            flavorIcon.sprite = flavorSprites[index];
            flavorIcon.enabled = true;
        }

        void ShowEmotionIcon(Sprite sprite)
        {
            if (flavorCountDown != null) flavorCountDown.enabled = false;

            if (flavorIcon == null || sprite == null)
            {
                HideHeadIcon();
                return;
            }

            flavorIcon.sprite = sprite;
            flavorIcon.enabled = true;
        }

        void HideHeadIcon()
        {
            if (flavorIcon == null) return;
            flavorIcon.enabled = false;
            flavorIcon.sprite = null;
        }

        // 送餐錯誤由 GameManager 判定，實際顯示仍集中在 CustomerController。
        public void ShowAngryEmotion()
        {
            ShowEmotionIcon(angrySprite);
        }

        void ClearOrder()
        {
            HasActiveOrder = false;
            if (patienceRoutine != null) { StopCoroutine(patienceRoutine); patienceRoutine = null; }
            HideOrderUI();
        }

        // 收掉菜單/倒數/盒子。抽出來讓 PatienceCountdown 超時時能用,
        // 又不必呼叫 ClearOrder(那會 StopCoroutine 把自己這條協程砍掉)。
        void HideOrderUI()
        {
            HideHeadIcon();
            if (flavorCountDown != null) flavorCountDown.enabled = false;

            // 訂單結束就收盒,菜單和盒子一起消失(避免「沒菜單卻有盒子」)。
            // 兩個例外先留著盒子:關盒動畫播放中、錯口味那顆還在盒裡等丟回。
            if (pizzaBox != null && !boxClosing && currentBoxPizza == null)
                pizzaBox.SetActive(false);
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
