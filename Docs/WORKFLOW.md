# Pizzala — 分工工作模式（WORKFLOW.md）

> 給每一位組員（和你的 AI）：多人並行開發從這份文件開始。
> 規劃與任務分派用組內紙筆/口頭進行，repo 只記「怎麼交付」和「目前狀態」。
> 目前進度與系統架構看 [ARCHITECTURE.md](ARCHITECTURE.md)。

---

## 交付循環（每個功能都走一遍）

1. **開工先同步**：pull 最新 `main`，看 [ARCHITECTURE.md](ARCHITECTURE.md) 的進度總覽確認全案走到哪。
2. **開功能分支**：命名 `<名字>/<功能>`（例：`yenchia/sauce-liquid`），在分支上工作。**每完成一個修改就 commit**——一個 commit 一件完整的小事（例如「加一個欄位」「修一個 bug」），訊息寫清楚改了什麼，不要整個功能做完才一包 commit。
3. **做完一個功能就交付**：push 分支後告知 Kendell **這個分支完成了哪些功能**（口頭、訊息或 PR 描述皆可），交付前自己先用 **Quest Link 連 PC 按 Play** 實測過一次。
4. **等待合併**：Kendell 照 [MERGE_GUIDE.md](MERGE_GUIDE.md) 合併進 `main` 並實測確認功能正常。
5. **繼續前進**：合併完成後 pull 最新 `main`，開下一個功能分支。等待合併期間可以先開新分支做下一個功能（從自己上一個分支切出去）。

## 模組分工地圖

每人負責一個系統，只動自己範圍內的檔案。負責人由組內分派後填入。

| 模組 | 負責人 | 程式碼範圍 | 相關資產範圍 |
|---|---|---|---|
| 回合流程 Core | | `Assets/Scripts/Core/` | `Assets/Settings/ThrowTuning.asset` |
| 客人系統 Customers | | `Assets/Scripts/Customers/` | `Assets/Prefabs/PZ_Customer*.prefab` |
| 丟擲系統 Throwing | | `Assets/Scripts/Throwing/` | `Assets/Prefabs/Pizza/` |
| 醬料髒污 Dirt | | `Assets/Scripts/Dirt/` | `Assets/Prefabs/SauceSplat/` |
| UI／拍照 | | `Assets/Scripts/UI/`、`Assets/Scripts/Photo/` | `Assets/Prefabs/UI/` |
| 數據紀錄 Data | | `Assets/Scripts/Data/` | — |
| 美術資產 | | — | `Assets/Art/`（流程見 [ART_WORKFLOW.md](ART_WORKFLOW.md)） |
| 場景整合 | Kendell | — | `Assets/Scenes/BackBone.unity` |

`Assets/Scripts/DevTools/` 是測試用觸發器，共用；要加自己模組的測試工具放這裡。

## 並行鐵律（違反就會出合併事故）

1. **`Assets/Scenes/BackBone.unity` 只有 Kendell 動。** 要進場景的東西一律做成 Prefab 交付，由 Kendell 擺進場景。（scene 檔是二進位式大 YAML，兩人同時改幾乎必衝突。）
2. **`.meta` 檔一定跟著資產一起 commit。** 少了 meta，別人 pull 下來就是 Missing Reference / 粉紅材質（原理見 [MERGE_GUIDE.md](MERGE_GUIDE.md) 第零節）。
3. **只動自己模組範圍的檔案。** 需要動到別人模組（例如 `GameManager` 的公開方法、`ThrowTuning` 加欄位）先在群組講好再動。
4. **改序列化欄位要同步 [PARAMETERS.md](PARAMETERS.md)**：新增／刪除／改名 `public` 欄位或 `[SerializeField]`、改預設值，都要更新對應表格並在底部「更新紀錄」加一行。

## AI 協作約定

- 你的 AI 開工時先讀專案根目錄的 `CLAUDE.md`（文件索引與必守規則都在那）。
- **AI 每完成一個修改就要 commit**（CLAUDE.md 已寫成必守規則，AI 會自動遵守）；commit 訊息由 AI 寫清楚改動內容，方便 Kendell 合併時逐條檢視。
- AI 改出來的東西**你自己要 Play 過再交付**——AI 沒戴頭盔，手感和 VR 內的視覺只有你驗得了。
- AI 改了序列化欄位，提醒它照規則同步 PARAMETERS.md（CLAUDE.md 裡有寫，正常會自動遵守）。
