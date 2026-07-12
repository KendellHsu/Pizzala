using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

// Shared by every prefab builder that needs to wire up XRControllerInput's InputActionReference
// fields, so the lookup logic and asset path only live in one place.
public static class XRActionFinder
{
    public const string InputActionsPath = "Assets/Samples/XR Interaction Toolkit/3.3.2/Starter Assets/XRI Default Input Actions.inputactions";

    public static InputActionReference FindAction(string mapName, string actionName)
    {
        var refs = AssetDatabase.LoadAllAssetsAtPath(InputActionsPath).OfType<InputActionReference>();
        var found = refs.FirstOrDefault(r => r.action != null && r.action.name == actionName && r.action.actionMap.name == mapName);
        if (found == null)
            Debug.LogWarning($"XRActionFinder: could not find action '{mapName}/{actionName}' - run Tools > Pizza VR > Import XRI Samples first.");
        return found;
    }
}
