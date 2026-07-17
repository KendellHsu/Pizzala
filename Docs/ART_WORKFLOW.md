# Pizzala — 美術組上手手冊（Unity / GitHub 新手適用）

> 給第一次碰 Unity 和 GitHub 的美術組夥伴。
> 目標:讓你能自己把美術檔案做成 **Prefab**、同步到 GitHub,讓全組即時共用。
> 全程用 **GitHub Desktop**(圖形介面,點按鈕就好,不用打指令)。
>
> 你只要照著做,遇到看不懂的名詞先跳過,能動就好。真的卡住看最後的「常見狀況」。
> 完成後的品質標準看 [ACCEPTANCE.md](ACCEPTANCE.md) 的「美術組驗收清單」;每個 Prefab 要做成什麼樣看 [PREFABS.md](PREFABS.md)。

---

## 先搞懂三個名詞(30 秒)

| 名詞 | 白話解釋 |
|------|----------|
| **Git / GitHub** | 一個「多人共用的檔案倉庫 + 自動記錄每次修改」的系統。你改的東西要「上傳(Push)」別人才看得到;別人改的東西你要「下載(Pull)」才會更新。 |
| **Prefab(預製物)** | Unity 裡把「模型 + 材質 + 設定」打包成一個可重複使用的積木。做好一次,遊戲組拖進場景就能用。副檔名 `.prefab`。 |
| **`.meta` 檔** | Unity 幫**每個**檔案自動產生的隱藏設定檔(例如 `pizza1.fbx.meta`)。**它一定要跟著檔案一起上傳**,否則別人下載後材質會消失、連結會斷。GitHub Desktop 會自動幫你一起上傳,你只要知道「不要刪它、不要漏它」。 |

---

## 一、一次性安裝(每台電腦只做一次)

1. **裝 Unity Hub + Unity 編輯器**
   - 到 <https://unity.com/download> 裝 Unity Hub。
   - 在 Hub 的 Installs 頁面裝 **Unity 6000.3.19f1**(版本要一模一樣,不然開專案會出錯)。
     裝的時候勾選 **Android Build Support**(含 SDK/NDK)。

