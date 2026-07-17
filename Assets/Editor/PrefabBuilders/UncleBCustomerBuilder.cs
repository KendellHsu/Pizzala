using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Pizzala.Customers;

namespace Pizzala.EditorTools
{
    // 從 PZ_Customer(Soldier 版)產生一個乾淨、可直接生成的 UncleB 客人 Prefab,
    // 存成 PZ_Customer 的 **Prefab Variant**(而不是斷線的獨立 prefab),
    // 之後 PZ_Customer 改共用邏輯(Zone/ThrowOrigin/FaceAnchor/FlavorIcon/
    // CustomerController 的程式行為)UncleB 會自動跟上,只有這裡動過的欄位保留覆寫。
    // 隊友的 PZ_Customer_uncleB_* 是預覽品質(模型/命名錯位、疊了兩個 Animator、
    // CustomerController 的 idle/walk 控制器沒接對),不適合直接丟給生成器。
    // 一鍵完成:
    //   1. Instantiate PZ_Customer(建立與基底連結的 prefab instance),
    //      把 Soldier 模型子物件換成 UncleB_standing 模型
    //   2. UncleB 模型掛「單一」Animator,idle 控制器 = uncleB_standing
    //   3. CustomerController.idle/walkAnimatorController = uncleB standing / walking
    //      (與 Soldier 相同機制:走動時切 walk 控制器,clip 靠骨架路徑對應)
    //   4. 清空 face 表情貼圖:UncleB 無「整身表情變體」貼圖,清空後 SetFace(null)
    //      會直接略過,模型保留自身貼圖(情緒仍靠丟回預警的閃紅表現)
    //   5. SaveAsPrefabAssetAndConnect 存成 Assets/Prefabs/PZ_Customer_UncleB.prefab
    //      (存到跟基底不同的路徑,Unity 會自動建立 Variant 關係)
    //   6. 把目前開啟場景 CustomerSpawner 的 customerPrefabs 設為
    //      [PZ_Customer, PZ_Customer_UncleB](兩角色混合隨機生成)
    // 可重複執行:prefab 原地覆蓋、場景欄位原地更新。
    //
    // 注意(需在 Unity 裡人工驗收):UncleB 與 Soldier 的體型/骨架比例可能不同,
    // 產生後請確認 (a) 站姿與地面貼合、(b) BodyZone/FaceZone/HandZone 碰撞框範圍、
    // (c) ThrowOrigin/FaceAnchor 高度是否需要微調。
    public static class UncleBCustomerBuilder
    {
        const string BasePrefabPath     = "Assets/Prefabs/PZ_Customer.prefab";
        const string OutPrefabPath      = "Assets/Prefabs/PZ_Customer_UncleB.prefab";
        const string ModelFbxPath       = "Assets/Art/NPC/UncleB/UncleB_standing.fbx";
        const string IdleControllerPath = "Assets/Art/NPC/UncleB/Animator Controller_uncleB_standing.controller";
        const string WalkControllerPath = "Assets/Art/NPC/UncleB/Animator Controller_uncleB_walking.controller";

        [MenuItem("Tools/Pizzala/Build UncleB Customer Prefab")]
        public static void Build()
        {
            var basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BasePrefabPath);
            var modelFbx = AssetDatabase.LoadAssetAtPath<GameObject>(ModelFbxPath);
            var idleCtrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(IdleControllerPath);
            var walkCtrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(WalkControllerPath);
            if (basePrefab == null)
            { Debug.LogError($"UncleBCustomerBuilder: 找不到基底 {BasePrefabPath}"); return; }
            if (modelFbx == null)
            { Debug.LogError($"UncleBCustomerBuilder: 找不到模型 {ModelFbxPath}"); return; }
            if (idleCtrl == null || walkCtrl == null)
            { Debug.LogError("UncleBCustomerBuilder: 找不到 UncleB idle/walk 動畫控制器,請確認 NPC 分支已合入。"); return; }

