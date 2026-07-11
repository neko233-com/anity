using System;

namespace UnityEditor;

public class AssetImporter : UnityEngine.AssetImporter
{
  public static AssetImporter GetAtPath(string assetPath)
  {
    return new AssetImporter { assetPath = assetPath };
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
