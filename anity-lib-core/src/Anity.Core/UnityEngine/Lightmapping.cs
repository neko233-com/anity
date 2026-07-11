using System;
using System.Collections.Generic;

namespace UnityEngine;

public static class Lightmapping
{
    private static readonly Dictionary<string, object> _settings = new();
    private static LightmapData[] _lightmaps = Array.Empty<LightmapData>();
    private static bool _isBaking;
    private static float _bakeProgress;

    public static bool giWorkflowMode { get; set; }
    public static bool realtimeGI { get; set; }
    public static bool bakedGI { get; set; } = true;

    public static bool isBaking => _isBaking;
    public static float bakeProgress => _bakeProgress;

    public static bool lightProbeOcculsionData
    {
        get => GetValue<bool>(nameof(lightProbeOcculsionData));
        set => SetValue(nameof(lightProbeOcculsionData), value);
    }

    public static LightingSettings? lightingSettings { get; set; }
    public static Object? lightingDataAsset
    {
        get => GetValue<Object>(nameof(lightingDataAsset));
        set => SetValue(nameof(lightingDataAsset), value);
    }

    public static LightmapsMode lightmapBakeDirectionalMode
    {
        get => GetValue<LightmapsMode>(nameof(lightmapBakeDirectionalMode), LightmapsMode.NonDirectional);
        set => SetValue(nameof(lightmapBakeDirectionalMode), value);
    }

    public static float bouncedBounceBoost
    {
        get => GetValue<float>(nameof(bouncedBounceBoost), 1f);
        set => SetValue(nameof(bouncedBounceBoost), value);
    }

    public static float lightmapIndirectResolution
    {
        get => GetValue<float>(nameof(lightmapIndirectResolution), 2f);
        set => SetValue(nameof(lightmapIndirectResolution), value);
    }

    public static float lightmapResolution
    {
        get => GetValue<float>(nameof(lightmapResolution), 40f);
        set => SetValue(nameof(lightmapResolution), value);
    }

    public static float lightmapPadding
    {
        get => GetValue<float>(nameof(lightmapPadding), 2f);
        set => SetValue(nameof(lightmapPadding), value);
    }

    public static int lightmapMaxSize
    {
        get => GetValue<int>(nameof(lightmapMaxSize), 1024);
        set => SetValue(nameof(lightmapMaxSize), value);
    }

    public static Lightmapper quality
    {
        get => GetValue<Lightmapper>(nameof(quality), Lightmapper.ProgressiveCPU);
        set => SetValue(nameof(quality), value);
    }

    public static bool compressLightmaps
    {
        get => GetValue<bool>(nameof(compressLightmaps), true);
        set => SetValue(nameof(compressLightmaps), value);
    }

    public static MixedLightingMode mixedLightingMode
    {
        get => GetValue<MixedLightingMode>(nameof(mixedLightingMode));
        set => SetValue(nameof(mixedLightingMode), value);
    }

    public static bool realtimeEnvironmentLighting
    {
        get => GetValue<bool>(nameof(realtimeEnvironmentLighting), true);
        set => SetValue(nameof(realtimeEnvironmentLighting), value);
    }

    public static bool finalGather
    {
        get => GetValue<bool>(nameof(finalGather));
        set => SetValue(nameof(finalGather), value);
    }

    public static int finalGatherRayCount
    {
        get => GetValue<int>(nameof(finalGatherRayCount), 1024);
        set => SetValue(nameof(finalGatherRayCount), value);
    }

    public static LightmapBakeFiltering finalGatherFiltering
    {
        get => GetValue<LightmapBakeFiltering>(nameof(finalGatherFiltering));
        set => SetValue(nameof(finalGatherFiltering), value);
    }

    public static float albedoBoost
    {
        get => GetValue<float>(nameof(albedoBoost), 1f);
        set => SetValue(nameof(albedoBoost), value);
    }

    public static bool exportTrainingData
    {
        get => GetValue<bool>(nameof(exportTrainingData));
        set => SetValue(nameof(exportTrainingData), value);
    }

    public static int directLightSamples
    {
        get => GetValue<int>(nameof(directLightSamples), 32);
        set => SetValue(nameof(directLightSamples), value);
    }

