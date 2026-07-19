using System;
using System.Runtime.CompilerServices;
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
      var whiteBalance = stack.Get<WhiteBalance>();
      var channelMixer = stack.Get<ChannelMixer>();
      var colorCurves = stack.Get<ColorCurves>();

      bool any =
        (bloom != null && bloom.IsActive()) ||
        (tonemap != null && tonemap.IsActive()) ||
        (colorAdj != null && colorAdj.IsActive()) ||
        (whiteBalance != null && whiteBalance.IsActive()) ||
        (channelMixer != null && channelMixer.IsActive()) ||
        (colorCurves != null && colorCurves.IsActive()) ||
        camData.isHdrEnabled ||
        HDROutputSettings.main.active;

      if (!any) return;

      CommandBuffer cmd = CommandBufferPool.Get("URP PostProcess HDR");
      using (new ProfilingScope(cmd, _sampler))
      {
        var device = NativeGraphicsDevice.Current;
        float postExposure = colorAdj != null ? colorAdj.postExposure.value : 0f;
        float bloomIntensity = bloom != null && bloom.IsActive() ? bloom.intensity.value : 0f;
        float bloomThreshold = bloom != null ? bloom.threshold.value : 0.9f;
        ulong bloomDirtTextureId = 0;
        float bloomDirtIntensity = 0f;
        if (bloom?.dirtTexture.value is Texture2D dirtTexture &&
            bloom.dirtIntensity.value > 0f && device?.EnsureTexture(dirtTexture) == true)
        {
          bloomDirtTextureId = unchecked((ulong)(uint)dirtTexture.GetInstanceID());
          bloomDirtIntensity = bloom.dirtIntensity.value;
        }
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
          temperature = Mathf.Clamp((colorAdj?.temperature.value ?? 0f) +
              (whiteBalance?.temperature.value ?? 0f), -100f, 100f),
          tint = Mathf.Clamp((colorAdj?.tint.value ?? 0f) +
              (whiteBalance?.tint.value ?? 0f), -100f, 100f),
          hueShift = colorAdj?.hueShift.value ?? 0f,
          colorFilterR = colorAdj?.colorFilter.value.r ?? 1f,
          colorFilterG = colorAdj?.colorFilter.value.g ?? 1f,
          colorFilterB = colorAdj?.colorFilter.value.b ?? 1f,
          mixerRedR = channelMixer?.red.value.x ?? 1f,
          mixerRedG = channelMixer?.red.value.y ?? 0f,
          mixerRedB = channelMixer?.red.value.z ?? 0f,
          mixerGreenR = channelMixer?.green.value.x ?? 0f,
          mixerGreenG = channelMixer?.green.value.y ?? 1f,
          mixerGreenB = channelMixer?.green.value.z ?? 0f,
          mixerBlueR = channelMixer?.blue.value.x ?? 0f,
          mixerBlueG = channelMixer?.blue.value.y ?? 0f,
          mixerBlueB = channelMixer?.blue.value.z ?? 1f,
          curveEnabled = colorCurves != null && colorCurves.IsActive() ? 1 : 0,
          curveLut = PostProcessRuntime.BakeColorCurves(colorCurves),
          bloomThreshold = bloomThreshold,
          bloomIntensity = bloomIntensity,
          bloomScatter = bloom?.scatter.value ?? 0.7f,
          bloomMaxIterations = bloom?.maxIterations.value ?? 6,
          bloomDownscale = bloom?.downscale.value ?? 1,
          bloomHighQualityFiltering = bloom?.highQualityFiltering.value == false ? 0 : 1,
          bloomTintR = bloom?.tint.value.r ?? 1f,
          bloomTintG = bloom?.tint.value.g ?? 1f,
          bloomTintB = bloom?.tint.value.b ?? 1f,
          bloomDirtTextureId = bloomDirtTextureId,
          bloomDirtIntensity = bloomDirtIntensity,
          tonemapMode = tonemapMode
        };

        // This is the actual final-stack native execution, not a readback
        // helper: the Metal backend dispatches the grade over the resolved
        // RGBA16Float attachment after all Base/Overlay camera work.
        if (camData.isHdrEnabled)
        {
          if (camData.nativeTargetTexture != null)
            device?.TryProcessCameraRenderTargetHDR(camData.nativeTargetTexture, PostProcessRuntime.LastGrade);
          else
            device?.TryProcessSwapchainHDR(PostProcessRuntime.LastGrade);
        }
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
      // A stacked camera shares the base target.  Only the final camera can
      // resolve/grade it; running the pass per overlay would tone-map the
      // already composited image repeatedly.
      bool isStandaloneRendererInvocation = !renderingData.cameraData.isCameraStacked;
      if ((isStandaloneRendererInvocation || (renderingData.postProcessingEnabled && renderingData.cameraData.isLastCameraInStack)) && renderingData.cameraData.camera != null)
        renderer.EnqueuePass(_pass!);
    }
  }

  public static class PostProcessRuntime
  {
    // Curves reside in a reusable Metal buffer, so the final pass is no longer
    // constrained by inline constant-buffer size.
    internal const int ColorCurveSamples = 128;
    public static AnityNative.HDRColorGrade LastGrade;
    private static readonly ConditionalWeakTable<ColorCurves, ColorCurveLutCache> s_ColorCurveLutCache = new();
    private static readonly float[] s_IdentityCurveLut = CreateIdentityCurveLut();

    private sealed class ColorCurveLutCache
    {
      private TextureCurveSnapshot[]? _snapshot;
      public float[]? lut;

      public bool Matches(TextureCurve?[] curves)
      {
        if (lut == null || _snapshot == null || _snapshot.Length != curves.Length) return false;
        for (int index = 0; index < curves.Length; index++)
          if (!_snapshot[index].Matches(curves[index])) return false;
        return true;
      }

      public void Store(TextureCurve?[] curves, float[] baked)
      {
        var snapshot = new TextureCurveSnapshot[curves.Length];
        for (int index = 0; index < curves.Length; index++)
          snapshot[index] = new TextureCurveSnapshot(curves[index]);
        _snapshot = snapshot;
        lut = baked;
      }
    }

    // TextureCurve fields remain public to match Unity's serializable object
    // model, so cache validity is based on an immutable value snapshot rather
    // than an unreliable manual dirty flag.
    private sealed class TextureCurveSnapshot
    {
      private readonly bool _hasCurve;
      private readonly float _loop;
      private readonly bool _zeroValueIsOne;
      private readonly WrapMode _preWrapMode;
      private readonly WrapMode _postWrapMode;
      private readonly Keyframe[] _keys;

      public TextureCurveSnapshot(TextureCurve? source)
      {
        _loop = source?.loop ?? 0f;
        _zeroValueIsOne = source?.zeroValueIsOne ?? false;
        var curve = source?.curve;
        _hasCurve = curve != null;
        _preWrapMode = curve?.preWrapMode ?? WrapMode.Default;
        _postWrapMode = curve?.postWrapMode ?? WrapMode.Default;
        _keys = curve?.keys ?? Array.Empty<Keyframe>();
      }

      public bool Matches(TextureCurve? source)
      {
        if (_loop != (source?.loop ?? 0f) ||
            _zeroValueIsOne != (source?.zeroValueIsOne ?? false)) return false;
        var curve = source?.curve;
        if (_hasCurve != (curve != null) ||
            _preWrapMode != (curve?.preWrapMode ?? WrapMode.Default) ||
            _postWrapMode != (curve?.postWrapMode ?? WrapMode.Default)) return false;
        var keys = curve?.keys ?? Array.Empty<Keyframe>();
        if (_keys.Length != keys.Length) return false;
        for (int index = 0; index < keys.Length; index++)
          if (!KeyframeEquals(_keys[index], keys[index])) return false;
        return true;
      }

      private static bool KeyframeEquals(Keyframe left, Keyframe right) =>
        left.time.Equals(right.time) && left.value.Equals(right.value) &&
        left.inTangent.Equals(right.inTangent) && left.outTangent.Equals(right.outTangent) &&
        left.inWeight.Equals(right.inWeight) && left.outWeight.Equals(right.outWeight) &&
        left.weightedMode == right.weightedMode && left.tangentMode == right.tangentMode;
    }

    internal static float[] CreateIdentityCurveLut()
    {
      var result = new float[ColorCurveSamples * 8];
      // Master/R/G/B and Hue-vs-Hue map an input coordinate to an output
      // coordinate, so their inactive value is the identity ramp. The three
      // saturation modifier curves are multiplicative and therefore inactive
      // at one.
      for (int curve = 0; curve < 5; curve++)
      for (int index = 0; index < ColorCurveSamples; index++)
        result[curve * ColorCurveSamples + index] = index / (float)(ColorCurveSamples - 1);
      for (int curve = 5; curve < 8; curve++)
      for (int index = 0; index < ColorCurveSamples; index++)
        result[curve * ColorCurveSamples + index] = 1f;
      return result;
    }

    internal static float[] BakeColorCurves(ColorCurves? curves)
    {
      if (curves == null) return s_IdentityCurveLut;
      TextureCurve?[] source =
      {
        curves.master.value, curves.red.value, curves.green.value, curves.blue.value,
        curves.hueVsHue.value, curves.hueVsSat.value, curves.satVsSat.value, curves.lumVsSat.value
      };
      var cache = s_ColorCurveLutCache.GetValue(curves, _ => new ColorCurveLutCache());
      if (cache.Matches(source)) return cache.lut!;

      var result = CreateIdentityCurveLut();
      for (int index = 0; index < source.Length; index++)
        BakeCurve(result, index, source[index]);
      cache.Store(source, result);
      return result;
    }

    private static void BakeCurve(float[] destination, int curveIndex, TextureCurve? curve)
    {
      if (curve == null) return;
      int offset = curveIndex * ColorCurveSamples;
      for (int index = 0; index < ColorCurveSamples; index++)
        destination[offset + index] = curve.Evaluate(index / (float)(ColorCurveSamples - 1));
    }

    public static bool ProcessSoftFrame(float[] rgbaHdr, int w, int h, float[] rgbaOut, bool hdr10 = false)
    {
      if (rgbaHdr == null || rgbaOut == null) return false;
      var g = LastGrade;
      if (g.curveLut == null || g.curveLut.Length != ColorCurveSamples * 8)
        g.curveLut = CreateIdentityCurveLut();
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
