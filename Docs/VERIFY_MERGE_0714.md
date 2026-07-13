# 合併驗收清單 — 2026-07-14 yenchia 美術資產

> 用法:在 Unity 裡照順序逐項打勾,全過後回報 Claude 執行 push。
> 遇到不符的項目**先不要修**,截圖/記下來回報,確認原因再動手。
> 一般性的合併流程見 [MERGE_GUIDE.md](MERGE_GUIDE.md),這份是本次合併的具體驗收。

## 這次合併進來的東西

| 類型 | 數量 | 位置 |
|---|---|---|
| 口味披薩 Prefab | 3(Margherita / Pepperoni / CosmicPinkMarshmallow) | `Assets/Prefabs/PZ_Pizza_*` |
| 丟回披薩 Prefab | 3(同上三口味) | `Assets/Prefabs/PZ_ThrowbackPizza_*` |
| 醬汁髒污 Prefab | 17(三口味系列) | `Assets/Prefabs/PZ_SauceSplat_*` |
| 新披薩模型 | pizza2、pizza3(含貼圖) | `Assets/Art/Pizza/` |
| 醬汁材質 | 17 | `Assets/Art/Meterials/` |
| 隊友的測試場景 | test.unity | `Assets/Scenes/` |

已在合併時修好、不用你處理:GUID 斷鏈(ThrowbackPizza ×2、test.unity)、口味 enum 值對調、Hawaiian → CosmicPinkMarshmallow 改名。

---

## 第 1 步:讓 Unity 匯入 + 全域檢查

1. 切回 Unity 視窗,等右下角匯入進度條跑完(pizza2/3 貼圖較大,約 1~2 分鐘)
2. 改名 enum 會觸發重新編譯,等 Console 安靜下來

- [ ] Console **沒有紅字**(黃字記下內容,不擋驗收)

## 第 2 步:口味披薩 Prefab ×3

Project 視窗 `Assets/Prefabs/`,逐一雙擊 `PZ_Pizza_Margherita`、`PZ_Pizza_Pepperoni`、`PZ_Pizza_CosmicPinkMarshmallow`:

- [ ] 模型顯示正常,**不是粉紅色**
- [ ] Inspector 有四個元件:Rigidbody、Collider、XR Grab Interactable、Pizza Projectile,**沒有 "Missing (Mono Script)"**
- [ ] Rigidbody:Mass = 0.3、Collision Detection = **Continuous Dynamic**(不是就記下來回報)
- [ ] XR Grab Interactable:Movement Type = **Velocity Tracking**、**Throw On Detach 有勾**
- [ ] Pizza Projectile 的 Flavor 欄位:Margherita / Pepperoni / **Cosmic Pink Marshmallow** 各自正確
- [ ] 三個外觀肉眼可區分

> 注意:隊友做的是三個獨立 Prefab,不是 PREFABS.md 說的「基底 + Variant」結構。之後調物理手感要**三個都改**,先記著就好。

## 第 3 步:丟回披薩 Prefab ×3

- [ ] `PZ_ThrowbackPizza_Pepperoni`:有 Rigidbody + Collider + **Throwback Projectile**,無 Missing Script
- [ ] `PZ_ThrowbackPizza_CosmicPinkMarshmallow`:同上
- [ ] `PZ_ThrowbackPizza_Margherita`:**隊友漏掛腳本,你來補**——開 Prefab → Add Component → `ThrowbackProjectile` → 存檔(Ctrl+S)
- [ ] 三個都**沒有** XR Grab Interactable 和 PizzaProjectile(有就移除:丟回披薩不能被玩家抓)
- [ ] Collider 的 Is Trigger **沒勾**

## 第 4 步:醬汁 Prefab 抽查

17 個不用全看,每個口味系列抽 1~2 個:

- [ ] 顯示正常不粉紅,形狀有變化
- [ ] 只有 MeshFilter + MeshRenderer(檔案層面已確認無 Collider,肉眼再掃一眼即可)

## 第 5 步:自己的東西沒被動到

- [ ] 開 `Assets/Scenes/SampleScene.unity`:Systems(四支腳本)、XR Origin(雙手 Sampler、HeadHitbox)都完好
- [ ] 按 Play(可以不戴頭盔):Console 無紅字
- [ ] (建議)戴頭盔 Play 一次:把 `PZ_Pizza_Margherita` 拖進場景,能抓能丟、砸牆不穿牆 → 測完刪掉

## 第 6 步:全過 → 上傳

回報 Claude 執行 push,或自己跑:

```bash
git push origin main
```

然後通知隊友:main 已包含他的資產 + GUID 修復,請他把自己的 fork/分支同步到最新的 main 再繼續做(不然他下次交付又是從舊基底分出來)。

---

## 驗收後的下一步(接回 BUILD_STEPS)

披薩(§5)、丟回披薩(§6)、醬汁(§7)這三節等於完成了,繼續:

1. [BUILD_STEPS.md](BUILD_STEPS.md) §7 收尾:把醬汁 Prefab 拖進 Systems → DirtManager 的 **Splat Prefabs** 陣列(17 個全拖或先挑 6 個)
2. §8 客人 Prefab → §9 出餐台(Spawner 的 Pizza Prefab 拖隊友的三個口味)→ §10~13
