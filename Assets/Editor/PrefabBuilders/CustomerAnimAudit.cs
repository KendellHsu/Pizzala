using System.Text;
using UnityEditor;
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
    //   3. idle/walk/throw 三個控制器是否有接
    //   4. 每個控制器的 clip 骨架路徑是否對得上 prefab 裡的模型
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
            hasProblem |= CheckController(sb, "idle ", cc.idleAnimatorController, animator,
                required: false); // idle 留空會 fallback 用 Animator 預設 controller
            hasProblem |= CheckController(sb, "walk ", cc.walkAnimatorController, animator,
                required: true);  // walk 沒接 = 走動時不換動畫 = 站著滑行
            hasProblem |= CheckController(sb, "throw", cc.throwAnimatorController, animator,
                required: false);

            if (cc.idleAnimatorController == null && animator.runtimeAnimatorController == null)
            {
                hasProblem = true;
                sb.AppendLine("❌ idle 控制器沒接、Animator 也沒有預設控制器 → 走完路切不回站立動畫");
            }

            if (hasProblem) Debug.LogWarning(sb.ToString());
            else Debug.Log(sb.AppendLine("✓ 全部通過").ToString());
        }

        // Generic rig 的 clip 用「transform 路徑」綁骨頭;路徑對不上模型,動畫播了骨頭也不會動。
        // 回傳 true = 有問題。
        static bool CheckController(StringBuilder sb, string label,
            RuntimeAnimatorController rac, Animator animator, bool required)
        {
            if (rac == null)
            {
                sb.AppendLine(required
                    ? $"❌ {label} 控制器未指定 → 走動時不會切走路動畫(站著滑行)"
                    : $"  {label} 控制器未指定(允許留空)");
                return required;
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
