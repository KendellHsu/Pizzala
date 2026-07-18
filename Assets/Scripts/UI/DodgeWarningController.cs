using System.Collections.Generic;
using Pizzala.Customers;
using UnityEngine;

namespace Pizzala.UI
{
    /// <summary>
    /// Shows the existing LeftWarning / RightWarning objects while customers outside
    /// the player's forward view are preparing to throw a pizza back.
    /// Attach this component to DodgeWarningCanvas under Main Camera.
    /// </summary>
    public class DodgeWarningController : MonoBehaviour
    {
        [Header("Scene references (auto-filled when left empty)")]
        public Transform playerHead;
        public GameObject leftWarning;
        public GameObject rightWarning;

        [Header("Direction")]
        [Range(0f, 90f)]
        public float noWarningHalfAngle = 45f;
        public bool debugLogs;

        readonly Dictionary<CustomerController, int> threats = new Dictionary<CustomerController, int>();
        readonly Dictionary<CustomerController, int> lastSide = new Dictionary<CustomerController, int>();

        public bool HasThreats => threats.Count > 0;

        void Awake()
        {
            AutoFindReferences();
            SetWarnings(false, false);
        }

        void OnDisable()
        {
            threats.Clear();
            lastSide.Clear();
            SetWarnings(false, false);
        }

        void LateUpdate()
        {
            RemoveDestroyedCustomers();
            if (playerHead == null || threats.Count == 0)
            {
                SetWarnings(false, false);
                return;
            }

            Vector3 forward = Vector3.ProjectOnPlane(playerHead.forward, Vector3.up).normalized;
            Vector3 right = Vector3.ProjectOnPlane(playerHead.right, Vector3.up).normalized;
            if (forward.sqrMagnitude < 0.0001f || right.sqrMagnitude < 0.0001f)
            {
                SetWarnings(false, false);
                return;
            }

            bool showLeft = false;
            bool showRight = false;
            foreach (var entry in threats)
            {
                CustomerController customer = entry.Key;
                Vector3 toCustomer = Vector3.ProjectOnPlane(
                    customer.transform.position - playerHead.position, Vector3.up);
                if (toCustomer.sqrMagnitude < 0.0001f) continue;
                toCustomer.Normalize();

                if (Vector3.Angle(forward, toCustomer) <= noWarningHalfAngle)
                    continue;

                float lateral = Vector3.Dot(toCustomer, right);
                int side;
                if (Mathf.Abs(lateral) > 0.05f)
                {
                    side = lateral < 0f ? -1 : 1;
                    lastSide[customer] = side;
                }
                else if (!lastSide.TryGetValue(customer, out side))
                {
                    float signedAngle = Vector3.SignedAngle(forward, toCustomer, Vector3.up);
                    side = signedAngle < 0f ? -1 : 1;
                    lastSide[customer] = side;
                }

                if (side < 0) showLeft = true;
                else showRight = true;
            }

            SetWarnings(showLeft, showRight);
        }

        public void BeginThreat(CustomerController customer)
        {
            if (ReferenceEquals(customer, null)) return;
            threats.TryGetValue(customer, out int count);
            threats[customer] = count + 1;
            if (debugLogs)
                Debug.Log($"[DodgeWarning] Begin: {customer.name}, active={threats.Count}");
        }

        public void EndThreat(CustomerController customer)
        {
            if (!ReferenceEquals(customer, null) && threats.TryGetValue(customer, out int count))
            {
                if (count > 1) threats[customer] = count - 1;
                else
                {
                    threats.Remove(customer);
                    lastSide.Remove(customer);
                }
            }

            RemoveDestroyedCustomers();
            if (threats.Count == 0) SetWarnings(false, false);
            if (debugLogs)
                Debug.Log($"[DodgeWarning] End: active={threats.Count}");
        }

        public void ClearAllThreats()
        {
            threats.Clear();
            lastSide.Clear();
            SetWarnings(false, false);
        }

        void AutoFindReferences()
        {
            if (playerHead == null)
            {
                Camera parentCamera = GetComponentInParent<Camera>();
                playerHead = parentCamera != null ? parentCamera.transform : Camera.main?.transform;
            }

            if (leftWarning == null)
            {
                Transform child = transform.Find("LeftWarning");
                if (child != null) leftWarning = child.gameObject;
            }

            if (rightWarning == null)
            {
                Transform child = transform.Find("RightWarning");
                if (child != null) rightWarning = child.gameObject;
            }
        }

        void SetWarnings(bool showLeft, bool showRight)
        {
            if (leftWarning != null && leftWarning.activeSelf != showLeft)
                leftWarning.SetActive(showLeft);
            if (rightWarning != null && rightWarning.activeSelf != showRight)
                rightWarning.SetActive(showRight);
        }

        void RemoveDestroyedCustomers()
        {
            List<CustomerController> destroyed = null;
            foreach (var entry in threats)
            {
                if (entry.Key != null) continue;
                destroyed ??= new List<CustomerController>();
                destroyed.Add(entry.Key);
            }

            if (destroyed == null) return;
            foreach (CustomerController customer in destroyed)
            {
                threats.Remove(customer);
                lastSide.Remove(customer);
            }
        }
    }
}
