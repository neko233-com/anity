using System;
using System.Collections.Generic;

namespace UnityEngine.Rendering;

public enum BuiltinRenderTextureType
{
    PropertyName = -4,
    BufferPtr = -3,
    RenderTexture = -2,
    BindableTexture = -1,
    None = 0,
    CurrentActive = 1,
    CameraTarget = 2,
    Depth = 3,
    DepthNormals = 4,
    ResolvedDepth = 5,
    GBuffer0 = 10,
    GBuffer1 = 11,
    GBuffer2 = 12,
    GBuffer3 = 13,
    GBuffer4 = 14,
    GBuffer5 = 15,
    GBuffer6 = 16,
    GBuffer7 = 17,
}

public enum RenderBufferLoadAction
{
    Load = 0,
    Clear = 1,
    DontCare = 2,
}

public enum RenderBufferStoreAction
{
    Store = 0,
    Resolve = 1,
    StoreAndResolve = 2,
    DontCare = 3,
}

public enum CullingOptions
{
    None = 0,
    ForceEvenIfCameraIsNotActive = 1,
    OcclusionCull = 2,
    NearestPortal = 4,
}

[Flags]
public enum SortingCriteria
{
    None = 0,
    SortByLayer = 0x20,
    SortByQueue = 0x40,
    SortByRendererPriority = 0x80,
}

[Flags]
public enum PerObjectData
{
    None = 0,
    LightProbe = 1,
    ReflectionProbes = 2,
    LightProbeProxyVolume = 4,
    Lightmaps = 8,
    LightData = 16,
    MotionVectors = 32,
    LightIndices = 64,
    ReflectionProbeData = 128,
    OcclusionProbe = 256,
    OcclusionProbeProxyVolume = 512,
    ShadowMask = 1024
}

public struct CullingParameters
{
    public Matrix4x4 cullingMatrix;
    public Vector3 worldOrigin;
    public bool stereoProjectionMatrix;
    public Vector3 lodParameters;
    public int cullingMask;
    public float shadowDistance;
    public bool conservative;
    public Vector4[] shadowCascadeDistances;
    public CullingOptions cullingOptions;
    public float layerCullDistances;
}

public struct VisibleLight
{
    public Light light;
    public LightType lightType;
    public Color finalColor;
    public Vector3 localToWorldMatrix0;
    public Vector3 localToWorldMatrix1;
    public Vector3 localToWorldMatrix2;
    public float range;
    public float spotAngle;
}

public struct VisibleReflectionProbe
{
    public ReflectionProbe probe;
    public Bounds bounds;
    public float blendDistance;
    public int importance;
    public int boxProjection;
}

public struct ShadowDrawingSettings
{
    public CullingResults cullingResults;
    public int lightIndex;
    public ShadowSplitData splitData;

    public ShadowDrawingSettings(CullingResults cullingResults, int lightIndex)
    {
        this.cullingResults = cullingResults;
        this.lightIndex = lightIndex;
        splitData = default;
    }
}

public struct ShadowSplitData
{
    public Vector4 cullingSphere;
    public float shadowCascadeBlendCullingFactor;
}

public enum CullMode { Off = 0, Front = 1, Back = 2 }
public enum CompareFunction { Disabled = 0, Never = 1, Less = 2, Equal = 3, LessEqual = 4, Greater = 5, NotEqual = 6, GreaterEqual = 7, Always = 8 }

public struct BlendState { public bool separateMRTBlend; }
public struct RasterState { public CullMode cullingMode; public float depthBias; }
public struct DepthState { public bool writeEnabled; public CompareFunction compareFunction; }
public struct StencilState { public bool enabled; public byte readMask; public byte writeMask; }

[Flags]
public enum RenderStateMask
{
    Nothing = 0,
    Blend = 1,
    Raster = 2,
    Depth = 4,
    Stencil = 8,
    Everything = 15,
}

public struct RenderStateBlock
{
    public RenderStateMask mask;
    public BlendState blendState;
    public RasterState rasterState;
    public DepthState depthState;
    public StencilState stencilState;
    public int stencilReference;
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
        CommandBufferPool.Release(cmd);
        ExecuteRender(context, camera);
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
