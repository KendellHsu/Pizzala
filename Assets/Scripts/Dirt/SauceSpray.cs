// ─────────────────────────────────────────────────────────────
// SauceSpray.cs — 披薩「飛行中」沿路甩醬弄髒環境
// 由 PizzaProjectile(出手時)/ ThrowbackProjectile(發射時)
// 動態 AddComponent 並 Activate,不需手動掛載。
// 兩種噴法可獨立調整(Inspector 設 0 = 關):
//   dripRate         — 醬汁垂直滴落,在地面留下飛行路徑的投影軌跡
//   flingRatePerSpin — 隨圓盤自旋從盤緣切線方向甩出,轉越快甩越多
//                      (FrisbeeFlight 讓出手越用力自旋越快,力道差異免費入手)
// 液滴落地走 DirtManager.SpawnSplatMark,純視覺、不計入 DirtCount。
// ─────────────────────────────────────────────────────────────
using UnityEngine;
using Pizzala.Data;

namespace Pizzala.Dirt
{
    [RequireComponent(typeof(Rigidbody))]
    public class SauceSpray : MonoBehaviour
    {
        [Tooltip("垂直滴落:不旋轉時的基礎滴落數/秒(醬汁自然滲落)。0 = 不轉就不滴")]
        public float baseDripRate = 1f;

        [Tooltip("垂直滴落:自旋每 1 rad/s 額外換算的每秒滴落數(轉越快離心力越大、滴越多)。0 = 關")]
        public float dripRatePerSpin = 0.4f;

        [Tooltip("垂直滴落的每秒上限")]
        public float maxDripRate = 12f;

        [Tooltip("切線甩出:自旋每 1 rad/s 換算的每秒甩出數。0 = 關")]
        public float flingRatePerSpin = 0.8f;

        [Tooltip("切線甩出的每秒上限")]
        public float maxFlingRate = 14f;

        [Tooltip("醬料存量:這趟飛行總共能噴幾滴")]
        public int budget = 25;

        [Tooltip("飛行速度低於此值不噴 (m/s)")]
        public float minSpeed = 1.5f;

        [Tooltip("盤半徑 (m),留 <=0 從 BoxCollider 自動推算")]
        public float discRadius = 0f;

        [Tooltip("滴落液滴繼承披薩速度的比例(0 = 垂直落下,痕跡就在正下方)")]
        [Range(0f, 1f)] public float dripInherit = 0.15f;

        [Tooltip("甩出液滴繼承披薩速度的比例")]
        [Range(0f, 1f)] public float flingInherit = 0.4f;

        [Tooltip("滴落液滴落地髒污縮放")]
        public float dripSplatScale = 0.3f;

        [Tooltip("甩出液滴落地髒污縮放")]
        public float flingSplatScale = 0.25f;

        PizzaFlavor? flavor;
        Rigidbody rb;
        bool active;
        int left;
        float dripAcc, flingAcc;

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            if (discRadius <= 0f)
            {
                var box = GetComponent<BoxCollider>();
                discRadius = box != null ? box.size.x * 0.5f : 0.15f;
            }
        }

        public void Activate(PizzaFlavor? flavor)
        {
            this.flavor = flavor;
            active = true;
            left = budget;
            dripAcc = flingAcc = 0f;
        }

        public void Deactivate() => active = false;

        void FixedUpdate()
        {
            if (!active || left <= 0 || DirtManager.Instance == null) return;
            if (rb.isKinematic || rb.linearVelocity.magnitude < minSpeed) return;

            float dt = Time.fixedDeltaTime;
            float spin = Mathf.Abs(Vector3.Dot(rb.angularVelocity, transform.up)); // 盤軸自旋 (rad/s)

            // ── 垂直滴落:地面投影軌跡,滴落量隨轉速(離心力)增加 ──
            dripAcc += Mathf.Min(baseDripRate + spin * dripRatePerSpin, maxDripRate) * dt;
            while (dripAcc >= 1f && left > 0)
            {
                dripAcc -= 1f;
                left--;
                // 從盤底下方一點生成,避免 Raycast 打到披薩自己的 Collider
                DirtManager.Instance.SpawnFlightDroplet(
                    transform.position + Vector3.down * 0.05f,
                    rb.linearVelocity * dripInherit + Vector3.down * 0.5f,
                    flavor, dripSplatScale);
            }

            // ── 切線甩出:轉越快甩越多、甩越遠 ──
            flingAcc += Mathf.Min(spin * flingRatePerSpin, maxFlingRate) * dt;
            while (flingAcc >= 1f && left > 0)
            {
                flingAcc -= 1f;
                left--;

                // 盤緣隨機一點,液滴初速 = 該點的切線速度 + 部分披薩速度
                float ang = Random.Range(0f, Mathf.PI * 2f);
                Vector3 rimDir = (transform.right * Mathf.Cos(ang)
                                  + transform.forward * Mathf.Sin(ang)).normalized;
                Vector3 rimOffset = rimDir * discRadius;
                Vector3 tangential = Vector3.Cross(rb.angularVelocity, rimOffset);

                DirtManager.Instance.SpawnFlightDroplet(
                    transform.position + rimOffset + rimDir * 0.02f, // 微微推出盤外防自撞
                    tangential + rb.linearVelocity * flingInherit,
                    flavor, flingSplatScale);
            }
        }
    }
}
