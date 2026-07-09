using System;

namespace UnityEngine.Rendering;

/// <summary>
/// Unity CommandBuffer for rendering commands.
/// </summary>
public class CommandBuffer : IDisposable
{
    private string _name = string.Empty;
    private bool _disposed;

    public string name
    {
        get => _name;
        set => _name = value ?? string.Empty;
    }

    public int sizeInBytes { get; }

    public void BeginSample(string name) { }
    public void EndSample(string name) { }
    public void Clear() { }
    public void Release() { }
    public void Dispose() { _disposed = true; }

    public void DrawRenderer(Renderer renderer, Material material) { }
    public void DrawRenderer(Renderer renderer, Material material, int submeshIndex) { }
    public void DrawRenderer(Renderer renderer, Material material, int submeshIndex, int shaderPass) { }

    public void DrawMesh(Mesh mesh, Matrix4x4 matrix, Material material) { }
    public void DrawMesh(Mesh mesh, Matrix4x4 matrix, Material material, int submeshIndex) { }
    public void DrawMesh(Mesh mesh, Matrix4x4 matrix, Material material, int submeshIndex, int shaderPass) { }
    public void DrawMesh(Mesh mesh, Matrix4x4 matrix, Material material, int submeshIndex, int shaderPass, MaterialPropertyBlock properties) { }

    public void DrawProcedural(Matrix4x4 matrix, Material material, ShaderPassName shaderPassName, MeshTopology topology, int vertexCount) { }
    public void DrawProcedural(Matrix4x4 matrix, Material material, ShaderPassName shaderPassName, MeshTopology topology, int vertexCount, int instanceCount) { }

    public void DrawMeshInstanced(Mesh mesh, int submeshIndex, Material material, Matrix4x4[] matrices) { }
    public void DrawMeshInstanced(Mesh mesh, int submeshIndex, Material material, Matrix4x4[] matrices, int count) { }
    public void DrawMeshInstanced(Mesh mesh, int submeshIndex, Material material, Matrix4x4[] matrices, int count, MaterialPropertyBlock properties) { }

    public void SetGlobalFloat(string name, float value) { }
    public void SetGlobalFloat(int nameID, float value) { }
    public void SetGlobalVector(string name, Vector4 value) { }
    public void SetGlobalVector(int nameID, Vector4 value) { }
    public void SetGlobalColor(string name, Color value) { }
    public void SetGlobalColor(int nameID, Color value) { }
    public void SetGlobalMatrix(string name, Matrix4x4 value) { }
    public void SetGlobalMatrix(int nameID, Matrix4x4 value) { }
    public void SetGlobalTexture(string name, RenderTargetIdentifier value) { }
    public void SetGlobalTexture(int nameID, RenderTargetIdentifier value) { }
    public void SetGlobalInt(string name, int value) { }
    public void SetGlobalInt(int nameID, int value) { }

    public void ClearRenderTarget(bool clearDepth, bool clearColor, Color backgroundColor) { }
    public void ClearRenderTarget(bool clearDepth, bool clearColor, Color backgroundColor, float depth) { }

    public void SetRenderTarget(RenderTargetIdentifier rt) { }
    public void SetRenderTarget(RenderTargetIdentifier color, RenderTargetIdentifier depth) { }
}

/// <summary>
/// Shader pass name for CommandBuffer.
/// </summary>
public struct ShaderPassName
{
    public string name;

    public ShaderPassName(string name)
    {
        this.name = name;
    }
}

/// <summary>
/// Render target identifier.
/// </summary>
public struct RenderTargetIdentifier
{
    public RenderTargetIdentifier(string name) { }
    public RenderTargetIdentifier(int nameID) { }
    public RenderTargetIdentifier(Texture tex) { }
}

/// <summary>
/// Render pipeline manager.
/// </summary>
public static class RenderPipelineManager
{
    public static event Action<ScriptableRenderContext, Camera[]> beginCameraRendering;
    public static event Action<ScriptableRenderContext, Camera[]> endCameraRendering;
    public static event Action<ScriptableRenderContext> beginFrameRendering;
    public static event Action<ScriptableRenderContext> endFrameRendering;
}

/// <summary>
/// Scriptable render context.
/// </summary>
public struct ScriptableRenderContext
{
    public void Submit() { }
    public void ExecuteCommandBuffer(CommandBuffer commandBuffer) { }
    public void DrawRenderers(CullingResults cullingResults, ref DrawingSettings drawingSettings, ref FilteringSettings filteringSettings) { }
}

/// <summary>
/// Culling results.
/// </summary>
public struct CullingResults
{
    public int lightAndReflectionProbeCount { get; }
    public int visibleLightCount { get; }
    public int visibleInstanceCount { get; }
}

/// <summary>
/// Drawing settings.
/// </summary>
public struct DrawingSettings
{
    public SortingCriteria sortingCriteria { get; set; }
    public PerObjectData renderingLayerMask { get; set; }
    public bool enableDynamicBatching { get; set; }
    public bool enableInstancing { get; set; }
}

/// <summary>
/// Filtering settings.
/// </summary>
public struct FilteringSettings
{
    public RenderQueueRange renderQueueRange { get; set; }
    public int layerMask { get; set; }
    public SortingLayerRange sortingLayerRange { get; set; }
}

/// <summary>
/// Sorting criteria.
/// </summary>
[Flags]
public enum SortingCriteria
{
    None = 0,
    SortingLayer = 1,
    RenderQueue = 2,
    BackToFront = 4,
    QuantizedFrontToBack = 8,
    OptimizeStateChanges = 16,
    CanvasOrder = 32,
    RendererPriority = 64,
    CommonOpaque = 2194,
    CommonTransparent = 2338
}

/// <summary>
/// Per-object data.
/// </summary>
[Flags]
public enum PerObjectData
{
    None = 0,
    LightProbe = 1,
    ReflectionProbes = 2,
    LightProbeProxyVolume = 4,
    Lightmaps = 8,
    LightData = 16,
    LightIndices = 32,
    ReflectionProbeData = 64,
    OcclusionProbe = 128,
    OcclusionProbeProxyVolume = 256,
    ShadowMask = 512
}

/// <summary>
/// Render queue range.
/// </summary>
public struct RenderingLayerRange
{
    public static RenderingLayerRange all => default;
}

/// <summary>
/// Sorting layer range.
/// </summary>
public struct SortingLayerRange
{
    public short lowerBound { get; set; }
    public short upperBound { get; set; }
}

/// <summary>
/// Render queue range.
/// </summary>
public struct RenderQueueRange
{
    public static RenderQueueRange all => default;
    public static RenderQueueRange opaque => default;
    public static RenderQueueRange transparent => default;
}
