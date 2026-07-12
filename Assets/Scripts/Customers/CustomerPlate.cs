using UnityEngine;
using PizzaVR.Gameplay;

namespace PizzaVR.Customers
{
    // Sits on the "Plate" child (a trigger collider) of the Customer prefab.
    public class CustomerPlate : MonoBehaviour
    {
        public Customer customer;

        void OnTriggerEnter(Collider other)
        {
            var pizza = other.GetComponent<PizzaProjectile>();
            if (pizza == null || !pizza.Thrown)
                return;

            customer.ReceiveDelivery(pizza.Flavor, hitPlate: true);
            Destroy(pizza.gameObject);
        }
    }
}
