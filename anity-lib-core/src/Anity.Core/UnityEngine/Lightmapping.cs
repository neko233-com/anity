using System;

namespace UnityEngine;

public static class Lightmapping
{
    public static bool giWorkflowMode { get; set; }
    public static bool realtimeGI { get; set; }
    public static bool bakedGI { get; set; } = true;

    public static void Clear() { }
    public static void ClearDiskCache() { }
    public static void ClearLightingDataAsset() { }

    public static void Bake() => onStarted?.Invoke();
    public static void BakeAsync() => onStarted?.Invoke();
    public static void BakeMultipleScenes(string[] paths) { _ = paths; onStarted?.Invoke(); }

    public static void Cancel() { }
    public static bool isRunning => false;

    public static event Action? onStarted;
    public static event Action? onCompleted;
    public static event Action<bool>? onBakeCompleted;

    public static LightingSettings? lightingSettings { get; set; }
    public static LightmapData[] lightmaps => Array.Empty<LightmapData>();

    public static void SetLightingDataAsset(UnityEngine.Object? asset) { _ = asset; }
    public static UnityEngine.Object? GetLightingDataAsset() => null;
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