    public static int indirectLightSamples
    {
        get => GetValue<int>(nameof(indirectLightSamples), 512);
        set => SetValue(nameof(indirectLightSamples), value);
    }

    public static int environmentSamples
    {
        get => GetValue<int>(nameof(environmentSamples), 256);
        set => SetValue(nameof(environmentSamples), value);
    }

    public static LightingSettings? lightmapEditorSettings
    {
        get => GetValue<LightingSettings>(nameof(lightmapEditorSettings));
        set => SetValue(nameof(lightmapEditorSettings), value);
    }

    public static LightmapData[] lightmaps
    {
        get => _lightmaps;
        set => _lightmaps = value ?? Array.Empty<LightmapData>();
    }

    public static int lightmapCount => _lightmaps.Length;

    public static event Action? LightmappingBakeStarted;
    public static event Action? BakeCompleted;
    public static event Action? onStarted;
    public static event Action? onCompleted;
    public static event Action<bool>? onBakeCompleted;

    public static void Clear() { }
    public static void ClearDiskCache() { }
    public static void ClearLightingDataAsset() { }
    public static void ClearBakedData() { }

    public static void Bake()
    {
        _isBaking = true;
        _bakeProgress = 0f;
        LightmappingBakeStarted?.Invoke();
        onStarted?.Invoke();
        SimulateBake();
    }

    public static void BakeAsync()
    {
        _isBaking = true;
        _bakeProgress = 0f;
        LightmappingBakeStarted?.Invoke();
        onStarted?.Invoke();
        SimulateBakeAsync();
    }

    private static async void SimulateBakeAsync()
    {
        await System.Threading.Tasks.Task.Delay(100);
        _bakeProgress = 1f;
        _isBaking = false;
        BakeCompleted?.Invoke();
        onCompleted?.Invoke();
        onBakeCompleted?.Invoke(true);
    }

    private static void SimulateBake()
    {
        _bakeProgress = 1f;
        _isBaking = false;
        BakeCompleted?.Invoke();
        onCompleted?.Invoke();
        onBakeCompleted?.Invoke(true);
    }

    public static void BakeMultipleScenes(string[] paths)
    {
        _ = paths;
        Bake();
    }

    public static void Cancel()
    {
        _isBaking = false;
        _bakeProgress = 0f;
    }

    public static bool isRunning => _isBaking;

    public static LightingSettings? GetLightmapSettings()
    {
        return lightingSettings;
    }

    public static void SetLightmapSettings(LightingSettings? settings)
    {
        lightingSettings = settings;
    }

    public static void SetLightingDataAsset(Object? asset)
    {
        lightingDataAsset = asset;
    }

    public static Object? GetLightingDataAsset()
    {
        return lightingDataAsset;
    }

    private static T GetValue<T>(string key, T defaultValue = default!)
    {
        if (_settings.TryGetValue(key, out var value) && value is T typedValue)
            return typedValue;
        return defaultValue;
    }

    private static void SetValue<T>(string key, T value)
    {
        _settings[key] = value!;
    }
}

public enum MixedLightingMode
{
    IndirectOnly = 0,
    Shadowmask = 2,
    Subtractive = 1
}

public enum LightmapsMode
{
    NonDirectional,
    CombinedDirectional,
    SeparatedDirectional
}

public enum LightmapBakeFiltering
{
    None,
    Auto,
    Advanced
}

public class LightingSettings : UnityEngine.Object
{
    public bool autoGenerate { get; set; }
    public Lightmapper lightmapper { get; set; } = Lightmapper.ProgressiveCPU;
    public float indirectResolution { get; set; } = 2f;
    public float lightmapMaxSize { get; set; } = 1024f;
    public float lightmapPadding { get; set; } = 2f;
    public bool compressLightmaps { get; set; } = true;
    public bool ambientOcclusion { get; set; }
    public float maxBounces { get; set; } = 4f;
    public float directSamples { get; set; } = 32f;
    public float indirectSamples { get; set; } = 512f;
    public float environmentSampleCount { get; set; } = 256f;
    public float environmentReflectionsSampleCount { get; set; } = 64f;
    public float lightProbeSampleCountMultiplier { get; set; } = 4f;
}

public enum Lightmapper
{
    Enlighten = 0,
    ProgressiveCPU = 1,
    ProgressiveGPU = 2
}
