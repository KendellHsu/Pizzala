using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Pizzala.EditorTools
{
    /// <summary>
    /// 一次把 splats_object（客人身上 3D 醬料的配料來源）底下所有掃描高模的
    /// import 設定壓下來：網格壓縮 + optimize + 不匯入動畫、貼圖上限降到 512 + Quest
    /// 壓縮格式。配料在客人身上很小，這些設定對外觀幾乎無感，但大幅降 Quest 負擔。
    ///
    /// 只動 import 設定，不改任何 prefab 引用；跑完 Unity 會自動 reimport。
    /// </summary>
    public static class OptimizeSauceToppingAssets
    {
        const string Root = "Assets/Art/splats_object";
        const int ToppingTextureMax = 512;

        [MenuItem("Tools/Pizzala/Optimize Sauce Topping Assets")]
        public static void Optimize()
        {
            if (!AssetDatabase.IsValidFolder(Root))
            {
                Debug.LogError($"[OptimizeToppings] 找不到資料夾 {Root}");
                return;
            }

            var meshes = new List<string>();
            var textures = new List<string>();
            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { Root }))
                meshes.Add(AssetDatabase.GUIDToAssetPath(guid));
            foreach (string guid in AssetDatabase.FindAssets("t:Texture", new[] { Root }))
                textures.Add(AssetDatabase.GUIDToAssetPath(guid));

            int meshChanged = 0, texChanged = 0;
            try
            {
                AssetDatabase.StartAssetEditing();

                foreach (string path in meshes)
                {
                    if (ModelImporter.GetAtPath(path) is not ModelImporter mi) continue;
                    bool dirty = false;

                    if (mi.meshCompression != ModelImporterMeshCompression.High)
                    { mi.meshCompression = ModelImporterMeshCompression.High; dirty = true; }
                    if (!mi.optimizeMeshVertices) { mi.optimizeMeshVertices = true; dirty = true; }
                    if (!mi.optimizeMeshPolygons) { mi.optimizeMeshPolygons = true; dirty = true; }
                    // 配料是靜物，不需要動畫/rig 資料
                    if (mi.importAnimation) { mi.importAnimation = false; dirty = true; }
                    if (mi.animationType != ModelImporterAnimationType.None)
                    { mi.animationType = ModelImporterAnimationType.None; dirty = true; }
                    // 客人身上的配料不會被射線探測，關掉 Read/Write 省一份記憶體
                    if (mi.isReadable) { mi.isReadable = false; dirty = true; }

                    if (dirty) { mi.SaveAndReimport(); meshChanged++; }
                }

                foreach (string path in textures)
                {
                    if (AssetImporter.GetAtPath(path) is not TextureImporter ti) continue;
                    bool dirty = false;

                    if (ti.maxTextureSize > ToppingTextureMax)
                    { ti.maxTextureSize = ToppingTextureMax; dirty = true; }
                    if (ti.textureCompression != TextureImporterCompression.Compressed)
                    { ti.textureCompression = TextureImporterCompression.Compressed; dirty = true; }

                    // Quest（Android）ASTC 覆寫
                    var android = ti.GetPlatformTextureSettings("Android");
                    if (!android.overridden
                        || android.maxTextureSize > ToppingTextureMax
                        || android.format != TextureImporterFormat.ASTC_6x6)
                    {
                        android.overridden = true;
                        android.maxTextureSize = ToppingTextureMax;
                        android.format = TextureImporterFormat.ASTC_6x6;
                        ti.SetPlatformTextureSettings(android);
                        dirty = true;
                    }

                    if (dirty) { ti.SaveAndReimport(); texChanged++; }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.Refresh();
            }

            Debug.Log($"[OptimizeToppings] 完成：{meshChanged}/{meshes.Count} 個網格、"
                + $"{texChanged}/{textures.Count} 張貼圖已優化（貼圖上限 {ToppingTextureMax}、"
                + "網格 High 壓縮 + optimize、不匯入動畫）。");
        }
    }
}
