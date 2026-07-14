// ─────────────────────────────────────────────────────────────
// HandMotionSampler.cs — 持續記錄手部運動軌跡(環形緩衝區)
// 掛載:XR Origin 底下的左右手控制器物件上,各掛一個。
// Inspector:
//   isLeftHand — 左手勾起來,右手不勾
//   head       — 拖入 Main Camera(XR Origin 的相機),留空會自動抓
// ─────────────────────────────────────────────────────────────
using System.Collections.Generic;
using UnityEngine;

namespace Pizzala.Throwing
{
    public struct MotionSample
    {
        public float time;
        public Vector3 worldPos;
        public Quaternion worldRot;
        public Vector3 velocity;            // m/s
        public float angularSpeedDeg;       // deg/s
    }

    public class HandMotionSampler : MonoBehaviour
    {
        public bool isLeftHand;
        public Transform head;

        const int Capacity = 128; // FixedUpdate 50Hz 下約 2.5 秒歷史

        readonly MotionSample[] buffer = new MotionSample[Capacity];
        int count, writeIdx;
        Vector3 lastPos;
        Quaternion lastRot;
        bool hasLast;

        void Start()
        {
            if (head == null && Camera.main != null) head = Camera.main.transform;
        }

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            Vector3 pos = transform.position;
            Quaternion rot = transform.rotation;

            var s = new MotionSample { time = Time.time, worldPos = pos, worldRot = rot };

            if (hasLast && dt > 0f)
            {
                s.velocity = (pos - lastPos) / dt;
                (rot * Quaternion.Inverse(lastRot)).ToAngleAxis(out float angle, out _);
                if (angle > 180f) angle = 360f - angle;
                s.angularSpeedDeg = angle / dt;
            }

            lastPos = pos;
            lastRot = rot;
            hasLast = true;

            buffer[writeIdx] = s;
            writeIdx = (writeIdx + 1) % Capacity;
            if (count < Capacity) count++;
        }

        // 取最近 seconds 秒的樣本,時間由舊到新
        public List<MotionSample> GetRecent(float seconds)
        {
            var result = new List<MotionSample>();
            float cutoff = Time.time - seconds;
            for (int i = 0; i < count; i++)
            {
                int idx = (writeIdx - count + i + Capacity * 2) % Capacity;
                if (buffer[idx].time >= cutoff) result.Add(buffer[idx]);
            }
            return result;
        }
    }
}
