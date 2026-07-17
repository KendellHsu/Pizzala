# Pizzala — 骨架組裝逐步操作手冊(Day 1)

> 這份文件是 [SETUP.md](SETUP.md) 第 3 節的展開版:每一步寫到「點哪裡、填什麼值」的粒度,照順序做即可。
> 每個 ✋ **驗收點** 做完請按 **Ctrl+S 存檔**,然後喊 Claude 讀檔確認,再繼續下一節。
> 前提:XR 套件與 Quest Link 設定已完成(已於 7/13 驗證);Editor 開著 `Assets/Scenes/SampleScene.unity`。

---

## 0. 環境地板(披薩要有東西可以砸)

1. Hierarchy 右鍵 → **3D Object → Plane**,改名 `Floor`,Position 歸零 (0, 0, 0)
2. Hierarchy 右鍵 → **3D Object → Cube**,改名 `Wall_Back`:
   - Position (0, 1.5, 5),Scale (10, 3, 0.2)
3. (可選,美術場景)把 `Assets/Art/scene/pisa_disk_v1/pizza_restaurant.fbx` 拖進場景試擺。
   **注意**:fbx 拖進來預設沒有 Collider,要在根物件 Add Component → **Mesh Collider**(子物件有分開的 Mesh 就各加各的),否則披薩會直接穿過去。今天骨架用 Plane + Cube 就夠,餐廳模型可以之後再整合。

✋ **驗收點 0**:場景有 Floor 和 Wall_Back。

---

## 1. Systems 物件(四支核心腳本)

1. Hierarchy 右鍵 → **Create Empty**,改名 `Systems`,Position 歸零
2. 選取 Systems,**Add Component** 依序加入(搜尋名字):
   - `SessionLogger` (不用填任何欄位)
   - `DirtManager` (欄位之後填)
   - `ActivityTracker`(欄位之後填)
   - `GameManager`  (欄位之後填)
3. (可選)有 ESP32 感測器計畫的話順手加 `SensorListener`,port 保持 8765。沒硬體也不影響任何功能。

✋ **驗收點 1**:Systems 掛好 4(或 5)支腳本。

---

## 2. XR Origin 進場景

1. **刪掉**場景裡預設的 `Main Camera`(XR Rig 自帶相機,兩個會衝突)
2. Project 視窗找到
   `Assets/Samples/XR Interaction Toolkit/3.3.2/Starter Assets/Prefabs/XR Origin (XR Rig).prefab`
   拖進 Hierarchy,Position 歸零
3. 展開 XR Origin (XR Rig) → Camera Offset,底下有 `Main Camera`、`Left Controller`、`Right Controller`

### 2-1. 左右手掛 HandMotionSampler
1. 選 `Left Controller` → Add Component → `HandMotionSampler` → 勾選 **Is Left Hand**
2. 選 `Right Controller` → Add Component → `HandMotionSampler` → **Is Left Hand 不勾**
3. 兩邊的 `Head` 欄位都留空(腳本會自動抓 Main Camera)

### 2-2. 頭部 Hitbox(閃避判定)
1. 在 `Main Camera`(Camera Offset 底下那個)右鍵 → **Create Empty**,改名 `HeadHitbox`,Position 歸零
2. 選 HeadHitbox,Add Component 三個:
   | 元件 | 設定 |
   |------|------|
   | `PlayerHeadHitbox` | 無欄位,純標記 |
   | Sphere Collider | **Is Trigger 打勾**,Radius = **0.22** |
   | Rigidbody | **Is Kinematic 打勾**,Use Gravity 取消 |

✋ **驗收點 2**:此時戴頭盔按 Play,頭手追蹤應正常。Console 不應有紅字。

---

## 3. ThrowTuning 參數資產

1. Project 視窗到 `Assets/Settings/`(或你喜歡的位置)右鍵 → **Create → Pizzala → Throw Tuning**
2. 命名 `ThrowTuning`,所有預設值不動(調參是 Day 6 的事)

✋ **驗收點 3**:資產存在,之後拖進 GameManager。

---

## 4. Prefab 資料夾

