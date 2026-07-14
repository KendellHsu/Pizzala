// ─────────────────────────────────────────────────────────────
// SensorListener.cs — 接收 ESP32 經 WiFi UDP 回報的感測數據
// 掛載:"Systems" 物件(和 SessionLogger 同一個即可)。
// Inspector:port 要和 ESP32 程式裡的 PORT 一致(預設 8765)。
// 前提:Quest 和 ESP32 連同一個熱點/路由器。
// 沒有硬體也完全沒關係——收不到包就是靜默待機,不影響遊戲。
// 封包格式:{"hr":92,"gsr":512,"t":123456}
// ─────────────────────────────────────────────────────────────
using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

namespace Pizzala.Data
{
    public class SensorListener : MonoBehaviour
    {
        [Tooltip("要和 ESP32 程式的 PORT 一致")]
        public int port = 8765;

        [Tooltip("寫入數據時間軸的間隔(秒)")]
        public float sampleInterval = 1f;

        public int LatestHr { get; private set; } = -1;
        public int LatestGsr { get; private set; } = -1;
        public bool Connected => Time.time - lastPacketTime < 3f;

        [Serializable]
        class Packet { public int hr = -1; public int gsr = -1; public int t; }

        UdpClient client;
        Thread thread;
        volatile bool running;
        readonly ConcurrentQueue<Packet> queue = new ConcurrentQueue<Packet>();
        float lastPacketTime = -999f;
        float lastSampleTime;

        void Start()
        {
            try
            {
                client = new UdpClient(port);
                running = true;
                thread = new Thread(ReceiveLoop) { IsBackground = true };
                thread.Start();
                Debug.Log($"[SensorListener] 監聽 UDP :{port}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SensorListener] 無法開啟 UDP({e.Message}),感測功能停用。");
            }
        }

        void ReceiveLoop()
        {
            var remote = new IPEndPoint(IPAddress.Any, 0);
            while (running)
            {
                try
                {
                    byte[] data = client.Receive(ref remote);
                    string json = Encoding.UTF8.GetString(data);
                    var p = JsonUtility.FromJson<Packet>(json);
                    if (p != null) queue.Enqueue(p);
                }
                catch { /* socket 關閉或壞封包,忽略 */ }
            }
        }

        void Update()
        {
            while (queue.TryDequeue(out var p))
            {
                if (p.hr > 0) LatestHr = p.hr;
                if (p.gsr >= 0) LatestGsr = p.gsr;
                lastPacketTime = Time.time;
            }

            var logger = SessionLogger.Instance;
            if (logger != null && logger.SessionActive && Connected
                && Time.time - lastSampleTime >= sampleInterval)
            {
                lastSampleTime = Time.time;
                logger.AddSensorSample(LatestHr, LatestGsr);
            }
        }

        void OnDestroy()
        {
            running = false;
            client?.Close();
        }
    }
}
