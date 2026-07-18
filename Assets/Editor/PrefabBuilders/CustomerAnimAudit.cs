using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Pizzala.Customers;

namespace Pizzala.EditorTools
{
    // 客人動畫設置健檢:一鍵列出每個客人 Prefab 的動畫關鍵設定,
    // 對應兩種常見症狀:
    //   「站著滑行」= walk 控制器沒接 / clip 骨架路徑對不上模型(Generic rig 靠路徑綁骨頭)
    //   「原地踏步/抖動」= Apply Root Motion 開著,動畫位移跟腳本 MoveTowards 打架
    // 檢查項目:
    //   1. Animator 數量(必須恰好 1 個;疊兩個會讓 CustomerController 抓錯)
    //   2. Apply Root Motion(必須關;位移全由 CustomerController 腳本驅動)
    //   3. 單一 animatorController 是否有接(內含 Idle/Walk/Throw 三 state)
    //   4. controller 內的 clip 骨架路徑是否對得上 prefab 裡的模型
    // 結果印在 Console,每個角色一則;有 ❌ 的就是要修的地方。
    public static class CustomerAnimAudit
    {
        static readonly string[] PrefabPaths =
        {
            "Assets/Prefabs/Customer/PZ_Customer_Solider.prefab",
            "Assets/Prefabs/Customer/PZ_Customer_UncleB.prefab",
            "Assets/Prefabs/Customer/PZ_Customer BathrobeDad.prefab",
        };

        [MenuItem("Tools/Pizzala/Audit Customer Animators")]
        public static void Audit()
        {
            foreach (var path in PrefabPaths)
                AuditOne(path);
            Debug.Log("[CustomerAnimAudit] 健檢完成,往上看每個角色的報告(有 ❌ 的要修)");
        }

        static void AuditOne(string path)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go == null)
            {
                Debug.LogError($"[CustomerAnimAudit] 找不到 prefab:{path}");
                return;
            }

            var sb = new StringBuilder();
            bool hasProblem = false;
            sb.AppendLine($"===== {go.name} =====");

            // 1. Animator 數量
            var animators = go.GetComponentsInChildren<Animator>(true);
            if (animators.Length == 0)
            {
                sb.AppendLine("❌ 整個 prefab 沒有 Animator → idle/walk 都不會播,只會滑行");
                Debug.LogError(sb.ToString());
                return;
            }
            if (animators.Length > 1)
            {
                hasProblem = true;
                sb.AppendLine($"❌ 有 {animators.Length} 個 Animator(應該只有 1 個),CustomerController 只會用第一個:");
                foreach (var a in animators)
                    sb.AppendLine($"     - {GetPath(go.transform, a.transform)}");
            }

            // CustomerController.Awake 用 GetComponentInChildren,拿到的就是第一個
            var animator = animators[0];
            sb.AppendLine($"Animator 掛在:{GetPath(go.transform, animator.transform)}");

            // 2. Apply Root Motion
            if (animator.applyRootMotion)
            {
                hasProblem = true;
                sb.AppendLine("❌ Apply Root Motion = ON → 動畫位移會跟腳本 MoveTowards 打架(原地踏步/抖動)。" +
                              "選模型子物件,在 Animator 元件把勾拿掉");
            }
            else
            {
                sb.AppendLine("✓ Apply Root Motion = OFF");
            }

            // FBX 直嵌的模型這欄常是 None;開場的 idle 已改由 CustomerController.Awake 掛上,
            // 這裡只是讓你看到各角色的原始狀態差異(Soldier 有指定、其他角色沒有)。
            sb.AppendLine($"Animator 預設 controller:{(animator.runtimeAnimatorController != null ? animator.runtimeAnimatorController.name : "None(由程式開場掛 idle)")}");

