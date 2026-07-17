# 可調整參數總表（PARAMETERS.md）

Pizzala 所有可以在 Unity 編輯器裡調整的參數都整理在這裡。

**維護規則**：新增、刪除或改名任何 `public` 序列化欄位（含 `[SerializeField]`）時，同步更新本文件對應的表格；改預設值也要更新「預設值」欄。

調整位置分兩種：

- **ThrowTuning 資產**：大部分玩法參數集中在 [Assets/Settings/ThrowTuning.asset](../Assets/Settings/ThrowTuning.asset)，選取後直接在 Inspector 改，**不用改程式碼、不用重新 build**。
- **元件 Inspector**：其餘參數掛在場景（BackBone.unity）或 Prefab 的元件上，在該物件的 Inspector 調整。

---

## 1. ThrowTuning 資產（玩法核心參數）

來源：[ThrowTuning.cs](../Assets/Scripts/Throwing/ThrowTuning.cs)
位置：`Assets/Settings/ThrowTuning.asset`，拖進 GameManager 的 `tuning` 欄位使用。

### 手勢分類閾值

| 參數 | 預設值 | 說明 |
|---|---|---|
| `overheadHeightOffset` | 0.05 | 放手點高於頭部多少公尺算「過頭砸」 |
| `underhandHeightOffset` | 0.45 | 放手點低於頭部多少公尺算「低手拋」（約腰部） |
| `underhandMinElevationDeg` | 15 | 低手拋最少要有的出手仰角（度） |
| `overheadVerticalRatio` | 0.45 | 過頭砸的軌跡垂直成分比例下限（0~1） |
| `crossBodyThreshold` | 0.05 | 揮動起點越過身體中線多少公尺算「反手」 |
| `swingSpeedFraction` | 0.3 | 認定「揮動段」的速度門檻（峰值速度的比例，範圍 0.1~0.6） |
| `swingWindowSeconds` | 0.6 | 分類時往回看的軌跡長度（秒） |

### 客人耐心與情緒加速

| 參數 | 預設值 | 說明 |
|---|---|---|
| `customerPatience` | 15 | 客人耐心（秒），超時就生氣 |
| `customerImpatientAt` | 5 | 等餐幾秒後開始不耐煩（慢速遊走） |
| `customerUrgentAt` | 10 | 等餐幾秒後變暴躁（快速遊走），需小於 `customerPatience` |
| `customerImpatientMoveSpeed` | 0.4 | 不耐煩時的遊走速度（m/s） |
| `customerUrgentMoveSpeed` | 0.9 | 暴躁時的遊走速度（m/s） |
| `customerWanderRadius` | 0.7 | 遊走範圍半徑（公尺），以站位點為圓心 |
| `customerWanderPauseChance` | 0.7 | 走到遊走目標點後停頓的機率（0~1） |
| `customerWanderPauseMinSeconds` | 1 | 每次停頓的秒數下限 |
| `customerWanderPauseMaxSeconds` | 2.5 | 每次停頓的秒數上限 |
| `customerWalkAnimBaseSpeed` | 0.5 | 走路動畫播 1x 時對應的移動速度（m/s）；移動越快動畫播越快，避免快走腳步打滑。調小＝同速下動畫更快 |

### 客人動態生成（CustomerSpawner 讀取）

| 參數 | 預設值 | 說明 |
|---|---|---|
| `initialSpawnCount` | 2 | 回合開始時 Spawner 先補幾個客人（場景預擺的不算） |
| `minSpawnInterval` | 8 | 之後每隔幾秒生成一個新客人（下限） |
| `maxSpawnInterval` | 15 | 之後每隔幾秒生成一個新客人（上限） |
| `customerLifetime` | 40 | 生成客人閒著沒訂單的最長停留秒數，超過就離場讓位 |
| `customerLeaveDelay` | 2.5 | 訂單結束（滿意/生氣）後停留幾秒才離場 |
| `customerPickupReach` | 0.8 | 超時客人撿地上 pizza 反擊：走到離目標披薩多近算「撿到」（公尺） |

