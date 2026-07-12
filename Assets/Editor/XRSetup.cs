using UnityEditor;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.XR.Management;

public static class XRSetup
{
    [MenuItem("Tools/Pizza VR/Setup XR Loaders (Standalone + Android)")]
    public static void SetupXRLoaders()
    {
        EnableOculusLoader(BuildTargetGroup.Standalone);
        EnableOculusLoader(BuildTargetGroup.Android);
        AssetDatabase.SaveAssets();
        Debug.Log("XRSetup: Oculus loader enabled for Standalone + Android.");
    }

    static void EnableOculusLoader(BuildTargetGroup targetGroup)
    {
        XRGeneralSettingsPerBuildTarget buildTargetSettings;
        EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey, out buildTargetSettings);
        if (buildTargetSettings == null)
        {
            buildTargetSettings = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
            if (!AssetDatabase.IsValidFolder("Assets/XR"))
                AssetDatabase.CreateFolder("Assets", "XR");
            AssetDatabase.CreateAsset(buildTargetSettings, "Assets/XR/XRGeneralSettings.asset");
            EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, buildTargetSettings, true);
        }

        var settings = buildTargetSettings.SettingsForBuildTarget(targetGroup);
        if (settings == null)
        {
            settings = ScriptableObject.CreateInstance<XRGeneralSettings>();
            settings.name = targetGroup.ToString() + " Settings";
            buildTargetSettings.SetSettingsForBuildTarget(targetGroup, settings);
            AssetDatabase.AddObjectToAsset(settings, buildTargetSettings);
        }

        if (settings.Manager == null)
        {
            var managerSettings = ScriptableObject.CreateInstance<XRManagerSettings>();
            managerSettings.name = targetGroup.ToString() + " Providers";
            AssetDatabase.AddObjectToAsset(managerSettings, settings);
            settings.Manager = managerSettings;
        }

        XRPackageMetadataStore.AssignLoader(settings.Manager, "Unity.XR.Oculus.OculusLoader", targetGroup);
        EditorUtility.SetDirty(buildTargetSettings);
    }
}