2. **裝 GitHub Desktop**
   - 到 <https://desktop.github.com> 下載安裝。
   - 開啟後用你的 GitHub 帳號登入(沒帳號就先去 <https://github.com> 註冊,再請 Kendell 把你加進 `KendellHsu/Pizzala` 這個 repo 的協作者)。

3. **Git LFS(大檔案處理)** — GitHub Desktop 已內建,通常不用另外裝。
   本專案的 `.fbx`、`.tiff`、`.psd`、`.wav`、`.mp4` 等大檔案都走 LFS,GitHub Desktop 會自動處理,你**不用管**。

---

## 二、第一次:把專案抓下來(Clone)

1. 打開 **GitHub Desktop** → 左上 **File → Clone repository**。
2. 選 **URL** 分頁,貼上:
   ```
   https://github.com/KendellHsu/Pizzala.git
   ```
3. **Local path** 選一個好找的資料夾(例:`文件/Pizzala`)。**路徑不要有中文或空格**,可避免莫名其妙的錯誤。
4. 按 **Clone**,等它下載完(第一次含 LFS 大檔案,可能要等幾分鐘)。
5. 下載完之後,用 **Unity Hub → Open → 選剛剛那個 Pizzala 資料夾** 開專案。第一次開會跑一陣子(Unity 在建 Library 快取),耐心等。

> 之後每天開工**不用再 Clone**,只要做下面第三步的「先 Pull」。

---

## 三、每天開工前的固定動作:先 Pull(超重要)

**動手改任何東西之前,先把別人的更新抓下來**,否則之後上傳容易撞在一起(衝突)。

1. 打開 **GitHub Desktop**。
2. 確認左上 **Current branch** 是 **main**。
3. 按上方的 **Fetch origin**,如果顯示 **Pull origin**(有更新)就按下去。
4. 顯示 "up to date" 就代表你是最新的,可以開始工作了。

> 口訣:**開工先 Pull,收工記得 Push。**

---

## 四、把美術檔案放進專案

1. 在你電腦的檔案總管/Finder 打開 Clone 下來的 `Pizzala` 資料夾。
2. 把你的模型、貼圖**依類型**丟進 `Assets/Art/` 底下對應的子資料夾:

   | 類型 | 放這裡 |
   |------|--------|
   | 披薩 | `Assets/Art/Pizza/` |
   | 客人/NPC | `Assets/Art/NPC/` |
   | 場景 | `Assets/Art/scene/` |
   | UI(相框、圖示) | `Assets/Art/UI/`(沒有就自己建一個) |
   | 醬汁濺灑圖 | `Assets/Art/Splats/`(沒有就自己建) |

3. **命名規則**(照 [ACCEPTANCE.md](ACCEPTANCE.md) A0):**全英文、無空格**,格式 `類型_名稱_變體`,例:`tex_face_angry`、`splat_sauce_03`。
4. 回到 Unity,切到 Unity 視窗它會自動匯入(這時 Unity 會產生對應的 `.meta` 檔,正常現象)。
5. 貼圖尺寸請用 2 的次方(256 / 512 / 1024 / 2048),單張別超過 2048。

---

## 五、在 Unity 裡把模型做成 Prefab(手把手)

以「做一個披薩 Prefab」為例,其它物件同理。**每個 Prefab 具體要掛什麼元件、做到什麼程度,一律以 [PREFABS.md](PREFABS.md) 為準**;這裡只教「怎麼做出一個 Prefab 這個動作」。

1. **建一個放 Prefab 的資料夾**(第一次做時):
   在 Project 視窗的 `Assets` 上按右鍵 → **Create → Folder**,命名 `Prefabs`。
   → 全專案的 Prefab 都統一放 `Assets/Prefabs/`,檔名以 **`PZ_`** 開頭(例 `PZ_Pizza_Base`)。
   → 依類型再放進對應子資料夾:披薩類 → `Pizza/`、醬汁髒污 → `SauceSplat/`、UI 元件 → `UI/`;不確定放哪就先放根目錄問遊戲組。

2. **把模型拖進場景**:
   在 Project 視窗找到你的模型(例 `Assets/Art/Pizza/pizza1.fbx`),
   用滑鼠**拖到中間的 Scene 視窗**或左邊的 Hierarchy 視窗。場景裡就會出現這個物件。

3. **檢查外觀**:模型應該有正常材質。若是**粉紅色**,代表材質遺失(URP 問題),
   點模型 → 在右側 Inspector 找 **Materials → 換上正確材質**,或請遊戲組協助。

4. **(依 PREFABS.md 需要)加元件**:
   選中該物件,在 Inspector 最下面按 **Add Component**,依 [PREFABS.md](PREFABS.md) 對應章節加上需要的元件(如 Rigidbody、Collider 等)。
   > 純美術資產(例如醬汁貼圖、相框)通常不用加程式元件,交給遊戲組組裝即可 — 看 PREFABS.md 的「負責」欄。

5. **存成 Prefab**:
   把 Hierarchy 裡調好的物件,**拖回 Project 視窗的 `Assets/Prefabs/` 對應子資料夾**(見上方分類)。
   它會變成一個藍色方塊圖示的 `.prefab` 檔 → 成功!把檔名改成 `PZ_開頭` 的名字。

6. **之後要改 Prefab**:直接在 Project 雙擊那個 Prefab 進入編輯模式修改,存檔即可,場景裡用到它的地方會一起更新。

> 小提醒:做好後**自己先拖進場景 Play 看一次**,Console(下方訊息列)不能有紅字。這是交付前的基本檢查(ACCEPTANCE A0)。

---

## 六、把成果同步回 GitHub(Commit + Push)

做完一段落(例如一個 Prefab 做好了),就上傳讓大家拿到:

1. 回到 **GitHub Desktop**。左邊 **Changes** 會列出你這次改動/新增的所有檔案
   —— 你會看到 `.prefab`、`.fbx`、還有一堆 `.meta`,**這些都要一起上傳,不要取消勾選任何 `.meta`**。
2. 左下角填 **Summary**(這次做了什麼的一句話,中英文都行),例:
   `新增披薩 Prefab PZ_Pizza_Base 與三種口味貼圖`
3. 按藍色 **Commit to main**(這是「存檔到本機紀錄」)。
4. 按右上 **Push origin**(這才是真正**上傳到 GitHub**,別人才看得到)。
5. Push 成功後,通知其他組員可以 Pull 更新。

> **Commit ≠ 上傳**。只有按了 **Push origin** 東西才會上雲端。收工前確認 Push 完成。

---

## 七、新手最常踩的雷(先看這裡再求救)

| 狀況 | 原因 / 解法 |
|------|-------------|
| 別人下載後我的模型變**粉紅色**、材質不見、連結斷掉 | 十之八九是 **`.meta` 檔沒一起上傳**。回 GitHub Desktop 確認那些 `.meta` 都有 Commit + Push,別漏、別手動刪。 |
| Push 時跳出**衝突(conflict)** | 你和別人改到同一個檔案。**別硬幹** — 先截圖訊息,找遊戲組/Kendell 幫忙合併。預防方法:**開工前一定先 Pull**。 |
| 忘記先 Pull,改了一堆才發現落後 | 先 Commit 你自己的改動 → 再 Pull → GitHub Desktop 通常能自動合併;若跳衝突同上找人幫。 |
| 檔案**太大**上傳很慢或失敗 | `.fbx`/`.tiff` 等已走 Git LFS 會自動處理;若是新的大型格式(例如很大的 `.png`)跑不動,回報一下,可能要把該格式也加進 LFS。 |
| Unity 打開專案報**套件/版本錯誤** | 確認 Unity 版本是 **6000.3.19f1**;XR 套件報錯的處理看 [SETUP.md](../SETUP.md) 第 0 節。 |
| 不小心把東西改壞了想還原 | 還沒 Commit 的話,GitHub Desktop 在該檔案上按右鍵 → **Discard changes** 可還原到上次的版本。 |
| `Library/`、`Temp/` 這些資料夾沒出現在 Changes | 正常。那是 Unity 的本機快取,已設定不進版控(見 `.gitignore`),不用理它。 |

---

## 八、交付前自我檢查(對照 ACCEPTANCE.md A0)

每個資產/Prefab 上傳前,快速過一遍:

- [ ] 檔案放在 `Assets/Art/` 或 `Assets/Prefabs/` 的正確子資料夾
- [ ] 命名全英文、無空格
- [ ] 貼圖尺寸是 2 的次方,單張 ≤ 2048
- [ ] 匯入後**沒有粉紅色材質**、比例正常
- [ ] 自己拖進場景 Play 看過一次,Console 無紅字
- [ ] GitHub Desktop 裡 **`.meta` 檔全部一起 Commit**,且已按 **Push origin**

全部打勾,就完成一次乾淨的交付了 🎉

---

### 這幾份文件怎麼分工

- **本文件(ART_WORKFLOW.md)** — 新手的「怎麼操作 Unity 和 GitHub」
- [PREFABS.md](PREFABS.md) — 每個 Prefab「要做成什麼樣、做到哪算完成」
- [ACCEPTANCE.md](ACCEPTANCE.md) — 美術/軟體組的品質驗收清單
- [SETUP.md](../SETUP.md) — 遊戲組的專案與場景組裝設定
