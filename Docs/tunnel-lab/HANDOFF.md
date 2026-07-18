# Tunnel Lab 交接說明（給組員)

> 狀態:✅ **已驗收通過**。手機走行動網路,開 `https://pizzala.hsupingjhao.info`,
> 四項測試(連線 / GET 回 JSON / POST 上傳往返 / 1MB 下載)全綠。
> 這證明了 `WEB_HISTORY_PLAN.md` 軌道 A:「校外手機 → 網域 → Kendell 電腦 8787 port,
> 且資料能正常往返」。

---

## 這是什麼 / 不是什麼

- **是**:一條「校外裝置 → 自有網域 → 本機服務」資料通道的**驗證雛形**,以及一個
  假後端(`server.py`)。用來證明整條路會動、資料能往返。
- **不是**:真後端。`server.py` 只是回假資料。真後端上線時,只要維持
  **`localhost:8787` + 相同 URL 結構**,tunnel 設定完全不用改 —— 這就是介面合約。

## 整條路長怎樣

```
手機(行動網路) ──→ https://pizzala.hsupingjhao.info
                     └→ Cloudflare Tunnel ──→ 本機 http://localhost:8787 (server.py)
```

## 檔案

| 檔案 | 作用 |
|---|---|
| `server.py` | Python `http.server` 假後端(四個端點,見下) |
| `public/index.html` | 手機打開的測試頁,四顆按鈕驗連線與傳輸 |
| `data/sessions.json` | 假場次資料(對應軌道 B 合約的 `/api/sessions`) |
| `README.md` | 完整從零設定 + 驗收流程(要重建整條路看這份) |
| `CONVERSATION_LOG.md` | 建置過程的決策紀錄(為什麼這樣選) |
| `HANDOFF.md` | 本檔 |

## 測試端點

| URL | 驗什麼 |
|---|---|
| `/` | 連線通不通 |
| `/api/ping` | GET 回 JSON(含伺服器時間、來源 IP、是否經過 Cloudflare) |
| `/api/echo`(POST)| 上傳原樣取回 = 上行資料完整 |
| `/download/sample.bin` | 大檔(1MB)下載穩不穩、速度(檔案首次請求時自動生成) |
| `/api/sessions` | 假場次列表(先跑通軌道 B 合約) |

---

## 組員要自己重跑一次的話

完整步驟在 `README.md`。最短路徑:

1. **裝真 Python**(本機原本只有 Windows Store stub 不能用):
   `winget install --id Python.Python.3.12 -e`
2. **起假後端**(視窗別關):
   `python server.py` → 電腦開 http://localhost:8787/ 確認四顆全綠
3. **裝 cloudflared**:`winget install --id Cloudflare.cloudflared -e`
4. **先用臨時網址驗通**:`cloudflared tunnel --url http://localhost:8787`
   → 拿到 `*.trycloudflare.com`,手機測四顆全綠
5. 要綁自有網域,再照 README 的 Step 2/3 做(DNS 搬 Cloudflare + named tunnel)。

---

## ⚠️ 機密 / 注意事項(重要)

- **憑證檔絕對不能外流、不能進 git**:Kendell 這台的 cloudflared 憑證在
  `C:\Users\dexlab\.cloudflared\`(`cert.pem` 與 `<tunnel-id>.json`)。
  那是 tunnel 的「密碼」,**不在這個資料夾裡**,也不要複製給任何人或推上版控。
- **`data/sample.bin` 不進版控**:那是 1MB 測試檔,首次下載時自動生成,已在 `.gitignore` 排除。
- **這個 tunnel 目前綁在 Kendell 這台電腦**:別人的電腦要跑,要自己建自己的 tunnel
  (照 README Step 3),不能共用同一組憑證。
- **`server.py` 綁 `0.0.0.0:8787`**:對區網開放。展演環境若要更保守,把 `server.py` 裡的
  `HOST = "0.0.0.0"` 改成 `"127.0.0.1"`,只讓 cloudflared 從本機連進去。

## 展演前待辦(穩定性)

- tunnel 掛著跑 1 小時以上,期間手機每隔一陣子重按 Ping
- 測「電腦休眠會怎樣」「Wi-Fi 斷線重連會不會自己恢復」
- 電源計畫關閉自動休眠
- 研究把 cloudflared 裝成 Windows service 開機自動啟動:`cloudflared service install`
