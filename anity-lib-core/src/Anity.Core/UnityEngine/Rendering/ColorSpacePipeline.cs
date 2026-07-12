using Anity.Core.Runtime.Native;
using UnityEngine;

namespace UnityEngine.Rendering;

/// <summary>
/// Active color space conversion pipeline (Unity Linear workflow + HDR).
/// </summary>
public static class ColorSpacePipeline
{
    public static ColorSpace activeColorSpace
    {
        get => QualitySettings.activeColorSpace;
        set => QualitySettings.activeColorSpace = value;
    }

    public static bool isLinear => activeColorSpace == ColorSpace.Linear;

    public static Color ConvertToActiveColorSpace(Color color)
    {
        if (isLinear)
            return GammaToLinear(color);
        return LinearToGamma(color);
    }

    public static Color LinearToGamma(Color c)
    {
        if (AnityNative.Available)
        {
            try
            {
                return new Color(
                    AnityNative.HDR_LinearToGammaSpace(c.r),
                    AnityNative.HDR_LinearToGammaSpace(c.g),
                    AnityNative.HDR_LinearToGammaSpace(c.b),
                    c.a);
            }
            catch { AnityNative.MarkUnavailable(); }
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
            try
            {
                return new Color(
                    AnityNative.HDR_GammaToLinearSpace(c.r),
                    AnityNative.HDR_GammaToLinearSpace(c.g),
                    AnityNative.HDR_GammaToLinearSpace(c.b),
                    c.a);
            }
            catch { AnityNative.MarkUnavailable(); }
        }
        return new Color(
            Mathf.GammaToLinearSpace(c.r),
            Mathf.GammaToLinearSpace(c.g),
            Mathf.GammaToLinearSpace(c.b),
            c.a);
    }

    /// <summary>Configure project for URP HDR Linear (Unity 2022 default for URP templates).</summary>
    public static void ConfigureURPLinearHDR()
    {
        activeColorSpace = ColorSpace.Linear;
        HDROutputSettings.main.automaticHDRTonemapping = true;
        if (GraphicsSettings.currentRenderPipeline is Universal.UniversalRenderPipelineAsset urp)
            urp.supportsHDR = true;
    }
}
