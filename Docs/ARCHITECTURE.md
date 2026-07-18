# Pizzala — 目前進度與系統架構（ARCHITECTURE.md）

> 新組員（和你的 AI）進場先讀這份：上半部是全案進度，下半部是系統怎麼串起來。
> 進度總覽由 Kendell 在**每次合併後**更新；模組間介面有變動時，改動的人順手更新對應段落。

---

## 目前進度總覽（2026-07-16）

| 模組 | 狀態 | 現況一句話 |
|---|---|---|
| 丟擲系統 Throwing | ✅ 可玩 | 飛盤式丟披薩（抓邊緣、空氣力學、手勢分類）完成，三口味披薩 Prefab 齊全，參數集中在 ThrowTuning |
| 客人系統 Customers | ✅ 可玩 | 動態生成（槽位制）＋三段情緒加速＋訂單超時丟回，2026-07-15 移植完成；UncleB／士兵兩套角色 Prefab 已建 |
| 醬料髒污 Dirt | 🔧 進行中 | 命中噴濺（decal＋角色染色）與飛行滴落可用；**貼圖懸浮問題待改善**——希望更像液體潑灑在地面（見 [Problem.md](Problem.md) 第 2 點） |
| UI／拍照 | ✅ 基本完成 | 結算畫面、髒臉照片、玩家視野糊醬完成；新的 UI 美術圖（開始／暫停／評分等）剛進來待接上 |
| 數據紀錄 Data | ✅ 完成 | 整場數據存 JSON（SessionLogger）、ESP32 感測經 UDP 接入（SensorListener） |
| 場景整合 | ✅ 同步 | 主場景 `Assets/Scenes/BackBone.unity` 已同步最新的客人／披薩／醬料／XR 設定（只有 Kendell 動） |

## 遊戲循環（一頁看懂）

一回合的流程，括號內是負責的腳本：

1. **回合開始**：`GameManager.BeginRound()` 重置髒污計數、開始記錄（`SessionLogger`、`ActivityTracker`）。
2. **客人生成**：`CustomerSpawner`（槽位制：左／中／右扇區 × 距離層）動態生成客人，經 `GameManager.RegisterCustomer` 進訂單池並推入 ThrowTuning 的移動參數。
3. **點餐與等待**：`GameManager` 派發訂單，`CustomerController` 狀態機跑「等餐 → 不耐煩 → 暴躁」，等越久遊走越快。
4. **玩家丟披薩**：`PizzaSpawner` 補貨 → 玩家用 XR 抓取（`FrisbeeEdgeGrab` 抓邊緣）→ 放手瞬間 `PizzaProjectile` 呼叫 `GameManager.OnPizzaReleased`，用 `HandMotionSampler` 的軌跡經 `ThrowClassifier` 分類手勢；飛行中 `FrisbeeFlight` 給空氣力學、`SauceSpray` 灑液滴。
5. **落地判定**：`PizzaProjectile` 碰撞 → `GameManager.OnPizzaLanded` 依 `CustomerHitZone` 判定結果（接到手／口味錯／砸臉／砸身／砸環境），砸臉觸發 `SnapshotCamera` 拍髒臉照，未接到的經 `DirtManager` 生成髒污。
6. **客人反擊**：訂單超時 → `GameManager.TryThrowback` 生成 `ThrowbackProjectile` 丟回玩家；砸中玩家頭（`PlayerHeadHitbox`）觸發 `FaceSplatOverlay` 視野糊醬，閃過則記錄閃避方向。
7. **回合結算**：`GameManager.EndRound()` → `SessionLogger` 算統計、存 JSON、`ResultsScreenController.Show()` 顯示結算（含照片牆 `PZ_PhotoEntry`）。

## 模組相依關係

