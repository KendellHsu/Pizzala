# Pizzala 專案指引

VR 披薩店遊戲：玩家用飛盤手勢丟披薩給客人，砸偏會噴醬料、客人會丟回來。
Unity 6000.3.19f1、Quest（OpenXR）、主場景 `Assets/Scenes/BackBone.unity`。

## 文件地圖（依情境讀對應文件）

| 想知道… | 讀這份 |
|---|---|
| 目前進度、系統架構、模組相依 | [Docs/ARCHITECTURE.md](Docs/ARCHITECTURE.md) |
| 分工模式、交付循環、分支規則 | [Docs/WORKFLOW.md](Docs/WORKFLOW.md) |
| 可調參數在哪、預設值多少 | [Docs/PARAMETERS.md](Docs/PARAMETERS.md) |
| 某個 Prefab 怎麼做、做到什麼算完成 | [Docs/PREFABS.md](Docs/PREFABS.md) |
| 合併分支進 main 的標準流程 | [Docs/MERGE_GUIDE.md](Docs/MERGE_GUIDE.md) |
| 專案環境設定、場景組裝 | [Docs/SETUP.md](Docs/SETUP.md)、[Docs/BUILD_STEPS.md](Docs/BUILD_STEPS.md) |
| 驗收標準（美術／軟體） | [Docs/ACCEPTANCE_ART.md](Docs/ACCEPTANCE_ART.md)、[Docs/ACCEPTANCE_Unity.md](Docs/ACCEPTANCE_Unity.md) |
| 美術組上手（Unity/GitHub 新手） | [Docs/ART_WORKFLOW.md](Docs/ART_WORKFLOW.md) |
| 已知待改善問題 | [Docs/Problem.md](Docs/Problem.md) |

## 多人並行必守規則

1. **`Assets/Scenes/BackBone.unity` 只有 Kendell 能改**。要進場景的東西做成 Prefab 交付。
2. **`.meta` 檔一定跟著資產一起 commit**，否則別人 pull 下來會 Missing Reference。
3. **只改自己負責模組範圍的檔案**（分工地圖見 WORKFLOW.md）；要動共用的 `GameManager`、`ThrowTuning`、`GameData` 先在群組講好。
4. 交付走功能分支（`<名字>/<功能>`），由 Kendell 統一合併，細節見 WORKFLOW.md。
5. **每完成一個修改就 commit**（在自己的功能分支上）：一個 commit 對應一件完整的小事，訊息寫清楚改了什麼；不要累積一大包才 commit，出問題才好回溯。

## 參數文件維護（必守）

`Docs/PARAMETERS.md` 是全專案可調整參數的總表。任何時候新增、刪除、改名腳本裡的序列化欄位（`public` 欄位或 `[SerializeField]`），或修改其預設值，**必須同步更新 `Docs/PARAMETERS.md`** 的對應表格，並在文件底部的「更新紀錄」加一行。

- 玩法參數優先放進 `ThrowTuning`（ScriptableObject，`Assets/Settings/ThrowTuning.asset`），現場調參不用改程式碼。
