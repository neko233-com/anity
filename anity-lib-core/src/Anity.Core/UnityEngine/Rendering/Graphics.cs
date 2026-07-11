using System;
using System.Collections.Generic;

namespace UnityEngine.Rendering;

internal struct DrawCommand
{
    public Mesh mesh;
    public Matrix4x4 matrix;
    public Material material;
    public int layer;
    public Camera camera;
    public int submeshIndex;
    public MaterialPropertyBlock properties;
    public ShadowCastingMode castShadows;
    public bool receiveShadows;
    public Transform probeAnchor;
}

public static class Graphics
{
    private static readonly List<DrawCommand> _drawCommands = new();

    internal static IReadOnlyList<DrawCommand> drawCommands => _drawCommands;
    internal static void ClearDrawCommands() => _drawCommands.Clear();

    public static void DrawMesh(Mesh mesh, Vector3 position, Quaternion rotation) => DrawMesh(mesh, position, rotation, null, 0, null);
    public static void DrawMesh(Mesh mesh, Vector3 position, Quaternion rotation, Material material) => DrawMesh(mesh, position, rotation, material, 0, null);
    public static void DrawMesh(Mesh mesh, Vector3 position, Quaternion rotation, Material material, int layer) => DrawMesh(mesh, position, rotation, material, layer, null);
    public static void DrawMesh(Mesh mesh, Vector3 position, Quaternion rotation, Material material, int layer, Camera camera) => DrawMesh(mesh, Matrix4x4.TRS(position, rotation, Vector3.one), material, layer, camera);

    public static void DrawMesh(Mesh mesh, Matrix4x4 matrix, Material material) => DrawMesh(mesh, matrix, material, 0, null);
    public static void DrawMesh(Mesh mesh, Matrix4x4 matrix, Material material, int layer) => DrawMesh(mesh, matrix, material, layer, null);
    public static void DrawMesh(Mesh mesh, Matrix4x4 matrix, Material material, int layer, Camera camera) => DrawMesh(mesh, matrix, material, layer, camera, 0, null, ShadowCastingMode.On, true, null);
    public static void DrawMesh(Mesh mesh, Matrix4x4 matrix, Material material, int layer, Camera camera, int submeshIndex) => DrawMesh(mesh, matrix, material, layer, camera, submeshIndex, null, ShadowCastingMode.On, true, null);
    public static void DrawMesh(Mesh mesh, Matrix4x4 matrix, Material material, int layer, Camera camera, int submeshIndex, MaterialPropertyBlock properties) => DrawMesh(mesh, matrix, material, layer, camera, submeshIndex, properties, ShadowCastingMode.On, true, null);

    public static void DrawMesh(Mesh mesh, Matrix4x4 matrix, Material material, int layer, Camera camera, int submeshIndex, MaterialPropertyBlock properties, ShadowCastingMode castShadows, bool receiveShadows, Transform probeAnchor)
    {
        _drawCommands.Add(new DrawCommand
        {
            mesh = mesh,
            matrix = matrix,
            material = material,
            layer = layer,
            camera = camera,
            submeshIndex = submeshIndex,
            properties = properties,
            castShadows = castShadows,
            receiveShadows = receiveShadows,
            probeAnchor = probeAnchor
        });
    }

    public static void DrawMeshNow(Mesh mesh, Matrix4x4 matrix) { }
    public static void DrawMeshNow(Mesh mesh, Matrix4x4 matrix, int materialIndex) { }

    public static void DrawMeshInstanced(Mesh mesh, int submeshIndex, Material material, Matrix4x4[] matrices) => DrawMeshInstanced(mesh, submeshIndex, material, matrices, matrices != null ? matrices.Length : 0, null);
    public static void DrawMeshInstanced(Mesh mesh, int submeshIndex, Material material, Matrix4x4[] matrices, int count) => DrawMeshInstanced(mesh, submeshIndex, material, matrices, count, null);
    public static void DrawMeshInstanced(Mesh mesh, int submeshIndex, Material material, Matrix4x4[] matrices, int count, MaterialPropertyBlock properties) { }
    public static void DrawMeshInstancedIndirect(Mesh mesh, int submeshIndex, Material material, Bounds bounds, ComputeBuffer bufferWithArgs) { }
    public static void DrawMeshInstancedIndirect(Mesh mesh, int submeshIndex, Material material, Bounds bounds, ComputeBuffer bufferWithArgs, int argsOffset) { }
    public static void DrawMeshInstancedProcedural(Mesh mesh, int submeshIndex, Material material, Bounds bounds, int count) { }

