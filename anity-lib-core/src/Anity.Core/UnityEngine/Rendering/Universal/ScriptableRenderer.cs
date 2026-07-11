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
      ConfigureCameraTarget(BuiltinRenderTextureType.CameraTarget, BuiltinRenderTextureType.CameraTarget);

      foreach (var feature in rendererFeatures)
      {
        if (feature != null && feature.isActive)
          feature.AddRenderPasses(this, ref renderingData);
      }
    }

    public virtual void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
      var camera = renderingData.cameraData.camera;

      if (camera.clearFlags == CameraClearFlags.Skybox)
      {
        context.DrawSkybox(camera);
      }

      DrawOpaquePass(context, ref renderingData);
      DrawTransparentPass(context, ref renderingData);

      foreach (var pass in activeRenderPassQueue)
      {
        pass?.Execute(context, ref renderingData);
      }
      activeRenderPassQueue.Clear();
    }

    protected void DrawOpaquePass(ScriptableRenderContext context, ref RenderingData renderingData)
    {
      var drawOpaqueSettings = new DrawingSettings(new ShaderTagId("SRPDefaultUnlit"), SortingCriteria.CommonOpaque)
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
      var drawTransparentSettings = new DrawingSettings(new ShaderTagId("SRPDefaultUnlit"), SortingCriteria.CommonTransparent)
      {
        enableDynamicBatching = renderingData.supportsDynamicBatching,
        enableInstancing = renderingData.supportsInstancing
      };
      drawTransparentSettings.SetShaderPassName(1, new ShaderTagId("UniversalForward"));

      var transparentFilter = new FilteringSettings(RenderQueueRange.transparent);
      context.DrawRenderers(renderingData.cullResults, ref drawTransparentSettings, ref transparentFilter);
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
    public RenderPassEvent renderPassEvent { get; set; }
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
  }

  public class DrawObjectsPass : ScriptableRenderPass
  {
    private ShaderTagId[] m_ShaderTagIds;
    private bool m_IsOpaque;
    private FilteringSettings m_FilteringSettings;
    private SortingCriteria m_SortingCriteria;

    public DrawObjectsPass(string profilerTag, ShaderTagId[] shaderTagIds, bool opaque, RenderQueueRange renderQueueRange, int layerMask)
    {
      m_ShaderTagIds = shaderTagIds;
      m_IsOpaque = opaque;
      m_FilteringSettings = new FilteringSettings(renderQueueRange, layerMask);
      m_SortingCriteria = opaque ? SortingCriteria.CommonOpaque : SortingCriteria.CommonTransparent;
      renderPassEvent = opaque ? RenderPassEvent.BeforeRenderingOpaques : RenderPassEvent.BeforeRenderingTransparents;
    }

    public DrawObjectsPass(string profilerTag, bool opaque, RenderQueueRange renderQueueRange, int layerMask)
      : this(profilerTag, new[] { new ShaderTagId("SRPDefaultUnlit"), new ShaderTagId("UniversalForward") }, opaque, renderQueueRange, layerMask)
    {
    }

    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
      var drawingSettings = new DrawingSettings(m_ShaderTagIds[0], m_SortingCriteria)
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
