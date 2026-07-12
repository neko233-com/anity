using System;
using Anity.Core.Runtime.Native;
using UnityEngine.Rendering;

namespace UnityEngine;

/// <summary>
/// UnityEngine.HDROutputSettings — HDR display output (Unity 2022.3 Pro).
/// Backed by anity-native AnityHDR_* when available.
/// </summary>
public class HDROutputSettings
{
    private static readonly HDROutputSettings _main = new HDROutputSettings(0);
    private static HDROutputSettings[] _displays = { _main };

    private readonly int _displayIndex;
    private bool _active;
    private float _paperWhiteNits = 160f;
    private bool _automaticHDRTonemapping = true;
    private ColorGamut _displayColorGamut = ColorGamut.sRGB;
    private HDRDisplayBitDepth _bitsPerColorComponent = HDRDisplayBitDepth.BitDepth10;

    public HDROutputSettings(int displayIndex)
    {
        _displayIndex = displayIndex;
        SyncFromNative();
    }

    public static HDROutputSettings main => _main;
    public static HDROutputSettings[] displays => _displays;
    public static int displayCount => _displays.Length;

    public int displayIndex => _displayIndex;

    public bool available
    {
        get
        {
            if (AnityNative.TryQueryHDR(out var info))
                return info.available != 0;
            return SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.DefaultHDR)
                   || SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf);
        }
    }

    public bool active
    {
        get => _active;
        set
        {
            _active = value && available;
            if (AnityNative.Available)
                AnityNative.HDR_SetActive(_active ? 1 : 0);
            if (_active)
                _displayColorGamut = ColorGamut.HDR10;
            else
                _displayColorGamut = ColorGamut.sRGB;
        }
    }

    public bool automaticHDRTonemapping
    {
        get => _automaticHDRTonemapping;
        set
        {
            _automaticHDRTonemapping = value;
            if (AnityNative.Available)
                AnityNative.HDR_SetAutomaticTonemapping(value ? 1 : 0);
        }
    }

    public float paperWhiteNits
    {
        get => _paperWhiteNits;
        set
        {
            _paperWhiteNits = Mathf.Clamp(value, 80f, 400f);
            if (AnityNative.Available)
                AnityNative.HDR_SetPaperWhiteNits(_paperWhiteNits);
        }
    }

    public float maxFullFrameToneMapLuminance { get; private set; } = 300f;
    public float maxToneMapLuminance { get; private set; } = 1000f;
    public float minToneMapLuminance { get; private set; } = 0.02f;

    public ColorGamut displayColorGamut
    {
        get => _displayColorGamut;
        set => _displayColorGamut = value;
    }

    public HDRDisplayBitDepth graphicsFormat
    {
        get => _bitsPerColorComponent;
        set => _bitsPerColorComponent = value;
    }

    public HDRDisplayBitDepth bitsPerColorComponent => _bitsPerColorComponent;

    public void RequestHDRModeChange(bool enabled) => active = enabled;

    private void SyncFromNative()
    {
        if (!AnityNative.TryQueryHDR(out var info)) return;
        _active = info.active != 0;
        _paperWhiteNits = info.paperWhiteNits;
        _automaticHDRTonemapping = info.automaticHDRTonemapping != 0;
        maxFullFrameToneMapLuminance = info.maxFullFrameToneMapLuminance;
        maxToneMapLuminance = info.maxToneMapLuminance;
        minToneMapLuminance = info.minToneMapLuminance;
        _displayColorGamut = (ColorGamut)info.displayColorGamut;
        _bitsPerColorComponent = (HDRDisplayBitDepth)info.bitsPerColorComponent;
    }
}

public enum ColorGamut
{
    sRGB = 0,
    Rec709 = 1,
    Rec2020 = 2,
    DisplayP3 = 3,
    HDR10 = 4,
    DolbyHDR = 5,
    HDR10Plus = 6
}

public enum HDRDisplayBitDepth
{
    BitDepth8 = 0,
    BitDepth10 = 1,
    BitDepth16 = 2
}

