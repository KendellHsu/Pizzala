// ─────────────────────────────────────────────────────────────
// SnapshotCamera.cs — 遊戲內截圖(髒臉照 / 環境髒亂總覽)
// 掛載:場景中一個獨立的 Camera 物件(不要放在 XR Origin 底下)。
// 設定:該 Camera 元件請「取消勾選」(disabled)——
//   平常不渲染省效能,截圖時由程式手動 Render 一次。
//   Audio Listener 記得移除(場景只能有一個)。
// 照片存到 persistentDataPath/photos/,路徑會寫回數據記錄。
// ─────────────────────────────────────────────────────────────
using System;
using System.IO;
using UnityEngine;

namespace Pizzala.Photo
{
    [RequireComponent(typeof(Camera))]
    public class SnapshotCamera : MonoBehaviour
    {
        public int resolution = 512;

        Camera cam;

        void Awake()
        {
            cam = GetComponent<Camera>();
            cam.enabled = false;
        }

        // 對準某個目標拍(髒臉照:target 給客人的 faceAnchor)
        public string CaptureAt(Transform target, float distance = 0.6f, string prefix = "face")
        {
            if (target == null) return "";
            transform.position = target.position + target.forward * distance;
            transform.LookAt(target.position);
            return Capture(prefix);
        }

        // 從指定機位拍(環境總覽:point 給俯瞰餐廳的空物件)
        public string CaptureFrom(Transform point, string prefix = "env")
        {
            if (point == null) return "";
            transform.SetPositionAndRotation(point.position, point.rotation);
            return Capture(prefix);
        }

        string Capture(string prefix)
        {
            var rt = RenderTexture.GetTemporary(resolution, resolution, 24);
            cam.targetTexture = rt;
            cam.Render();

            RenderTexture.active = rt;
            var tex = new Texture2D(resolution, resolution, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, resolution, resolution), 0, 0);
            tex.Apply();

            cam.targetTexture = null;
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt);

            byte[] png = tex.EncodeToPNG();
            Destroy(tex);

            string dir = Path.Combine(Application.persistentDataPath, "photos");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, $"{prefix}_{DateTime.Now:HHmmss_fff}.png");
            File.WriteAllBytes(path, png);
            return path;
        }
    }
}
