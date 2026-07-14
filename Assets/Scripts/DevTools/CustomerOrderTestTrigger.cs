// Dev-only trigger for testing PZ_Customer's flavor icon / waiting-timeout behavior before
// GameManager exists to give orders for real (GiveOrder/PatienceCountdown/timeout are all
// self-contained on CustomerController, so this part doesn't need GameManager to test). Press O
// in Play Mode to give the customer a random flavor order - watch the icon above its head match
// the flavor, and if you wait past patienceSeconds without resolving it, State goes Angry.
using UnityEngine;
using UnityEngine.InputSystem;
using Pizzala.Customers;
using Pizzala.Data;

namespace Pizzala.DevTools
{
    public class CustomerOrderTestTrigger : MonoBehaviour
    {
        public CustomerController customer;
        public float patienceSeconds = 8f;

        void Update()
        {
            if (Keyboard.current != null && Keyboard.current.oKey.wasPressedThisFrame)
                GiveOrder();
        }

        void GiveOrder()
        {
            if (customer == null)
            {
                Debug.LogError("CustomerOrderTestTrigger: customer not assigned.");
                return;
            }

            var flavor = (PizzaFlavor)Random.Range(0, System.Enum.GetValues(typeof(PizzaFlavor)).Length);
            customer.GiveOrder(flavor, patienceSeconds);
            Debug.Log($"CustomerOrderTestTrigger: gave order for {flavor}, patience={patienceSeconds}s - watch the icon above the customer's head.");
        }
    }
}
