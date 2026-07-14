// Dev-only trigger for manually testing PZ_ThrowbackPizza_* before the Customer/GameManager
// pipeline exists to launch them for real. Press triggerKey in Play Mode to spawn one and send
// it at targetHead's position *at that instant* (ThrowbackProjectile doesn't track afterwards,
// so stepping aside or ducking after the throw is what makes it miss).
using UnityEngine;
using UnityEngine.InputSystem;
using Pizzala.Customers;

namespace Pizzala.DevTools
{
    public class ThrowbackTestTrigger : MonoBehaviour
    {
        public GameObject throwbackPrefab;
        public Transform targetHead;
        public float speed = 6f;

        void Update()
        {
            // Project uses the Input System package exclusively (UnityEngine.Input is disabled
            // in Player Settings), so read the key through Keyboard.current instead.
            if (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame)
                Throw();
        }

        void Throw()
        {
            if (throwbackPrefab == null || targetHead == null)
            {
                Debug.LogError("ThrowbackTestTrigger: throwbackPrefab or targetHead not assigned.");
                return;
            }

            var instance = Instantiate(throwbackPrefab, transform.position, Quaternion.identity);
            var projectile = instance.GetComponent<ThrowbackProjectile>();
            if (projectile == null)
            {
                Debug.LogError("ThrowbackTestTrigger: spawned prefab has no ThrowbackProjectile.");
                return;
            }

            projectile.onResolved += hitPlayer =>
                Debug.Log(hitPlayer
                    ? "ThrowbackTestTrigger: hit the player - dodge failed."
                    : "ThrowbackTestTrigger: missed - dodge succeeded, splat spawned on whatever it hit.");

            projectile.Launch(targetHead.position, speed);
            Debug.Log($"ThrowbackTestTrigger: launched at {targetHead.position} (speed={speed}).");
        }
    }
}
