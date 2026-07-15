# Pizzala 專案指引

## 參數文件維護（必守）

`Docs/PARAMETERS.md` 是全專案可調整參數的總表。任何時候新增、刪除、改名腳本裡的序列化欄位（`public` 欄位或 `[SerializeField]`），或修改其預設值，**必須同步更新 `Docs/PARAMETERS.md`** 的對應表格，並在文件底部的「更新紀錄」加一行。

- 玩法參數優先放進 `ThrowTuning`（ScriptableObject，`Assets/Settings/ThrowTuning.asset`），現場調參不用改程式碼。
- 主場景是 `Assets/Scenes/BackBone.unity`。
