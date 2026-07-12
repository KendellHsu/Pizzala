using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PizzaVR.Core;

namespace PizzaVR.Customers
{
    public class CustomerSpawner : MonoBehaviour
    {
        public GameObject customerPrefab;
        public GameObject pizzaPrefab; // used for a customer's wrong-flavor throw-back
        public GameBalanceConfig config;

        readonly HashSet<(int sector, int distanceIndex)> occupiedSlots = new HashSet<(int, int)>();

        void Start()
        {
            for (int i = 0; i < config.initialCustomerCount; i++)
                SpawnAtRandomFreeSlot();

            StartCoroutine(SpawnLoop());
        }

        IEnumerator SpawnLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(Random.Range(config.minSpawnInterval, config.maxSpawnInterval));
                SpawnAtRandomFreeSlot();
            }
        }

        void SpawnAtRandomFreeSlot()
        {
            var freeSlots = new List<(int sector, int distanceIndex)>();
            for (int s = 0; s < config.sectorCount; s++)
                for (int d = 0; d < config.distanceTiers.Length; d++)
                    if (!occupiedSlots.Contains((s, d)))
                        freeSlots.Add((s, d));

            if (freeSlots.Count == 0)
                return;

            var slot = freeSlots[Random.Range(0, freeSlots.Count)];
            SpawnCustomer(slot.sector, slot.distanceIndex);
        }

        void SpawnCustomer(int sector, int distanceIndex)
        {
            occupiedSlots.Add((sector, distanceIndex));

            ComputeSpawnPose(sector, distanceIndex, out var position, out var direction);

            var go = Instantiate(customerPrefab, position, Quaternion.LookRotation(-direction), transform);
            var flavor = (PizzaFlavor)Random.Range(0, System.Enum.GetValues(typeof(PizzaFlavor)).Length);
            go.GetComponent<Customer>().Init(this, sector, distanceIndex, flavor);
        }

        // Scatters the spawn point within the sector's angular slice and the distance
        // tier's radius band, instead of a fixed grid point. Exposed (rather than private)
        // so the placement math can be sanity-checked without entering Play Mode.
        public void ComputeSpawnPose(int sector, int distanceIndex, out Vector3 position, out Vector3 direction)
        {
            float sectorWidth = 360f / config.sectorCount;
            float angle = sector * sectorWidth + Random.Range(-sectorWidth * config.sectorJitter, sectorWidth * config.sectorJitter);
            var tier = config.distanceTiers[distanceIndex];
            float radius = Random.Range(tier.minRadius, tier.maxRadius);

            direction = Quaternion.Euler(0, angle, 0) * Vector3.forward;
            position = direction * radius;
        }

        public void ReleaseSlot(int sector, int distanceIndex)
        {
            occupiedSlots.Remove((sector, distanceIndex));
        }
    }
}
