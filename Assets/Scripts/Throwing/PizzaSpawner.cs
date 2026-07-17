// ─────────────────────────────────────────────────────────────
// PizzaSpawner.cs — 出餐台:披薩被拿走後自動補一顆新的
// 掛載:出餐台上的空物件,位置就是披薩生成點。
// 建議做三個 Spawner,一種口味一個,排在玩家面前的檯面上。
// Inspector:
//   pizzaPrefab — 披薩 Prefab
//   flavor      — 這個出餐口的口味(會覆寫 Prefab 上的設定)
// ─────────────────────────────────────────────────────────────
using UnityEngine;
using Pizzala.Core;
using Pizzala.Data;

namespace Pizzala.Throwing
{
    public class PizzaSpawner : MonoBehaviour
    {
        public GameObject pizzaPrefab;
        public PizzaFlavor flavor;
        public float respawnDelay = 1.5f;

        [Tooltip("披薩離開生成點多遠就準備補貨(公尺)")]
        public float leaveDistance = 0.5f;

        PizzaProjectile current;
        PizzaProjectile departed; // 已離開生成點、補貨倒數中的披薩(放回來可取消補貨)
        float respawnTimer = -1f;

        void Start() => Spawn();

        void Update()
        {
            if (current != null)
            {
                // 只看距離,不看有沒有放手:拿起來又放回原位不算拿走
                if (Vector3.Distance(current.transform.position, transform.position) > leaveDistance)
                {
                    departed = current; // 舊披薩留在世界裡當髒污,不回收
                    current = null;
                    respawnTimer = respawnDelay;
                }
                return;
            }

            // current 被銷毀(客人收走)→ 直接進補貨倒數
            if (respawnTimer < 0f) { respawnTimer = respawnDelay; departed = null; }

            // 倒數期間披薩被放回原位 → 取消補貨,重新採用它
            if (departed != null
                && Vector3.Distance(departed.transform.position, transform.position) <= leaveDistance)
            {
                current = departed;
                departed = null;
                respawnTimer = -1f;
                return;
            }

            respawnTimer -= Time.deltaTime;
            if (respawnTimer <= 0f)
            {
                // 回合結束後不再補貨;已經在場上的披薩(玩家手上或已落地)不受影響,照樣留著
                if (GameManager.Instance != null && !GameManager.Instance.RoundActive) return;
                departed = null;
                Spawn();
            }
        }

        void Spawn()
        {
            respawnTimer = -1f;
            if (pizzaPrefab == null) return;
            var go = Instantiate(pizzaPrefab, transform.position, transform.rotation);
            current = go.GetComponent<PizzaProjectile>();
            if (current != null) current.flavor = flavor;
        }
    }
}
