// ─────────────────────────────────────────────────────────────
// ThrowbackProjectile.cs — 客人丟回來的披薩
// 掛載:丟回披薩 Prefab 根物件(可以直接複製玩家披薩 Prefab,保留
//   XRGrabInteractable 和 PizzaProjectile,再加掛這支)。
// Prefab 需求:Rigidbody + Collider(不勾 Trigger)。
//   想讓玩家「接住再丟回去」,還要 XRGrabInteractable + PizzaProjectile。
// 關鍵設計:發射時鎖定「當下」的玩家頭部位置,不追蹤,
//   所以玩家側身/下蹲躲得掉;也可以伸手接住。
// 接住(selectEntered)時本元件會停掉自毀倒數並關閉自己,把控制權交給
//   PizzaProjectile —— 之後就跟一般披薩一樣可以再丟給客人計分。
//   (PizzaProjectile 靠自己的 inFlight 旗標,在被丟出前不會誤判落地,
//    所以兩支元件同時掛著不會打架。)
// ─────────────────────────────────────────────────────────────
using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using Pizzala.Core;
using Pizzala.Data;
using Pizzala.Throwing;

namespace Pizzala.Customers
{
    [RequireComponent(typeof(Rigidbody))]
    public class ThrowbackProjectile : MonoBehaviour
    {
        public PizzaFlavor flavor; // 生成時由 GameManager 覆寫,落空髒污用
        public System.Action<bool> onResolved; // true = 砸中玩家臉
        bool resolved;
        float launchTime;
        Coroutine lifeRoutine; // 用協程做自毀倒數(Destroy(obj, t) 取消不掉,接住時要能停)

        const float Lifetime = 5f;

        void Awake()
        {
            // 有 XRGrabInteractable 才可能被接住;沒有就是純投射物,維持舊行為
            var grab = GetComponent<XRGrabInteractable>();
            if (grab != null) grab.selectEntered.AddListener(_ => OnCaught());
        }

        public void Launch(Vector3 targetPos, float speed)
        {
            launchTime = Time.time;
            var rb = GetComponent<Rigidbody>();

            // 接住後要再丟時,PizzaProjectile 得知道這顆是什麼口味才能配對訂單
            var pizza = GetComponent<PizzaProjectile>();
            if (pizza != null) pizza.flavor = flavor;

            // 簡單拋物線補償:依飛行時間加一點向上初速抵銷重力
            Vector3 toTarget = targetPos - transform.position;
            float flightTime = toTarget.magnitude / Mathf.Max(speed, 0.1f);
            Vector3 velocity = toTarget / flightTime;
            velocity.y += 0.5f * Mathf.Abs(Physics.gravity.y) * flightTime;

            rb.linearVelocity = velocity;
            rb.angularVelocity = new Vector3(0f, 10f, 0f); // 轉一下比較像飛盤

            // 飛行中沿路甩醬(垂直滴落 + 盤緣切線甩出)
            var spray = GetComponent<Pizzala.Dirt.SauceSpray>();
            if (spray == null) spray = gameObject.AddComponent<Pizzala.Dirt.SauceSpray>();
            spray.Activate(flavor);

            lifeRoutine = StartCoroutine(LifeRoutine());
        }

        IEnumerator LifeRoutine()
        {
            yield return new WaitForSeconds(Lifetime);
            Destroy(gameObject);
        }

        // 玩家伸手接住:算閃過(沒被砸中),交棒給 PizzaProjectile 當一般披薩用
        void OnCaught()
        {
            if (resolved) return;
            resolved = true; // 之後不再做丟回的命中判定

            if (lifeRoutine != null) { StopCoroutine(lifeRoutine); lifeRoutine = null; } // 取消自毀,接住的披薩要留著

            var spray = GetComponent<Pizzala.Dirt.SauceSpray>();
            if (spray != null) spray.Deactivate();

            onResolved?.Invoke(false); // 接住 = 沒被砸到,等同閃過
            enabled = false;           // 停止本元件的碰撞判定,PizzaProjectile 接手
        }

        void OnCollisionEnter(Collision c) => Resolve(c.collider, c.GetContact(0).point, c.GetContact(0).normal);
        void OnTriggerEnter(Collider other) => Resolve(other, transform.position, Vector3.up);

        void Resolve(Collider col, Vector3 point, Vector3 normal)
        {
            if (resolved || Time.time - launchTime < 0.1f) return;

            bool hitPlayer = col.GetComponentInParent<PlayerHeadHitbox>() != null;
            resolved = true;

            var spray = GetComponent<Pizzala.Dirt.SauceSpray>();
            if (spray != null) spray.Deactivate(); // 落地後換滑行痕跡接手

            if (!hitPlayer && Pizzala.Dirt.DirtManager.Instance != null)
            {
                Pizzala.Dirt.DirtManager.Instance.SpawnSplat(point, normal, flavor); // 閃過→牆上多一塊髒污
                gameObject.AddComponent<Pizzala.Dirt.SauceTrail>().Activate(flavor); // 銷毀前的彈跳也留痕
            }

            onResolved?.Invoke(hitPlayer);
            Destroy(gameObject, hitPlayer ? 0f : 1.5f);
        }
    }
}
