using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Pizzala.EditorTools
{
    // 骨架命名對不上診斷:把每個走路/丟回控制器的 clip「要求的骨架路徑」
    // 跟模型 FBX「實際有的節點路徑」並排列出,精準看出差在哪。
    // Generic rig 的動畫靠 transform 路徑綁骨頭;路徑對不上,animator 有在跑但骨頭不動。
    //
    // 判斷修法:
    //   * 只差一個共同前綴(例 clip 要 "Armature/Hips" 但模型是 "Hips")→ 差在根節點命名,
    //     通常是匯出時 Armature/rig 名稱不同,重匯或改根節點名即可
    //   * 每個骨頭名字都不一樣 → 兩套骨架根本不同,得請美術用同一套骨架重匯 walking/throwing
    public static class BoneMismatchReport
    {
        // (模型 FBX, 動畫控制器)成對比對
        static readonly (string model, string walkCtrl)[] Targets =
        {
            ("Assets/Art/NPC/UncleB/UncleB_standing.fbx",
             "Assets/Art/NPC/UncleB/Animator Controller_uncleB_walking.controller"),
            ("Assets/Art/NPC/bathrobeDad/bathrobeDad_standing.fbx",
             "Assets/Art/NPC/bathrobeDad/Animator Controller_bathrobeDad_walking.controller"),
            ("Assets/Art/NPC/Soldier/Soldier_standing.fbx",
             "Assets/Art/NPC/Soldier/Animator Controller_soldier_walking.controller"),
        };

        [MenuItem("Tools/Pizzala/Report Bone Mismatch")]
        public static void Report()
        {
            foreach (var (model, ctrl) in Targets)
                ReportOne(model, ctrl);
        }

        static void ReportOne(string modelPath, string ctrlPath)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"===== {System.IO.Path.GetFileName(modelPath)}  vs  {System.IO.Path.GetFileName(ctrlPath)} =====");

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            var ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ctrlPath);
            if (model == null) { sb.AppendLine($"❌ 找不到模型 {modelPath}"); Debug.LogError(sb); return; }
            if (ctrl == null) { sb.AppendLine($"❌ 找不到控制器 {ctrlPath}"); Debug.LogError(sb); return; }

            // 模型實際有的所有 transform 路徑(相對根)
            var modelPaths = new HashSet<string>();
            CollectPaths(model.transform, model.transform, modelPaths);

            // clip 要求的路徑
            var clipPaths = new HashSet<string>();
            foreach (var clip in ctrl.animationClips)
                foreach (var b in AnimationUtility.GetCurveBindings(clip))
                    if (!string.IsNullOrEmpty(b.path))
                        clipPaths.Add(b.path);

            var missing = clipPaths.Where(p => !modelPaths.Contains(p)).OrderBy(p => p).ToList();
            sb.AppendLine($"clip 要求 {clipPaths.Count} 條路徑,模型有 {modelPaths.Count} 個節點," +
                          $"對不上 {missing.Count} 條");

            if (missing.Count == 0)
            {
                sb.AppendLine("✓ 全部對得上,這對不是問題");
                Debug.Log(sb);
                return;
            }

            sb.AppendLine("\n-- clip 要的路徑(對不上的,前 8 條)--");
            foreach (var p in missing.Take(8)) sb.AppendLine($"    {p}");
            sb.AppendLine("\n-- 模型實際有的路徑(前 8 條,拿來比對命名差在哪)--");
            foreach (var p in modelPaths.OrderBy(x => x).Take(8)) sb.AppendLine($"    {p}");

            Debug.LogWarning(sb);
        }

        static void CollectPaths(Transform root, Transform t, HashSet<string> into)
        {
            if (t != root) into.Add(RelPath(root, t));
            foreach (Transform c in t) CollectPaths(root, c, into);
        }

        static string RelPath(Transform root, Transform t)
        {
            var parts = new List<string>();
            for (var p = t; p != null && p != root; p = p.parent) parts.Add(p.name);
            parts.Reverse();
            return string.Join("/", parts);
        }
    }
}
