# Pizzala Tunnel Lab（軌道 A 雛形）

目標:證明**校外手機 → `pizzala.hsupingjhao.info` → 你電腦的 8787 port**
這條路不只連得到,還能**正常傳輸資料**(GET / POST 上傳 / 檔案下載)。

這是驗證用的假伺服器,不是真後端。真後端上線時只要維持
`localhost:8787` 與相同 URL 結構,**tunnel 設定完全不用改** —— 這就是介面合約的意義。

```
手機(行動網路) ──→ pizzala.hsupingjhao.info
                     └→ Cloudflare Tunnel ──→ 本機 http://localhost:8787 (server.py)
```

檔案:
- `server.py` — Python `http.server` 假伺服器(在 `HTTPServer` 上補了幾個 API handler)
- `public/index.html` — 手機打開後的測試頁,四顆按鈕驗連線與傳輸
- `data/sessions.json` — 假場次資料(對應合約的 `/api/sessions`)

> 為什麼不是純 `python -m http.server`:內建那支只做靜態檔案伺服,
> 不能做 POST `/api/echo` 往返、也不能回動態 JSON。`server.py` 沿用同一套
> `http.server`,只是在上面補了 `/api/ping`、`/api/echo`、下載等 handler,
> 啟動方式與心智模型跟 `python -m http.server` 一樣。

---

## 提供的測試端點

| URL | 驗什麼 |
|---|---|
| `/` | 連線通不通(看得到頁 = 通) |
| `/api/ping` | 資料回得來嗎(回 JSON,含伺服器時間、你的來源 IP、是否經過 Cloudflare) |
| `/api/echo`（POST）| 資料傳得上去嗎(上傳原樣取回 = 資料完整) |
| `/download/sample.bin` | 大檔(1 MB)下載穩不穩、速度多少 |
| `/api/sessions` | 假場次列表(順便先跑通軌道 B 合約) |

---

## Step 0 —（只做一次）裝真的 Python

本機原本只有 Windows Store 的 python stub(打開會跳商店安裝頁,不能真的跑),
所以先裝一份真 Python:

```powershell
winget install --id Python.Python.3.12 -e
```

裝好後,真的 python.exe 通常在:

```
C:\Users\dexlab\AppData\Local\Programs\Python\Python312\python.exe
```

> `http.server` 直接綁 `0.0.0.0:8787`(所有網卡),**不需要 netsh 授權**,
> 這是它比 HttpListener 省事的地方。Windows 防火牆若跳出詢問,勾選允許(私人網路即可)。

---

## Step 1 — 本機起假伺服器

開 PowerShell,在此資料夾執行(用上面那條真 python.exe 的完整路徑):

```powershell
& "C:\Users\dexlab\AppData\Local\Programs\Python\Python312\python.exe" server.py
```

> 若 `python` 已加進 PATH(重開終端機後),也可以直接 `python server.py`。
> 想換 port:`python server.py 9000`。

看到「假伺服器已啟動」後,**先用電腦瀏覽器開** http://localhost:8787/ ,
四顆按鈕都按一次,全綠 = 伺服器本身沒問題,再往下接 tunnel。

---

## Step 2 — DNS 搬到 Cloudflare

1. 到 Cloudflare 免費方案,加入網域 `hsupingjhao.info`
2. 到原註冊商後台,把 nameserver 改成 Cloudflare 給你的兩組
3. 等生效(通常幾分鐘到數小時),Cloudflare 顯示 Active

---

## Step 3 — 建 Tunnel,指到 8787

安裝 `cloudflared`(擇一):

```powershell
winget install --id Cloudflare.cloudflared
```

登入並建立 tunnel:

```powershell
cloudflared tunnel login
cloudflared tunnel create pizzala-lab
```

把子網域指到本機 8787。最快的方式是用「命名 tunnel + 設定檔」,
或直接用一行 quick tunnel 先驗一次(先確認能通,再做穩定版):

```powershell
# 快速驗證版:跑起來會給你一組臨時 *.trycloudflare.com 網址,先用它測資料通不通
cloudflared tunnel --url http://localhost:8787
```

確認 quick tunnel 那組網址在手機上能跑通後,再綁正式子網域:

```powershell
# 綁 pizzala.hsupingjhao.info → 這個 tunnel
cloudflared tunnel route dns pizzala-lab pizzala.hsupingjhao.info
```

並在設定檔(通常 `C:\Users\dexlab\.cloudflared\config.yml`)寫:

```yaml
tunnel: <上一步 create 出來的 tunnel ID>
credentials-file: C:\Users\dexlab\.cloudflared\<tunnel-id>.json
ingress:
  - hostname: pizzala.hsupingjhao.info
    service: http://localhost:8787
  - service: http_status:404
```

然後執行:

```powershell
cloudflared tunnel run pizzala-lab
```

---

## Step 4 — ✅ 驗收(這就是你要的驗證流程)

**手機關掉 Wi-Fi、走行動網路**,瀏覽器開:

```
https://pizzala.hsupingjhao.info
```

依序:

1. 頁面出現 → **連線通** ✓
2. 按「測試 Ping」→ 綠字 + 顯示往返 ms,且出現
   `經過 Cloudflare tunnel（CF-Connecting-IP=…）` → **資料回得來、確實走 tunnel** ✓
3. 按「上傳一段文字並取回」→「上傳的內容原樣取回,資料完整」→ **上行資料正常** ✓
4. 按「下載測試檔並計時」→ 顯示速度 → **大檔傳輸沒問題** ✓

四項全綠 = 整條資料通道打通,可以進到軌道 B 接真後端。

---

## Step 5 — 穩定性(展演前要做)

- 掛著跑 1 小時以上,期間手機每隔一陣子重按 Ping
- 測「電腦休眠會怎樣」「Wi-Fi 斷線重連會不會自己恢復」,把行為記在下面
- 電源計畫關閉自動休眠
- 研究把 `cloudflared` 裝成 Windows service 開機自動啟動:
  `cloudflared service install`

### 實測筆記（自己填）

- tunnel 設定方式:
- 對外網址:
- 斷線重連行為:
- 電腦休眠後行為:
- 已知限制:
