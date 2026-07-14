// ─────────────────────────────────────────────────────────────
// ActivityTracker.cs — 身體活動量:頭部移動距離、下蹲次數、轉身角度
// 掛載:"Systems" 物件。
// Inspector:head 拖入 Main Camera(XR Origin 的相機),留空自動抓。
// 回合開始時 GameManager 會呼叫 Begin() 校準站立高度,
// 所以請提醒受試者:遊戲開始時要站直。
// ─────────────────────────────────────────────────────────────
using UnityEngine;

namespace Pizzala.Core
{
    public class ActivityTracker : MonoBehaviour
    {
        public Transform head;

        [Tooltip("頭低於站立高度的這個比例算下蹲")]
        [Range(0.5f, 0.9f)] public float squatRatio = 0.72f;

        [Tooltip("回升到這個比例算站起來(可再計下一次)")]
        [Range(0.7f, 1f)] public float recoverRatio = 0.9f;

        public float TotalHeadDistance { get; private set; }
        public int SquatCount { get; private set; }
        public float TurnDegreesTotal { get; private set; }

        float standingHeight;
        bool inSquat;
        Vector3 lastPos;
        float lastYaw;
        bool tracking;

        void Start()
        {
            if (head == null && Camera.main != null) head = Camera.main.transform;
        }

        public void Begin()
        {
            if (head == null) return;
            standingHeight = head.position.y;
            lastPos = head.position;
            lastYaw = head.eulerAngles.y;
            TotalHeadDistance = 0f;
            SquatCount = 0;
            TurnDegreesTotal = 0f;
            inSquat = false;
            tracking = true;
        }

        public void End() => tracking = false;

        void Update()
        {
            if (!tracking || head == null) return;

            Vector3 pos = head.position;
            float step = Vector3.Distance(pos, lastPos);
            if (step > 0.003f) TotalHeadDistance += step; // 過濾感測抖動
            lastPos = pos;

            float yaw = head.eulerAngles.y;
            TurnDegreesTotal += Mathf.Abs(Mathf.DeltaAngle(lastYaw, yaw));
            lastYaw = yaw;

            if (!inSquat && pos.y < standingHeight * squatRatio)
            {
                inSquat = true;
                SquatCount++;
            }
            else if (inSquat && pos.y > standingHeight * recoverRatio)
            {
                inSquat = false;
            }
        }
    }
}
