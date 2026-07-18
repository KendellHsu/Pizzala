# 對話紀錄:建立 Tunnel Lab 雛形

> 日期:2026-07-18
> 情境:根據 `WEB_HISTORY_PLAN.md` 的「軌道 A」,建立可驗證整條資料通道的雛形設定文件。
> 驗證方式:用手機連到自有網域 `hsupingjhao.info` 的子網域,確認連線是否通順以及可以正常傳輸資料。

---

## 需求

依 `WEB_HISTORY_PLAN.md`,軌道 A 的目標是證明:

> 站在校外的手機,打網域,能看到 Kendell 電腦 8787 port 上的東西。

使用者額外要求:不只驗「看得到」,還要確認**可以正常傳輸資料**。

---

## 過程中拍板的決定

| 項目 | 決定 | 理由 |
|---|---|---|
| 驗證深度 | **連線 + 資料往返** | 純看得到頁面不夠;要驗 GET 回得來、POST 上得去、大檔下載穩不穩 |
| 子網域 | `pizzala.hsupingjhao.info` | 用專案名,之後正式上線可沿用 |
| 檔案位置 | 專案外 `C:\Users\dexlab\pizzala-tunnel-lab\` | 這是 tunnel 驗證用臨時架構,不進 Unity git repo |
| 伺服器技術 | **Python `http.server`(`server.py`)** | 使用者偏好 Python 路線;見下方環境探勘與 2026-07-18 更新 |

---

## 環境探勘結果(選型關鍵)

實際檢查本機環境後發現:

- `python` 指向 `C:\Users\dexlab\AppData\Local\Microsoft\WindowsApps\python.exe`
  —— 這是 **Windows Store 的 stub**(打開會跳商店安裝頁),不是真 Python,
  `python -m http.server` 不可靠。
- **沒有** Node.js。
- **沒有** cloudflared(待安裝)。
- 專案內**沒有** `Data/` 範例資料夾。

> **註(2026-07-18 稍晚更新)**:使用者決定維持 Python 路線,因此改為
> 先 `winget install Python.Python.3.12` 裝真 Python,再用 `server.py`。
> 下面這段是「當時還沒裝 Python」時的推理,保留作為背景。

當時的推理:

1. 捨棄文件建議的 `python -m http.server`,改用 **Windows 內建的
   `System.Net.HttpListener`**(PowerShell,零安裝)。
2. 這個方案同時能做到「回 JSON / 收 POST / 送檔案」,一次滿足
   「連線 + 資料正常傳輸」的驗證需求(純靜態 `http.server` 做不到 POST)。

### 可靠性驗證

用非同步 round-trip 測試確認 HttpListener 在本機能正常起監聽、回 JSON、被打通:

```
CLIENT RECEIVED: {"ok":true,"path":"/api/ping","time":"2026-07-18T20:36:42..."}
```

→ 核心機制可靠,Python stub 問題完全繞開。

---

## 交付的檔案

| 檔案 | 作用 |
|---|---|
| `server.py` | Python `http.server` 假伺服器(`HTTPServer` + 自訂 API handler) |
| `public/index.html` | 手機打開的測試頁,四顆按鈕驗連線與傳輸 |
| `data/sessions.json` | 假場次資料,順便先跑通軌道 B 合約的 `/api/sessions` |
| `README.md` | 完整設定 + 驗收流程 |
| `CONVERSATION_LOG.md` | 本檔 |

### 測試端點

| URL | 驗什麼 |
|---|---|
| `/` | 連線通不通 |
| `/api/ping` | 資料回得來嗎(JSON,含伺服器時間、來源 IP、是否經過 Cloudflare) |
| `/api/echo`(POST)| 資料傳得上去嗎(原樣取回 = 完整) |
| `/download/sample.bin` | 大檔(1 MB)下載穩不穩、速度 |
| `/api/sessions` | 假場次列表(先跑通軌道 B 合約) |

### 手機四顆按鈕 對應 驗收項目

1. 頁面出現 → 連線通
2. Ping → 回 JSON + 顯示 `CF-Connecting-IP`(證明走 tunnel)
3. 上傳並取回 → 上行資料完整
4. 下載 1MB + 計時 → 大檔傳輸與速度

---

## 使用者接下來要做(照 README)

1.(已完成)`winget install Python.Python.3.12` —— 真 python.exe 在
   `C:\Users\dexlab\AppData\Local\Programs\Python\Python312\python.exe`
2. 執行 `python server.py`,先在電腦開 `http://localhost:8787/` 確認四顆全綠
   (`http.server` 直接綁 `0.0.0.0:8787`,不需要 netsh 授權)
3. `winget install --id Cloudflare.cloudflared`
4. 先用 `cloudflared tunnel --url http://localhost:8787` 拿臨時網址在手機測通
5. 通了再綁 `pizzala.hsupingjhao.info`

---

## 待回覆的待決事項

~~伺服器監聽位置 A/B~~ —— 已隨改用 `server.py` 一併定案。目前 `server.py`
綁 `0.0.0.0:8787`(對外全網卡),不需要 `netsh` 授權。若之後想更保守
(8787 不對區網開,只讓 cloudflared 從本機連進去),把 `server.py` 的
`HOST = "0.0.0.0"` 改成 `HOST = "127.0.0.1"` 即可,其餘不動。

---

## 更新紀錄

- 2026-07-18:初版。建立 Tunnel Lab 雛形(server.ps1 / index.html / sessions.json / README),
  完成 HttpListener 可靠性驗證,環境探勘記錄 Python stub 問題。
- 2026-07-18(稍晚):使用者要求維持 Python 路線。以 `winget install Python.Python.3.12`
  裝真 Python(3.12.10),把伺服器從 `server.ps1` 改寫為 `server.py`
  (Python `http.server` + 自訂 API handler,四個端點行為與前端完全不變),
  刪除 `server.ps1`,更新 README 與本檔。已本機實測六項全過
  (`/`、`/api/ping`、POST `/api/echo` 往返、`/api/sessions`、1MB 下載、404)。
