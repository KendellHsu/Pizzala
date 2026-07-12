// ─────────────────────────────────────────────────────────────
// PlayerHeadHitbox.cs — 玩家頭部命中區(閃避判定用)
// 掛載:XR Origin → Camera Offset → Main Camera 底下,
//   加一個子物件掛這支 + Sphere Collider(Is Trigger 打勾,半徑 0.22)
//   + Rigidbody(Is Kinematic 打勾,trigger 偵測需要)。
// 純標記用,沒有邏輯——ThrowbackProjectile 靠找到這個元件判定砸中。
// ─────────────────────────────────────────────────────────────
using UnityEngine;

namespace Pizzala.Core
{
    public class PlayerHeadHitbox : MonoBehaviour { }
}
