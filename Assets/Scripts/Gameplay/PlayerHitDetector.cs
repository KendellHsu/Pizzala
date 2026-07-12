using UnityEngine;
using PizzaVR.Core;

namespace PizzaVR.Gameplay
{
    // Sits on the player's head (Main Camera) as a trigger collider. Only reacts to a
    // customer's wrong-flavor throw-back (IsReturnThrow) - the player's own held/thrown
    // pizza should never register as a hit against themselves.
    public class PlayerHitDetector : MonoBehaviour
    {
        public GameBalanceConfig config;

        void OnTriggerEnter(Collider other)
        {
            var pizza = other.GetComponent<PizzaProjectile>();
            if (pizza == null || !pizza.Thrown || !pizza.IsReturnThrow)
                return;

            ScoreManager.Instance?.AddScore(config.dodgeFailPenalty);
            Debug.Log("PlayerHitDetector: hit by a returned pizza, didn't dodge in time!");
            Destroy(pizza.gameObject);
        }
    }
}