```mermaid
graph TD
    GM[Core: GameManager<br/>回合流程總指揮]
    TT[(ThrowTuning.asset<br/>全部玩法參數)]
    GD[(Data: GameData<br/>資料結構唯一定義)]

    SPAWN[Customers: CustomerSpawner] -->|RegisterCustomer| GM
    CC[Customers: CustomerController] -->|OnOrderTimeout / OnOrderResolved 事件| GM
    GM -->|ApplyTuning、GiveOrder、丟回| CC
    TB[Customers: ThrowbackProjectile] -->|落空生髒污| DIRT
    GM -->|生成| TB

    PP[Throwing: PizzaProjectile] -->|OnPizzaReleased / OnPizzaLanded| GM
    PP --> CLS[Throwing: ThrowClassifier]
    PP --> HMS[Throwing: HandMotionSampler]
    FLY[Throwing: FrisbeeFlight / FrisbeeEdgeGrab] -->|讀 GameManager.Instance.tuning| TT

    GM -->|SpawnSplat| DIRT[Dirt: DirtManager]
    SPRAY[Dirt: SauceSpray / SauceTrail / SauceDroplet] --> DIRT

    GM --> SNAP[Photo: SnapshotCamera]
    GM --> FSO[UI: FaceSplatOverlay]
    GM --> RES[UI: ResultsScreenController]

    GM -->|Record / BuildSummary| LOG[Data: SessionLogger]
    SEN[Data: SensorListener] --> LOG
    GM --> ACT[Core: ActivityTracker]

    GM -.持有.-> TT
    LOG -.讀寫.-> GD
```

文字版重點：

- **`GameManager` 是中樞**（singleton）：持有 `ThrowTuning` 資產、客人清單、各 UI／紀錄元件的引用。丟擲、客人、醬料模組都只跟它對話，彼此不直接引用。
- **共用資產兩個**：`Assets/Settings/ThrowTuning.asset`（所有玩法參數，各模組經 `GameManager.Instance.tuning` 讀取，改欄位要同步 [PARAMETERS.md](PARAMETERS.md)）和 `GameData.cs`（所有 enum／資料結構的唯一定義處，如 `PizzaFlavor`、`ThrowRecord`）。**動這兩個等於動全模組，先在群組講。**
- **singleton 有三個**：`GameManager.Instance`、`DirtManager.Instance`、`SessionLogger.Instance`。
- **事件流**：客人對 GameManager 用 C# event（`OnOrderTimeout`、`OnOrderResolved`）；其餘是直接方法呼叫。
- `Assets/Scripts/DevTools/` 的兩支 TestTrigger 是可獨立測試客人點餐／丟回的開發工具，不進正式流程。

## 開場串接（前導 → 教學 → 遊戲）

整體流程由 `GameFlowController`（狀態機）＋兩個新場景腳本串起：

1. **Intro 場景**（`Assets/Scenes/Intro.unity`，build index 0）：標題畫面按「Start Game」→ `IntroSequenceController` 播前導 Timeline（`PlayableDirector`）。Timeline 上的對話框停等點放 Signal Emitter → `OnDialoguePause()` 暫停等玩家按 trigger 續播。播完 `LoadScene(BackBone)`。
2. **BackBone 進場 → 教學**：`GameFlowController` 開場進 `Tutorial` 狀態，`TutorialController` 用 `VideoPlayer` 播 4 段教學影片；搖桿左右翻頁（與結算共用 `StickFlickReader`），最後一頁按 trigger「開始遊戲」→ 倒數 → `StartRound()`。
3. **結算 → 循環**：`ResultsScreenController` 三頁；翻到最後一頁出現 Share ＋ 單一「回到開頭」按鈕（標「New Player」/「Play Again」皆可，行為相同，不分兩種）。按下一律載回 Intro 全流程——沒有「跳過前導直接重玩」的路徑，每一局都會重看一次教學。

> **不再分實驗組/對照組**：所有玩家看完整三頁結算 + boss note；`GameManager.condition` 只當資料標籤。

## 規劃中／未實作（與現況區隔）

- **醬料液體化**：現在是平面 decal 貼在撞擊點，貼在人身上或斜面會懸浮；目標是像液體潑灑到地面（[Problem.md](Problem.md) 第 2 點）。
- **Intro/教學場景與 prefab 組裝**：程式（IntroSequenceController / TutorialController / PZ_DialogueBox 契約）已就緒，Intro 場景、Timeline、對話框/教學 canvas 尚待在 Unity 內組裝（見 PREFABS.md 對話框契約、交付給 Kendell 的場景清單）。
- 其他新發現的問題記在 [Problem.md](Problem.md)，要動工就照 [WORKFLOW.md](WORKFLOW.md) 開功能分支。

---

## 更新紀錄

- 2026-07-16：建立文件（進度總覽、遊戲循環、模組相依、規劃中項目）。
- 2026-07-18：Game flow 重構——加入開場串接（Intro Timeline 前導 → 教學影片 → 遊戲）、GameFlowController 新增 Tutorial 狀態、結算單一「回到開頭」按鈕（不分 Play Again/New Player，一律回 Intro 重來）；移除結算的實驗分組（所有人三頁）。
