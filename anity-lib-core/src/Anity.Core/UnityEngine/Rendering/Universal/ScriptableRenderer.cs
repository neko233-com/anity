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
      return Create();
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

  public abstract class ScriptableRenderer
  {
    protected ScriptableRenderer() { }

    public List<ScriptableRenderPass> activeRenderPassQueue { get; } = new List<ScriptableRenderPass>();
    public List<ScriptableRendererFeature> rendererFeatures { get; } = new List<ScriptableRendererFeature>();

    public virtual void SetupCullingParameters(ref ScriptableCullingParameters cullingParameters, ref CameraData cameraData) { }
    public virtual void Setup(ScriptableRenderContext context, ref RenderingData renderingData) { }
    public virtual void Execute(ScriptableRenderContext context, ref RenderingData renderingData) { }
    public virtual void FinishRendering(CommandBuffer cmd) { }

    public void EnqueuePass(ScriptableRenderPass pass)
    {
      if (pass != null)
        activeRenderPassQueue.Add(pass);
    }

    public virtual void Dispose() { }
  }

  public abstract class ScriptableRenderPass
  {
    public RenderPassEvent renderPassEvent { get; set; }
    public string[] colorAttachmentIdentifiers { get; set; }
    public string depthAttachmentIdentifier { get; set; }
    public bool clearFlag { get; set; }
    public bool overrideCameraTarget { get; set; }
    public bool isMainPass { get; set; }

    public ScriptableRenderPass() { }

    public abstract void Execute(ScriptableRenderContext context, ref RenderingData renderingData);

    public virtual void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData) { }
    public virtual void OnCameraCleanup(CommandBuffer cmd) { }
    public virtual void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor) { }
    public virtual void ConfigureTarget(RenderTargetIdentifier colorAttachment, RenderTargetIdentifier depthAttachment) { }
    public virtual void ConfigureClear(ClearFlag clearFlag, Color clearColor) { }
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
