using UnityEngine;

namespace UnityEditor;

public class ModelImporter : AssetImporter
{
  public ModelImporterMeshCompression meshCompression { get; set; }
  public bool optimizeMesh { get; set; } = true;
  public bool importBlendShapes { get; set; } = true;
  public bool importVisibility { get; set; } = true;
  public bool importCameras { get; set; } = true;
  public bool importLights { get; set; } = true;
  public bool importAnimation { get; set; } = true;
  public bool importAnimations { get; set; } = true;
  public ModelImporterAnimationType animationType { get; set; } = ModelImporterAnimationType.Generic;
  public bool isReadable { get; set; }
  public float globalScale { get; set; } = 1f;
  public bool useFileScale { get; set; } = true;
  public float fileScale { get; set; } = 1f;
  public ModelImporterNormals importNormals { get; set; } = ModelImporterNormals.Import;
  public ModelImporterTangents importTangents { get; set; } = ModelImporterTangents.CalculateMikk;
  public ModelImporterMaterialSearch materialSearch { get; set; } = ModelImporterMaterialSearch.Local;
  public ModelImporterMaterialName materialName { get; set; } = ModelImporterMaterialName.BasedOnMaterialName;
  public bool swapUVChannels { get; set; }
  public bool generateSecondaryUV { get; set; }
  public float secondaryUVAngleDistortion { get; set; } = 8f;
  public float secondaryUVAreaDistortion { get; set; } = 15.00001f;
  public float secondaryUVHardAngle { get; set; } = 88f;
  public float secondaryUVPackMargin { get; set; } = 4f;
  public bool addCollider { get; set; }

  public static new ModelImporter GetAtPath(string path)
  {
    return new ModelImporter { assetPath = path };
  }
}

public enum ModelImporterMeshCompression
{
  Off,
  Low,
  Medium,
  High
}

public enum ModelImporterAnimationType
{
  None,
  Legacy,
  Generic,
  Human
}

public enum ModelImporterNormals
{
  Import,
  Calculate,
  None
}

public enum ModelImporterTangents
{
  Import,
  CalculateLegacy,
  CalculateMikk,
  None
}

public enum ModelImporterMaterialSearch
{
  Local,
  RecursiveUp,
  Everywhere
}

public enum ModelImporterMaterialName
{
  BasedOnMaterialName,
  BasedOnTextureName,
  BasedOnModelNameAndMaterialName,
  BasedOnTextureNameOrModelNameAndMaterialName
}
