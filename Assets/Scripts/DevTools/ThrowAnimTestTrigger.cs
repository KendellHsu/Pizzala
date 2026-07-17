// Dev-only trigger for tuning the customer throwback timeline (telegraph → PlayThrow →
// throwbackReleaseDelay → launch) without setting up a real miss/timeout first. This goes through
// GameManager.DebugTriggerThrowback, i.e. the *actual* ThrowbackRoutine - same animation and
// release-delay timing you'd see in a real playthrough. Press triggerKey in Play Mode.
using UnityEngine;
using UnityEngine.InputSystem;
using Pizzala.Core;
using Pizzala.Customers;
using Pizzala.Data;

namespace Pizzala.DevTools
{
    public class ThrowAnimTestTrigger : MonoBehaviour
    {
        public CustomerController customer;
        public PizzaFlavor flavor;

        void Update()
        {
            if (Keyboard.current != null && Keyboard.current.tKey.wasPressedThisFrame)
                Trigger();
        }

        void Trigger()
        {
            if (customer == null || GameManager.Instance == null)
            {
                Debug.LogError("ThrowAnimTestTrigger: customer not assigned or GameManager not ready.");
                return;
            }

            GameManager.Instance.DebugTriggerThrowback(customer, flavor);
            Debug.Log($"ThrowAnimTestTrigger: triggered real throwback routine on {customer.name} (flavor={flavor}).");
        }
    }
}