Project 視窗 `Assets/` 右鍵 → **Create → Folder**,命名 `Prefabs`。之後所有 Prefab 都存這裡,命名 `PZ_` 開頭。

---

## 5. PZ_Pizza_Base + 三個口味 Variant

### 5-1. 基底
1. 把 `Assets/Art/Pizza/pizza1/pizza1.fbx` 拖進 Hierarchy,改名 `PZ_Pizza_Base`
   (模型太大/太小先不管,下一步用 Collider 尺寸校準:目標直徑約 0.3m)
2. 選根物件,Add Component:
   | 元件 | 設定 |
   |------|------|
   | Rigidbody | Mass = **0.3**,Collision Detection = **Continuous Dynamic** |
   | Box Collider | Size 約 (0.3, 0.04, 0.3),中心對齊餅面;若模型比例差太多,調整根物件 Scale 讓披薩實際約 0.3m 寬 |
   | XR Grab Interactable | Movement Type = **Velocity Tracking**;**Throw On Detach 打勾**;Throw Velocity Scale = **1.5**;Smooth Position 建議打勾 |
   | `PizzaProjectile` | Flavor = Margherita(基底隨便設) |
3. 把 `PZ_Pizza_Base` 從 Hierarchy 拖進 `Assets/Prefabs/Pizza/` 存成 Prefab,場景裡的先留著測試用

### 5-2. 口味 Variant
1. Project 視窗右鍵 `PZ_Pizza_Base` → **Create → Prefab Variant**,做三個:
   - `PZ_Pizza_Margherita`(PizzaProjectile.Flavor = Margherita)
   - `PZ_Pizza_Pepperoni`(Flavor = Pepperoni)
   - `PZ_Pizza_CosmicPinkMarshmallow`(Flavor = CosmicPinkMarshmallow)
2. 口味貼圖還沒有 → 先用材質底色區分:每個 Variant 裡 Model 的材質換成不同顏色的簡單材質(白/紅/黃),之後美術貼圖來了再換

✋ **驗收點 5**:戴頭盔 Play,手可以抓場景裡的披薩、丟出去會飛、砸牆不穿牆(對照 PREFABS.md §1 驗收)。
測完把場景裡的測試披薩刪掉。

---

## 6. PZ_ThrowbackPizza(丟回披薩)

1. 把 `PZ_Pizza_Base` 拖進場景 → 右鍵 → **Prefab → Unpack Completely**,改名 `PZ_ThrowbackPizza`
2. **移除**元件:XR Grab Interactable、`PizzaProjectile`
3. **加上**:`ThrowbackProjectile`(無欄位)
4. Collider 的 Is Trigger 保持**不勾**;Rigidbody 保留
5. 拖進 `Assets/Prefabs/Pizza/` 存成新 Prefab,場景裡的刪掉

✋ **驗收點 6**:Prefab 上只有 Rigidbody + Collider + ThrowbackProjectile。

---

## 7. PZ_SauceSplat 髒污(先做保底版)

1. Hierarchy 右鍵 → **3D Object → Quad**,改名 `PZ_SauceSplat_01`
2. **移除 Mesh Collider**(髒污不需要碰撞,不移除會擋到後續披薩)
3. 建一個材質 `M_SauceSplat_01`:Shader = **URP/Lit**,Surface Type = **Transparent**,Base Map 顏色調成半透明暗紅(醬汁貼圖之後美術補)
4. 材質拖給 Quad,Scale 約 (0.4, 0.4, 1)
5. 存成 Prefab,場景裡的刪掉
6. 複製 Prefab 改顏色/大小做 2~3 個變化(`_02`、`_03`),先求有

✋ **驗收點 7**:回 Systems → DirtManager,把做好的 Splat Prefab 全部拖進 **Splat Prefabs** 陣列。

---

## 8. PZ_Customer(客人)

用 Soldier 模型當佔位。表情貼圖(faceNormal/Happy/Angry/Dirty)美術還沒給,**欄位先留空**——腳本對 null 貼圖會靜默跳過,不會報錯;口味圖示同理。