            // 3+4. 三個控制器
            var cc = go.GetComponent<CustomerController>();
            if (cc == null)
            {
                sb.AppendLine("❌ 根物件沒有 CustomerController");
                Debug.LogError(sb.ToString());
                return;
            }
            // 單一 controller:優先看 CustomerController.animatorController,留空則看 Animator 元件上掛的。
            var rac = cc.animatorController != null ? cc.animatorController : animator.runtimeAnimatorController;
            if (rac == null)
            {
                hasProblem = true;
                sb.AppendLine("❌ CustomerController.animatorController 沒接、Animator 也沒掛控制器 → 完全不會播動畫");
            }
            else
            {
                hasProblem |= CheckController(sb, "controller", rac, animator);
                hasProblem |= CheckStates(sb, rac);
            }

            if (hasProblem) Debug.LogWarning(sb.ToString());
            else Debug.Log(sb.AppendLine("✓ 全部通過").ToString());
        }

        // Generic rig 的 clip 用「transform 路徑」綁骨頭;路徑對不上模型,動畫播了骨頭也不會動。
        // 回傳 true = 有問題。
        static bool CheckController(StringBuilder sb, string label,
            RuntimeAnimatorController rac, Animator animator)
        {
            if (rac == null)
            {
                sb.AppendLine($"❌ {label} 未指定");
                return true;
            }

            int total = 0, missing = 0;
            string missingSample = null;
            foreach (var clip in rac.animationClips)
            {
                foreach (var b in AnimationUtility.GetCurveBindings(clip))
                {
                    if (string.IsNullOrEmpty(b.path)) continue; // 綁在 Animator 根上的曲線一定找得到
                    total++;
                    if (animator.transform.Find(b.path) == null)
                    {
                        missing++;
                        missingSample ??= b.path;
                    }
                }
            }

            if (rac.animationClips.Length == 0)
            {
                sb.AppendLine($"❌ {label} = {rac.name}:控制器裡沒有任何 clip");
                return true;
            }
            if (missing > 0)
            {
                sb.AppendLine($"❌ {label} = {rac.name}:{missing}/{total} 條骨架路徑對不上模型" +
                              $"(例:{missingSample})→ 這個動畫播了骨頭也不會動(站著滑行)");
                return true;
            }
            sb.AppendLine($"✓ {label} = {rac.name}(骨架路徑 {total} 條全對上)");
            return false;
        }

        // 單一 controller 必須有 Idle/Walk/Throw 三個 state 與 Walking(bool)/Throw(trigger) 兩個參數,
        // CustomerController 才切得動。回傳 true = 有問題。
        static bool CheckStates(StringBuilder sb, RuntimeAnimatorController rac)
        {
            var ac = rac as AnimatorController;
            if (ac == null)
            {
                // Override controller 之類:退而求其次,只確認 clip 數量。
                sb.AppendLine($"  (非 AnimatorController,略過 state/參數檢查)");
                return false;
            }

            bool problem = false;
            var paramNames = ac.parameters.Select(p => p.name).ToHashSet();
            foreach (var need in new[] { "Walking", "Throw" })
                if (!paramNames.Contains(need))
                {
                    problem = true;
                    sb.AppendLine($"❌ 缺參數 '{need}' → CustomerController 切不動{(need == "Walking" ? "走路" : "丟回")}動畫");
                }

            var stateNames = ac.layers.Length > 0
                ? ac.layers[0].stateMachine.states.Select(s => s.state.name).ToHashSet()
                : new System.Collections.Generic.HashSet<string>();
            foreach (var need in new[] { "Idle", "Walk", "Throw" })
                if (!stateNames.Contains(need))
                {
                    problem = true;
                    sb.AppendLine($"❌ 缺 state '{need}'(現有:{string.Join(", ", stateNames)})");
                }
            if (!problem) sb.AppendLine("✓ Idle/Walk/Throw 三 state 與 Walking/Throw 參數齊全");
            return problem;
        }

        static string GetPath(Transform root, Transform t)
        {
            if (t == root) return t.name;
            var path = t.name;
            for (var p = t.parent; p != null && p != root; p = p.parent)
                path = p.name + "/" + path;
            return root.name + "/" + path;
        }
    }
}
