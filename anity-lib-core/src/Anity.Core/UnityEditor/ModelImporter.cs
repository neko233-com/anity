using UnityEngine;

namespace UnityEditor;

public class ModelImporter : AssetImporter
{
  public ModelImporterMeshCompression meshCompression { get; set; }
  public bool optimizeMesh { get; set; } = true;
  public bool optimizeGameObjects { get; set; }
  public bool importBlendShapes { get; set; } = true;
  public bool importVisibility { get; set; } = true;
  public bool importCameras { get; set; } = true;
  public bool importLights { get; set; } = true;
  public bool importAnimation { get; set; } = true;
  public bool importAnimations { get; set; } = true;
  private ModelImporterMaterialImportMode _materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
  public bool importMaterials => materialImportMode != ModelImporterMaterialImportMode.None;
  public ModelImporterMaterialImportMode materialImportMode { get => _materialImportMode; set => _materialImportMode = value; }
  public ModelImporterMaterialLocation materialLocation { get; set; } = ModelImporterMaterialLocation.External;
  public bool useSRGBMaterialColor { get; set; } = true;
  public ModelImporterAnimationType animationType { get; set; } = ModelImporterAnimationType.Generic;
  private ModelImporterAvatarSetup _avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
  public ModelImporterAvatarSetup avatarSetup { get => _avatarSetup; set => _avatarSetup = value; }
  [Obsolete("Use avatarSetup instead.")]
  public ModelImporterAvatarSetup avatarDefinition { get => avatarSetup; set => avatarSetup = value; }
  public bool autoGenerateAvatarMappingIfUnspecified { get; set; } = true;
  public bool isReadable { get; set; }
  public float globalScale { get; set; } = 1f;
  public bool useFileScale { get; set; } = true;
  public float fileScale { get; set; } = 1f;
  public bool useFileUnits { get; set; } = true;
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
  public ModelImporterAnimationCompression animationCompression { get; set; } = ModelImporterAnimationCompression.KeyframeReductionAndCompression;
  public float animationRotationError { get; set; } = 0.5f;
  public float animationPositionError { get; set; } = 0.5f;
  public float animationScaleError { get; set; } = 0.5f;
  public bool resampleCurves { get; set; } = true;
  public ModelImporterClipAnimation[] clipAnimations { get; set; }
  public bool bakeSimulation { get; set; }
  public bool bakeIK { get; set; }
  public bool removeConstantScaleCurves { get; set; }
  public bool importAnimatedCustomProperties { get; set; }
  public bool importBlendShapeDeformPercent { get; set; } = true;
  public bool bakeAxisConversion { get; set; }
  public bool preserveHierarchy { get; set; }
  public bool strictVertexDataChecks { get; set; }
  public bool importPhysicalCameras { get; set; } = true;
  public HumanDescription humanDescription { get; set; }
  public bool isHuman { get; set; }
  public string sourceAvatar { get; set; } = string.Empty;
  public Avatar avatar { get; set; }
  public ModelImporterRigImportMode rigImportMode { get; set; } = ModelImporterRigImportMode.Default;
  public bool skinWeights { get; set; } = true;
  public ModelImporterSkinWeights skinWeightsMode { get; set; } = ModelImporterSkinWeights.Standard;
  public int maxBonesPerVertex { get; set; } = 4;
  public float minBoneWeight { get; set; } = 0.001f;
  public bool optimizeBones { get; set; } = true;
  public bool extraExposedTransformPaths { get; set; }
  public bool importConstraints { get; set; }
  public bool keepQuads { get; set; }
  public bool weldVertices { get; set; } = true;
  public ModelImporterIndexFormat indexFormat { get; set; } = ModelImporterIndexFormat.Auto;
  public bool sortHierarchyByName { get; set; } = true;
  public ModelImporterMeshCompression meshCompressionOption { get => meshCompression; set => meshCompression = value; }

  public static new ModelImporter GetAtPath(string path)
  {
    return AssetDatabase.GetImporterAtPath(path) as ModelImporter ?? new ModelImporter { assetPath = path, importSettingsMissing = true };
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
  Human,
  Humanoid = Human
}

public enum ModelImporterAvatarSetup
{
  NoAvatar,
  CreateFromThisModel,
  CopyFromOther
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

public enum ModelImporterMaterialImportMode
{
  None = 0,
  ImportStandard = 1,
  LegacyImport = ImportStandard,
  ImportViaMaterialDescription = 2,
  Import = ImportViaMaterialDescription,
}

public enum ModelImporterMaterialLocation
{
  External = 0,
  InPrefab = 1,
}

public enum ModelImporterMaterialName
{
  BasedOnMaterialName,
  BasedOnTextureName,
  BasedOnModelNameAndMaterialName,
  BasedOnTextureNameOrModelNameAndMaterialName
}

public enum ModelImporterAnimationCompression
{
  Off,
  KeyframeReduction,
  KeyframeReductionAndCompression,
  Optimal
}

public enum ModelImporterRigImportMode
{
  Default,
  Legacy
}

public enum ModelImporterSkinWeights
{
  None = 0,
  Standard = 1,
  Custom = 2,
  Unlimited = 255
}

public enum ModelImporterIndexFormat
{
  Auto,
  UInt16,
  UInt32
}

public class ModelImporterClipAnimation
{
  public string name { get; set; } = string.Empty;
  public string takeName { get; set; } = string.Empty;
  public float firstFrame { get; set; }
  public float lastFrame { get; set; }
  public bool loopTime { get; set; }
  public bool loopPose { get; set; }
  public bool lockRootRotation { get; set; }
  public bool lockRootHeightY { get; set; }
  public bool lockRootPositionXZ { get; set; }
  public bool mirror { get; set; }
  public WrapMode wrapMode { get; set; } = WrapMode.Default;
  public float cycleOffset { get; set; }
  public bool keepOriginalOrientation { get; set; }
  public bool keepOriginalPositionY { get; set; }
  public bool keepOriginalPositionXZ { get; set; }
  public bool heightFromFeet { get; set; }
  public AdditiveReferencePose additiveReferencePose { get; set; }
  public bool maskType { get; set; }
  public AvatarMask maskSource { get; set; }
  public bool hasAdditiveReferencePose { get; set; }

  public ModelImporterClipAnimation()
  {
    firstFrame = 0f;
    lastFrame = -1f;
  }
}

public struct AdditiveReferencePose
{
  public bool m_Enabled;
  public float m_NormalizedTime;
}