1. Hierarchy 右鍵 → **Create Empty**,改名 `PZ_Customer`,Position 歸零
2. 把 `Assets/Art/NPC/Soldier/Soldier_standing.fbx` 拖成 PZ_Customer 的**子物件**,改名 `Model`,調整 Scale 讓身高約 1.7m
3. 在 Model 底下建臉的佔位:右鍵 Model → **3D Object → Quad**,改名 `Face`,移到臉部位置、面朝前方,Scale 約 (0.15, 0.2, 1)——這是表情貼圖的顯示面(之後美術可整合進模型)
4. 在 PZ_Customer 根物件底下建五個子物件:

   | 子物件 | 元件與設定 | 位置 |
   |--------|-----------|------|
   | `HandZone` | `CustomerHitZone`(Zone = **Hand**)+ Sphere Collider(**Trigger 勾**,R = 0.15) | 伸出的手前方,高約 1.0m |
   | `FaceZone` | `CustomerHitZone`(Zone = **Face**)+ Sphere Collider(**Trigger 勾**,R = 0.12) | 臉正前方 |
   | `BodyZone` | `CustomerHitZone`(Zone = **Body**)+ Capsule Collider(**Trigger 不勾**,包住軀幹) | 身體中心 |
   | `FaceAnchor` | 空物件,**Z 軸(藍箭頭)朝外**(截圖相機會停在它前方回頭拍) | 臉部正中 |
   | `ThrowOrigin` | 空物件 | 胸前 |

   三個 Zone 的 `Customer` 欄位可以留空(Awake 會自動往上找 CustomerController)。
5. 根物件 Add Component → `CustomerController`,欄位:
   | 欄位 | 填什麼 |
   |------|--------|
   | Customer Id | 0(擺進場景後每隻改成不同編號) |
   | Sector | 先 Center(擺進場景後依方位改) |
   | Face Renderer | 拖 Model/Face 的 Quad |
   | Face Normal/Happy/Angry/Dirty | **留空**(等美術) |
   | Flavor Icon | 下一步做 |
   | Flavor Sprites | **留空**(等美術;順序必須 0=Margherita 1=Pepperoni 2=CosmicPinkMarshmallow) |
   | Face Anchor / Throw Origin | 拖對應子物件 |
   | Required Throw Type | Unknown |
6. FlavorIcon:根物件底下右鍵 → **2D Object → Sprite**(或 Create Empty + Add Component → Sprite Renderer),改名 `FlavorIcon`,位置在頭頂上方約 0.4m,拖進 CustomerController 的 Flavor Icon 欄位
7. 存成 Prefab `Assets/Prefabs/PZ_Customer`
8. 場景擺 **3 隻**,距玩家出生點(原點)約 3~4m,呈左中右扇形:
   - 每隻改 `Customer Id` = 0 / 1 / 2
   - `Sector` 依實際方位 = Left / Center / Right
   - 都要**面向玩家**(旋轉 Y 讓正面朝原點)

✋ **驗收點 8**:場景 3 隻客人,Id 和 Sector 都不重複。

---

## 9. 出餐台(PizzaSpawner ×3)

1. Hierarchy 建 `SpawnerStation`(Create Empty)
2. 底下建三個 Cube 當檯面:Scale (0.4, 0.9, 0.4),Position 排在玩家面前 0.5m,左中右間隔 0.5m(例:x = -0.5 / 0 / 0.5,z = 0.5,y = 0.45)
3. 每個 Cube 底下建空物件 `SpawnPoint`,位置在檯面上方(y 約 0.95)
4. 每個 SpawnPoint 掛 `PizzaSpawner`:
   | Spawner | Pizza Prefab | Flavor |
   |---------|-------------|--------|
   | 左 | PZ_Pizza_Margherita | Margherita |
   | 中 | PZ_Pizza_Pepperoni | Pepperoni |
   | 右 | PZ_Pizza_CosmicPinkMarshmallow | CosmicPinkMarshmallow |

   Respawn Delay = 1.5,Leave Distance = 0.5

✋ **驗收點 9**:Play 後三座各長出一顆披薩,拿走 1.5 秒後自動補。

