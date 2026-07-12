using UnityEngine;

namespace PizzaVR.Core
{
    public class ScoreManager : MonoBehaviour
    {
        public static ScoreManager Instance { get; private set; }
        public int Score { get; private set; }

        void Awake()
        {
            Instance = this;
        }

        public void AddScore(int amount)
        {
            Score += amount;
            Debug.Log($"ScoreManager: {(amount >= 0 ? "+" : "")}{amount} -> total {Score}");
        }
    }
}