            // 用獨立的 preview scene instantiate 基底 prefab —— 這裡的 root 是與
            // PZ_Customer「連線」的 prefab instance,存檔時才會被存成 Variant。
            // (LoadPrefabContents 產生的是斷線副本,SaveAsPrefabAsset 存出來只會是
            // 獨立 prefab,不會建立 Variant 關係。)
            var previewScene = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab, previewScene);

                // Soldier 模型 = 帶 Animator 且不在根物件上的那個子物件
                var soldierAnim = root.GetComponentsInChildren<Animator>(true)
                                      .FirstOrDefault(a => a.gameObject != root);
                if (soldierAnim == null)
                { Debug.LogError("UncleBCustomerBuilder: 基底找不到模型 Animator,結構可能已改變。"); return; }

                var cc = root.GetComponent<CustomerController>();
                if (cc == null)
                { Debug.LogError("UncleBCustomerBuilder: 基底根物件沒有 CustomerController。"); return; }

                // 記下 Soldier 模型的擺放,換上 UncleB 後沿用同位置/旋轉/層級
                var st = soldierAnim.transform;
                var parent = st.parent;
                Vector3 lp = st.localPosition;
                Quaternion lr = st.localRotation;
                int sib = st.GetSiblingIndex();

                var model = (GameObject)PrefabUtility.InstantiatePrefab(modelFbx, parent);
                model.name = "UncleB_Model";
                model.transform.localPosition = lp;
                model.transform.localRotation = lr;
                // localScale 保留 fbx 匯入尺度(UncleB 與 Soldier 體型不同,不套用 Soldier 的縮放)
                model.transform.SetSiblingIndex(sib);

                var anim = model.GetComponent<Animator>();
                if (anim == null) anim = model.AddComponent<Animator>();
                anim.runtimeAnimatorController = idleCtrl; // 初始 idle,走動時 CustomerController 會切 walk
                anim.applyRootMotion = false;

                Object.DestroyImmediate(soldierAnim.gameObject); // 移除舊 Soldier 模型

                // 接 CustomerController:idle/walk 控制器 + 清空表情貼圖
                cc.idleAnimatorController = idleCtrl;
                cc.walkAnimatorController = walkCtrl;
                cc.faceNormal = cc.faceHappy = cc.faceAngry = cc.faceDirty = null;

                PrefabUtility.SaveAsPrefabAssetAndConnect(root, OutPrefabPath, InteractionMode.AutomatedAction);
                Debug.Log($"UncleBCustomerBuilder: 已產生 {OutPrefabPath}(PZ_Customer 的 Prefab Variant)");
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(previewScene);
            }

            AssetDatabase.SaveAssets();
            WireSpawner();
        }

        // 把兩個客人 prefab 設進目前場景的 CustomerSpawner(混合隨機生成)
        static void WireSpawner()
        {
            var spawner = Object.FindFirstObjectByType<CustomerSpawner>(FindObjectsInactive.Include);
            if (spawner == null)
            {
                Debug.LogWarning("UncleBCustomerBuilder: 目前場景沒有 CustomerSpawner(請開 BackBone.unity 再跑一次),"
                               + "請手動把 PZ_Customer 與 PZ_Customer_UncleB 兩個 prefab 加進 customerPrefabs。");
                return;
            }

            var soldier = AssetDatabase.LoadAssetAtPath<GameObject>(BasePrefabPath);
            var uncleB  = AssetDatabase.LoadAssetAtPath<GameObject>(OutPrefabPath);
            Undo.RecordObject(spawner, "Wire UncleB into CustomerSpawner");
            spawner.customerPrefabs = new[] { soldier, uncleB };
            EditorUtility.SetDirty(spawner);
            EditorSceneManager.MarkSceneDirty(spawner.gameObject.scene);
            Debug.Log("UncleBCustomerBuilder: 已把 CustomerSpawner.customerPrefabs 設為 "
                    + "[PZ_Customer, PZ_Customer_UncleB](記得存場景 Ctrl+S)。");
        }
    }
}