---

## 10. 截圖相機 + 俯瞰點

1. Hierarchy 右鍵 → **Camera**,改名 `SnapshotCamera`(**獨立物件,不要放 XR Origin 底下**)
2. **移除 Audio Listener** 元件(場景只能有一個,Main Camera 上已經有)
3. Add Component → `SnapshotCamera`,Resolution = 512(Camera 元件的勾勾腳本會自動關,不用手動)
4. Create Empty 命名 `OverviewPoint`,擺到能俯瞰整個遊玩區的高處角落(例 Position (4, 3, 4)),**旋轉調到看得到地板和客人**——Scene 視窗右上角 Gizmo 對照,或暫時把 SnapshotCamera 移到該位置用 Camera Preview 對構圖,對好把座標抄給 OverviewPoint

✋ **驗收點 10**:場景只有 Main Camera 一個啟用相機、一個 Audio Listener,Console 無警告。

---

## 11. PZ_FaceSplatCanvas(被砸臉的醬汁視野)

1. Hierarchy 右鍵 → **UI → Canvas**,改名 `PZ_FaceSplatCanvas`
   - Render Mode = **Screen Space - Camera**
   - Render Camera = XR Origin 底下的 **Main Camera**
   - Plane Distance = **0.35**
2. Canvas 底下右鍵 → **UI → Image**,改名 `SplatImage`:
   - Anchor 設成整面延展(Anchor Presets 按住 Alt 點右下角的 stretch-stretch)
   - 顏色調半透明暗紅(醬汁貼圖之後換)
   - **Raycast Target 取消勾選**
3. Canvas 根物件 Add Component → `FaceSplatOverlay`,Splat Image 拖入 SplatImage;Hold = 1.2、Fade = 2 保持預設
4. (可選,已有多口味 splat 素材時做)`Flavor Splats` 陣列 Size 設 3,依 PizzaFlavor 列舉順序拖入對應貼圖(需先把 png 的 **Texture Type 改成 Sprite (2D and UI)** 才能拖進 Sprite 欄位):
   - Element 0 = Margherita 用的濺灑圖
   - Element 1 = Pepperoni 用的濺灑圖
   - Element 2 = CosmicPinkMarshmallow 用的濺灑圖
   - 某個 Element 留空,該口味被砸中時畫面會沿用 Splat Image 目前設定的預設圖,不會報錯
   - 被丟回砸中時,`GameManager` 會依**丟回披薩的口味**自動換圖(超時=該筆訂單的口味;丟錯口味=你丟的那顆的口味),不用額外接線

✋ **驗收點 11**:Play 時畫面上看不到紅色(Start 會把 alpha 歸零)。故意讓某口味訂單超時觸發丟回被砸中,螢幕應該糊上對應口味的濺灑圖(若 Flavor Splats 有填的話)。

---

## 12. PZ_ResultsCanvas(結算畫面)+ PZ_PhotoEntry

> 第一次拉 TMP_Text 會跳出 **TMP Importer** 視窗 → 按 **Import TMP Essentials**。

### 12-1. PZ_PhotoEntry(拍立得單元,先做)
1. Hierarchy 隨便建一個暫時 Canvas(等下刪),底下右鍵 → **Create Empty**,改名 `PZ_PhotoEntry`
   - Rect Transform:Width **200**、Height **240**
2. 底下 UI → Image 改名 `Frame`:stretch 全填滿,顏色白色(拍立得框,美術之後換圖)
3. Frame 底下 UI → **Raw Image** 改名 `Photo`:Width 180、Height 180,置於框內偏上(Pos Y 約 +20)
4. 把 PZ_PhotoEntry 拖進 `Assets/Prefabs/UI/` 存 Prefab,**刪掉暫時 Canvas**

### 12-2. PZ_ResultsCanvas
1. Hierarchy 右鍵 → **UI → Canvas**,改名 `PZ_ResultsCanvas`
   - Render Mode = **World Space**
   - Rect Transform:Position (0, 1.6, 2),Width 1000、Height 800,**Scale (0.001, 0.001, 0.001)**
