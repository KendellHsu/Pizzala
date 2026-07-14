# Pizzala — Prefab 建置手冊與驗收清單

> 搭配 [SETUP.md](../SETUP.md) 使用:SETUP.md 管「專案與場景層級」的設定,這份文件管「每一個 Prefab 怎麼做、做到什麼程度算完成」。
> 所有 Prefab 統一放在 `Assets/Prefabs/`,命名以 `PZ_` 開頭。
> 每節的驗收目標全部打勾,該 Prefab 才算交付。驗收用 **Quest Link 連 PC 按 Play** 測試(不用每次 Build APK)。

---

## 建置順序與分工總覽

| # | Prefab | 負責 | 依賴素材(美術組) | 被誰使用 |
|---|--------|------|--------------------|----------|
| 1 | `PZ_Pizza_Base` + 三個口味 Variant | 遊戲組 | 披薩模型 + 3 張口味貼圖 | PizzaSpawner |
| 2 | `PZ_ThrowbackPizza` | 遊戲組 | (複製 #1) | GameManager |
| 3 | `PZ_SauceSplat_01`~`06` | 美術組做圖、遊戲組組裝 | 4~6 張醬汁濺灑貼圖(透明背景) | DirtManager |
| 4 | `PZ_Customer` | 兩組合作 | 客人模型/看板、4 張表情貼圖、3 張口味圖示 | GameManager |
| 5 | `PZ_SpawnerStation` | 遊戲組 | 出餐檯面模型(可先用 Cube) | 場景 |
| 6 | `PZ_PhotoEntry` | 美術組做框、遊戲組組裝 | 拍立得相框圖 | ResultsScreenController |
| 7 | `PZ_ResultsCanvas` | 遊戲組 | 結算畫面版面設計(美術) | GameManager |
| 8 | `PZ_FaceSplatCanvas` | 遊戲組 | 1 張全螢幕醬汁濺灑圖 | GameManager |
| 9 | `PZ_SnapshotCamera` | 遊戲組 | 無 | GameManager |

建議順序:1 → 2 → 3(先讓核心循環動起來)→ 4 → 5 → 8 → 9 → 6 → 7(結算畫面最後,但版面設計可以先平行進行)。

---

## 1. PZ_Pizza_Base(披薩本體)

玩家抓取與丟擲的核心物件。先做一個基底 Prefab,再用 **Prefab Variant** 做三種口味(右鍵 Prefab → Create → Prefab Variant),之後改物理參數只要改基底,三個口味自動跟上。

**階層結構**
```
PZ_Pizza_Base
└─ Model(披薩模型,美術素材在 Assets/Art/Pizza/)
```

**根物件元件設定**

| 元件 | 設定 |
|------|------|
| Rigidbody | Mass = 0.3,Collision Detection = **Continuous Dynamic** |
| Box Collider | 壓扁盤狀,約 0.3 × 0.04 × 0.3(依模型調) |
| XR Grab Interactable | Movement Type = **Velocity Tracking**、勾 **Throw On Detach**、Throw Velocity Scale 先設 1.5 |
| PizzaProjectile | `flavor` 基底隨便設,口味 Variant 各自覆寫(Spawner 也會覆寫) |

**三個 Variant**:`PZ_Pizza_Margherita` / `PZ_Pizza_Pepperoni` / `PZ_Pizza_CosmicPinkMarshmallow`——只改 Model 的材質貼圖和 `flavor` 欄位。

**驗收目標**
- [x] 拖進場景按 Play(Quest Link),手可以抓起、放開會飛出去,方向大致跟揮動方向一致
- [x] 用力丟牆壁,披薩**不會穿牆**(Continuous Dynamic 沒設對就會穿)
- [x] 丟到地板,Console 出現落地判定(或髒污生成),**沒有紅字錯誤**
- [x] 放著不動 10 秒,披薩不會自己抖動或滑走
- [x] 三個口味 Variant 外觀可肉眼區分,`flavor` 欄位與貼圖相符

---

## 2. PZ_ThrowbackPizza(客人丟回來的披薩)

**做法**:複製 `PZ_Pizza_Base` → 改名 → **移除** XR Grab Interactable 和 PizzaProjectile → **掛上** `ThrowbackProjectile` → Collider 的 Is Trigger 保持**不勾**。

**驗收目標**
- [x] 丟**錯口味**到客人手上:客人接住後閃紅約 0.8 秒,再把那顆披薩丟回來(超時**不**丟回——沒拿到披薩的客人沒東西可丟;實驗若需要更多閃避事件,勾 GameManager 的 `throwbackOnTimeout` 恢復超時丟回)
- [x] 站著不動會被砸中 → 螢幕糊醬汁(需 #8 完成)
- [x] 預警瞬間側跨一步或下蹲,披薩**會落空**,砸在身後牆上並留下髒污
- [x] 落空的披薩 1.5 秒後消失,5 秒後保底自毀,場上不會堆積
- [x] 玩家自己丟的披薩碰到自己的頭**不會**觸發任何判定

---

## 3. PZ_SauceSplat_01 ~ 06(醬汁髒污)

兩種做法擇一,**先用方案 B 保底,方案 A 效果好但要動 renderer 設定**(見 SETUP.md 第 2 節)。

**方案 B(Quad,建議先做這個)**
```
PZ_SauceSplat_01
└─ Quad(正面朝 +Z)
   材質:URP/Lit,Surface Type = Transparent,Base Map = 醬汁貼圖
   ※ 移除 Quad 自帶的 Mesh Collider(髒污不需要碰撞)
```

**方案 A(URP Decal Projector)**:空物件 + Decal Projector 元件,材質用 `Shader Graphs/Decal`,投影方向 +Z。

做 4~6 個不同形狀,全部拖進 Systems → DirtManager 的 `splatPrefabs` 陣列。

**美術規格**:1024×1024 PNG 透明背景,紅醬為主、可混 1~2 張起司色;邊緣自然噴濺、不要規則圓形。

**驗收目標**
- [x] 連續亂丟 10 顆披薩到牆面、地板、桌面:每個失誤落點都出現髒污
- [x] 髒污**貼合表面**且無 z-fighting 閃爍(有閃爍 → 調 DirtManager 的 `surfaceOffset`)
- [x] 髒污形狀有隨機變化(旋轉、大小、圖案都會隨機)
- [x] 丟同一個位置 10 次,幀率無明顯下降(Quest Link 下 72fps 不掉)
- [ ] 回合結束後結算畫面的髒污計數,與肉眼數的一致

---

## 4. PZ_Customer(客人)

整個遊戲最複雜的 Prefab,兩組合作:美術給模型與貼圖,遊戲組組裝判定區。

**階層結構**
```
PZ_Customer
├─ Model(客人模型;MVP 可用簡單人形或看板)
│  └─ Face(臉部獨立 Renderer,材質貼圖會被程式替換)
├─ HandZone   ← CustomerHitZone(zone=Hand)+ SphereCollider(Trigger,半徑 0.15,放在伸出的手前)
├─ FaceZone   ← CustomerHitZone(zone=Face)+ SphereCollider(Trigger,半徑 0.12,放臉前)
├─ BodyZone   ← CustomerHitZone(zone=Body)+ CapsuleCollider(不勾 Trigger,包住身體)
├─ FaceAnchor ← 空物件,位置在臉部正中、Z 軸朝外(截圖相機會對準它)
├─ ThrowOrigin← 空物件,位置在胸前(丟回披薩的出發點)
└─ FlavorIcon ← SpriteRenderer,懸在頭上約 0.4m
```

**根物件 CustomerController 欄位**

| 欄位 | 填什麼 |
|------|--------|
| `customerId` | 場景裡每隻給不同編號 0, 1, 2…(Prefab 內先填 0,擺進場景後改) |
| `sector` | 擺進場景後依實際方位設 Left / Center / Right |
| `faceRenderer` | 拖入 Model/Face 的 Renderer |
| `faceNormal / faceHappy / faceAngry / faceDirty` | 美術的四張表情貼圖 |
| `flavorIcon` | 拖入 FlavorIcon |
| `flavorSprites` | 三張口味圖示,**順序必須是 0=Margherita、1=Pepperoni、2=CosmicPinkMarshmallow** |
| `faceAnchor` / `throwOrigin` | 拖入對應空物件 |
| `requiredThrowType` | 保持 Unknown(= 不限投擲方式) |

**美術規格**:表情貼圖四張同一張臉(正常/開心/生氣/沾醬),UV 一致;口味圖示 256×256 透明背景,遠處可辨識。

**驗收目標**
- [ ] Play 後客人頭上出現口味圖示(GameManager 派單後),等待超時會變生氣臉
- [ ] 丟**正確口味**到 HandZone:變開心臉、圖示消失、披薩消失(被收下)
- [ ] 丟**錯誤口味**到 HandZone:變生氣臉,並觸發丟回
- [ ] 砸中 FaceZone:變髒臉(且**維持髒臉不復原**,直到回合結束),`photos/` 資料夾多一張該客人的髒臉照
- [ ] 砸中 BodyZone:披薩彈開、身上出現髒污、客人變生氣
- [ ] 表情變化 2.5 秒後恢復(髒臉除外)
- [ ] 沒點餐的客人被塞披薩,不算命中(數據記為 MissBody)

---

## 5. PZ_SpawnerStation(出餐台)

**階層結構**
```
PZ_SpawnerStation
├─ Counter(檯面模型,MVP 用 Cube 即可,高度約 0.9m)
└─ SpawnPoint ← PizzaSpawner(位置在檯面上方 0.05m)
```

PizzaSpawner 欄位:`pizzaPrefab` 拖對應口味 Variant、`flavor` 設同口味、`respawnDelay` = 1.5。
場景擺三座,一種口味一座,排在玩家面前伸手可及處(距離約 0.5m,高度 0.9~1.1m)。

**驗收目標**
- [x] Play 後三座各出現一顆披薩,口味外觀正確
- [x] 拿走披薩丟出去,約 1.5 秒後自動補新的一顆
- [x] 補的披薩口味永遠正確(Spawner 會覆寫 `flavor`)
- [ ] 把披薩拿起來又放回原位不丟,不會瘋狂重複生成
- [x] 連續丟 20 顆,場上舊披薩留在原地當髒污、不影響幀率

---

## 6. PZ_PhotoEntry(拍立得照片)

實驗組照片牆的單元。

**階層結構(UI Prefab)**
```
PZ_PhotoEntry(RectTransform 約 200×240)
├─ Frame(Image:拍立得白框,下緣較寬)
└─ Photo(RawImage:程式會把截圖填進來,約 180×180,置於框內上方)
```

**驗收目標**
- [ ] RawImage 在子物件中(程式用 `GetComponentInChildren<RawImage>()` 找)
- [ ] 隨便指定一張貼圖到 RawImage,照片不變形、不超出相框
- [ ] 被 ResultsScreenController 生成時帶有隨機 ±6° 歪斜(程式處理,不用自己做)

---

## 7. PZ_ResultsCanvas(結算畫面)★ 實驗操弄的本體

這是你們實驗的自變項呈現處,**兩個 Panel 的資訊密度和視覺投入要由美術統一把關**,避免「實驗組比較好看只是因為比較用心做」的混淆變因。

**階層結構**
```
PZ_ResultsCanvas(World Space Canvas,位置:玩家前方 2m、高 1.6m,Scale = 0.001)
└─ Root
   ├─ ControlPanel(對照組)
   │  └─ StatsText(TMP_Text,整面數據,字級建議 36+)
   └─ ExperimentalPanel(實驗組)
      ├─ CaptionText(TMP_Text,標題)
      └─ PhotoGrid(Grid Layout Group:Cell Size 200×240、Spacing 20)
```

ResultsScreenController 欄位:`controlPanel`、`statsText`、`experimentalPanel`、`photoGrid`、`photoEntryPrefab`(拖 PZ_PhotoEntry)、`captionText`。

**驗收目標**
- [ ] Play 開始時兩個 Panel 都隱藏
- [ ] GameManager `condition = Control` 跑完一輪:只出現數據面板,內容含準度、最快出手、甩腕、出手方式統計、方位命中、身體活動量
- [ ] `condition = Experimental` 跑完一輪:只出現照片牆,含客人髒臉照 + 環境總覽照(有被砸臉的話還有玩家髒臉圖),標題數字正確
- [ ] 戴頭盔實測:2m 外文字清晰可讀、照片不糊
- [ ] 照片超過 8 張時版面不爆(Grid 自動換行)
- [ ] **兩個 Panel 的美術完成度相當**(同字型、同配色系統)——這條由美術組驗收

---

## 8. PZ_FaceSplatCanvas(被砸臉的醬汁視野)

**階層結構**
```
PZ_FaceSplatCanvas(Canvas:Screen Space - Camera,Camera = Main Camera,Plane Distance = 0.35)
└─ SplatImage(Image:全螢幕醬汁濺灑圖,Raycast Target 取消勾選)
```
掛 `FaceSplatOverlay`,`splatImage` 拖入 SplatImage。

**驗收目標**
- [x] Play 開始時看不到醬汁(alpha = 0)
- [x] 被丟回披薩砸中:醬汁瞬間糊滿視野 → 停留 1.2 秒 → 2 秒淡出
- [x] 醬汁跟著頭轉(像糊在臉上),而不是釘在世界某處
- [x] 連續被砸兩次,效果重新播放、不疊加出錯
- [ ] 遮擋程度戴頭盔實測**不引起不適**(太滿就把貼圖中央挖空一點)

---

## 9. PZ_SnapshotCamera(截圖相機)

```
PZ_SnapshotCamera(Camera + SnapshotCamera 腳本)
```
設定:**移除 Audio Listener**;Camera 元件的勾勾由腳本自動關閉,不用手動處理。Clear Flags/Background 保持預設 Skybox。場景中另建空物件 `OverviewPoint` 擺在能俯瞰全餐廳的角落(拖進 GameManager 的 `overviewCameraPoint`)。

**驗收目標**
- [ ] 場景中同時只有 Main Camera 一個啟用的相機、一個 Audio Listener(Console 無相關警告)
- [ ] 砸客人臉後,`persistentDataPath/photos/` 出現 `face_*.png`,照片裡是**該客人的臉部特寫**(角度不對就調 FaceAnchor 的朝向)
- [ ] 回合結束後出現 `env_*.png`,構圖能看到大部分髒污
- [ ] 截圖瞬間遊戲不明顯卡頓(512 解析度下應無感)

---

## 整合驗收(全部 Prefab 完成後的端到端測試)

依序執行,全過才算 MVP 達標:

1. [ ] 戴上頭盔按 Play,5 秒後回合自動開始,客人陸續出現口味需求
2. [ ] 完整玩 3 分鐘:命中、砸臉、砸身體、砸環境、超時被丟回、閃避成功——每種情況至少發生一次
3. [ ] 全程 Console 無紅字
4. [ ] 回合結束自動出現結算畫面(對照組/實驗組各測一輪)
5. [ ] `sessions/*.json` 檔案存在,打開檢查:每筆 throw 都有 throwType 和 features 原始值、方位統計非全零
6. [ ] JSON 的統計數字和結算畫面顯示一致
7. [ ] 反手、正手、過頭、低手各刻意丟 5 次,分類正確率 ≥ 8 成(不足就調 ThrowTuning,見 SETUP.md 第 5 節)
8. [ ] Build APK 裝上 Quest 3 脫線實跑一輪,幀率穩定、數據檔可用 SideQuest 取回
