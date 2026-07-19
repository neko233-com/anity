using System;

namespace UnityEditor;

public class AssetImporter : UnityEngine.AssetImporter
{
  public string assetBundleName
  {
    get => AssetDatabase.GetAssetBundleName(assetPath);
    set => AssetDatabase.SetAssetBundleNameAndVariant(assetPath, value, assetBundleVariant);
  }

  public string assetBundleVariant
  {
    get => AssetDatabase.GetAssetBundleVariant(assetPath);
    set => AssetDatabase.SetAssetBundleNameAndVariant(assetPath, assetBundleName, value);
  }

  public static AssetImporter GetAtPath(string assetPath)
  {
    return AssetDatabase.GetImporterAtPath(assetPath) ?? new AssetImporter { assetPath = assetPath, importSettingsMissing = true };
  }

  public static void SaveAndReimport(string assetPath)
  {
    _ = assetPath;
  }

  public string[]? importedObjectType { get; set; }
  public long? assetTimeStamp { get; private set; } = DateTime.UtcNow.Ticks;
  public int? userData { get; set; }
  public bool preserveExistingAssetSettings { get; set; }
}
