using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace PizzaVR.XR
{
    // Sets the XR tracking origin to floor level so the head/controller poses from
    // XRControllerInput line up with real-world height on real hardware.
    //
    // The XR Device Simulator has no real headset to report height from, so it reports
    // head/hand poses near (0,0,0) - at floor level - instead of real standing height. Left
    // uncorrected, the camera ends up embedded in the floor/kitchen counter geometry with
    // nothing visible. When no real XR device is active, this lifts the whole rig (camera +
    // both hands move together, since they're all children of rigRoot) by simulatorEyeHeight
    // so the simulator starts at a normal standing eye height instead.
    public class XRBootstrap : MonoBehaviour
    {
        public Transform rigRoot;
        public float simulatorEyeHeight = 1.6f;

        void Start()
        {
            var subsystems = new List<XRInputSubsystem>();
            SubsystemManager.GetSubsystems(subsystems);
            foreach (var subsystem in subsystems)
                subsystem.TrySetTrackingOriginMode(TrackingOriginModeFlags.Floor);

            if (rigRoot != null && !XRSettings.isDeviceActive)
                rigRoot.localPosition += new Vector3(0f, simulatorEyeHeight, 0f);
        }
    }
}
