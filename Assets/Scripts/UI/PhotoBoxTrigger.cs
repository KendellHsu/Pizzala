// ─────────────────────────────────────────────────────────────
// PhotoBoxTrigger.cs — fires a UnityEvent when the object it's on gets selected (trigger
// ray click, not a grab). Attach to: any prop with a Collider + XR Simple Interactable
// (e.g. a pizza box) that should reveal a set of recorded photos when pointed at and clicked.
//
// Deliberately generic (UnityEvent, no reference to any specific results/photo script) -
// this is step one of the planned memorial-hall feature (see chat), where each past
// player's box will eventually load that specific session's photos. For now, wire
// onActivated to DemoResultsLoader.ShowRecordedPhotos() to prove out the interaction with
// the one sample session that loader already uses for keys 1/2/3.
// ─────────────────────────────────────────────────────────────
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Pizzala.UI
{
    [RequireComponent(typeof(XRSimpleInteractable))]
    public class PhotoBoxTrigger : MonoBehaviour
    {
        public UnityEvent onActivated;

        XRSimpleInteractable interactable;

        void Awake() => interactable = GetComponent<XRSimpleInteractable>();
        void OnEnable() => interactable.selectEntered.AddListener(OnSelected);
        void OnDisable() => interactable.selectEntered.RemoveListener(OnSelected);

        void OnSelected(SelectEnterEventArgs args) => onActivated?.Invoke();
    }
}