    public static void DrawProcedural(Matrix4x4 matrix, Material material, ShaderPassName shaderPassName, MeshTopology topology, int vertexCount) { }
    public static void DrawProcedural(Matrix4x4 matrix, Material material, ShaderPassName shaderPassName, MeshTopology topology, int vertexCount, int instanceCount) { }
    public static void DrawProceduralIndirect(Matrix4x4 matrix, Material material, ShaderPassName shaderPassName, MeshTopology topology, ComputeBuffer bufferWithArgs) { }
    public static void DrawProceduralIndirect(Matrix4x4 matrix, Material material, ShaderPassName shaderPassName, MeshTopology topology, ComputeBuffer bufferWithArgs, int argsOffset) { }

    public static void DrawTexture(Rect screenRect, Texture texture) { }
    public static void DrawTexture(Rect screenRect, Texture texture, int leftBorder, int rightBorder, int topBorder, int bottomBorder) { }
    public static void DrawTexture(Rect screenRect, Texture texture, Material mat) { }
    public static void DrawTexture(Rect screenRect, Texture texture, Rect sourceRect) { }
    public static void DrawTexture(Rect screenRect, Texture texture, Rect sourceRect, int leftBorder, int rightBorder, int topBorder, int bottomBorder) { }
    public static void DrawTexture(Rect screenRect, Texture texture, Rect sourceRect, int leftBorder, int rightBorder, int topBorder, int bottomBorder, Color color) { }
    public static void DrawTexture(Rect screenRect, Texture texture, Rect sourceRect, int leftBorder, int rightBorder, int topBorder, int bottomBorder, Color color, Material mat) { }
    public static void DrawTextureRT(Rect screenRect, Texture texture, Material mat) { }

    public static void Blit(Texture source, RenderTexture dest) => Blit(source, dest, null, 0);
    public static void Blit(Texture source, RenderTexture dest, Material mat) => Blit(source, dest, mat, 0);
    public static void Blit(Texture source, Material mat) => Blit(source, null, mat, 0);
    public static void Blit(Texture source, RenderTexture dest, Material mat, int pass) { }
    public static void Blit(RenderTargetIdentifier source, RenderTargetIdentifier dest) { }
    public static void Blit(RenderTargetIdentifier source, RenderTargetIdentifier dest, Material mat) { }
    public static void Blit(RenderTargetIdentifier source, RenderTargetIdentifier dest, Material mat, int pass) { }

    public static void SetRenderTarget(RenderTargetIdentifier rt) { }
    public static void SetRenderTarget(RenderTargetIdentifier color, RenderTargetIdentifier depth) { }
    public static void SetRenderTarget(RenderTargetIdentifier[] colors, RenderTargetIdentifier depth) { }
    public static void SetRenderTarget(RenderTargetSetup setup) { }

    public static void ClearRenderTarget(bool clearDepth, bool clearColor, Color backgroundColor) => ClearRenderTarget(clearDepth, clearColor, backgroundColor, 1f);
    public static void ClearRenderTarget(bool clearDepth, bool clearColor, Color backgroundColor, float depth)
    {
        if (clearColor || clearDepth)
        {
            _drawCommands.Add(new DrawCommand());
        }
    }

    public static RenderBuffer activeColorBuffer { get; set; }
    public static RenderBuffer activeDepthBuffer { get; set; }
    public static GraphicsTier activeTier { get; set; } = GraphicsTier.Tier1;
    public static ColorGamut activeColorGamut { get; set; } = ColorGamut.sRGB;

    public static string deviceName => SystemInfo.graphicsDeviceName;
    public static string deviceVersion => SystemInfo.graphicsDeviceVersion;
    public static GraphicsDeviceType graphicsDeviceType => SystemInfo.graphicsDeviceType;
    public static int minOpenGLESVersion => 30;
    public static int minComputeBufferOffsetAlignment => 256;

    public static void ExecuteCommandBuffer(CommandBuffer commandBuffer) { }
    public static void ExecuteCommandBufferAsync(CommandBuffer commandBuffer, ComputeQueueType queueType) { }

