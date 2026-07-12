// ─────────────────────────────────────────────────────────────
// PizzaSpawner.cs — 出餐台:披薩被拿走後自動補一顆新的
// 掛載:出餐台上的空物件,位置就是披薩生成點。
// 建議做三個 Spawner,一種口味一個,排在玩家面前的檯面上。
// Inspector:
//   pizzaPrefab — 披薩 Prefab
//   flavor      — 這個出餐口的口味(會覆寫 Prefab 上的設定)
// ─────────────────────────────────────────────────────────────
using UnityEngine;
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
        float respawnTimer = -1f;

        void Start() => Spawn();

        void Update()
        {
            if (current != null)
            {
                bool left = current.WasThrown
                            || Vector3.Distance(current.transform.position, transform.position) > leaveDistance;
                if (left)
                {
                    current = null; // 舊披薩留在世界裡當髒污,不回收
                    respawnTimer = respawnDelay;
                }
            }
            else if (respawnTimer > 0f)
            {
                respawnTimer -= Time.deltaTime;
                if (respawnTimer <= 0f) Spawn();
            }
        }

        void Spawn()
        {
            if (pizzaPrefab == null) return;
            var go = Instantiate(pizzaPrefab, transform.position, transform.rotation);
            current = go.GetComponent<PizzaProjectile>();
            if (current != null) current.flavor = flavor;
        }
    }
}
