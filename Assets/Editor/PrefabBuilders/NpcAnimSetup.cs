using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Pizzala.EditorTools
{
    // NPC 動畫一鍵設置(統一 Armature 骨架版):
    // 美術把每個角色的三段動作烘進單一 <name>_animation.fbx,內含三個 take:
    //   Armature|standing(=站立/Idle)、Armature|walking(=走路)、Armature|throwing(=丟回)。
    // 本工具為 Assets/Art/NPC/<角色>/ 底下每個 *_animation.fbx:
    //   1. 設定 Import Settings:把三個 take 切成三個具名 clip
    //      (standing / walking 勾 loop;throwing 不 loop,播一次)。
    //   2. 產生一個 AnimatorController(<name>_animator.controller,存在同資料夾):
    //      state:Idle(standing)/Walk(walking)/Throw(throwing);
    //      參數:Walking(bool)、Throw(trigger);
    //      transition:Idle↔Walk 靠 Walking、Any→Throw 靠 Throw trigger、
    //                  Throw 播完自動回 Idle(hasExitTime)。
    // CustomerController 只認得這一個 controller:走動 SetBool("Walking")、丟回 SetTrigger("Throw")。
    //
    // 可重複執行:import 設定與 controller 皆原地覆蓋;新增角色資料夾後重跑即可。
    public static class NpcAnimSetup
    {
        const string NpcRoot = "Assets/Art/NPC";

        // FBX take 名稱 → 我們要的 clip 名稱與 loop 設定
        struct ClipDef { public string take; public string clip; public bool loop; }
        static readonly ClipDef[] Clips =
        {
            new ClipDef { take = "standing", clip = "standing", loop = true  },
            new ClipDef { take = "walking",  clip = "walking",  loop = true  },
            new ClipDef { take = "throwing", clip = "throwing", loop = false },
        };

        [MenuItem("Tools/Pizzala/Setup NPC Animations (clips + controllers)")]
        public static void SetupAll()
        {
            var fbxGuids = AssetDatabase.FindAssets("t:Model", new[] { NpcRoot });
            var animationFbxPaths = fbxGuids
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => p.EndsWith("_animation.fbx"))
                .Distinct()
                .OrderBy(p => p)
                .ToList();

            if (animationFbxPaths.Count == 0)
            {
                Debug.LogError($"[NpcAnimSetup] {NpcRoot} 底下找不到任何 *_animation.fbx。");
                return;
            }

            int ok = 0;
            foreach (var fbxPath in animationFbxPaths)
            {
                if (SetupOne(fbxPath)) ok++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[NpcAnimSetup] 完成:{ok}/{animationFbxPaths.Count} 個角色設置成功。" +
                      "接著跑 Tools/Pizzala/Build NPC Customer Prefabs 產生/更新客人 prefab。");
        }

        static bool SetupOne(string fbxPath)
        {
            var importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
            if (importer == null)
            {
                Debug.LogError($"[NpcAnimSetup] {fbxPath} 不是 Model,略過。");
                return false;
            }

            // 目前 FBX 的 take → frame 範圍,靠 defaultClipAnimations 讀出(Unity 依 take 自動切好的預設)。
            var defaults = importer.defaultClipAnimations;
            var byTake = new Dictionary<string, ModelImporterClipAnimation>();
            foreach (var d in defaults)
            {
                // take 名可能是 "Armature|standing" 或 "standing";取尾段比對。
                string key = d.takeName.Contains('|') ? d.takeName.Split('|').Last() : d.takeName;
                byTake[key.ToLowerInvariant()] = d;
                // 也用 name 當備援 key
                string nkey = d.name.Contains('|') ? d.name.Split('|').Last() : d.name;
                if (!byTake.ContainsKey(nkey.ToLowerInvariant()))
                    byTake[nkey.ToLowerInvariant()] = d;
            }

            var result = new List<ModelImporterClipAnimation>();
            foreach (var def in Clips)
            {
                if (!byTake.TryGetValue(def.take, out var src))
                {
                    Debug.LogWarning($"[NpcAnimSetup] {Path.GetFileName(fbxPath)}: 找不到 take '{def.take}'," +
                                     $"該角色缺這段動畫(現有 take:{string.Join(", ", byTake.Keys)})。");
                    continue;
                }
                var clip = new ModelImporterClipAnimation
                {
                    name = def.clip,
                    takeName = src.takeName,
                    firstFrame = src.firstFrame,
                    lastFrame = src.lastFrame,
                    loopTime = def.loop,
                    loop = def.loop,
                    wrapMode = def.loop ? WrapMode.Loop : WrapMode.Once,
                };
                result.Add(clip);
            }

            if (result.Count == 0)
            {
                Debug.LogError($"[NpcAnimSetup] {Path.GetFileName(fbxPath)}: 一個 take 都沒對上,不改 import。");
                return false;
            }

            importer.clipAnimations = result.ToArray();
            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();

            // reimport 後,從 FBX 撈出剛切好的 AnimationClip(依名字)去建 controller
            var clipsByName = AssetDatabase.LoadAllAssetsAtPath(fbxPath)
                .OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview__"))
                .GroupBy(c => c.name)
                .ToDictionary(g => g.Key, g => g.First());

            AnimationClip idle  = Find(clipsByName, "standing");
            AnimationClip walk  = Find(clipsByName, "walking");
            AnimationClip throwc = Find(clipsByName, "throwing");

            if (idle == null && walk == null && throwc == null)
            {
                Debug.LogError($"[NpcAnimSetup] {Path.GetFileName(fbxPath)}: reimport 後撈不到切好的 clip。");
                return false;
            }

            BuildController(fbxPath, idle, walk, throwc);
            return true;
        }

        static AnimationClip Find(Dictionary<string, AnimationClip> map, string name)
            => map.TryGetValue(name, out var c) ? c : null;

        // 產生 <角色資料夾>/<name>_animator.controller
        static void BuildController(string fbxPath, AnimationClip idle, AnimationClip walk, AnimationClip throwc)
        {
            string dir = Path.GetDirectoryName(fbxPath).Replace('\\', '/');
            string baseName = Path.GetFileNameWithoutExtension(fbxPath); // e.g. cowboy_animation
            string ctrlPath = $"{dir}/{baseName}_animator.controller";

            // 原地覆蓋:先刪掉舊的,重建乾淨。
            AssetDatabase.DeleteAsset(ctrlPath);
            var ac = AnimatorController.CreateAnimatorControllerAtPath(ctrlPath);

            ac.AddParameter("Walking", AnimatorControllerParameterType.Bool);
            ac.AddParameter("Throw", AnimatorControllerParameterType.Trigger);

            var sm = ac.layers[0].stateMachine;

            var idleState  = sm.AddState("Idle");
            idleState.motion = idle;
            var walkState  = sm.AddState("Walk");
            walkState.motion = walk;
            var throwState = sm.AddState("Throw");
            throwState.motion = throwc;

            sm.defaultState = idleState;

            // Idle → Walk(Walking == true)
            var toWalk = idleState.AddTransition(walkState);
            toWalk.hasExitTime = false;
            toWalk.duration = 0.1f;
            toWalk.AddCondition(AnimatorConditionMode.If, 0, "Walking");

            // Walk → Idle(Walking == false)
            var toIdle = walkState.AddTransition(idleState);
            toIdle.hasExitTime = false;
            toIdle.duration = 0.1f;
            toIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "Walking");

            // Any State → Throw(Throw trigger);排除自己避免重入
            var toThrow = sm.AddAnyStateTransition(throwState);
            toThrow.hasExitTime = false;
            toThrow.duration = 0.05f;
            toThrow.canTransitionToSelf = false;
            toThrow.AddCondition(AnimatorConditionMode.If, 0, "Throw");

            // Throw 播完 → Idle(hasExitTime,播一次就回)
            var throwDone = throwState.AddTransition(idleState);
            throwDone.hasExitTime = true;
            throwDone.exitTime = 0.95f;
            throwDone.duration = 0.1f;

            EditorUtility.SetDirty(ac);
            Debug.Log($"[NpcAnimSetup] 產生 controller:{ctrlPath}" +
                      $"(Idle={(idle ? idle.name : "缺")}, Walk={(walk ? walk.name : "缺")}, Throw={(throwc ? throwc.name : "缺")})");
        }
    }
}
