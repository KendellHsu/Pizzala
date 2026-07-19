# 歷史紀錄網站:分工與合併流程

> 目標:一個掛在自有網域下的網站。首頁是玩家拍立得牆(AI 黏土風玩家照 + hashtag),
> 點一張 → 該場成果頁(數據、主廚評語、玩家照、遊戲人格)+ 可分享的 QR code。
> 所有遊戲資料統一存在 Kendell 電腦,由後端直接讀取,經 tunnel 對外。

```
訪客 ──→ 網域 (DNS 託管於 Cloudflare)
        └→ Cloudflare Tunnel ──→ Kendell 電腦 localhost:8787(後端)
                                        └── 讀 Data/sessions/*.json + Data/photos/*.png
```

---

## 雙軌介面合約(兩邊都要遵守,合併的成敗在這)

先把「兩軌交會的那個點」釘死,之後各自怎麼做都不會互卡:

| 項目 | 約定 |
|---|---|
| 後端監聽位置 | `localhost:8787`,純 HTTP(TLS 由 tunnel 負責,本機**不要**弄 HTTPS) |
| Tunnel 對應 | 網域(或子網域)→ `http://localhost:8787` |
| URL 結構 | `/` = 拍立得牆首頁;`/s/{sessionId}` = 單場成果頁;`/api/sessions` = 場次列表 JSON;`/photos/...` = 圖片 |
| 資料位置 | Kendell 電腦上的一個資料夾(路徑寫在後端設定檔,不寫死在程式裡) |
| QR code 內容 | `https://<網域>/s/{sessionId}`(所以 QR 只在有網域之後才長得出正確內容) |

Kendell 的 prototype 期間**不需要真後端**:任何會在 8787 吐東西的假伺服器都行
(`python -m http.server 8787` 即可),他測的是「外網 → 網域 → 這個 port」這條路通不通。

---

## 軌道 A:Kendell — Tunnel 對外架構 prototype

**目標**:證明「站在校外的手機,打網域,能看到 Kendell 電腦 8787 port 上的東西」。

### 步驟
1. 本機起一個假伺服器:隨便一個資料夾放個 `index.html`,跑 `python -m http.server 8787`,先確認 `http://localhost:8787` 自己看得到
2. 把網域的 DNS 託管搬到 Cloudflare(免費方案即可;在原註冊商那邊把 nameserver 改成 Cloudflare 給的)
3. 安裝 `cloudflared`,建立 tunnel,把網域(建議先用子網域,例如 `lab.<網域>`)指到 `http://localhost:8787`
4. **驗收關鍵**:用手機關掉 Wi-Fi、走行動網路,開網域 → 看得到步驟 1 那頁 = 通了
5. 穩定性測試:掛著跑 1 小時以上,期間測「電腦休眠會怎樣」「Wi-Fi 斷線重連會不會自己恢復」,把行為記下來
6. 順手設定:電腦電源計畫關閉自動休眠;研究 `cloudflared` 開機自動啟動(裝成 Windows service)

### 交付物
- 一份簡短筆記:tunnel 設定方式、對外網址、斷線重連的實測行為、已知限制
- 一條可以隨時重現「網域 → localhost:8787」的設定(留在 Cloudflare 帳號裡)

### 驗收標準
- [ ] 校外裝置(行動網路)可透過網域看到本機內容
- [ ] `cloudflared` 斷線後可自動恢復,或至少知道怎麼手動一鍵恢復
- [ ] 知道電腦休眠/重開機後要做什麼才能讓網站復活(最好是全自動)

---

## 軌道 B:本地後端 + 網站(與 Claude 討論開發)

**目標**:一個在 `localhost:8787` 跑起來的後端 + 前端頁面,讀本機 `Data/`
資料夾,在**完全不管網域和 tunnel 的情況下**,用瀏覽器開 `localhost:8787` 就能完整展示。

### 步驟
1. **資料層擴充**(動 Unity 端,量很小):
   - `SessionData` 加 `playerPersona`(遊戲人格)、`playerCardImage`(黏土照檔名)兩個欄位
   - 主廚評語 `bossComment` **已經有存**(含第一行 hashtag),不用改;
     訂死拆解規則:「第一行 = hashtag,其餘 = 評語本文」,Unity 與網站共用這條規則
2. **後端 prototype**:讀 `Data/sessions/*.json` + `Data/photos/`,提供合約表列的四種 URL;
   技術選型從簡(Node 或 Python 皆可,single-file 起步)
3. **首頁拍立得牆**:照設計圖排版(PIZZALAB 標題、拍立得格線、hashtag 在白邊)
4. **成果頁 + QR**:數據、評語、玩家照、人格;頁面上同時放本頁網址的 QR code
5. 用現有 20 份範例 session 實測(照片多的那幾份當主展示)

### 待決事項(開工前要拍板)
- [ ] 黏土玩家照在哪生成:Unity 端 `EndRound` 時呼叫圖像生成 API 存進 `photos/`(資料集中,推薦)?還是網站端生成?
- [ ] 圖像生成用哪家 API、prompt 怎麼下(你們說要另外處理)
- [ ] 遊戲人格怎麼判定:跟評語同一次 LLM 呼叫順便回傳(省 API)?還是用數據規則分類?
- [ ] 玩家臉照上網的同意書/隱私範圍(公開牆 vs 私人連結)

### 驗收標準
- [ ] 本機開 `localhost:8787` 能看到拍立得牆,點入成果頁,QR 掃了能到成果頁
- [ ] 資料夾丟進新的 session JSON + 照片後,重新整理就出現(不用重啟後端更好)

---

## 合併流程(兩軌都綠燈後)

1. **交接後端**:把後端程式放上 Kendell 電腦(從 repo pull),設定檔指向他電腦上真正的 `Data/` 路徑,本機跑起來、`localhost:8787` 自測通過
2. **切換 tunnel 目標**:把軌道 A 的假伺服器關掉,tunnel 原封不動(port 相同,設定零修改)——這就是合約的意義
3. **端到端驗收**:
   - [ ] 校外手機開網域 → 看到真的拍立得牆
   - [ ] 現場玩一場 → 存檔落地 → 網站上出現這場(記錄從結束到可見的延遲)
   - [ ] 掃成果頁 QR → 手機看到該場成果
4. **展演日彩排 checklist**:電腦不休眠、`cloudflared` 與後端都設成開機自動啟動、斷網演練一次(拔網路線 → 插回 → 確認自動復活)
5. **備援方案**:若 tunnel 當天不穩,退回「靜態匯出」:跑腳本把 `Data/` 轉成靜態網站推上免費託管、網域改指託管商(事先把這條路也演練過一次,才叫備援)

---

## 更新紀錄
- 2026-07-18:初版(分工:Kendell = tunnel prototype;另一人 = 本地後端;定義介面合約與合併流程)
