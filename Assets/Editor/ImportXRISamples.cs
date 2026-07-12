using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager.UI;
using UnityEngine;

public static class ImportXRISamples
{
    [MenuItem("Tools/Pizza VR/Import XRI Samples (Starter Assets + Device Simulator)")]
    public static void ImportSamples()
    {
        var samples = Sample.FindByPackage("com.unity.xr.interaction.toolkit", null).ToList();
        ImportOne(samples, "Starter Assets");
        ImportOne(samples, "XR Device Simulator");
        AssetDatabase.Refresh();
        Debug.Log("ImportXRISamples: done.");
    }

    static void ImportOne(System.Collections.Generic.List<Sample> samples, string name)
    {
        var sample = samples.FirstOrDefault(s => s.displayName == name);
        if (sample.displayName == null)
        {
            Debug.LogError($"ImportXRISamples: sample '{name}' not found.");
            return;
        }
        bool ok = sample.Import(Sample.ImportOptions.OverridePreviousImports);
        Debug.Log($"ImportXRISamples: imported '{name}' = {ok}");
    }
}
