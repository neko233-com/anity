using System;
using System.Collections.Generic;

namespace UnityEngine.Rendering;

public struct ShadowDrawingSettings
{
    public CullingResults cullingResults;
    public int lightIndex;
    public ShadowSplitData splitData;
    public bool useRenderingLayerMaskTest;
    public PerObjectData perObjectData;

    public ShadowDrawingSettings(CullingResults cullingResults, int lightIndex)
    {
        this.cullingResults = cullingResults;
        this.lightIndex = lightIndex;
        splitData = default;
        useRenderingLayerMaskTest = false;
        perObjectData = PerObjectData.None;
    }

    public ShadowDrawingSettings(CullingResults cullingResults, int lightIndex, ShadowSplitData splitData)
    {
        this.cullingResults = cullingResults;
        this.lightIndex = lightIndex;
        this.splitData = splitData;
        useRenderingLayerMaskTest = false;
        perObjectData = PerObjectData.None;
    }
}

public struct ShadowSplitData
{
    public Vector4 cullingSphere;
    public float shadowCascadeBlendCullingFactor;
}

public struct RenderStateBlock
{
    public RenderStateMask mask;
    public BlendState blendState;
    public RasterState rasterState;
    public DepthState depthState;
    public StencilState stencilState;
    public int stencilReference;

    public RenderStateBlock(RenderStateMask mask)
    {
        this.mask = mask;
        blendState = BlendState.Opaque;
        rasterState = RasterState.Default;
        depthState = DepthState.Default;
        stencilState = StencilState.Default;
        stencilReference = 0;
    }
}

public abstract class RenderPipeline : IDisposable
{
    private bool _disposed;

    public void Render(ScriptableRenderContext context, Camera[] cameras)
    {
        if (cameras == null) return;
        RenderPipelineManager.InvokeBeginFrameRendering(context);
        for (int i = 0; i < cameras.Length; i++)
        {
            var cam = cameras[i];
            if (cam == null) continue;
            RenderPipelineManager.InvokeBeginCameraRendering(context, new[] { cam });
            RenderSingleCamera(context, cam);
            RenderPipelineManager.InvokeEndCameraRendering(context, new[] { cam });
        }
        RenderPipelineManager.InvokeEndFrameRendering(context);
        context.Submit();
    }

    public void Render(ScriptableRenderContext context, List<Camera> cameras)
    {
        if (cameras == null) return;
        RenderPipelineManager.InvokeBeginFrameRendering(context);
        for (int i = 0; i < cameras.Count; i++)
        {
            var cam = cameras[i];
            if (cam == null) continue;
            RenderPipelineManager.InvokeBeginCameraRendering(context, new[] { cam });
            RenderSingleCamera(context, cam);
            RenderPipelineManager.InvokeEndCameraRendering(context, new[] { cam });
        }
        RenderPipelineManager.InvokeEndFrameRendering(context);
        context.Submit();
    }

    private void RenderSingleCamera(ScriptableRenderContext context, Camera camera)
    {
        var cmd = CommandBufferPool.Get("Camera.Render");
        cmd.ClearRenderTarget(true, true, camera.backgroundColor);
        context.ExecuteCommandBuffer(cmd);
        ExecuteRender(context, camera);
        // Native Planar VFX is an immediate transparent pass in the first
        // production subset. Submit it after the recorded camera clear and
        // scene work so the framebuffer is not cleared over the particles.
        UnityEngine.VFX.VFXManager.ProcessCameraCommandFromRenderLoop(camera, cmd);
        CommandBufferPool.Release(cmd);
    }

    protected abstract void ExecuteRender(ScriptableRenderContext context, Camera camera);

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;
        _disposed = true;
    }
}

public abstract class RenderPipelineAsset
{
    protected abstract RenderPipeline CreatePipeline();
    internal RenderPipeline InternalCreatePipeline() => CreatePipeline();
    public virtual Type pipelineType => GetType();
    public virtual string[] renderingLayerMaskNames => null;
    public virtual Material defaultMaterial => null;
    public virtual Shader defaultShader => null;
    public virtual Material defaultParticleMaterial => null;
    public virtual Material defaultTerrainMaterial => null;
    public virtual Material defaultUIMaterial => null;
    public virtual Material defaultUIOverdrawMaterial => null;
    public virtual Material defaultUIETC1SupportedMaterial => null;
    public virtual Material default2DMaterial => null;
    public virtual Material default2DMaskMaterial => null;
    public virtual Texture2D defaultLineMaterial => null;
    public virtual Type[] terrainBrushTypes => null;
    public virtual Type[] terrainDetailRenderPrototypes => null;
    public virtual Shader autodeskInteractiveShader => null;
    public virtual Shader autodeskInteractiveTransparentShader => null;
    public virtual Shader autodeskInteractiveMaskedShader => null;
    public virtual Shader terrainDetailLitShader => null;
    public virtual Shader terrainDetailGrassShader => null;
    public virtual Shader terrainDetailGrassBillboardShader => null;
}

public static class CommandBufferPool
{
    private static readonly Stack<CommandBuffer> _pool = new();
    private static readonly Dictionary<string, Stack<CommandBuffer>> _namedPools = new();

    public static CommandBuffer Get(string name = null)
    {
        CommandBuffer cmd;
        if (name != null)
        {
            if (_namedPools.TryGetValue(name, out var stack) && stack.Count > 0)
            {
                cmd = stack.Pop();
            }
            else
            {
                cmd = new CommandBuffer { name = name };
            }
        }
        else
        {
            cmd = _pool.Count > 0 ? _pool.Pop() : new CommandBuffer();
        }
        cmd.Clear();
        return cmd;
    }

    public static void Release(CommandBuffer buffer)
    {
        if (buffer == null) return;
        buffer.Clear();
        if (string.IsNullOrEmpty(buffer.name))
            _pool.Push(buffer);
        else
        {
            if (!_namedPools.TryGetValue(buffer.name, out var stack))
            {
                stack = new Stack<CommandBuffer>();
                _namedPools[buffer.name] = stack;
            }
            stack.Push(buffer);
        }
    }
}