/// <summary>
/// Camera HDR / color management helpers aligned with Unity 2022 URP.
/// </summary>
public static class HDRUtilities
{
    public static bool IsCameraHDR(Camera camera)
    {
        if (camera == null) return false;
        if (!camera.allowHDR) return false;
        var urp = GraphicsSettings.currentRenderPipeline as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
        if (urp != null && !urp.supportsHDR) return false;
        return true;
    }

    public static RenderTextureFormat GetHDRRenderTextureFormat()
    {
        if (SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.DefaultHDR))
            return RenderTextureFormat.DefaultHDR;
        if (SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf))
            return RenderTextureFormat.ARGBHalf;
        if (SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBFloat))
            return RenderTextureFormat.ARGBFloat;
        return RenderTextureFormat.ARGB32;
    }

    public static Color LinearToGamma(Color c)
    {
        if (AnityNative.Available)
        {
            return new Color(
                AnityNative.HDR_LinearToGammaSpace(c.r),
                AnityNative.HDR_LinearToGammaSpace(c.g),
                AnityNative.HDR_LinearToGammaSpace(c.b),
                c.a);
        }
        return new Color(
            Mathf.LinearToGammaSpace(c.r),
            Mathf.LinearToGammaSpace(c.g),
            Mathf.LinearToGammaSpace(c.b),
            c.a);
    }

    public static Color GammaToLinear(Color c)
    {
        if (AnityNative.Available)
        {
            return new Color(
                AnityNative.HDR_GammaToLinearSpace(c.r),
                AnityNative.HDR_GammaToLinearSpace(c.g),
                AnityNative.HDR_GammaToLinearSpace(c.b),
                c.a);
        }
        return new Color(
            Mathf.GammaToLinearSpace(c.r),
            Mathf.GammaToLinearSpace(c.g),
            Mathf.GammaToLinearSpace(c.b),
            c.a);
    }

    /// <summary>
    /// Process HDR float RGBA frame through native tonemap + grade (CPU path).
    /// </summary>
    public static bool ProcessFrame(float[] rgbaHdr, int width, int height,
        float postExposure, float bloomIntensity, int tonemapMode /* 0 none 1 neutral 2 ACES */,
        float[] rgbaOut, bool outputHdr10 = false)
    {
        if (rgbaHdr == null || rgbaOut == null || width <= 0 || height <= 0) return false;
        if (!AnityNative.Available)
        {
            // managed ACES fallback
            int n = width * height;
            float exp = Mathf.Pow(2f, postExposure);
            for (int i = 0; i < n; i++)
            {
                float r = rgbaHdr[i * 4] * exp;
                float g = rgbaHdr[i * 4 + 1] * exp;
                float b = rgbaHdr[i * 4 + 2] * exp;
                r = Aces(r); g = Aces(g); b = Aces(b);
                if (!outputHdr10)
                {
                    r = Mathf.LinearToGammaSpace(r);
                    g = Mathf.LinearToGammaSpace(g);
                    b = Mathf.LinearToGammaSpace(b);
                }
                rgbaOut[i * 4] = r;
                rgbaOut[i * 4 + 1] = g;
                rgbaOut[i * 4 + 2] = b;
                rgbaOut[i * 4 + 3] = rgbaHdr[i * 4 + 3];
            }
            return true;
        }

        var grade = new AnityNative.HDRColorGrade
        {
            postExposure = postExposure,
            bloomThreshold = 0.9f,
            bloomIntensity = bloomIntensity,
            tonemapMode = tonemapMode
        };
        return AnityNative.HDR_ProcessFrame(rgbaHdr, width, height, ref grade, rgbaOut, outputHdr10 ? 1 : 0)
               == AnityNative.Result.Ok;
    }

    private static float Aces(float x)
    {
        const float a = 2.51f, b = 0.03f, c = 2.43f, d = 0.59f, e = 0.14f;
        return Mathf.Clamp01((x * (a * x + b)) / (x * (c * x + d) + e));
    }
}
