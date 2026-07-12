using UnityEngine;
using PizzaVR.Gameplay;

namespace PizzaVR.Customers
{
    // Sits on the "Body" child (a solid collider) of the Customer prefab - the pizza should
    // physically bonk off it if the throw missed the plate (too high/low/wide), matching
    // "披薩有可能會丟到客人臉上" from the design.
    public class CustomerBody : MonoBehaviour
    {
        public Customer customer;

        void OnCollisionEnter(Collision collision)
        {
            var pizza = collision.gameObject.GetComponent<PizzaProjectile>();
            if (pizza == null || !pizza.Thrown)
                return;

            customer.ReceiveDelivery(pizza.Flavor, hitPlate: false);
            Destroy(pizza.gameObject);
        }
    }
}
