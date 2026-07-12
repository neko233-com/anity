using System;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace UnityEditor;

public static class ProjectTemplates
{
    public static void Create3DURPProject(string path, ScreenOrientation orientation = ScreenOrientation.LandscapeLeft)
    {
        CreateProjectInternal(path, orientation, true, false);
    }

    public static void Create2DURPProject(string path, ScreenOrientation orientation = ScreenOrientation.LandscapeLeft)
    {
        CreateProjectInternal(path, orientation, true, true);
    }

    public static void CreateEmptyProject(string path)
    {
        CreateProjectInternal(path, ScreenOrientation.AutoRotation, false, false);
    }

    private static void CreateProjectInternal(string path, ScreenOrientation orientation, bool useURP, bool is2D)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Project path cannot be empty", nameof(path));

        var normalizedPath = NormalizePath(path);
        Directory.CreateDirectory(normalizedPath);

        ApplyOrientationSettings(orientation);
        CreateProjectSettings(normalizedPath, orientation, useURP, is2D);
        CreatePackagesManifest(normalizedPath, useURP);
        CreateDirectoryStructure(normalizedPath);
        CreateSampleScene(normalizedPath, orientation, is2D);
    }

    private static void ApplyOrientationSettings(ScreenOrientation orientation)
    {
        PlayerSettings.defaultScreenOrientation = (UIOrientation)orientation;
        ProjectSettings.defaultScreenOrientation = (UIOrientation)orientation;

        bool isPortrait = orientation == ScreenOrientation.Portrait || orientation == ScreenOrientation.PortraitUpsideDown;
        bool isLandscape = orientation == ScreenOrientation.LandscapeLeft || orientation == ScreenOrientation.LandscapeRight;

        if (isPortrait)
        {
            PlayerSettings.defaultScreenWidth = 1080;
            PlayerSettings.defaultScreenHeight = 1920;
            ProjectSettings.defaultScreenWidth = 1080;
            ProjectSettings.defaultScreenHeight = 1920;
            Screen.SetResolution(1080, 1920, false);
        }
        else if (isLandscape)
        {
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            ProjectSettings.defaultScreenWidth = 1920;
            ProjectSettings.defaultScreenHeight = 1080;
            Screen.SetResolution(1920, 1080, false);
        }
        else
        {
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            ProjectSettings.defaultScreenWidth = 1920;
            ProjectSettings.defaultScreenHeight = 1080;
            Screen.SetResolution(1920, 1080, false);
        }

        PlayerSettings.allowedAutorotateToPortrait = orientation == ScreenOrientation.AutoRotation || orientation == ScreenOrientation.Portrait;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = orientation == ScreenOrientation.AutoRotation || orientation == ScreenOrientation.PortraitUpsideDown;
        PlayerSettings.allowedAutorotateToLandscapeLeft = orientation == ScreenOrientation.AutoRotation || orientation == ScreenOrientation.LandscapeLeft;
        PlayerSettings.allowedAutorotateToLandscapeRight = orientation == ScreenOrientation.AutoRotation || orientation == ScreenOrientation.LandscapeRight;
    }

    private static void CreateProjectSettings(string path, ScreenOrientation orientation, bool useURP, bool is2D)
    {
        var settingsDir = Path.Combine(path, "ProjectSettings");
        Directory.CreateDirectory(settingsDir);

        bool isPortrait = orientation == ScreenOrientation.Portrait || orientation == ScreenOrientation.PortraitUpsideDown;
        int screenW = isPortrait ? 1080 : 1920;
        int screenH = isPortrait ? 1920 : 1080;

        var projectSettings = $@"%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!129 &1
PlayerSettings:
  m_ObjectHideFlags: 0
  serializedVersion: 24
  productName: {PlayerSettings.productName}
  companyName: {PlayerSettings.companyName}
  defaultScreenOrientation: {(int)orientation}
  defaultScreenWidth: {screenW}
  defaultScreenHeight: {screenH}
  defaultIsFullScreen: 1
  allowedAutorotateToPortrait: {(PlayerSettings.allowedAutorotateToPortrait ? 1 : 0)}
  allowedAutorotateToPortraitUpsideDown: {(PlayerSettings.allowedAutorotateToPortraitUpsideDown ? 1 : 0)}
  allowedAutorotateToLandscapeRight: {(PlayerSettings.allowedAutorotateToLandscapeRight ? 1 : 0)}
  allowedAutorotateToLandscapeLeft: {(PlayerSettings.allowedAutorotateToLandscapeLeft ? 1 : 0)}
  useAnimatedAutoRotation: 1
  applicationIdentifier:
    Android: {PlayerSettings.applicationIdentifier}
    Standalone: {PlayerSettings.applicationIdentifier}
  AndroidMinSdkVersion: {(int)PlayerSettings.Android.minSdkVersion}
  AndroidTargetSdkVersion: {(int)PlayerSettings.Android.targetSdkVersion}
  AndroidTargetArchitectures: {(int)PlayerSettings.Android.targetArchitectures}
  bundleVersion: {PlayerSettings.bundleVersion}
  AndroidBundleVersionCode: {PlayerSettings.Android.bundleVersionCode}
  colorSpace: {(int)PlayerSettings.colorSpace}";

        File.WriteAllText(Path.Combine(settingsDir, "ProjectSettings.asset"), projectSettings);
    }

    private static void CreatePackagesManifest(string path, bool useURP)
    {
        var packagesDir = Path.Combine(path, "Packages");
        Directory.CreateDirectory(packagesDir);

        var manifest = $@"{{
  ""dependencies"": {{
    ""com.unity.render-pipelines.universal"": ""14.0.11"",
    ""com.unity.ugui"": ""1.0.0""{(useURP ? @",
    ""com.unity.render-pipelines.core"": ""14.0.11""" : "")}
  }}
}}";
        File.WriteAllText(Path.Combine(packagesDir, "manifest.json"), manifest);
    }

    private static void CreateDirectoryStructure(string path)
    {
        var dirs = new[]
        {
            "Assets",
            "Assets/Scenes",
            "Assets/Scripts",
            "Assets/Prefabs",
            "Assets/Materials",
            "Assets/Textures",
            "Assets/Models",
            "Assets/Audio",
            "Assets/UI",
            "Assets/Resources",
            "Packages",
            "ProjectSettings"
        };

        foreach (var dir in dirs)
        {
            Directory.CreateDirectory(Path.Combine(path, dir));
        }
    }

    private static void CreateSampleScene(string path, ScreenOrientation orientation, bool is2D)
    {
        var scenesDir = Path.Combine(path, "Assets", "Scenes");
        Directory.CreateDirectory(scenesDir);

        bool isPortrait = orientation == ScreenOrientation.Portrait || orientation == ScreenOrientation.PortraitUpsideDown;
        bool isAutoRotate = orientation == ScreenOrientation.AutoRotation;
        float refW = isPortrait ? 1080f : 1920f;
        float refH = isPortrait ? 1920f : 1080f;
        float matchWidthOrHeight = isAutoRotate ? 0.5f : (isPortrait ? 1f : 0f);

        var sceneContent = $@"%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!29 &1
OcclusionCullingSettings:
  m_ObjectHideFlags: 0
  serializedVersion: 2
  m_OcclusionBakeSettings:
    smallestOccluder: 5
    smallestHole: 0.25
    backfaceThreshold: 100
  m_SceneGUID: 00000000000000000000000000000000
  m_OcclusionCullingData: {{fileID: 0}}
--- !u!104 &2
RenderSettings:
  m_ObjectHideFlags: 0
  serializedVersion: 9
  m_Fog: 0
  m_FogColor: {{r: 0.5, g: 0.5, b: 0.5, a: 1}}
  m_FogMode: 3
  m_FogDensity: 0.01
  m_LinearFogStart: 0
  m_LinearFogEnd: 300
  m_AmbientSkyColor: {{r: 0.212, g: 0.227, b: 0.259, a: 1}}
  m_AmbientEquatorColor: {{r: 0.114, g: 0.125, b: 0.133, a: 1}}
  m_AmbientGroundColor: {{r: 0.047, g: 0.043, b: 0.035, a: 1}}
  m_AmbientIntensity: 1
  m_AmbientMode: 0
  m_SubtractiveShadowColor: {{r: 0.42, g: 0.478, b: 0.627, a: 1}}
  m_SkyboxMaterial: {{fileID: 0}}
  m_HaloStrength: 0.5
  m_FlareStrength: 1
  m_FlareFadeSpeed: 3
  m_HaloTexture: {{fileID: 0}}
  m_SpotCookie: {{fileID: 10001, guid: 0000000000000000e000000000000000, type: 0}}
  m_DefaultReflectionMode: 0
  m_DefaultReflectionResolution: 128
  m_ReflectionBounces: 1
  m_ReflectionIntensity: 1
  m_CustomReflection: {{fileID: 0}}
  m_Sun: {{fileID: 0}}
  m_IndirectSpecularColor: {{r: 0, g: 0, b: 0, a: 1}}
  m_UseRadianceAmbientProbe: 0
--- !u!157 &3
LightmapSettings:
  m_ObjectHideFlags: 0
  serializedVersion: 12
  m_GIWorkflowMode: 1
  m_GISettings:
    serializedVersion: 2
    m_BounceScale: 1
    m_IndirectOutputScale: 1
    m_AlbedoBoost: 1
    m_EnvironmentLightingMode: 0
    m_EnableBakedLightmaps: 0
    m_EnableRealtimeLightmaps: 0
  m_LightmapEditorSettings:
    serializedVersion: 12
    m_Resolution: 2
    m_BakeResolution: 40
    m_AtlasSize: 1024
    m_AO: 0
    m_AOMaxDistance: 1
    m_CompAOExponent: 1
    m_CompAOExponentDirect: 0
    m_ExtractAmbientOcclusion: 0
    m_Padding: 2
    m_LightmapParameters: {{fileID: 0}}
    m_LightmapsBakeMode: 1
    m_TextureCompression: 1
    m_FinalGather: 0
    m_FinalGatherFiltering: 1
    m_FinalGatherRayCount: 256
    m_ReflectionCompression: 2
    m_MixedBakeMode: 2
    m_BakeBackend: 1
    m_PVRSampling: 1
    m_PVRDirectSampleCount: 32
    m_PVRSampleCount: 512
    m_PVRBounces: 2
    m_PVREnvironmentSampleCount: 256
    m_PVREnvironmentReferencePointCount: 2048
    m_PVRFilteringMode: 1
    m_PVRDenoiserTypeDirect: 1
    m_PVRDenoiserTypeIndirect: 1
    m_PVRDenoiserTypeAO: 1
    m_PVRFilterTypeDirect: 0
    m_PVRFilterTypeIndirect: 0
    m_PVRFilterTypeAO: 0
    m_PVREnvironmentMIS: 1
    m_PVRCulling: 1
    m_PVRFilteringGaussRadiusDirect: 1
    m_PVRFilteringGaussRadiusIndirect: 5
    m_PVRFilteringGaussRadiusAO: 2
    m_PVRFilteringAtrousPositionSigmaDirect: 0.5
    m_PVRFilteringAtrousPositionSigmaIndirect: 2
    m_PVRFilteringAtrousPositionSigmaAO: 1
    m_ExportTrainingData: 0
    m_TrainingDataDestination: TrainingData
    m_LightProbeSampleCountMultiplier: 4
  m_LightingDataAsset: {{fileID: 0}}
  m_LightingSettings: {{fileID: 0}}
--- !u!19 &4
NavMeshSettings:
  serializedVersion: 2
  m_ObjectHideFlags: 0
  m_BuildSettings:
    serializedVersion: 3
    agentTypeID: 0
    agentRadius: 0.5
    agentHeight: 2
    agentSlope: 45
    agentClimb: 0.4
    ledgeDropHeight: 0
    maxJumpAcrossDistance: 0
    minRegionArea: 2
    manualCellSize: 0
    cellSize: 0.16666667
    manualTileSize: 0
    tileSize: 256
    buildHeightMesh: 0
    maxJobWorkers: 0
    preserveTilesOutsideBounds: 0
    debug:
      m_Flags: 0
  m_NavMeshData: {{fileID: 0}}
--- !u!1 &100000
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: 100001}}
  - component: {{fileID: 100002}}
  - component: {{fileID: 100003}}
  m_Layer: 0
  m_Name: Main Camera
  m_TagString: MainCamera
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &100001
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 100000}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: -10}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: 0}}
  m_RootOrder: 0
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
--- !u!20 &100002
Camera:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 100000}}
  m_Enabled: 1
  serializedVersion: 2
  m_ClearFlags: {(is2D ? 4 : 1)}
  m_BackGroundColor: {{r: 0.19215687, g: 0.3019608, b: 0.4745098, a: 0}}
  m_projectionMatrixMode: 1
  m_GateFitMode: 2
  m_FOVAxisMode: 0
  m_Iso: 200
  m_ShutterSpeed: 0.005
  m_Aperture: 16
  m_FocusDistance: 10
  m_FocalLength: 50
  m_BladeCount: 5
  m_Curvature: {{x: 2, y: 11}}
  m_BarrelClipping: 0.25
  m_Anamorphism: 0
  m_SensorSize: {{x: 36, y: 24}}
  m_LensShift: {{x: 0, y: 0}}
  m_NormalizedViewPortRect:
    serializedVersion: 2
    x: 0
    y: 0
    width: 1
    height: 1
  near clip plane: 0.3
  far clip plane: 1000
  field of view: {(is2D ? 60 : 60)}
  orthographic: {(is2D ? 1 : 0)}
  orthographic size: {(is2D ? 5 : 5)}
  m_Depth: -1
  m_CullingMask:
    serializedVersion: 2
    m_Bits: 4294967295
  m_RenderingPath: -1
  m_TargetTexture: {{fileID: 0}}
  m_TargetDisplay: 0
  m_TargetEye: 3
  m_HDR: 1
  m_AllowMSAA: 1
  m_AllowDynamicResolution: 0
  m_ForceIntoRT: 0
  m_OcclusionCulling: 1
  m_StereoConvergence: 10
  m_StereoSeparation: 0.022
--- !u!81 &100003
AudioListener:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 100000}}
  m_Enabled: 1
--- !u!1 &100010
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: 100011}}
  - component: {{fileID: 100012}}
  m_Layer: 0
  m_Name: Directional Light
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: {(is2D ? 0 : 1)}
--- !u!4 &100011
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 100010}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0.40821788, y: -0.23456968, z: 0.10938163, w: 0.8754262}}
  m_LocalPosition: {{x: 0, y: 3, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: 0}}
  m_RootOrder: 1
  m_LocalEulerAnglesHint: {{x: 50, y: -30, z: 0}}
--- !u!108 &100012
Light:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 100010}}
  m_Enabled: 1
  serializedVersion: 10
  m_Type: 1
  m_Shape: 0
  m_Color: {{r: 1, g: 0.95686275, b: 0.8392157, a: 1}}
  m_Intensity: 1
  m_Range: 10
  m_SpotAngle: 30
  m_InnerSpotAngle: 21.80208
  m_CookieSize: 10
  m_Shadows:
    m_Type: 2
    m_Resolution: -1
    m_CustomResolution: -1
    m_Strength: 1
    m_Bias: 0.05
    m_NormalBias: 0.4
    m_NearPlane: 0.2
    m_CullingMatrixOverride:
      e00: 1
      e01: 0
      e02: 0
      e03: 0
      e10: 0
      e11: 1
      e12: 0
      e13: 0
      e20: 0
      e21: 0
      e22: 1
      e23: 0
      e30: 0
      e31: 0
      e32: 0
      e33: 1
    m_UseCullingMatrixOverride: 0
  m_Cookie: {{fileID: 0}}
  m_DrawHalo: 0
  m_Flare: {{fileID: 0}}
  m_RenderMode: 0
  m_CullingMask:
    serializedVersion: 2
    m_Bits: 4294967295
  m_RenderingLayerMask: 1
  m_Lightmapping: 4
  m_LightShadowCasterMode: 0
  m_AreaSize: {{x: 1, y: 1}}
  m_BounceIntensity: 1
  m_ColorTemperature: 6570
  m_UseColorTemperature: 0
  m_BoundingSphereOverride: {{x: 0, y: 0, z: 0, w: 0}}
  m_UseBoundingSphereOverride: 0
  m_UseViewFrustumForShadowCasterCull: 1
  m_ForceVisible: 0
--- !u!1 &100020
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: 100021}}
  - component: {{fileID: 100022}}
  - component: {{fileID: 100023}}
  - component: {{fileID: 100024}}
  m_Layer: 5
  m_Name: Canvas
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!224 &100021
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 100020}}
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 0, y: 0, z: 0}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: 0}}
  m_RootOrder: 2
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
  m_AnchorMin: {{x: 0, y: 0}}
  m_AnchorMax: {{x: 0, y: 0}}
  m_AnchoredPosition: {{x: 0, y: 0}}
  m_SizeDelta: {{x: 0, y: 0}}
  m_Pivot: {{x: 0, y: 0}}
--- !u!223 &100022
Canvas:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 100020}}
  m_Enabled: 1
  serializedVersion: 3
  m_RenderMode: 0
  m_Camera: {{fileID: 0}}
  m_PlaneDistance: 100
  m_PixelPerfect: 0
  m_ReceivesEvents: 1
  m_OverrideSorting: 0
  m_SortingBucketNormalizedSize: 0
  m_OverridePixelPerfect: 0
  m_SortingLayerID: 0
  m_SortingLayer: 0
  m_SortingOrder: 0
  m_TargetDisplay: 0
  m_AdditionalShaderChannelsFlag: 0
  m_SortingOrderOverridden: 0
  m_UpdateRectTransformForStandalone: 1
--- !u!114 &100023
CanvasScaler:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 100020}}
  m_Enabled: 1
  m_UiScaleMode: 1
  m_ReferencePixelsPerUnit: 100
  m_ScaleFactor: 1
  m_ReferenceResolution: {{x: {refW}, y: {refH}}}
  m_ScreenMatchMode: 0
  m_MatchWidthOrHeight: {matchWidthOrHeight}
  m_PhysicalUnit: 3
  m_FallbackScreenDPI: 96
  m_DefaultSpriteDPI: 96
  m_DynamicPixelsPerUnit: 1
--- !u!114 &100024
CanvasRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 100020}}
  m_Enabled: 1
--- !u!1 &100030
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: 100031}}
  - component: {{fileID: 100032}}
  m_Layer: 5
  m_Name: EventSystem
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!224 &100031
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 100030}}
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: 0}}
  m_RootOrder: 3
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
  m_AnchorMin: {{x: 0.5, y: 0.5}}
  m_AnchorMax: {{x: 0.5, y: 0.5}}
  m_AnchoredPosition: {{x: 0, y: 0}}
  m_SizeDelta: {{x: 0, y: 0}}
  m_Pivot: {{x: 0.5, y: 0.5}}
--- !u!114 &100032
EventSystem:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 100030}}
  m_Enabled: 1
  m_FirstSelected: {{fileID: 0}}
  m_sendPointerEventsToFocusedObject: 1
  m_DragThreshold: 5";

        File.WriteAllText(Path.Combine(scenesDir, "SampleScene.unity"), sceneContent);
    }

    private static string NormalizePath(string path)
    {
        return path.Replace('\\', '/');
    }
}
