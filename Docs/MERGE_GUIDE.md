# Pizzala — 合併隊友成果到 main 的標準流程

> 適用情境:隊友在自己的分支(或 fork)上交付了資產,要合回 `main`。
> 本文以 2026-07-14 第一次合併 `yenchia` 分支的實戰為藍本。
> 美術隊友的上傳流程見 [ART_WORKFLOW.md](ART_WORKFLOW.md);本文是**負責合併的人**(通常是 Kendell)要做的事。

---

## 零、背景知識:為什麼 Unity 專案的合併特別容易出事

1. **`.meta` 檔與 GUID**:Unity 給每個資產一個 GUID(存在 `.meta`),所有引用(Prefab 掛哪支腳本、用哪張材質)都是 GUID 對 GUID。
   同一個檔案如果在兩台機器上「各自」被 Unity 生成 meta,GUID 會不一樣 → 合併後引用斷裂 → **Missing Script / 粉紅材質**。
2. **場景與 Prefab 是巨大的 YAML**:兩人改同一個場景,git 幾乎無法乾淨合併。**一個場景同時只能一個人動**,隊友要測試就開自己的場景(如 `test.unity`)。
3. **設定檔以 main 為準**:`ProjectSettings/`、`Assets/XR*` 是全專案共用的環境設定,main 上的版本是驗證過的(Quest Link 可用),合併衝突時一律保留 main。

---

## 一、合併前(隊友交付時要求的事)

- [ ] 隊友把東西 **push 上雲端**(Commit ≠ 上傳,要按到 Push)
- [ ] 問清楚:推到**哪個 repo**(本家 `Kendellhsu/Pizzala` 還是他的 fork)、**哪個分支**
- [ ] 問一句:「**有沒有動到 SampleScene?**」有 → 請他改交付 Prefab、場景改動由你在 main 重做
- [ ] 你自己的 working tree 先清乾淨:`git status` 有東西就先 commit

## 二、抓下來、先看再合(不要直接 merge)

```bash
# fork 只需加一次 remote(yenchia 的已加過,之後直接 fetch)
git remote add <名字> https://github.com/<帳號>/Pizzala.git

git fetch <名字>

# 1. 他做了哪些 commit
git log --oneline main..<名字>/<分支>

# 2. 動了哪些檔案(重點看最後的統計行,和有沒有動到不該動的)
git diff --stat main...<名字>/<分支>

# 3. 找出「兩邊都動過」的檔案 = 衝突熱區(MB=共同祖先)
MB=$(git merge-base main <名字>/<分支>)
comm -12 <(git diff --name-only $MB main | sort) \
         <(git diff --name-only $MB <名字>/<分支> | sort)
```

**看到什麼要警覺:**
| 交集裡出現 | 意義 | 對策 |
|---|---|---|
| `*.cs.meta`、資料夾 `.meta` | 兩台機器各自生成 GUID | 衝突取 main 的;合併後要做 GUID 消毒(見第四節) |
| `ProjectSettings/*`、`Assets/XR*` | 他動了專案設定 | 一律取 main 的 |
| 同一個 `.unity` 場景 | 真正麻煩的衝突 | 不要硬合;取一邊,另一邊的改動在 Editor 重做 |
| 同一個 `.prefab` | 兩人改同一個 Prefab | 找作者確認哪版是新的,取那版 |

## 三、合併與衝突處理

```bash
git merge <名字>/<分支>

# 衝突時:設定檔/meta 全取 main(--ours)
git diff --name-only --diff-filter=U -z | xargs -0 git checkout --ours --
git diff --name-only --diff-filter=U -z | xargs -0 git add --

# 若某個檔應該取對方的(例如他改過的美術資產):
git checkout --theirs -- <路徑>
git add <路徑>
```

> 原則:**環境設定聽 main 的,資產內容聽作者的**。拿不準就兩版都看一眼(`git show main:<路徑>` / `git show <名字>/<分支>:<路徑>`)。

## 四、GUID 消毒(Unity 特有,merge 不會自動報錯!)

只要第二節的交集裡有 `.meta` 檔就必須做。目的:找出他的 Prefab/場景裡引用了「他的 GUID」但我們保留了「main 的 GUID」的斷鏈。

```bash
# 對每個衝突過的 .cs.meta:比對兩邊 GUID,再全域搜尋他的 GUID 殘留
THEIRS=$(git show <名字>/<分支>:"Assets/Scripts/XX.cs.meta" | grep -o "guid: [a-f0-9]*" | cut -d' ' -f2)
OURS=$(grep -o "guid: [a-f0-9]*" "Assets/Scripts/XX.cs.meta" | cut -d' ' -f2)
grep -rl "$THEIRS" Assets --include="*.prefab" --include="*.unity" --include="*.asset" --include="*.mat"

# 有搜到 → 把舊 GUID 換成 main 的
sed -i "s/$THEIRS/$OURS/g" <搜到的每個檔案>
```

換完 `git add` 進 merge commit 一起提交。

> 治本之道:**所有 `.meta` 都進版控**之後,舊檔案不會再發生這種事;只剩「兩人同時新建同路徑檔案」會重演,所以**分工不要重疊檔案路徑**。

## 五、合併後驗收(推上去之前必做)

1. **切回 Unity 等重新匯入**,Console **無紅字**(黃字看一眼內容)
2. Project 視窗抽查每個新 Prefab:
   - Inspector 沒有 **"Missing (Mono Script)"**
   - 模型**不是粉紅色**(粉紅 = 材質引用斷)
   - 該掛的元件都在(對照 [PREFABS.md](PREFABS.md) 該節的元件表)
   - 腳本欄位值合理(例:`flavor` 對應 0=Margherita 1=Pepperoni 2=CosmicPinkMarshmallow)
3. 開 `SampleScene` 確認自己的東西沒被動到,按 **Play** 快速跑一圈
4. 全過才推:
   ```bash
   git push origin main
   ```
5. 通知全隊 pull;隊友那邊(fork)要把 main 同步回去才能繼續做

## 六、快速檢查清單(印下來勾)

- [ ] 隊友已 push,repo/分支確認
- [ ] 我的 working tree 乾淨
- [ ] fetch 後先看 log / diff --stat / 交集
- [ ] merge;衝突:設定與 meta 取 main,資產取作者
- [ ] 交集有 meta → GUID 消毒
- [ ] Unity:Console 無紅字、Prefab 無 Missing/粉紅、Play 正常
- [ ] push + 通知全隊