### 丟回機制

| 參數 | 預設值 | 說明 |
|---|---|---|
| `telegraphSeconds` | 0.8 | 丟回前的預警時間（秒），給玩家反應窗口 |
| `throwbackSpeed` | 5 | 丟回披薩的飛行速度 m/s（越慢越好躲） |
| `throwbackReleaseDelay` | 0.3 | 出手動畫開始後等幾秒披薩才真正離手；調到動畫揮臂放手那一刻，讓披薩飛出時機對上動作 |
| `faceHitThrowbackChance` | 1 | 被砸到臉時觸發丟回的機率（0~1）；1=一定丟回、0=從不。丟錯口味的丟回不受此影響 |

### 回合設定

| 參數 | 預設值 | 說明 |
|---|---|---|
| `roundDurationSeconds` | 180 | 一回合長度（秒） |
| `orderIntervalSeconds` | 6 | 每隔幾秒產生一張新訂單 |

### 飛盤氣動模擬（FrisbeeFlight 讀取，Hummel/Hubbard 飛盤模型）

三顆披薩共用；透過 `GameManager.Instance.tuning` 讀取，改這裡就好，Play 中即時生效。
自旋來自使用者手腕甩的力道（XR Throw On Detach 轉進 `rb.angularVelocity`），這裡只決定「飛不飛得起來」與氣動力大小。俯仰/滾轉/自旋衰減力矩經陀螺進動產生真實飛盤的 turn & fade 弧線。

> 披薩 Prefab 的 Rigidbody 基準（`PZ_Pizza_*`，為配合此氣動模型調過）：Mass 0.175 kg、Angular Damping 0、Linear Damping 0、Interpolate = Interpolate、Collision Detection = Continuous Dynamic、Movement Type（XR Grab）= Velocity Tracking。慣性張量用 collider 自動計算（扁盒→盤軸慣量 ≈ 2× 徑向，天生有陀螺穩定）。

| 參數 | 預設值 | 說明 |
|---|---|---|
| `frisbeeSpinToFly` | 6 | 自旋要多大（rad/s）盤子才產生升力、開始平飛；甩不夠力就直接掉。VR 手甩約 5~10，故調低好觸發 |
| `frisbeeSpinRatioThreshold` | 0.5 | 自旋純度門檻 0~1：(沿盤軸自旋)² / 總角速度²，越接近 1 越嚴格。VR 帶手臂雜訊，放寬 |
| `frisbeeMaxAngularVelocity` | 100 | 剛體自旋上限（rad/s）。Unity 預設只有 7 會砍掉自旋，飛盤必須調高 |
| `frisbeeAeroScale` | 3 | 氣動力整體倍率（升力/阻力/力矩一起）；VR 出手慢(力∝V²)故放大補償，太飄調低、太重調高 |
| `frisbeeArea` | 0.0568 | 盤面參考面積 A（m²） |
| `frisbeeCL0` | 0.1 | 升力係數：零攻角升力 CL0 |
| `frisbeeCLA` | 1.4 | 升力係數：每弧度攻角的升力斜率 CLα |
| `frisbeeCD0` | 0.08 | 阻力係數：零攻角阻力 CD0 |
| `frisbeeCDA` | 2.72 | 阻力係數：攻角平方項 CDα |
| `frisbeeAlpha0Deg` | -4 | 最小阻力對應的攻角 α0（度） |
| `frisbeeDiameter` | 0.21 | 盤直徑 d（m），力矩的力臂 |
| `frisbeeCM0` | -0.08 | 俯仰力矩：零攻角俯仰（負=天生低頭，配合自旋產生 fade） |
| `frisbeeCMA` | 0.43 | 俯仰力矩：每弧度攻角 CMα |
| `frisbeeCMq` | -0.005 | 俯仰力矩：俯仰角速度阻尼 CMq |
| `frisbeeCRR` | 0.014 | 滾轉力矩：隨自旋 CRr（配合陀螺效應造成 turn/bank） |
| `frisbeeCRP` | -0.0055 | 滾轉力矩：滾轉角速度阻尼 CRp |
| `frisbeeCNR` | -0.0000071 | 自旋衰減力矩 CNr（自旋隨飛行慢慢變慢） |
| `frisbeeWobbleDamping` | 3 | 晃動阻尼（1/s）：吃掉盤面翻擺（垂直盤軸角速度），效果隨自旋縮放。0=最會晃、調高=更穩 |

