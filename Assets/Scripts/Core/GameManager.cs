// ─────────────────────────────────────────────────────────────
// GameManager.cs — 回合流程總指揮(訂單、判定、丟回、結算)
// 掛載:"Systems" 物件。
// Inspector 必填:
//   condition   — Control(對照組)/ Experimental(實驗組)★實驗分組開關
//   tuning      — ThrowTuning 資產
//   customers   — 場上所有客人
//   snapshotCamera / overviewCameraPoint / faceSplatOverlay /
//   resultsScreen / activityTracker / throwbackPrefab
//   head        — Main Camera(留空自動抓)
// 玩法保險絲:
//   enableThrowback  — 丟回機制,demo 前調不好就關掉
//   enforceFlavor    — 口味配對,關掉=送到手就算成功
//   enforceThrowType — 指定投擲方式玩法(P2,預設關)
// ─────────────────────────────────────────────────────────────
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Pizzala.Data;
using Pizzala.Throwing;
using Pizzala.Customers;
using Pizzala.Dirt;
using Pizzala.Photo;
using Pizzala.UI;
using Pizzala.LLM;

namespace Pizzala.Core
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("實驗設定")]
        public ExperimentCondition condition = ExperimentCondition.Control;
        public string participantId = "P00";

        [Header("玩法開關(保險絲)")]
        public bool enableThrowback = true;

        [Tooltip("訂單超時也丟回?關=只有實際拿到披薩的客人(丟錯口味、被砸臉)才丟回")]
        public bool throwbackOnTimeout = false;

        public bool enforceFlavor = true;
        public bool enforceThrowType = false;

        [Header("回合啟動")]
        public bool autoStart = true;
        public float autoStartDelay = 5f;

        [Header("參數資產")]
        public ThrowTuning tuning;

        [Header("場景參照")]
        public CustomerController[] customers;
        public Transform head;
        public SnapshotCamera snapshotCamera;
        public Transform overviewCameraPoint;
        public FaceSplatOverlay faceSplatOverlay;
        public ResultsScreenController resultsScreen;
        public ActivityTracker activityTracker;
        public BossCommentService bossCommentService; // experimental group only; leave empty to skip

        [Tooltip("後備丟回披薩(下面陣列沒填到的口味用這個)")]
        public GameObject throwbackPrefab;

        [Tooltip("依 PizzaFlavor 列舉順序:0=Margherita 1=Pepperoni 2=CosmicPinkMarshmallow")]
        public GameObject[] throwbackPrefabsByFlavor;

        [Header("玩家髒臉素材(美術提供,依中彈次數選圖,貼圖需勾 Read/Write)")]
        public Texture2D[] playerDirtyFaceTextures;

        public bool RoundActive { get; private set; }

        int missedOrders;
        int playerFaceHitCount;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        void Start()
        {
            if (head == null && Camera.main != null) head = Camera.main.transform;
            foreach (var c in customers)
                if (c != null) c.OnOrderTimeout += HandleOrderTimeout;
            if (autoStart) StartCoroutine(AutoStartRoutine());
        }

        IEnumerator AutoStartRoutine()
        {
            yield return new WaitForSeconds(autoStartDelay);
            StartRound();
        }

        public void StartRound()
        {
            if (RoundActive || tuning == null || SessionLogger.Instance == null) return;
            missedOrders = 0;
            playerFaceHitCount = 0;
            if (DirtManager.Instance != null) DirtManager.Instance.ResetCount();
            SessionLogger.Instance.BeginSession(condition, participantId);
            if (activityTracker != null) activityTracker.Begin();
            RoundActive = true;
            StartCoroutine(RoundLoop());
        }

        IEnumerator RoundLoop()
        {
            float endTime = Time.time + tuning.roundDurationSeconds;
            while (Time.time < endTime)
            {
                GiveOrderToRandomIdleCustomer();
                yield return new WaitForSeconds(tuning.orderIntervalSeconds);
            }
            EndRound();
        }

        void GiveOrderToRandomIdleCustomer()
        {
            var idle = new List<CustomerController>();
            foreach (var c in customers)
                if (c != null && !c.HasActiveOrder) idle.Add(c);
            if (idle.Count == 0) return;

            var pick = idle[Random.Range(0, idle.Count)];
            var flavor = (PizzaFlavor)Random.Range(0, System.Enum.GetValues(typeof(PizzaFlavor)).Length);
            pick.GiveOrder(flavor, tuning.customerPatience);
        }

        // ══ 玩家出手(PizzaProjectile 放手時呼叫)══
        public ThrowRecord OnPizzaReleased(PizzaProjectile pizza, HandMotionSampler sampler)
        {
            if (!RoundActive) return null;

            var record = SessionLogger.Instance.CreateThrow();
            record.thrownFlavor = pizza.flavor;

            if (sampler != null)
            {
                record.hand = sampler.isLeftHand ? "Left" : "Right";
                var swing = sampler.GetRecent(tuning.swingWindowSeconds);
                record.throwType = ThrowClassifier.Classify(swing, head, sampler.isLeftHand, tuning, record.features);
            }
            return record;
        }

        // ══ 披薩落地(PizzaProjectile 碰撞時呼叫)══
        public void OnPizzaLanded(PizzaProjectile pizza, ThrowRecord record,
                                  CustomerHitZone zone, Vector3 point, Vector3 normal, float flightTime)
        {
            if (record == null) return;

            record.flightTime = flightTime;
            record.landingPosition = point;

            // 方位歸屬:打中誰算誰;打偏就歸給最近的待餐客人
            var attributed = (zone != null && zone.customer != null)
                             ? zone.customer : FindNearestActiveOrderCustomer(point);
            if (attributed != null)
            {
                record.targetCustomerId = attributed.customerId;
                record.targetSector = attributed.sector;
                if (attributed.HasActiveOrder)
                {
                    record.requestedFlavor = attributed.CurrentOrder;
                    record.reactionTime = (SessionLogger.Instance.SessionStartTime + record.gameTime)
                                          - attributed.OrderStartTime;
                }
            }

            if (zone != null && zone.customer != null)
            {
                switch (zone.zone)
                {
                    case HitZoneType.Hand:
                        ResolveHandCatch(record, zone.customer, pizza);
                        break;

                    case HitZoneType.Face:
                        record.outcome = ThrowOutcome.MissFace;
                        zone.customer.GetDirty();
                        if (snapshotCamera != null)
                        {
                            record.photoPath = snapshotCamera.CaptureAt(zone.customer.faceAnchor);
                            SessionLogger.Instance.AddCustomerFacePhoto(record.photoPath, "Hit in the face");
                        }
                        TryThrowback(zone.customer, pizza.flavor); // 砸到臉的那顆被丟回來
                        break;

                    case HitZoneType.Body:
                        record.outcome = ThrowOutcome.MissBody;
                        zone.customer.GetDirty();
                        if (DirtManager.Instance != null)
                            DirtManager.Instance.SpawnSplat(point, normal, pizza.flavor, zone.customer.transform);
                        break;
                }
            }
            else
            {
                record.outcome = ThrowOutcome.MissEnvironment;
                if (DirtManager.Instance != null) DirtManager.Instance.SpawnSplat(point, normal, pizza.flavor);
            }

            SessionLogger.Instance.Record(record);
        }

        void ResolveHandCatch(ThrowRecord record, CustomerController customer, PizzaProjectile pizza)
        {
            if (!customer.HasActiveOrder)
            {
                record.outcome = ThrowOutcome.MissBody; // 沒點餐硬塞不算命中
                return;
            }

            bool flavorOk = !enforceFlavor || pizza.flavor == customer.CurrentOrder;
            bool throwOk = !enforceThrowType
                           || customer.requiredThrowType == ThrowType.Unknown
                           || record.throwType == customer.requiredThrowType;

            if (flavorOk && throwOk)
            {
                record.outcome = ThrowOutcome.Hit;
                customer.ResolveOrder(true);
                Destroy(pizza.gameObject, 0.5f); // 客人收下披薩
            }
            else
            {
                record.outcome = ThrowOutcome.WrongFlavor;
                customer.ResolveOrder(false);
                Destroy(pizza.gameObject, 0.5f);        // 客人接住那顆錯的
                TryThrowback(customer, pizza.flavor);   // 再原樣丟回來
            }
        }

        void HandleOrderTimeout(CustomerController customer)
        {
            missedOrders++;
            if (!throwbackOnTimeout)
            {
                Debug.Log($"[GameManager] 客人 {customer.customerId} 訂單超時(沒拿到披薩,不丟回)");
                return;
            }
            Debug.Log($"[GameManager] 客人 {customer.customerId} 訂單超時 → 準備丟回");
            TryThrowback(customer, customer.CurrentOrder);
        }

        CustomerController FindNearestActiveOrderCustomer(Vector3 point)
        {
            CustomerController best = null;
            float bestDist = float.MaxValue;
            foreach (var c in customers)
            {
                if (c == null || !c.HasActiveOrder) continue;
                float d = Vector3.Distance(c.transform.position, point);
                if (d < bestDist) { bestDist = d; best = c; }
            }
            return best;
        }

        // ══ 丟回 & 閃避 ══
        void TryThrowback(CustomerController customer, PizzaFlavor flavor)
        {
            var prefab = PickThrowbackPrefab(flavor);
            if (!enableThrowback || !RoundActive || prefab == null || head == null) return;
            StartCoroutine(ThrowbackRoutine(customer, flavor, prefab));
        }

        GameObject PickThrowbackPrefab(PizzaFlavor flavor)
        {
            if (throwbackPrefabsByFlavor != null && (int)flavor < throwbackPrefabsByFlavor.Length
                && throwbackPrefabsByFlavor[(int)flavor] != null)
                return throwbackPrefabsByFlavor[(int)flavor];
            return throwbackPrefab;
        }

        IEnumerator ThrowbackRoutine(CustomerController customer, PizzaFlavor flavor, GameObject prefab)
        {
            float telegraphStart = Time.time;
            Vector3 headStart = head.position;

            yield return customer.Telegraph(tuning.telegraphSeconds, null);

            Vector3 origin = customer.throwOrigin != null
                             ? customer.throwOrigin.position
                             : customer.transform.position + Vector3.up * 1.4f;

            var go = Instantiate(prefab, origin, Quaternion.identity);
            var proj = go.GetComponent<ThrowbackProjectile>();
            if (proj == null) { Destroy(go); yield break; }
            proj.flavor = flavor;

            // 出生點在客人胸前、BodyZone 膠囊「內部」,不忽略碰撞會被客人自己擋下來
            foreach (var customerCol in customer.GetComponentsInChildren<Collider>())
                foreach (var pizzaCol in go.GetComponentsInChildren<Collider>())
                    Physics.IgnoreCollision(pizzaCol, customerCol, true);

            Debug.Log($"[GameManager] 客人 {customer.customerId} 丟回披薩發射(口味 {flavor})");

            bool resolved = false, hitPlayer = false;
            proj.onResolved = h => { resolved = true; hitPlayer = h; };
            proj.Launch(head.position, tuning.throwbackSpeed); // 鎖定發射瞬間的頭部位置,不追蹤

            float reactionTime = -1f;
            float timeout = Time.time + 4f;
            while (!resolved && Time.time < timeout)
            {
                if (reactionTime < 0f && HeadDisplacement(headStart) > 0.15f)
                    reactionTime = Time.time - telegraphStart;
                yield return null;
            }

            SessionLogger.Instance.RecordDodge(!hitPlayer, reactionTime, ClassifyDodgeDirection(headStart));

            if (hitPlayer)
            {
                playerFaceHitCount++;
                if (faceSplatOverlay != null) faceSplatOverlay.Show(flavor);
            }
        }

        float HeadDisplacement(Vector3 start)
        {
            Vector3 d = head.position - start;
            float horizontal = new Vector2(d.x, d.z).magnitude;
            float duck = Mathf.Abs(Mathf.Min(d.y, 0f));
            return Mathf.Max(horizontal, duck);
        }

        DodgeDirection ClassifyDodgeDirection(Vector3 start)
        {
            Vector3 d = head.position - start;
            if (d.y < -0.25f) return DodgeDirection.Duck;
            float lateral = Vector3.Dot(d, head.right);
            if (Mathf.Abs(lateral) < 0.12f) return DodgeDirection.None;
            return lateral < 0f ? DodgeDirection.Left : DodgeDirection.Right;
        }

        // ══ 結算 ══
        public void EndRound()
        {
            if (!RoundActive) return;
            RoundActive = false;
            StopAllCoroutines();
            if (activityTracker != null) activityTracker.End();

            // 環境髒亂總覽照(實驗組素材)
            if (snapshotCamera != null && overviewCameraPoint != null)
                SessionLogger.Instance.AddEnvironmentPhoto(snapshotCamera.CaptureFrom(overviewCameraPoint), "Store overview");

            SavePlayerFacePhoto();

            var act = activityTracker;
            SessionLogger.Instance.BuildSummary(
                DirtManager.Instance != null ? DirtManager.Instance.DirtCount : 0,
                missedOrders,
                act != null ? act.TotalHeadDistance : 0f,
                act != null ? act.SquatCount : 0,
                act != null ? act.TurnDegreesTotal : 0f);

            var session = SessionLogger.Instance.Session;
            if (resultsScreen != null) resultsScreen.Show(session); // shows a "writing..." placeholder for the boss note

            if (bossCommentService != null && session.condition == ExperimentCondition.Experimental)
            {
                // Async - don't block EndRound() on the network call. Save happens once the
                // comment (or its fallback) is in hand, so the logged JSON always has it.
                bossCommentService.GetComment(session.summary, comment =>
                {
                    session.bossComment = comment;
                    if (resultsScreen != null) resultsScreen.SetBossComment(comment);
                    SessionLogger.Instance.SaveToDisk();
                });
            }
            else
            {
                SessionLogger.Instance.SaveToDisk();
            }
        }

        // 依中彈次數挑一張「玩家髒臉」合成圖存檔(美術產出 2~3 個髒度層次)
        void SavePlayerFacePhoto()
        {
            if (playerFaceHitCount <= 0 || playerDirtyFaceTextures == null || playerDirtyFaceTextures.Length == 0)
                return;

            var tex = playerDirtyFaceTextures[Mathf.Min(playerFaceHitCount - 1, playerDirtyFaceTextures.Length - 1)];
            if (tex == null) return;

            try
            {
                byte[] png = tex.EncodeToPNG();
                string dir = System.IO.Path.Combine(Application.persistentDataPath, "photos");
                System.IO.Directory.CreateDirectory(dir);
                string path = System.IO.Path.Combine(dir, $"player_{System.DateTime.Now:HHmmss}.png");
                System.IO.File.WriteAllBytes(path, png);
                SessionLogger.Instance.AddPlayerFacePhoto(path, $"Got hit {playerFaceHitCount}x");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[GameManager] 玩家髒臉圖存檔失敗(貼圖要勾 Read/Write Enabled):{e.Message}");
            }
        }
    }
}
