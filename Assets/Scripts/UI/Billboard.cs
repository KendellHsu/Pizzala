// ─────────────────────────────────────────────────────────────
// Billboard.cs — 讓物件永遠面向玩家相機(頭上菜單用)
// 掛載:客人 Prefab 的 FlavorIcon / FlavorIconFrame。
// 客人會轉身(遊走、面向行走方向),掛了這個之後菜單不會跟著轉側面。
// yAxisOnly = 只繞 Y 軸轉(保持直立,不會仰角傾斜),預設開。
// ─────────────────────────────────────────────────────────────
using UnityEngine;

namespace Pizzala.UI
{
    public class Billboard : MonoBehaviour
    {
        public bool yAxisOnly = true;

        Transform cam;

        // LateUpdate:等客人本體的移動/轉向都做完,最後才把菜單轉回來
        void LateUpdate()
        {
            if (cam == null)
            {
                if (Camera.main == null) return;
                cam = Camera.main.transform;
            }

            Vector3 toCam = cam.position - transform.position;
            if (yAxisOnly) toCam.y = 0f;
            if (toCam.sqrMagnitude < 0.0001f) return;

            // 圖示的正面(+Z)朝向相機,跟客人面向玩家時的可讀方向一致
            transform.rotation = Quaternion.LookRotation(toCam);
        }
    }
}