### 飛盤抓取（FrisbeeGrabInteractable 讀取）

| 參數 | 預設值 | 說明 |
|---|---|---|
| `frisbeeDiscRadius` | 0.15 | 盤半徑（公尺）；≤ 0 會從披薩的 BoxCollider 自動推算 |
| `frisbeeGripHeight` | 0 | 握點相對盤面的高度（本地 Y，公尺）；0 = 盤面中線 |
| `frisbeeAlignToController` | true | 抓取時把盤面對齊控制器（而非保留抓取當下的傾角） |
| `frisbeeGripRotationEuler` | (0,0,0) | 盤面相對控制器的旋轉 offset（尤拉角，度）；`frisbeeAlignToController` 開時生效 |

---

## 2. GameManager（場景 BackBone → GameManager 物件）

來源：[GameManager.cs](../Assets/Scripts/Core/GameManager.cs)

| 參數 | 預設值 | 說明 |
|---|---|---|
| `condition` | Control | 實驗條件（Control / Experimental） |
| `participantId` | "P00" | 受試者編號 |
| `enableThrowback` | true | 玩法保險絲：關掉就完全沒有丟回 |
| `throwbackOnTimeout` | false | 訂單超時的客人是否去撿地上 pizza 丟回玩家？撿不到（場上沒可撿的）就直接離場。關 = 超時直接離場 |
| `enforceFlavor` | true | 是否檢查口味正確 |
| `enforceThrowType` | false | 是否檢查投擲手勢類型 |
| `autoStart` | false | 關 = 等開始畫面按 B 才開始（正式流程）；開 = 延遲後自動開始，方便沒有開始畫面時測試 |
| `autoStartDelay` | 5 | 自動開始的延遲（秒） |
| `bossCommentService` | （空） | Boss 評論服務（Gemini）；只有實驗組會用到，留空則跳過 |
| `boothScreen` | （空） | 攤位上即時顯示命中數／剩餘時間的螢幕；留空則跳過 |

---

## 3. CustomerSpawner（客人生成器）

來源：[CustomerSpawner.cs](../Assets/Scripts/Customers/CustomerSpawner.cs)
（生成節奏在 ThrowTuning 的「客人動態生成」區；這裡是空間佈局。）

| 參數 | 預設值 | 說明 |
|---|---|---|
| `customerPrefabs` | （空） | 客人 Prefab 清單，生成時均勻隨機挑一個（多角色混合，例：PZ_Customer + PZ_Customer_UncleB）。留空則用 `customerPrefab` |
| `customerPrefab` | PZ_Customer | 單一客人 Prefab（舊欄位／備援）；`customerPrefabs` 為空時才用 |
| `sectorCount` | 6 | 整圈均分幾個扇區（越多槽位越密） |
| `sectorJitter` | 0.35 | 扇區內的角度隨機抖動比例（0~0.5），0 = 永遠站正中央 |
| `distanceTiers` | (2.4~3), (4.6~5.4) | 距離層：每層一個半徑帶（`minRadius` / `maxRadius`，公尺） |
| `maxSpawnedCustomers` | 6 | 同時在場的生成客人上限（不含場景預擺的） |
| `minSpacing` | 1.5 | 生成點與任何現有客人的最小間距（公尺） |

---

## 4. 披薩補貨（PizzaSpawner）

