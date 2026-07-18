// ─────────────────────────────────────────────────────────────
// RayLengthSwitcher.cs — flips the controllers' ray between "grab length" and "UI length".
// Attach to: the "Systems" object; GameFlowController drives it.
//
// Why this exists: the NearFarInteractor's far-cast is deliberately kept tiny during play
// (0.15m - see the "Shorten Near-Far Interactor cast distance" commits) so the trigger
// grabs the pizza in front of you instead of yanking one across the room. But menus float
// ~1.5m away, so with a 0.15m ray you physically cannot point at the Start button. This
// stretches the ray while a menu is up and puts it back when play resumes.
//
// The distance lives on CurveInteractionCaster, not on NearFarInteractor itself, and the
// ICurveInteractionCaster interface doesn't expose it - so we reach the concrete caster
// component. Interactors are found at runtime rather than wired in the Inspector because
// the XR rig is a Starter Assets prefab that gets dropped into the scene fresh.
// ─────────────────────────────────────────────────────────────
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Casters;

namespace Pizzala.UI
{
    public class RayLengthSwitcher : MonoBehaviour
    {
        [Tooltip("Ray length while a menu is up, in metres. Must comfortably exceed the panel distance (~1.5m).")]
        public float uiRayDistance = 5f;

        [Tooltip("Ray length during play. Left at 0 the original prefab value is restored instead, which is the safer default.")]
        public float playRayDistance = 0f;

        readonly List<CurveInteractionCaster> casters = new List<CurveInteractionCaster>();
        readonly List<float> originalDistances = new List<float>();
        bool cached;

        void CacheCasters()
        {
            if (cached) return;
            casters.Clear();
            originalDistances.Clear();

            foreach (var interactor in FindObjectsByType<NearFarInteractor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var caster = interactor.farInteractionCaster as CurveInteractionCaster;
                if (caster == null) continue;
                casters.Add(caster);
                originalDistances.Add(caster.castDistance); // remember the tuned value rather than hard-coding it
            }
            cached = true;

            if (casters.Count == 0)
                Debug.LogWarning("[RayLengthSwitcher] No NearFarInteractor found - is the XR Origin prefab in the scene? " +
                                 "Menus will still show, but the ray won't reach them.");
        }

        /// <summary>Stretch the ray so menus ~1.5m away are pointable.</summary>
        public void UseUiRay()
        {
            CacheCasters();
            foreach (var c in casters) if (c != null) c.castDistance = uiRayDistance;
        }

        /// <summary>Back to the short grab ray so the trigger picks up pizza, not the room.</summary>
        public void UsePlayRay()
        {
            CacheCasters();
            // c may be null here: OnDisable() calls this during scene unload, by which point the
            // XR rig's casters can already be destroyed (Unity fake-null). Skip those.
            for (int i = 0; i < casters.Count; i++)
                if (casters[i] != null)
                    casters[i].castDistance = playRayDistance > 0f ? playRayDistance : originalDistances[i];
        }

        // The cast distance is a live value on a scene component, so a run that ends while a
        // menu is up would otherwise leave the ray long for the next Play session.
        void OnDisable()
        {
            if (cached) UsePlayRay();
        }
    }
}