2. 底下建兩個 Panel(UI → Panel):
   - `ControlPanel`:底下 UI → **Text - TextMeshPro** 改名 `StatsText`,stretch 填滿、字級 36、對齊左上
   - `ExperimentalPanel`:
     - 底下 TMP_Text 改名 `CaptionText`,錨在頂部,字級 40
     - 底下 Create Empty 改名 `PhotoGrid`:Add Component → **Grid Layout Group**,Cell Size (200, 240)、Spacing (20, 20);Rect 拉大占 Panel 中下部
3. Canvas 根物件 Add Component → `ResultsScreenController`,接線:
   | 欄位 | 拖入 |
   |------|------|
   | Control Panel | ControlPanel |
   | Stats Text | StatsText |
   | Experimental Panel | ExperimentalPanel |
   | Photo Grid | PhotoGrid |
   | Photo Entry Prefab | Assets/Prefabs/UI/PZ_PhotoEntry |
   | Caption Text | CaptionText |

✋ **驗收點 12**:Play 時兩個 Panel 自動隱藏(Start 會關)。

---

## 13. GameManager 總接線(最後一步)

選 Systems,GameManager 逐欄拖:

| 欄位 | 拖入 |
|------|------|
| Condition | Control(每位受試者開測前切換) |
| Participant Id | P00 |
| Enable Throwback / Enforce Flavor | 勾(預設) |
| Enforce Throw Type | 不勾 |
| Auto Start | 勾,Delay 5 |
| Tuning | Assets/Settings/ThrowTuning |
| Customers | Size 3,拖入場景三隻客人 |
| Head | 留空(自動抓 Main Camera) |
| Snapshot Camera | 場景的 SnapshotCamera |
| Overview Camera Point | OverviewPoint |
| Face Splat Overlay | PZ_FaceSplatCanvas |
| Results Screen | PZ_ResultsCanvas |
| Activity Tracker | Systems 自己(拖 Systems 物件即可) |
| Throwback Prefab | Assets/Prefabs/Pizza/PZ_ThrowbackPizza |
| Player Dirty Face Textures | 留空(等美術) |

同場補完:
- ActivityTracker 的 Head → 留空(自動抓)
- DirtManager 的 Splat Prefabs → 確認第 7 步已拖入

✋ **驗收點 13(端到端)**:戴頭盔 Play,5 秒後回合開始:
1. 客人頭上……口味圖示還沒有,所以看 Console/行為:客人會開始等餐
2. 丟披薩到客人手前 → 披薩被收走(口味對)或觸發丟回(口味錯)
3. 亂丟地板/牆 → 出現紅色髒污
4. 丟**錯口味**給客人 → 客人閃紅 0.8 秒 → 那顆披薩飛回來 → 站著不動會滿臉紅、側跳能躲掉(訂單超時只會生氣+記 missedOrder,不丟回;要恢復超時丟回勾 GameManager 的 `throwbackOnTimeout`)
5. 3 分鐘回合結束 → 結算畫面出現(Control = 數據面板)
6. `%USERPROFILE%\AppData\LocalLow\<公司名>\Pizzala\sessions\` 出現 JSON

全過 = 今日骨架完成,對照 [ACCEPTANCE_Unity.md](ACCEPTANCE_Unity.md) B1/B3 開始逐項打勾。

---

## 已知的佔位缺口(等美術,不擋骨架驗收)

| 缺口 | 影響 | 對應素材 |
|------|------|----------|
| 表情貼圖 ×4 | 客人不會變臉(邏輯照跑) | 美術:同一張臉四種表情 |
| 口味圖示 ×3 | 頭上不顯示訂單(先看 Console) | 美術:256×256 透明背景 |
| 醬汁濺灑貼圖 | 髒污是純色色塊 | 美術:1024×1024 透明背景 4~6 張 |
| 玩家髒臉圖 | 實驗組少一類照片 | 美術:2~3 個髒度層次,貼圖勾 Read/Write |
| 全螢幕濺醬圖 | 被砸臉時是整面紅色 | 美術:中央略透的濺灑圖 |