> 飛盤手感參數（自旋／穩定／升力／盤半徑／盤面對齊）已集中到 ThrowTuning，見上面 [1. ThrowTuning → 飛盤物理](#飛盤物理frisbeeflight--frisbeeedgegrab-讀取)。`FrisbeeFlight` / `FrisbeeGrabInteractable` 元件本身已無可調欄位。

### PizzaSpawner（[PizzaSpawner.cs](../Assets/Scripts/Throwing/PizzaSpawner.cs)）

| 參數 | 預設值 | 說明 |
|---|---|---|
| `respawnDelay` | 1.5 | 補貨延遲（秒） |
| `leaveDistance` | 0.5 | 披薩離開生成點多遠就準備補貨（公尺） |

### PickupExclusionZone（[PickupExclusionZone.cs](../Assets/Scripts/Throwing/PickupExclusionZone.cs)）

超時客人撿地上 pizza 的「不可撿」範圍。掛在場景物件上、物件需帶 Collider（勾 Is Trigger），在 Scene 視圖拖大小/位置圈住出餐台、玩家周圍補貨圈等。可擺多個；無可調欄位，範圍就是那顆 Collider。地上 pizza 由 `PizzaProjectile` 落地（命中環境）時自動登記進 `GroundPizzaRegistry`。

---

## 5. 髒污與醬料

### DirtManager（[DirtManager.cs](../Assets/Scripts/Dirt/DirtManager.cs)）

| 參數 | 預設值 | 說明 |
|---|---|---|
| `surfaceOffset` | 0.01 | 髒污沿法線抬起的距離，避免 z-fighting |
| `dropletCount` | 3~5 | 命中平面/牆面時噴出的液滴數（隨機） |
| `dropletCountSteep` | 6~9 | 砸中客人或陡面時的液滴數 |
| `dropletSpeed` | 1.5~3.5 | 液滴初速範圍（m/s） |
| `dropletSize` | 0.045 | 液滴直徑（m） |
| `dropletSplatScale` | 0.35 | 液滴落地生成的小髒污相對縮放 |
| `characterSplatScale` | 0.55 | 砸中客人時貼在身上的髒污縮小比例 |
| `attachToNearestBone` | true | 砸中客人時把髒污掛到離命中點最近的骨頭上，跟著動畫走（染色失敗退回 Decal 時用） |
| `characterDecalDepthScale` | 0.5 | 客人身上的 Decal 投影深度縮放，越小越不會誤染揮過的手臂（染色失敗退回 Decal 時用） |
| `paintOnCharacters` | true | 砸中客人時直接把醬料畫進角色貼圖（texture-space 染色，完全跟著動畫）；失敗才退回 Decal 掛骨頭 |
| `paintSize` | 0.28~0.42 | 畫進角色貼圖的醬料直徑範圍（公尺，隨機取） |
| `paintWrapDepth` | 0.25 | 染色沿命中法線的作用厚度（公尺），太大會染穿到身體另一側 |
| `steepThreshold` | 0.6 | normal.y 低於此值視為陡面（0~1） |
| `dropletHitMask` | ~0（全部） | 液滴落地判定的圖層 |
| `flavorDropletColors` | — | 液滴顏色，依 PizzaFlavor 列舉順序 |

### SauceTrail（[SauceTrail.cs](../Assets/Scripts/Dirt/SauceTrail.cs)，披薩滑行留痕）

| 參數 | 預設值 | 說明 |
|---|---|---|
| `spacing` | 0.18 | 滑行時每移動多遠留一塊痕跡（m） |
| `minSpeed` | 0.25 | 速度低於此值不留痕（視為靜止） |
| `budget` | 12 | 醬料存量：總共能留幾塊痕跡 |
| `startScale` | 0.5 | 第一塊痕跡的縮放，之後隨存量遞減 |
| `minScale` | 0.15 | 痕跡縮放下限 |

---

## 6. UI 與回饋

### FaceSplatOverlay（[FaceSplatOverlay.cs](../Assets/Scripts/UI/FaceSplatOverlay.cs)，玩家被砸臉特效）

| 參數 | 預設值 | 說明 |
|---|---|---|
| `holdSeconds` | 1.2 | 滿版停留秒數 |
| `fadeSeconds` | 2 | 淡出秒數 |

### Billboard（[Billboard.cs](../Assets/Scripts/UI/Billboard.cs)）

| 參數 | 預設值 | 說明 |
|---|---|---|
| `yAxisOnly` | true | 只繞 Y 軸面向玩家（不上下傾斜） |

### SnapshotCamera（[SnapshotCamera.cs](../Assets/Scripts/Photo/SnapshotCamera.cs)）

| 參數 | 預設值 | 說明 |
|---|---|---|
| `resolution` | 512 | 截圖解析度（px） |

---

## 7. 數據收集（實驗用）

### ActivityTracker（[ActivityTracker.cs](../Assets/Scripts/Core/ActivityTracker.cs)）

| 參數 | 預設值 | 說明 |
|---|---|---|
| `squatRatio` | 0.72 | 頭低於站立高度的這個比例算下蹲（0.5~0.9） |
| `recoverRatio` | 0.9 | 回升到這個比例算站起來（0.7~1） |

### SensorListener（[SensorListener.cs](../Assets/Scripts/Data/SensorListener.cs)，ESP32 心率）

| 參數 | 預設值 | 說明 |
|---|---|---|
| `port` | 8765 | UDP 埠號，要和 ESP32 程式的 PORT 一致 |
| `sampleInterval` | 1 | 寫入數據時間軸的間隔（秒） |

---

## 8. 個別客人（CustomerController，掛在 PZ_Customer Prefab）

來源：[CustomerController.cs](../Assets/Scripts/Customers/CustomerController.cs)
（多數行為參數讀 ThrowTuning；這裡只有個體開關。）

| 參數 | 預設值 | 說明 |
|---|---|---|
| `customerId` | — | 客人編號 |
| `requiredThrowType` | Unknown | 進階玩法：指定投擲手勢；Unknown = 不限 |
| `canWander` | true | 是否允許情緒遊走 |
| `flavorCountDown` | （倒數圈 Image） | 頭上耐心倒數圈，UI Image（Filled／Vertical／Bottom）；`fillAmount` 隨剩餘耐心 1→0（水位下降）。留空 = 不顯示 |
| `pizzaBox` | （盒子物件） | Pizza 盒物件（含模型與 HandZone）；點餐前隱藏、接單時才出現。留空 = 一直顯示 |
| `pizzaBoxAnimation` | （盒子 Animation） | Pizza 盒 Animation 元件（舊版動畫）；接住正確口味時播關盒 clip。留空 = 不播關盒 |
| `pizzaBoxCloseClip` | Close | 關盒動畫的 clip 名稱（要和 Animation 元件 clip 清單裡一致） |
| `pizzaBoxSlot` | （盒中位置） | 盒中生成 pizza 的位置（空物件，擺在盒子開口內）。留空 = 不生成盒中 pizza |
| `boxPizzaByFlavor` | — | 盒中展示用 pizza prefab，順序對應 PizzaFlavor（建議用靜態、不可抓取的版本） |
| `idleAnimatorController` | （站立控制器） | 站立動畫 Animator Controller；留空 = 不切動畫 |
| `walkAnimatorController` | （走路控制器） | 走動時切換的 Animator Controller；留空 = 走動不換動畫 |
| `throwAnimatorController` | （丟回控制器） | 丟回披薩出手時切換的 Animator Controller；留空 = 丟回不換動畫 |

---

## 9. DevTools（測試用，不影響正式玩法）

| 元件 | 參數 | 預設值 | 說明 |
|---|---|---|---|
| [ThrowbackTestTrigger.cs](../Assets/Scripts/DevTools/ThrowbackTestTrigger.cs) | `speed` | 6 | 測試丟回的飛行速度 |
| [CustomerOrderTestTrigger.cs](../Assets/Scripts/DevTools/CustomerOrderTestTrigger.cs) | `patienceSeconds` | 8 | 測試訂單的耐心秒數 |
| [ThrowAnimTestTrigger.cs](../Assets/Scripts/DevTools/ThrowAnimTestTrigger.cs) | `flavor` | Margherita | 按 T 直接觸發真實丟回流程（預警→出手動畫→throwbackReleaseDelay→發射），對動畫/延遲時間軸用 |

---

## 更新紀錄

| 日期 | 變更 |
|---|---|
| 2026-07-15 | 初版：掃描全部 Scripts 建立總表 |
| 2026-07-15 | DirtManager 新增 `attachToNearestBone`、`characterDecalDepthScale`（髒污掛骨頭跟動畫、身上投影深度縮放） |
| 2026-07-15 | FrisbeeEdgeGrab 新增 `alignToController` / `gripRotationEuler`：抓取時盤面對齊控制器、角度可調 |
| 2026-07-15 | 飛盤手感參數集中進 ThrowTuning（`frisbee*` 8 項）；FrisbeeFlight / FrisbeeEdgeGrab 改讀 `GameManager.Instance.tuning`，元件不再有可調欄位 |
| 2026-07-17 | CustomerController 新增 `throwAnimatorController`（丟回出手動畫控制器）；補列既有的 `idle/walkAnimatorController`。丟回發射瞬間由 GameManager 呼叫 `PlayThrow()` 播放，播完自動切回 idle。已接到 PZ_Customer(soldier throwing)、PZ_Customer_UncleB(uncleB throwing) 與 UncleBCustomerBuilder |
| 2026-07-17 | ThrowTuning 新增 `throwbackReleaseDelay`（預設 0.3s）：丟回時先播出手動畫，等這個秒數披薩才真正離手發射，用來對齊動畫揮臂放手的時機 |
| 2026-07-17 | CustomerController 新增 Pizza 盒欄位 `pizzaBoxAnimation`（舊版 Animation 元件）/ `pizzaBoxCloseClip` / `pizzaBoxSlot` / `boxPizzaByFlavor`。接到 Hand（對/錯口味皆是）由 `ShowPizzaInBox()` 在盒中生成丟進去那顆口味的 pizza；只有正確口味才 `CloseBox()` 播關盒 clip。GameManager.ResolveHandCatch 兩分支都呼叫，並銷毀丟中的 pizza。錯口味丟回時，盒中那顆由 `ClearBoxPizza()` 在丟回發射瞬間消失（延遲＝`throwbackReleaseDelay`）。新增 `pizzaBox` 欄位：點餐前隱藏盒子、接單（`GiveOrder`）時才顯示；接住正確口味 `CloseBox()` 播完關盒動畫後才把盒子收起來（`SetActive(false)`），不會播到一半消失 |
| 2026-07-17 | CustomerController 移除換貼圖表情機制：刪除序列化欄位 `faceRenderer`、`faceNormal/faceHappy/faceAngry/faceDirty`（情緒改由頭上倒數圈呈現）。狀態列舉更名為三相位語意 `CustomerState { PreOrder, Waiting, Served, Angry }`（原 Idle→PreOrder、Happy→Served）。`GetDirty()` 只標記髒污不再換臉；丟回預警閃紅（`Telegraph`）保留 |
| 2026-07-17 | CustomerController 新增 `flavorCountDown`（頭上耐心倒數圈，UI Image 靠 `fillAmount` 呈現剩餘耐心水位）；接單顯示、耐心歸零/解決時隱藏。ThrowTuning 新增 `customerWalkAnimBaseSpeed`（走路動畫隨移動速度變速，越快越急）|
| 2026-07-18 | 丟錯口味改為丟回但**不離場、訂單保留**（GameManager.ResolveHandCatch else 不再 ResolveOrder）。砸臉丟回改機率觸發：ThrowTuning 新增 `faceHitThrowbackChance`。超時客人改為撿地上 pizza 反擊：新增 `GroundPizzaRegistry`（PizzaProjectile 落地登記）+ `PickupExclusionZone`（場景排除區）；CustomerController.PatienceCountdown 超時走去撿最近可撿的 pizza 丟回、撿不到直接離場，開關＝`GameManager.throwbackOnTimeout`（語意改為「超時撿地上 pizza 丟回」），距離＝`ThrowTuning.customerPickupReach` |
| 2026-07-15 | 飛盤飛行改為 Hummel/Hubbard 氣動模型：自旋改讀手腕實際角速度並提高剛體 maxAngularVelocity，升力由自旋力道與純度 gate；ThrowTuning 飛盤欄位改版（移除 spinPerSpeed/maxSpin/stabilizeStrength/liftPerSpeed，新增 spinToFly/spinRatioThreshold/maxAngularVelocity/aeroScale/area/CL0/CLA/CD0/CDA/alpha0Deg） |
| 2026-07-15 | 加入完整力矩模型（俯仰 CM0/CMA/CMq、滾轉 CRR/CRP、自旋衰減 CNR + 直徑 d）產生 turn & fade；披薩 Prefab Rigidbody 調整為 Mass 0.175、Angular Damping 0、Interpolate |
| 2026-07-15 | 加入 `frisbeeWobbleDamping` 章動阻尼（隨自旋縮放）抑制丟出後的晃動 |
| 2026-07-16 | 針對 VR 出手速度偏慢調整飛盤預設：`frisbeeSpinToFly` 10→6、`frisbeeSpinRatioThreshold` 0.8→0.5、`frisbeeAeroScale` 1→3（升力更易觸發、滑翔與迴旋更明顯）；同步 ThrowTuning.asset |
| 2026-07-16 | 修正邊緣抓取失效：`selectEntered` 監聽晚於 XRGeneralGrabTransformer 快取 attach，改用子類別 `FrisbeeGrabInteractable`（覆寫 `InitializeDynamicAttachPose`，在快取前設定盤緣握點+對齊）；三顆 prefab 的 XRGrabInteractable 換成它，移除 FrisbeeEdgeGrab |
| 2026-07-18 | 修 bug：丟回披薩（`PZ_ThrowbackPizza_*`）抓不到邊緣 — 三顆的 XRGrabInteractable 換成 `FrisbeeGrabInteractable` 並開啟 Use Dynamic Attach |
| 2026-07-18 | 修 bug：丟回披薩被玩家周圍的撿取禁區（PickupExclusionZone）trigger 提前判定，打不到玩家頭 → 玩家臉特效不觸發。`ThrowbackProjectile` 改為只認玩家頭(命中)或實心環境(落地)，略過其他 trigger 區 |
| 2026-07-16 | DirtManager 新增 `paintOnCharacters`、`paintSize`、`paintWrapDepth`：砸中客人改用 texture-space 染色（SaucePaintable 把醬料畫進角色貼圖 UV 空間，完全跟著蒙皮動畫），Decal 掛骨頭降為後備路徑 |
| 2026-07-16 | CustomerSpawner 新增 `customerPrefabs`（客人 Prefab 清單，生成時均勻隨機挑一個，支援多角色混合）；原 `customerPrefab` 保留為備援。搭配新工具 `Tools/Pizzala/Build UncleB Customer Prefab` 產生第二隻角色 UncleB 客人並自動接進生成器 |
| 2026-07-18 | 合併 partner-mvp 分支：`autoStart` 預設 true→false（改為等開始畫面按 B）；GameManager 新增暫停系統（`IsPaused`/`CanThrow`/`PauseRound()`/`ResumeRound()`，暫停靠 `Time.timeScale = 0`）與新欄位 `bossCommentService`、`boothScreen`（攤位即時命中數／倒數畫面） |
