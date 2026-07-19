# 前後端 API 契約(歷史紀錄網站)

> 給前端同學:這份定義後端會吐什麼、怎麼拿。後端把 Unity 存檔的原始資料整理成
> 前端友善的格式——hashtag 已拆好、圖片已是可直接用的網址,前端不用碰檔案系統、
> 不用解析任何字串。

## 怎麼傳

- **協定**:HTTP GET,回傳 `application/json`(UTF-8)
- **開發時 base URL**:`http://localhost:8787`(正式上線換成網域,路徑不變)
- **圖片**:直接用 `<img src="{baseURL}{該欄位的網址}">`,後端當靜態檔供應
- **CORS**:後端會開 `Access-Control-Allow-Origin: *`,前端可跨網域 fetch
- 所有時間欄位為 ISO 8601 字串;所有圖片欄位為相對路徑網址(前面接 base URL)

---

## 端點 1:首頁拍立得牆

**`GET /api/sessions`** — 回傳所有場次的精簡列表(畫牆只需要這些,輕量)。

```json
{
  "sessions": [
    {
      "id": "20260718_203048_Control",
      "playedAt": "2026-07-18T20:30:48",
      "cardImage": "/photos/card_20260718_203048_Control.png",
      "hashtags": ["#SauceTornado", "#BigEffort"],
      "persona": "Sauce Tornado"
    }
  ]
}
```

| 欄位 | 型別 | 說明 |
|---|---|---|
| `id` | string | 場次唯一 ID,點開詳情時用 |
| `playedAt` | string(ISO) | 遊玩時間,可拿來排序/顯示 |
| `cardImage` | string(URL) | 黑框裡的 LLM 生成玩家照。**可能為 `null`**(見下方「邊界情況」) |
| `hashtags` | string[] | 白邊上的標籤,已含 `#`,通常 2 個 |
| `persona` | string | 人格稱號(首頁用不到也先給,牆上想加可用) |

- 陣列預設**依 `playedAt` 由新到舊**排序。
- 0 投擲的空場次後端會自動濾掉,不會出現在牆上。

---

## 端點 2:單場成果頁(點開拍立得)

**`GET /api/sessions/{id}`** — 用列表裡的 `id`。回傳這一場的完整內容。

```json
{
  "id": "20260718_203048_Control",
  "playedAt": "2026-07-18T20:30:48",

  "card": {
    "image": "/photos/card_20260718_203048_Control.png",
    "hashtags": ["#SauceTornado", "#BigEffort"],
    "persona": "Sauce Tornado"
  },

  "bossComment": "Great arm tonight! You painted the shop more than the pizzas, but the customers had a blast. Same time tomorrow?",

  "photos": [
    {
      "image": "/photos/face_203112_045.png",
      "caption": "Hit in the face",
      "gameTime": 24.3,
      "type": "customerFace"
    },
    {
      "image": "/photos/env_203340_812.png",
      "caption": "Store overview",
      "gameTime": 180.0,
      "type": "environment"
    }
  ]
}
```

### 欄位對照(對應你要的畫面元素)

| 你要的元素 | 對應欄位 |
|---|---|
| 玩家拍立得(黑框照片) | `card.image` |
| 玩家拍立得(白邊 hashtag) | `card.hashtags` |
| 什麼型人格 | `card.persona` |
| 兩個 hashtag | `card.hashtags` |
| 老闆 LLM 回饋 | `bossComment`(**已拆掉 hashtag 那行,只剩留言本文**) |
| 場景&客人被砸 highlight 拍立得 | `photos[]`(客人被砸 + 店內場景合併,最多 6 張) |

### `photos[]`(場景 + 客人被砸,合併最多 6 張)

- 後端把「客人被砸臉照」和「店內場景照」**合併成一批**,依 `gameTime` 排序後,
  取代表性的**最多 6 張**(照結算畫面同一套挑選邏輯,時間上均勻分布,不是只取前 6)。
- 實際張數視那場拍到多少而定(可能少於 6);不足就全給。

每筆:

| 欄位 | 說明 |
|---|---|
| `image` | 照片網址(黑框裡放這個) |
| `caption` | 照片說明(白邊文字,例:「Hit in the face」) |
| `gameTime` | 這張照片在遊戲中第幾秒拍的(要顯示時間戳可用) |
| `type` | `"customerFace"`(客人被砸)或 `"environment"`(店內慘況);前端想分類可用,不分也行 |

- `photos` 已**依 `gameTime` 由早到晚**排序,最多 6 張。
- 玩家自己的臉照(真實照片)**不會**出現在這裡,只有生成的黏土照(`card.image`)——隱私考量。

---

## 邊界情況(請前端務必處理)

1. **`cardImage` / `card.image` 可能是 `null`**:黏土照生成失敗又沒有備援圖時。前端請顯示一個佔位圖(灰框或預設圖),不要壞版。
2. **`photos` 可能是空陣列 `[]`**:那場沒拍到任何照片。照片牆區塊請顯示「這場沒有照片」之類,不要留空洞。
3. **`bossComment` 可能是空字串 `""`**:對照組(Control)不生成老闆留言。前端該區塊可隱藏。
4. **`hashtags` 可能少於 2 個或為空**:LLM 偶爾不照格式。前端別假設一定是 2 個。
5. **找不到的 `id`**:回 `404` + `{"error": "session not found"}`。

---

## 給前端的一句話總結

打 `/api/sessions` 畫牆 → 使用者點某張 → 用它的 `id` 打 `/api/sessions/{id}` 畫成果頁。
所有圖片欄位前面接 base URL 就能當 `<img src>`;hashtag 已經是陣列、留言已經拆好,
拿到即用,不需要任何字串解析。

## 更新紀錄
- 2026-07-18:初版 API 契約(端點、JSON schema、邊界情況)