    public static void CopyTexture(Texture src, Texture dst) { }
    public static void CopyTexture(Texture src, int srcElement, Texture dst, int dstElement) { }
    public static void CopyTexture(Texture src, int srcElement, int srcMip, Texture dst, int dstElement, int dstMip) { }

    public static void WaitOnLoadTick() { }
}

public struct ShaderPassName
{
    public string name;
    public ShaderPassName(string name) { this.name = name; }
}

public enum ComputeQueueType
{
    Default = 0,
    Background = 1,
    Normal = 2,
    High = 3
}

public enum GraphicsTier
{
    Tier1 = 0,
    Tier2 = 1,
    Tier3 = 2
}

public enum ColorGamut
{
    sRGB = 0,
    Rec709 = 0,
    Rec2020 = 1,
    DisplayP3 = 2,
    HDR10 = 3,
    DolbyHDR = 4
}

public struct RenderBuffer
{
    public RenderTargetIdentifier m_RT;
    public int LoadAction { get; set; }
    public int StoreAction { get; set; }
}

public struct RenderTargetIdentifier
{
    public RenderTargetIdentifier(string name) { }
    public RenderTargetIdentifier(int nameID) { }
    public RenderTargetIdentifier(Texture tex) { }
}

public struct RenderTargetSetup
{
    public RenderTargetIdentifier[] color;
    public RenderTargetIdentifier depth;
    public int mipLevel;
    public CubemapFace cubemapFace;
    public int depthSlice;
}

public struct ScriptableRenderContext
{
    public void Submit() { }
    public void ExecuteCommandBuffer(CommandBuffer commandBuffer) { }
    public void DrawRenderers(CullingResults cullingResults, ref DrawingSettings drawingSettings, ref FilteringSettings filteringSettings) { }
    public void Cull(ref ScriptableCullingParameters parameters, out CullingResults results) { results = default; }
}

public struct CullingResults
{
    public int lightAndReflectionProbeCount { get; }
    public int visibleLightCount { get; }
    public int visibleInstanceCount { get; }
}

public struct DrawingSettings
{
    public SortingCriteria sortingCriteria { get; set; }
    public PerObjectData renderingLayerMask { get; set; }
    public bool enableDynamicBatching { get; set; }
    public bool enableInstancing { get; set; }

    public DrawingSettings(ShaderTagId shaderTagId, SortingCriteria sortingCriteria) { }
}

public struct FilteringSettings
{
    public RenderQueueRange renderQueueRange { get; set; }
    public int layerMask { get; set; }
    public SortingLayerRange sortingLayerRange { get; set; }

    public FilteringSettings(RenderQueueRange range) { }
}

public struct SortingLayerRange
{
    public short lowerBound { get; set; }
    public short upperBound { get; set; }
    public static SortingLayerRange all => default;
}

public struct RenderQueueRange
{
    public static RenderQueueRange all => default;
    public static RenderQueueRange opaque => default;
    public static RenderQueueRange transparent => default;
}

public struct ShaderTagId
{
    public string name;
    private int _id;

    public ShaderTagId(string name)
    {
        this.name = name;
        _id = name?.GetHashCode() ?? 0;
    }

    public static implicit operator ShaderTagId(string name) => new(name);
}

public static class RenderPipelineManager
{
    public static event Action<ScriptableRenderContext, Camera[]> beginCameraRendering;
    public static event Action<ScriptableRenderContext, Camera[]> endCameraRendering;
    public static event Action<ScriptableRenderContext> beginFrameRendering;
    public static event Action<ScriptableRenderContext> endFrameRendering;

    public static RenderPipeline? currentPipeline { get; private set; }

    public static void SetCurrentPipeline(RenderPipeline? pipeline)
    {
        currentPipeline = pipeline;
        RenderPipeline.current = pipeline;
    }

    internal static void InvokeBeginFrameRendering(ScriptableRenderContext context) => beginFrameRendering?.Invoke(context);
    internal static void InvokeBeginCameraRendering(ScriptableRenderContext context, Camera[] cameras) => beginCameraRendering?.Invoke(context, cameras);
    internal static void InvokeEndCameraRendering(ScriptableRenderContext context, Camera[] cameras) => endCameraRendering?.Invoke(context, cameras);
    internal static void InvokeEndFrameRendering(ScriptableRenderContext context) => endFrameRendering?.Invoke(context);
}
