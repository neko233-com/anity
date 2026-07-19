using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace UnityEngine.Rendering.Universal
{
  public abstract class ScriptableRendererData : ScriptableObject
  {
    [SerializeField] internal List<ScriptableRendererFeature> m_RendererFeatures = new List<ScriptableRendererFeature>();
    [SerializeField] internal List<long> m_RendererFeatureMap = new List<long>();
    [SerializeField] internal bool m_UseNativeRenderPass;

    public List<ScriptableRendererFeature> rendererFeatures => m_RendererFeatures;
    public bool useNativeRenderPass => m_UseNativeRenderPass;

    internal ScriptableRenderer InternalCreateRenderer()
    {
      var renderer = Create();
      if (renderer != null)
      {
        foreach (var feature in m_RendererFeatures)
        {
          feature?.Create();
          renderer.rendererFeatures.Add(feature);
        }
      }
      return renderer;
    }

    protected abstract ScriptableRenderer Create();

    protected virtual void OnValidate() { }
    protected virtual void Reset() { }
  }

  public abstract class ScriptableRendererFeature : IDisposable
  {
    internal bool isActive { get; set; } = true;

    public abstract void Create();
    public abstract void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData);

    public virtual void SetupRenderPasses(ScriptableRenderer renderer, in RenderingData renderingData) { }

    public virtual void OnCameraPreCull(ScriptableRenderer renderer, in CameraData cameraData) { }
    public virtual void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData) { }
    public virtual void OnCameraCleanup(CommandBuffer cmd) { }

    public void Dispose()
    {
      Dispose(true);
      GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing) { }
  }

  public abstract class ScriptableRenderer : IDisposable
  {
    private RenderTargetIdentifier m_ColorTarget;
    private RenderTargetIdentifier m_DepthTarget;
    private bool m_HasCameraTarget;

    public List<ScriptableRenderPass> activeRenderPassQueue { get; } = new List<ScriptableRenderPass>();
    public List<ScriptableRendererFeature> rendererFeatures { get; } = new List<ScriptableRendererFeature>();
    public RenderTargetIdentifier cameraColorTarget => m_ColorTarget;
    public RenderTargetIdentifier cameraDepthTarget => m_DepthTarget;

    protected ScriptableRenderer() { }

    public virtual void SetupCullingParameters(ref ScriptableCullingParameters cullingParameters, ref CameraData cameraData) { }

    public virtual void Setup(ScriptableRenderContext context, ref RenderingData renderingData)
    {
      // A renderer instance is reused for every camera.  The active queue is
      // intentionally per-camera state: retaining passes after Execute makes
      // renderer features run against a later camera, while retaining only the
      // constructor-time passes makes every camera after the first miss URP's
      // opaque/transparent work entirely.
      activeRenderPassQueue.Clear();
      // RenderingData is also constructed directly by custom renderer tests
      // and integrations.  A default RenderTargetIdentifier represents None,
      // while Unity's implicit camera target is BuiltinRenderTextureType.CameraTarget.
      var target = renderingData.cameraData.targetTexture;
      if (target.m_NameID == (int)BuiltinRenderTextureType.None && target.m_BufferPtr == IntPtr.Zero)
        target = BuiltinRenderTextureType.CameraTarget;
      ConfigureCameraTarget(target, target);

      // Ensure default URP post-process feature for HDR / Volume stack (Unity 2022 Pro parity)
      EnsureDefaultPostProcessFeature();

      foreach (var feature in rendererFeatures)
      {
        if (feature != null && feature.isActive)
        {
          feature.AddRenderPasses(this, ref renderingData);
          feature.SetupRenderPasses(this, in renderingData);
        }
      }

      UpdateCameraInputRequirements(ref renderingData);
      if (renderingData.cameraData.requiresOpaqueTexture)
        EnqueuePass(new CameraOpaqueTexturePass());
      if (renderingData.cameraData.requiresDepthTexture)
        EnqueuePass(new CameraDepthTexturePass());
      if (renderingData.cameraData.requiresNormalsTexture)
        EnqueuePass(new CameraNormalsTexturePass());
      if (renderingData.cameraData.requiresMotionVectors)
        EnqueuePass(new CameraMotionVectorsTexturePass());
    }

    private void EnsureDefaultPostProcessFeature()
    {
      for (int i = 0; i < rendererFeatures.Count; i++)
      {
        if (rendererFeatures[i] is PostProcessRendererFeature)
          return;
      }
      var f = new PostProcessRendererFeature();
      f.Create();
      rendererFeatures.Add(f);
    }

    public virtual void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
      ExecuteRenderPassQueue(context, ref renderingData, drawSkybox: true);
    }

    /// <summary>
    /// Executes the per-camera pass queue with Unity-style event ordering and
    /// a guaranteed cleanup path.  Derived renderers use this instead of
    /// iterating <see cref="activeRenderPassQueue"/> directly.
    /// </summary>
    protected void ExecuteRenderPassQueue(ScriptableRenderContext context, ref RenderingData renderingData, bool drawSkybox)
    {
      SortStableByRenderPassEvent();

      var camera = renderingData.cameraData.camera;
      int setupPassCount = 0;
      int setupFeatureCount = 0;
      try
      {
        for (int i = 0; i < rendererFeatures.Count; i++)
        {
          var feature = rendererFeatures[i];
          if (feature != null && feature.isActive)
          {
            setupFeatureCount = i + 1;
            feature.OnCameraSetup(null, ref renderingData);
          }
        }

        if (drawSkybox && camera != null && camera.clearFlags == CameraClearFlags.Skybox)
          context.DrawSkybox(camera);

        for (int i = 0; i < activeRenderPassQueue.Count; i++)
        {
          var pass = activeRenderPassQueue[i];
          if (pass == null)
            continue;

          // Mark before invoking either setup callback so partial setup is
          // still cleaned up when a renderer feature or pass throws.
          setupPassCount = i + 1;
          pass.Configure(null, renderingData.cameraData.cameraTargetDescriptor);
          pass.OnCameraSetup(null, ref renderingData);
          pass.Execute(context, ref renderingData);
        }
      }
      finally
      {
        for (int i = setupPassCount - 1; i >= 0; i--)
        {
          activeRenderPassQueue[i]?.OnCameraCleanup(null);
        }

        for (int i = setupFeatureCount - 1; i >= 0; i--)
        {
          var feature = rendererFeatures[i];
          if (feature != null && feature.isActive)
            feature.OnCameraCleanup(null);
        }

        activeRenderPassQueue.Clear();
      }
    }

    private void SortStableByRenderPassEvent()
    {
      // List.Sort is not stable.  Renderer features are allowed to enqueue
      // multiple passes at the same event, so preserve enqueue order for ties.
      for (int i = 1; i < activeRenderPassQueue.Count; i++)
      {
        var current = activeRenderPassQueue[i];
        int eventValue = (int)(current?.renderPassEvent ?? RenderPassEvent.BeforeRendering);
        int j = i - 1;
        while (j >= 0 && (int)(activeRenderPassQueue[j]?.renderPassEvent ?? RenderPassEvent.BeforeRendering) > eventValue)
        {
          activeRenderPassQueue[j + 1] = activeRenderPassQueue[j];
          j--;
        }
        activeRenderPassQueue[j + 1] = current;
      }
    }

    protected void DrawOpaquePass(ScriptableRenderContext context, ref RenderingData renderingData)
    {
      var sortingSettings = new SortingSettings(renderingData.cameraData.camera)
      {
        criteria = SortingCriteria.CommonOpaque
      };
      var drawOpaqueSettings = new DrawingSettings(new ShaderTagId("SRPDefaultUnlit"), sortingSettings)
      {
        enableDynamicBatching = renderingData.supportsDynamicBatching,
        enableInstancing = renderingData.supportsInstancing
      };
      drawOpaqueSettings.SetShaderPassName(1, new ShaderTagId("UniversalForward"));

      var opaqueFilter = new FilteringSettings(RenderQueueRange.opaque);
      context.DrawRenderers(renderingData.cullResults, ref drawOpaqueSettings, ref opaqueFilter);
    }

    protected void DrawTransparentPass(ScriptableRenderContext context, ref RenderingData renderingData)
    {
      var sortingSettings = new SortingSettings(renderingData.cameraData.camera)
      {
        criteria = SortingCriteria.CommonTransparent
      };
      var drawTransparentSettings = new DrawingSettings(new ShaderTagId("SRPDefaultUnlit"), sortingSettings)
      {
        enableDynamicBatching = renderingData.supportsDynamicBatching,
        enableInstancing = renderingData.supportsInstancing
      };
      drawTransparentSettings.SetShaderPassName(1, new ShaderTagId("UniversalForward"));

      var transparentFilter = new FilteringSettings(RenderQueueRange.transparent);
      context.DrawRenderers(renderingData.cullResults, ref drawTransparentSettings, ref transparentFilter);
    }

    /// <summary>
    /// Reduces the per-pass input declarations into the per-camera resource
    /// contract.  Features are allowed to enqueue/configure passes during
    /// SetupRenderPasses, so this intentionally runs after that callback.
    /// Derived renderers call it again after adding their built-in passes.
    /// </summary>
    protected void UpdateCameraInputRequirements(ref RenderingData renderingData)
    {
      var cameraData = renderingData.cameraData;
      foreach (var pass in activeRenderPassQueue)
      {
        if (pass == null)
          continue;

        var input = pass.input;
        cameraData.requiresDepthTexture |= (input & ScriptableRenderPassInput.Depth) != 0;
        cameraData.requiresOpaqueTexture |= (input & ScriptableRenderPassInput.Color) != 0;
        cameraData.requiresNormalsTexture |= (input & ScriptableRenderPassInput.Normal) != 0;
        cameraData.requiresMotionVectors |= (input & ScriptableRenderPassInput.Motion) != 0;
      }
      renderingData.cameraData = cameraData;
    }

    public void ConfigureCameraTarget(RenderTargetIdentifier colorTarget, RenderTargetIdentifier depthTarget)
    {
      m_ColorTarget = colorTarget;
      m_DepthTarget = depthTarget;
      m_HasCameraTarget = true;

      var cmd = CommandBufferPool.Get("Set Camera Target");
      cmd.SetRenderTarget(colorTarget, depthTarget);
      Graphics.ExecuteCommandBuffer(cmd);
      CommandBufferPool.Release(cmd);
    }

    public void EnqueuePass(ScriptableRenderPass pass)
    {
      if (pass != null)
        activeRenderPassQueue.Add(pass);
    }

    public virtual void FinishRendering(CommandBuffer cmd) { }

    public virtual void Dispose()
    {
      foreach (var feature in rendererFeatures)
      {
        feature?.Dispose();
      }
      rendererFeatures.Clear();
      activeRenderPassQueue.Clear();
    }
  }

  public abstract class ScriptableRenderPass
  {
    private ScriptableRenderPassInput m_Input;

    public RenderPassEvent renderPassEvent { get; set; }
    public ScriptableRenderPassInput input => m_Input;
    public RenderTargetIdentifier[] colorAttachments { get; set; }
    public RenderTargetIdentifier colorAttachment { get; set; }
    public RenderTargetIdentifier depthAttachment { get; set; }
    public ClearFlag clearFlag { get; set; }
    public Color clearColor { get; set; }
    public bool overrideCameraTarget { get; set; }
    public bool isMainPass { get; set; }

    protected ScriptableRenderPass()
    {
      clearFlag = ClearFlag.None;
      clearColor = Color.black;
    }

    public abstract void Execute(ScriptableRenderContext context, ref RenderingData renderingData);

    public virtual void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData) { }
    public virtual void OnCameraCleanup(CommandBuffer cmd) { }
    public virtual void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor) { }
    public virtual void ConfigureTarget(RenderTargetIdentifier colorAttachment, RenderTargetIdentifier depthAttachment)
    {
      this.colorAttachment = colorAttachment;
      this.depthAttachment = depthAttachment;
      overrideCameraTarget = true;
    }
    public virtual void ConfigureTarget(RenderTargetIdentifier[] colorAttachments, RenderTargetIdentifier depthAttachment)
    {
      this.colorAttachments = colorAttachments;
      this.depthAttachment = depthAttachment;
      overrideCameraTarget = true;
    }
    public virtual void ConfigureClear(ClearFlag clearFlag, Color clearColor)
    {
      this.clearFlag = clearFlag;
      this.clearColor = clearColor;
    }
    protected void ConfigureInput(ScriptableRenderPassInput passInput)
    {
      m_Input = passInput;
    }
  }

  internal static class XrTransientTextureUtility
  {
    internal static void ConfigureDescriptor(ref RenderTextureDescriptor descriptor, in CameraData cameraData)
    {
      if (!cameraData.isSinglePassInstanced)
        return;
      descriptor.dimension = UnityEngine.TextureDimension.Tex2DArray;
      descriptor.volumeDepth = cameraData.xrEyeCount;
    }

    internal static bool CopyAllEyeSlices(in CameraData cameraData, Func<int, int, bool> copy)
    {
      int eyeCount = cameraData.isSinglePassInstanced ? cameraData.xrEyeCount : 1;
      for (int eye = 0; eye < eyeCount; eye++)
      {
        int slice = cameraData.xrDepthSlice + eye;
        if (!copy(slice, slice))
          return false;
      }
      return true;
    }
  }

  /// <summary>
  /// Captures the resolved camera color target immediately after opaque work.
  /// The native backend performs this as a GPU copy; a missing backend path
  /// intentionally leaves the global unset rather than publishing a blank
  /// managed texture as if it contained the scene.
  /// </summary>
  internal sealed class CameraOpaqueTexturePass : ScriptableRenderPass
  {
    internal const string GlobalTextureName = "_CameraOpaqueTexture";
    private RenderTexture? m_Texture;

    public CameraOpaqueTexturePass()
    {
      renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
    }

    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
      var cameraData = renderingData.cameraData;
      if (!cameraData.requiresOpaqueTexture)
        return;

      var descriptor = cameraData.cameraTargetDescriptor;
      if (descriptor.width <= 0 || descriptor.height <= 0)
        return;
      descriptor.depthBufferBits = 0;
      descriptor.msaaSamples = 1;
      descriptor.useMipMap = false;
      descriptor.autoGenerateMips = false;
      descriptor.enableRandomWrite = false;
      XrTransientTextureUtility.ConfigureDescriptor(ref descriptor, in cameraData);

      var texture = RenderTexture.GetTemporary(descriptor);
      texture.filterMode = FilterMode.Bilinear;
      texture.wrapMode = UnityEngine.TextureWrapMode.Clamp;
      var device = Anity.Core.Runtime.Native.NativeGraphicsDevice.Current;
      bool sourceIsCameraTarget = cameraData.nativeTargetTexture == null;
      if (device?.EnsureCameraRenderTarget(texture) != true ||
          !XrTransientTextureUtility.CopyAllEyeSlices(in cameraData, (sourceSlice, destinationSlice) =>
              device.TryCopyCameraRenderTargetColor(cameraData.nativeTargetTexture, sourceIsCameraTarget, texture,
                sourceSlice, destinationSlice)))
      {
        RenderTexture.ReleaseTemporary(texture);
        return;
      }

      m_Texture = texture;
      cameraData.opaqueTexture = texture;
      renderingData.cameraData = cameraData;
      Shader.SetGlobalTexture(GlobalTextureName, texture);
    }

    public override void OnCameraCleanup(CommandBuffer cmd)
    {
      if (m_Texture == null)
        return;
      if (ReferenceEquals(Shader.GetGlobalTexture(GlobalTextureName), m_Texture))
        Shader.SetGlobalTexture(GlobalTextureName, null);
      RenderTexture.ReleaseTemporary(m_Texture);
      m_Texture = null;
    }
  }

  /// <summary>Publishes a GPU-produced linear depth texture in R after opaque rendering.</summary>
  internal sealed class CameraDepthTexturePass : ScriptableRenderPass
  {
    internal const string GlobalTextureName = "_CameraDepthTexture";
    private RenderTexture? m_Texture;

    public CameraDepthTexturePass()
    {
      renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
    }

    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
      var cameraData = renderingData.cameraData;
      if (!cameraData.requiresDepthTexture)
        return;

      var descriptor = cameraData.cameraTargetDescriptor;
      if (descriptor.width <= 0 || descriptor.height <= 0)
        return;
      descriptor.depthBufferBits = 0;
      descriptor.msaaSamples = 1;
      // _CameraDepthTexture is a single-channel semantic carried in R. Keep a
      // concrete non-HDR RGBA8 storage target so Vulkan's compute conversion
      // has a portable writable image format just like the Metal path.
      descriptor.colorFormat = RenderTextureFormat.ARGB32;
      descriptor.useMipMap = false;
      descriptor.autoGenerateMips = false;
      descriptor.enableRandomWrite = true;
      XrTransientTextureUtility.ConfigureDescriptor(ref descriptor, in cameraData);

      var texture = RenderTexture.GetTemporary(descriptor);
      texture.filterMode = FilterMode.Point;
      texture.wrapMode = UnityEngine.TextureWrapMode.Clamp;
      var device = Anity.Core.Runtime.Native.NativeGraphicsDevice.Current;
      bool sourceIsCameraTarget = cameraData.nativeTargetTexture == null;
      if (device?.EnsureCameraRenderTarget(texture) != true ||
          !XrTransientTextureUtility.CopyAllEyeSlices(in cameraData, (sourceSlice, destinationSlice) =>
              device.TryCopyCameraRenderTargetDepthToColor(cameraData.nativeTargetTexture, sourceIsCameraTarget, texture,
                sourceSlice, destinationSlice)))
      {
        RenderTexture.ReleaseTemporary(texture);
        return;
      }

      m_Texture = texture;
      cameraData.depthTexture = texture;
      renderingData.cameraData = cameraData;
      Shader.SetGlobalTexture(GlobalTextureName, texture);
    }

    public override void OnCameraCleanup(CommandBuffer cmd)
    {
      if (m_Texture == null)
        return;
      if (ReferenceEquals(Shader.GetGlobalTexture(GlobalTextureName), m_Texture))
        Shader.SetGlobalTexture(GlobalTextureName, null);
      RenderTexture.ReleaseTemporary(m_Texture);
      m_Texture = null;
    }
  }

  /// <summary>Publishes the native opaque mesh-normal attachment for URP passes.</summary>
  internal sealed class CameraNormalsTexturePass : ScriptableRenderPass
  {
    internal const string GlobalTextureName = "_CameraNormalsTexture";
    private RenderTexture? m_Texture;

    public CameraNormalsTexturePass()
    {
      renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
    }

    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
      var cameraData = renderingData.cameraData;
      if (!cameraData.requiresNormalsTexture)
        return;
      var descriptor = cameraData.cameraTargetDescriptor;
      if (descriptor.width <= 0 || descriptor.height <= 0)
        return;
      descriptor.depthBufferBits = 0;
      descriptor.msaaSamples = 1;
      descriptor.colorFormat = RenderTextureFormat.ARGB32;
      descriptor.graphicsFormat = GraphicsFormat.R8G8B8A8_SNorm;
      descriptor.useMipMap = false;
      descriptor.autoGenerateMips = false;
      descriptor.enableRandomWrite = false;
      XrTransientTextureUtility.ConfigureDescriptor(ref descriptor, in cameraData);

      var texture = RenderTexture.GetTemporary(descriptor);
      texture.filterMode = FilterMode.Point;
      texture.wrapMode = UnityEngine.TextureWrapMode.Clamp;
      var device = Anity.Core.Runtime.Native.NativeGraphicsDevice.Current;
      bool sourceIsCameraTarget = cameraData.nativeTargetTexture == null;
      if (device?.EnsureCameraRenderTarget(texture) != true ||
          !XrTransientTextureUtility.CopyAllEyeSlices(in cameraData, (sourceSlice, destinationSlice) =>
              device.TryCopyCameraRenderTargetNormalsToColor(cameraData.nativeTargetTexture, sourceIsCameraTarget, texture,
                sourceSlice, destinationSlice)))
      {
        RenderTexture.ReleaseTemporary(texture);
        return;
      }

      m_Texture = texture;
      cameraData.normalsTexture = texture;
      renderingData.cameraData = cameraData;
      Shader.SetGlobalTexture(GlobalTextureName, texture);
    }

    public override void OnCameraCleanup(CommandBuffer cmd)
    {
      if (m_Texture == null)
        return;
      if (ReferenceEquals(Shader.GetGlobalTexture(GlobalTextureName), m_Texture))
        Shader.SetGlobalTexture(GlobalTextureName, null);
      RenderTexture.ReleaseTemporary(m_Texture);
      m_Texture = null;
    }
  }

  /// <summary>Publishes the native RG16Float per-pixel motion-vector attachment.</summary>
  internal sealed class CameraMotionVectorsTexturePass : ScriptableRenderPass
  {
    internal const string GlobalTextureName = "_MotionVectorTexture";
    private RenderTexture? m_Texture;

    public CameraMotionVectorsTexturePass()
    {
      renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
    }

    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
      var cameraData = renderingData.cameraData;
      if (!cameraData.requiresMotionVectors)
        return;
      var descriptor = cameraData.cameraTargetDescriptor;
      if (descriptor.width <= 0 || descriptor.height <= 0)
        return;
      descriptor.depthBufferBits = 0;
      descriptor.msaaSamples = 1;
      descriptor.colorFormat = RenderTextureFormat.RGHalf;
      descriptor.graphicsFormat = GraphicsFormat.R16G16_SFloat;
      descriptor.useMipMap = false;
      descriptor.autoGenerateMips = false;
      descriptor.enableRandomWrite = false;
      XrTransientTextureUtility.ConfigureDescriptor(ref descriptor, in cameraData);

      var texture = RenderTexture.GetTemporary(descriptor);
      texture.filterMode = FilterMode.Point;
      texture.wrapMode = UnityEngine.TextureWrapMode.Clamp;
      var device = Anity.Core.Runtime.Native.NativeGraphicsDevice.Current;
      bool sourceIsCameraTarget = cameraData.nativeTargetTexture == null;
      if (device?.EnsureCameraRenderTarget(texture) != true ||
          !XrTransientTextureUtility.CopyAllEyeSlices(in cameraData, (sourceSlice, destinationSlice) =>
              device.TryCopyCameraRenderTargetMotionToColor(cameraData.nativeTargetTexture, sourceIsCameraTarget, texture,
                sourceSlice, destinationSlice)))
      {
        RenderTexture.ReleaseTemporary(texture);
        return;
      }

      m_Texture = texture;
      cameraData.motionVectorTexture = texture;
      renderingData.cameraData = cameraData;
      Shader.SetGlobalTexture(GlobalTextureName, texture);
    }

    public override void OnCameraCleanup(CommandBuffer cmd)
    {
      if (m_Texture == null)
        return;
      if (ReferenceEquals(Shader.GetGlobalTexture(GlobalTextureName), m_Texture))
        Shader.SetGlobalTexture(GlobalTextureName, null);
      RenderTexture.ReleaseTemporary(m_Texture);
      m_Texture = null;
    }
  }

  [Flags]
  public enum ScriptableRenderPassInput
  {
    None = 0,
    Depth = 1 << 0,
    Normal = 1 << 1,
    Color = 1 << 2,
    Motion = 1 << 3
  }

  public class DrawObjectsPass : ScriptableRenderPass
  {
    private ShaderTagId[] m_ShaderTagIds;
    private bool m_IsOpaque;
    private FilteringSettings m_FilteringSettings;
    private SortingCriteria m_SortingCriteria;

    public DrawObjectsPass(string profilerTag, ShaderTagId[] shaderTagIds, bool opaque, RenderQueueRange renderQueueRange, uint layerMask)
    {
      m_ShaderTagIds = shaderTagIds;
      m_IsOpaque = opaque;
      m_FilteringSettings = new FilteringSettings(renderQueueRange, layerMask);
      m_SortingCriteria = opaque ? SortingCriteria.CommonOpaque : SortingCriteria.CommonTransparent;
      renderPassEvent = opaque ? RenderPassEvent.BeforeRenderingOpaques : RenderPassEvent.BeforeRenderingTransparents;
    }

    public DrawObjectsPass(string profilerTag, bool opaque, RenderQueueRange renderQueueRange, uint layerMask)
      : this(profilerTag, new[] { new ShaderTagId("SRPDefaultUnlit"), new ShaderTagId("UniversalForward") }, opaque, renderQueueRange, layerMask)
    {
    }

    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
      var sortingSettings = new SortingSettings(renderingData.cameraData.camera)
      {
        criteria = m_SortingCriteria
      };
      var drawingSettings = new DrawingSettings(m_ShaderTagIds[0], sortingSettings)
      {
        enableDynamicBatching = renderingData.supportsDynamicBatching,
        enableInstancing = renderingData.supportsInstancing
      };

      for (int i = 1; i < m_ShaderTagIds.Length; i++)
      {
        drawingSettings.SetShaderPassName(i, m_ShaderTagIds[i]);
      }

      context.DrawRenderers(renderingData.cullResults, ref drawingSettings, ref m_FilteringSettings);
    }
  }

  public enum RenderPassEvent
  {
    BeforeRendering = 0,
    BeforeRenderingShadows = 50,
    AfterRenderingShadows = 100,
    BeforeRenderingPrePasses = 150,
    AfterRenderingPrePasses = 200,
    BeforeRenderingGbuffer = 210,
    AfterRenderingGbuffer = 220,
    BeforeRenderingDeferredLights = 230,
    AfterRenderingDeferredLights = 240,
    BeforeRenderingOpaques = 300,
    AfterRenderingOpaques = 400,
    BeforeRenderingSkybox = 450,
    AfterRenderingSkybox = 500,
    BeforeRenderingTransparents = 600,
    AfterRenderingTransparents = 700,
    BeforeRenderingPostProcessing = 750,
    AfterRenderingPostProcessing = 800,
    AfterRendering = 1000
  }
}
