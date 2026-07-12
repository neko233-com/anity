using System;
using Anity.Core.Runtime.Native;
using UnityEngine;
using UnityEngine.Rendering;

namespace UnityEngine.Rendering.Universal
{
  /// <summary>
  /// URP post-processing pass — Bloom + Tonemapping + ColorAdjustments for HDR cameras.
  /// </summary>
  public sealed class PostProcessPass : ScriptableRenderPass
  {
    private readonly ProfilingSampler _sampler = new ProfilingSampler("PostProcess");

    public PostProcessPass()
    {
      renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
    }

    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
      var camData = renderingData.cameraData;
      VolumeManager.instance.Update(camData.camera != null ? camData.camera.transform : null);
      var stack = VolumeManager.instance.stack;

      var bloom = stack.Get<Bloom>();
      var tonemap = stack.Get<Tonemapping>();
      var colorAdj = stack.Get<ColorAdjustments>();

      bool any =
        (bloom != null && bloom.IsActive()) ||
        (tonemap != null && tonemap.IsActive()) ||
        (colorAdj != null && colorAdj.IsActive()) ||
        camData.isHdrEnabled ||
        HDROutputSettings.main.active;

      if (!any) return;

      CommandBuffer cmd = CommandBufferPool.Get("URP PostProcess HDR");
      using (new ProfilingScope(cmd, _sampler))
      {
        float postExposure = colorAdj != null ? colorAdj.postExposure.value : 0f;
        float bloomIntensity = bloom != null && bloom.IsActive() ? bloom.intensity.value : 0f;
        float bloomThreshold = bloom != null ? bloom.threshold.value : 0.9f;
        int tonemapMode = 0;
        if (tonemap != null && tonemap.IsActive())
        {
          tonemapMode = tonemap.mode.value switch
          {
            TonemappingMode.Neutral => 1,
            TonemappingMode.ACES => 2,
            _ => 0
          };
        }
        else if (HDROutputSettings.main.automaticHDRTonemapping && camData.isHdrEnabled)
        {
          tonemapMode = 2;
        }

        Shader.SetGlobalFloat("_PostExposure", postExposure);
        Shader.SetGlobalFloat("_BloomIntensity", bloomIntensity);
        Shader.SetGlobalFloat("_BloomThreshold", bloomThreshold);
        Shader.SetGlobalInt("_TonemapMode", tonemapMode);
        Shader.SetGlobalFloat("_HDRPaperWhite", HDROutputSettings.main.paperWhiteNits);

        if (camData.isHdrEnabled)
          Shader.EnableKeyword("_HDR_ON");
        else
          Shader.DisableKeyword("_HDR_ON");

        if (colorAdj != null && colorAdj.IsActive())
        {
          Shader.SetGlobalFloat("_Contrast", colorAdj.contrast.value);
          Shader.SetGlobalFloat("_Saturation", colorAdj.saturation.value);
          Shader.SetGlobalFloat("_HueShift", colorAdj.hueShift.value);
          Shader.SetGlobalColor("_ColorFilter", colorAdj.colorFilter.value);
        }

        PostProcessRuntime.LastGrade = new AnityNative.HDRColorGrade
        {
          postExposure = postExposure,
          contrast = colorAdj?.contrast.value ?? 0f,
          saturation = colorAdj?.saturation.value ?? 0f,
          temperature = colorAdj?.temperature.value ?? 0f,
          tint = colorAdj?.tint.value ?? 0f,
          bloomThreshold = bloomThreshold,
          bloomIntensity = bloomIntensity,
          tonemapMode = tonemapMode
        };
      }

      context.ExecuteCommandBuffer(cmd);
      CommandBufferPool.Release(cmd);
    }
  }

  public sealed class PostProcessRendererFeature : ScriptableRendererFeature
  {
    private PostProcessPass? _pass;

    public override void Create()
    {
      _pass = new PostProcessPass();
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
      if (_pass == null) Create();
      if (renderingData.cameraData.camera != null)
        renderer.EnqueuePass(_pass!);
    }
  }

  public static class PostProcessRuntime
  {
    public static AnityNative.HDRColorGrade LastGrade;

    public static bool ProcessSoftFrame(float[] rgbaHdr, int w, int h, float[] rgbaOut, bool hdr10 = false)
    {
      if (rgbaHdr == null || rgbaOut == null) return false;
      var g = LastGrade;
      if (AnityNative.Available)
      {
        try
        {
          return AnityNative.HDR_ProcessFrame(rgbaHdr, w, h, ref g, rgbaOut, hdr10 ? 1 : 0) == AnityNative.Result.Ok;
        }
        catch
        {
          AnityNative.MarkUnavailable();
        }
      }
      return HDRUtilities.ProcessFrame(rgbaHdr, w, h, g.postExposure, g.bloomIntensity, g.tonemapMode, rgbaOut, hdr10);
    }
  }

  /// <summary>Unity.Profiling-compatible sampler for render passes.</summary>
  public sealed class ProfilingSampler
  {
    public string name { get; }
    public ProfilingSampler(string name) => this.name = name ?? "Sampler";
  }

  public readonly struct ProfilingScope : IDisposable
  {
    public ProfilingScope(CommandBuffer cmd, ProfilingSampler sampler)
    {
      _ = cmd;
      _ = sampler;
    }

    public void Dispose() { }
  }
}
