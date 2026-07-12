# Pizzala — 組裝手冊(從腳本到可玩 Demo)

> 骨架腳本已全部寫好,照這份清單一步步組,不用寫程式。
> 遇到卡關:先看 Console 的紅字,每支腳本的錯誤訊息都會說明缺什麼。

---

## 0. 開啟專案(第一次)

1. 用 Unity Hub 開啟專案(6000.3.19f1),等套件自動安裝。
2. 若 Package Manager 對 XR 套件版本報錯:開 **Window → Package Manager → Unity Registry**,手動安裝最新版的
   `XR Interaction Toolkit`、`XR Plugin Management`、`OpenXR Plugin` 即可。
3. **Package Manager → XR Interaction Toolkit → Samples → 匯入 `Starter Assets`**
   (裡面有配好輸入的 XR Origin prefab,直接用,不要自己從零配)。

## 1. Quest 3 建置設定(Day 1 唯一目標:上機看到東西)

1. **File → Build Profiles → Android → Switch Platform**
2. **Edit → Project Settings → XR Plug-in Management**:
   - Android 分頁:勾 **OpenXR**
   - OpenXR → Android 分頁:
     - Interaction Profiles 加入 **Oculus Touch Controller Profile**
     - OpenXR Feature Groups 勾 **Meta Quest Support**
3. **Project Settings → Player → Android**:
   - Scripting Backend = **IL2CPP**,Target Architectures 只勾 **ARM64**
   - Minimum API Level = **29** 以上
   - Texture Compression = **ASTC**(Build Profiles 裡設定)
4. Quest 3 開發者模式:手機 Meta Horizon App → 頭盔設定 → 開發者模式開啟,USB 連線後在頭盔內允許 USB 偵錯。
5. **平日開發建議用 Quest Link(連 PC 直接按 Play)**,迭代速度差十倍;Build APK 一天一次確認就好。

## 2. URP Decal(醬汁髒污,二選一)

- **方案 A(效果好)**:選 `Assets/Settings/Mobile_Renderer` → Inspector → **Add Renderer Feature → Decal**。
  髒污 Prefab = 空物件 + **URP Decal Projector**,材質用 `Shader Graphs/Decal`,貼上醬汁貼圖。
- **方案 B(保底,不動 renderer)**:髒污 Prefab = 一片 **Quad** + 透明材質(URP/Lit,Surface Type = Transparent)貼醬汁圖,正面朝 +Z。

做 4~6 個不同形狀的 Prefab,全部拖進 DirtManager 的 `splatPrefabs`。

## 3. 場景組裝順序

### 3-1. Systems 物件
建空物件 `Systems`,掛上這四支:
`SessionLogger`、`DirtManager`、`ActivityTracker`、`GameManager`

### 3-2. XR Origin
1. 把 Starter Assets 的 **XR Origin (XR Rig)** prefab 拖進場景(自帶 Interaction Manager 與輸入設定)。
2. 左/右手控制器物件各掛一支 **HandMotionSampler**,左手勾 `isLeftHand`。
3. Main Camera 底下建子物件 `HeadHitbox`:掛 **PlayerHeadHitbox** + SphereCollider(**Is Trigger**,半徑 0.22)+ Rigidbody(**Is Kinematic**)。

### 3-3. 披薩 Prefab
1. 披薩模型(美術的 `Art/Pizza`)加:
   - **Rigidbody**:Collision Detection = **Continuous Dynamic**,質量 0.3
   - Collider:壓扁的 Box 即可
   - **XR Grab Interactable**:Movement Type = **Velocity Tracking**,勾 **Throw On Detach**
   - **PizzaProjectile**
2. 存成 Prefab。丟起來太無力就調 XR Grab Interactable 的 **Throw Velocity Scale**(1.5~2.5 之間試)。

### 3-4. 丟回披薩 Prefab
複製披薩 Prefab → 移除 XRGrabInteractable 和 PizzaProjectile → 掛 **ThrowbackProjectile** → 存成新 Prefab。

### 3-5. 客人 Prefab
1. 根物件掛 **CustomerController**,依腳本頂部註解填欄位(四張表情貼圖、口味圖示等)。
2. 三個子物件掛 **CustomerHitZone**:
   - `Hand`:SphereCollider trigger,半徑 0.15,放在伸出的手位置
   - `Face`:SphereCollider trigger,半徑 0.12,放臉前
   - `Body`:普通 Collider 包住身體(不勾 trigger)
3. 建 `FaceAnchor` 空物件在臉部、`ThrowOrigin` 在胸前,拖進對應欄位。
4. 場景擺 3~5 個,`customerId` 給 0,1,2...,`sector` 依實際方位設 Left/Center/Right。

### 3-6. 出餐台
三個空物件掛 **PizzaSpawner**,各設一種口味,排在玩家面前檯面上。

### 3-7. 相機與 UI
1. 建獨立 Camera 掛 **SnapshotCamera**(相機元件會自動停用;移除它的 Audio Listener)。
2. 建空物件 `OverviewPoint`,擺在能俯瞰全餐廳的位置,角度調好。
3. Canvas(Screen Space - Camera,Camera = Main Camera,Plane Distance 0.35)+ 全螢幕醬汁 Image → 掛 **FaceSplatOverlay**。
4. World Space Canvas(玩家前方 2m、高 1.6m,縮放 0.001)→ 掛 **ResultsScreenController**,底下做 controlPanel(一個 TMP_Text)和 experimentalPanel(GridLayoutGroup + RawImage prefab)。

### 3-8. 參數與總線
1. Project 視窗右鍵 → **Create → Pizzala → Throw Tuning**。
2. GameManager 所有欄位照 Inspector 拖好(腳本頂部有逐欄說明)。

## 4. 實驗操作(每位受試者)

| 欄位 | 說明 |
|---|---|
| GameManager → `condition` | **Control** = 對照組,**Experimental** = 實驗組(每人開測前切換) |
| GameManager → `participantId` | 受試者編號 |
| 數據位置 | Quest:`Android/data/<包名>/files/sessions/*.json` 和 `photos/`,用 SideQuest 或 `adb pull` 取回 |

## 5. 手感調參(Day 6 重點)

| 症狀 | 調哪裡 |
|---|---|
| 丟不遠、軟趴趴 | XR Grab Interactable → Throw Velocity Scale 調高 |
| 出手方向亂飄 | XR Grab Interactable → 勾 Throw Smoothing;揮動放大動作 |
| 手勢分類不準 | 改 ThrowTuning 的閾值(數據 JSON 裡有每次的原始特徵值可對照) |
| 丟回躲不掉/太好躲 | ThrowTuning → `throwbackSpeed`(5=容易躲)和 `telegraphSeconds` |
| 客人太快生氣 | ThrowTuning → `customerPatience` |

## 6. Demo 現場檢查清單

- [ ] 遊玩區淨空 2m × 2m(玩家會側跳和下蹲!)
- [ ] Quest Guardian 邊界設好
- [ ] 受試者開場站直(活動量追蹤要校準站立高度)
- [ ] `condition` 和 `participantId` 已設定
- [ ] 電量 > 50%,手把電池備品
