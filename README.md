# Pizzala — VR 披薩投擲實驗遊戲

> **這份文件是全組(和各組員的 AI 助手)的共同起點**:說明這個遊戲是什麼、怎麼玩、有哪些模式、系統怎麼組成、目前做到哪。
> 這裡不寫操作步驟——「怎麼做」請看文末的[文件地圖](#文件地圖)。
> 最後更新:2026-07-14

---

## 一句話介紹

Pizzala 是一款 **Meta Quest 3 的 VR 披薩店遊戲**,玩家用真實的投擲動作把披薩丟給客人;它同時是一個**行為實驗平台**——研究「回合結束時看到的結算畫面形式」如何影響玩家體驗,並完整記錄投擲動作、閃避反應與身體活動量數據。

- 引擎:Unity 6000.3.19f1 + URP + XR Interaction Toolkit(OpenXR / Quest 3)
- 平日開發用 **Quest Link 連 PC 按 Play** 迭代,Build APK 做脫線驗證

---

## 玩法(核心循環,一回合 3 分鐘)

1. **客人點餐**:場上 3~5 位客人,頭上出現口味圖示,舉手等餐(有耐心倒數)。
2. **玩家出餐**:從面前三座出餐台各拿一種口味的披薩,用真實投擲丟向客人。
3. **命中判定**(每一丟必落在五種結果之一):

   | 結果 | 條件 | 後果 |
   |---|---|---|
   | `Hit` | 丟中客人手掌區 + 口味正確 | 客人開心,訂單完成 |
   | `WrongFlavor` | 到手了但口味錯 | 客人生氣 → **丟回披薩** |
   | `MissFace` | 砸到客人臉 | 客人變髒臉(不復原)+ 自動拍髒臉照 |
   | `MissBody` | 砸到客人身體 | 披薩彈開、客人生氣、身上留髒污 |
   | `MissEnvironment` | 砸到牆/地/桌 | 落點生成醬汁髒污(整場累積) |

4. **丟回與閃避**:訂單超時或口味錯 → 客人閃紅預警約 0.8 秒後把披薩丟回玩家。玩家**側跳或下蹲**可躲開;站著不動會被砸中,整個視野糊上該口味的醬汁(1.2 秒後淡出)。
5. **結算**:3 分鐘到,出現結算畫面(內容依實驗組別而不同,見下節),同時寫出數據 JSON 與照片。

**三種口味(全專案統一,enum 順序固定)**:`0 = Margherita`(瑪格麗特)、`1 = Pepperoni`(臘腸)、`2 = CosmicPinkMarshmallow`(宇宙粉紅棉花糖)。

---

## 實驗設計(這個專案存在的理由)

**自變項 = 結算畫面的呈現形式**,由 GameManager 的 `condition` 切換,每位受試者測前設定:

| 組別 | 結算畫面看到什麼 |
|---|---|
| `Control`(對照組) | **數據面板**:準度、最快出手速度、甩腕峰值、投擲方式統計、方位命中率、身體活動量 |
| `Experimental`(實驗組) | **照片牆**:客人髒臉拍立得 + 環境髒污總覽照(+ 玩家被砸中時的髒臉圖) |

兩個版面的美術完成度必須相當(由美術組把關),避免「實驗組比較好看只是因為比較用心做」的混淆變因。

**數據輸出**(每回合一份,PC 在 `AppData/LocalLow/<公司名>/Pizzala/`、Quest 在 `Android/data/<包名>/files/`):

- `sessions/*.json` — 完整 SessionData:每一丟的手勢分類與原始特徵值(出手速度、仰角、腕部角速度……可事後重跑分類)、每次閃避的反應時間與方向、頭部移動距離/深蹲次數/轉身角度、(選配)心率與皮膚電導時間軸
- `photos/` — 客人髒臉特寫 `face_*.png`、環境總覽 `env_*.png`

**投擲手勢分類**:每一丟即時分類為 **反手 / 正手 / 過頭砸 / 低手**(見 `ThrowType`),閾值集中在 `ThrowTuning` 資產可調。

**玩法開關(保險絲)**,demo 前哪個機制不穩就關哪個:

- `enableThrowback` — 丟回機制
- `enforceFlavor` — 口味配對(關掉 = 送到手就算成功)
- `enforceThrowType` — 指定投擲方式玩法(P2 延伸,預設關)

---

## 系統架構

所有遊戲邏輯在 `Assets/Scripts/`,依職責分資料夾;`GameManager` 是唯一的流程總指揮,其餘腳本各管一件事:

| 模組 | 腳本 | 職責 |
|---|---|---|
| **Core** | `GameManager` | 回合流程總指揮:派單、判定、丟回、結算 |
| | `ActivityTracker` | 身體活動量(頭部移動、深蹲、轉身) |
| **Data** | `GameData` | 所有 enum 與數據結構的唯一定義處 |
| | `SessionLogger` | 回合數據收集與 JSON 輸出 |
| | `SensorListener` | (選配)ESP32 心率/GSR 感測器,沒硬體不影響 |
| **Throwing** | `PizzaProjectile` / `PizzaSpawner` | 披薩本體與出餐台自動補貨 |
| | `HandMotionSampler` / `ThrowClassifier` | 控制器軌跡取樣 → 手勢分類 |
| | `ThrowTuning` | 所有可調參數的 ScriptableObject(手感、丟回速度、客人耐心) |
| **Customers** | `CustomerController` / `CustomerHitZone` | 客人狀態機(點餐/表情/超時)與手/臉/身判定區 |
| | `ThrowbackProjectile` / `PlayerHeadHitbox` | 丟回披薩與玩家頭部被擊判定 |
| **Dirt** | `DirtManager` | 醬汁髒污生成與計數 |
| **Photo** | `SnapshotCamera` | 髒臉特寫與環境總覽截圖 |
| **UI** | `FaceSplatOverlay` | 被砸臉的全螢幕醬汁(依口味換圖) |
| | `ResultsScreenController` | 結算畫面(依 condition 切換兩個 Panel) |

**場景**:

- `Assets/Scenes/BackBone.unity` — **主場景**,骨架組裝進行中(場景同時只能一個人動,見協作規則)
- `Assets/Scenes/test.unity` — 隊友的測試場景,想試東西開自己的場景

**Prefab**(統一放 `Assets/Prefabs/`,`PZ_` 開頭):三口味披薩、三口味丟回披薩、17 個醬汁髒污、PhotoEntry 拍立得單元;客人 / 出餐台 / 兩個 Canvas 還在場景組裝階段。口味披薩是**各自模型(pizza1/2/3)的 Prefab Variant**,調物理手感要三個都改。

---

## 目前進度(2026-07-14)

**已完成**
- 全部 18 支骨架腳本(不用再寫程式,剩組裝)
- Quest 3 建置環境 + Quest Link 驗證(7/13)
- 美術資產第一批合併:三口味披薩/丟回披薩/17 個醬汁 Prefab、pizza2/3 模型(7/14,驗收清單見 `VERIFY_MERGE_0714.md`)
- FaceSplatOverlay 支援依口味切換濺灑圖

**進行中**
- `BackBone.unity` 主場景骨架組裝(照 `BUILD_STEPS.md` 逐節推進,目前做到結算畫面一帶)

**待辦 / 等美術**
- 表情貼圖 ×4、口味圖示 ×3、醬汁濺灑貼圖、玩家髒臉圖、全螢幕濺醬圖(佔位缺口不擋骨架驗收,清單見 `BUILD_STEPS.md` 文末)
- 客人 Prefab、出餐台、端到端驗收、Build APK 脫線實測
- Day 6:手感調參(`ThrowTuning`)

---

## 協作規則(三條鐵律)

1. **`.meta` 檔一定跟著資產一起 commit**,否則別人 pull 下來 Missing Script / 粉紅材質。
2. **一個場景同時只能一個人動**;隊友測試開自己的場景(如 `test.unity`),交付以 Prefab 為單位。
3. **`ProjectSettings/` 與 XR 設定以 main 為準**,合併衝突一律保留 main(main 上是驗證過 Quest Link 可用的版本)。

合併他人成果照 `MERGE_GUIDE.md` 走,不要直接 merge。

---

## 文件地圖

| 我想…… | 看這份 |
|---|---|
| 了解專案全貌(本文) | `README.md` |
| 設定專案 / Quest 3 建置 / 調參對照表 | [Docs/SETUP.md](Docs/SETUP.md) |
| 逐步組場景(點哪裡、填什麼值) | [Docs/BUILD_STEPS.md](Docs/BUILD_STEPS.md) |
| 每個 Prefab 的規格與驗收標準 | [Docs/PREFABS.md](Docs/PREFABS.md) |
| 美術組上手(Unity/GitHub 新手向) | [Docs/ART_WORKFLOW.md](Docs/ART_WORKFLOW.md) |
| 美術 / 軟體驗收清單 | [Docs/ACCEPTANCE_ART.md](Docs/ACCEPTANCE_ART.md) / [Docs/ACCEPTANCE_Unity.md](Docs/ACCEPTANCE_Unity.md) |
| 合併隊友分支的標準流程 | [Docs/MERGE_GUIDE.md](Docs/MERGE_GUIDE.md) |
| 7/14 美術合併的驗收紀錄 | [Docs/VERIFY_MERGE_0714.md](Docs/VERIFY_MERGE_0714.md) |

> **給 AI 助手**:先讀本文建立全貌,再依任務進對應文件。數據結構與 enum 一律以 [GameData.cs](Assets/Scripts/Data/GameData.cs) 為準;流程接線以 [GameManager.cs](Assets/Scripts/Core/GameManager.cs) 頂部註解為準。修改場景前先確認沒有其他人正在動同一個場景。
