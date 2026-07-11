using System;
using System.Collections.Generic;

namespace UnityEngine.Rendering;

internal enum DrawCommandType
{
    DrawMesh,
    DrawMeshNow,
    DrawMeshInstanced,
    DrawMeshInstancedIndirect,
    DrawMeshInstancedProcedural,
    DrawProcedural,
    DrawProceduralIndirect,
    DrawTexture,
    Blit,
    ClearRenderTarget,
    SetRenderTarget,
    CopyTexture,
    ExecuteCommandBuffer,
}

internal struct DrawCommand
{
    public DrawCommandType type;
    public Mesh mesh;
    public Matrix4x4 matrix;
    public Matrix4x4[] matrices;
    public Material material;
    public int layer;
    public Camera camera;
    public int submeshIndex;
    public int materialIndex;
    public MaterialPropertyBlock properties;
    public ShadowCastingMode castShadows;
    public bool receiveShadows;
    public Transform probeAnchor;
    public int instanceCount;
    public Bounds bounds;
    public ComputeBuffer bufferWithArgs;
    public int argsOffset;
    public ShaderPassName shaderPassName;
    public MeshTopology topology;
    public int vertexCount;
    public Rect screenRect;
    public Texture texture;
    public Rect sourceRect;
    public int leftBorder;
    public int rightBorder;
    public int topBorder;
    public int bottomBorder;
    public Color color;
    public Material blitMaterial;
    public int blitPass;
    public Texture blitSource;
    public RenderTexture blitDest;
    public RenderTargetIdentifier rtId;
    public RenderTargetIdentifier rtColor;
    public RenderTargetIdentifier rtDepth;
    public RenderTargetIdentifier[] rtColors;
    public RenderTargetSetup rtSetup;
    public bool clearDepth;
    public bool clearColor;
    public Color clearBackgroundColor;
    public float clearDepthValue;
    public Texture copySrc;
    public Texture copyDst;
    public int copySrcElement;
    public int copyDstElement;
    public int copySrcMip;
    public int copyDstMip;
    public CommandBuffer commandBuffer;
    public ComputeQueueType queueType;
    public bool isImmediate;
}

public static class Graphics
{
    private static readonly List<DrawCommand> _drawCommands = new();
    internal static RenderTargetIdentifier _currentColorRT;
    internal static RenderTargetIdentifier _currentDepthRT;
    internal static RenderTargetIdentifier[] _currentColorRTs = Array.Empty<RenderTargetIdentifier>();
    internal static bool _hasRenderTarget;
    internal static Color _clearColor = Color.clear;
    internal static float _clearDepth = 1f;

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
            type = DrawCommandType.DrawMesh,
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

    public static void DrawMeshNow(Mesh mesh, Matrix4x4 matrix) => DrawMeshNow(mesh, matrix, 0);
    public static void DrawMeshNow(Mesh mesh, Matrix4x4 matrix, int materialIndex)
    {
        _drawCommands.Add(new DrawCommand
        {
            type = DrawCommandType.DrawMeshNow,
            mesh = mesh,
            matrix = matrix,
            materialIndex = materialIndex,
            isImmediate = true
        });
        FlushImmediate();
    }

    public static void DrawMeshInstanced(Mesh mesh, int submeshIndex, Material material, Matrix4x4[] matrices) => DrawMeshInstanced(mesh, submeshIndex, material, matrices, matrices != null ? matrices.Length : 0, null);
    public static void DrawMeshInstanced(Mesh mesh, int submeshIndex, Material material, Matrix4x4[] matrices, int count) => DrawMeshInstanced(mesh, submeshIndex, material, matrices, count, null);
    public static void DrawMeshInstanced(Mesh mesh, int submeshIndex, Material material, Matrix4x4[] matrices, int count, MaterialPropertyBlock properties)
    {
        _drawCommands.Add(new DrawCommand
        {
            type = DrawCommandType.DrawMeshInstanced,
            mesh = mesh,
            submeshIndex = submeshIndex,
            material = material,
            matrices = matrices,
            instanceCount = count,
            properties = properties
        });
    }

    public static void DrawMeshInstancedIndirect(Mesh mesh, int submeshIndex, Material material, Bounds bounds, ComputeBuffer bufferWithArgs)
        => DrawMeshInstancedIndirect(mesh, submeshIndex, material, bounds, bufferWithArgs, 0);
    public static void DrawMeshInstancedIndirect(Mesh mesh, int submeshIndex, Material material, Bounds bounds, ComputeBuffer bufferWithArgs, int argsOffset)
    {
        _drawCommands.Add(new DrawCommand
        {
            type = DrawCommandType.DrawMeshInstancedIndirect,
            mesh = mesh,
            submeshIndex = submeshIndex,
            material = material,
            bounds = bounds,
            bufferWithArgs = bufferWithArgs,
            argsOffset = argsOffset
        });
    }

    public static void DrawMeshInstancedProcedural(Mesh mesh, int submeshIndex, Material material, Bounds bounds, int count)
    {
        _drawCommands.Add(new DrawCommand
        {
            type = DrawCommandType.DrawMeshInstancedProcedural,
            mesh = mesh,
            submeshIndex = submeshIndex,
            material = material,
            bounds = bounds,
            instanceCount = count
        });
    }

    public static void DrawProcedural(Matrix4x4 matrix, Material material, ShaderPassName shaderPassName, MeshTopology topology, int vertexCount)
        => DrawProcedural(matrix, material, shaderPassName, topology, vertexCount, 1);
    public static void DrawProcedural(Matrix4x4 matrix, Material material, ShaderPassName shaderPassName, MeshTopology topology, int vertexCount, int instanceCount)
    {
        _drawCommands.Add(new DrawCommand
        {
            type = DrawCommandType.DrawProcedural,
            matrix = matrix,
            material = material,
            shaderPassName = shaderPassName,
            topology = topology,
            vertexCount = vertexCount,
            instanceCount = instanceCount
        });
    }

    public static void DrawProceduralIndirect(Matrix4x4 matrix, Material material, ShaderPassName shaderPassName, MeshTopology topology, ComputeBuffer bufferWithArgs)
        => DrawProceduralIndirect(matrix, material, shaderPassName, topology, bufferWithArgs, 0);
    public static void DrawProceduralIndirect(Matrix4x4 matrix, Material material, ShaderPassName shaderPassName, MeshTopology topology, ComputeBuffer bufferWithArgs, int argsOffset)
    {
        _drawCommands.Add(new DrawCommand
        {
            type = DrawCommandType.DrawProceduralIndirect,
            matrix = matrix,
            material = material,
            shaderPassName = shaderPassName,
            topology = topology,
            bufferWithArgs = bufferWithArgs,
            argsOffset = argsOffset
        });
    }

    public static void DrawTexture(Rect screenRect, Texture texture) => DrawTexture(screenRect, texture, new Rect(0, 0, 1, 1), 0, 0, 0, 0, Color.white, null);
    public static void DrawTexture(Rect screenRect, Texture texture, int leftBorder, int rightBorder, int topBorder, int bottomBorder)
        => DrawTexture(screenRect, texture, new Rect(0, 0, 1, 1), leftBorder, rightBorder, topBorder, bottomBorder, Color.white, null);
    public static void DrawTexture(Rect screenRect, Texture texture, Material mat)
        => DrawTexture(screenRect, texture, new Rect(0, 0, 1, 1), 0, 0, 0, 0, Color.white, mat);
    public static void DrawTexture(Rect screenRect, Texture texture, Rect sourceRect)
        => DrawTexture(screenRect, texture, sourceRect, 0, 0, 0, 0, Color.white, null);
    public static void DrawTexture(Rect screenRect, Texture texture, Rect sourceRect, int leftBorder, int rightBorder, int topBorder, int bottomBorder)
        => DrawTexture(screenRect, texture, sourceRect, leftBorder, rightBorder, topBorder, bottomBorder, Color.white, null);
    public static void DrawTexture(Rect screenRect, Texture texture, Rect sourceRect, int leftBorder, int rightBorder, int topBorder, int bottomBorder, Color color)
        => DrawTexture(screenRect, texture, sourceRect, leftBorder, rightBorder, topBorder, bottomBorder, color, null);
    public static void DrawTexture(Rect screenRect, Texture texture, Rect sourceRect, int leftBorder, int rightBorder, int topBorder, int bottomBorder, Color color, Material mat)
    {
        _drawCommands.Add(new DrawCommand
        {
            type = DrawCommandType.DrawTexture,
            screenRect = screenRect,
            texture = texture,
            sourceRect = sourceRect,
            leftBorder = leftBorder,
            rightBorder = rightBorder,
            topBorder = topBorder,
            bottomBorder = bottomBorder,
            color = color,
            material = mat
        });
    }

    public static void DrawTextureRT(Rect screenRect, Texture texture, Material mat)
    {
        DrawTexture(screenRect, texture, mat);
    }

    public static void Blit(Texture source, RenderTexture dest) => Blit(source, dest, null, 0);
    public static void Blit(Texture source, RenderTexture dest, Material mat) => Blit(source, dest, mat, 0);
    public static void Blit(Texture source, Material mat) => Blit(source, null, mat, 0);
    public static void Blit(Texture source, RenderTexture dest, Material mat, int pass)
    {
        _drawCommands.Add(new DrawCommand
        {
            type = DrawCommandType.Blit,
            blitSource = source,
            blitDest = dest,
            blitMaterial = mat,
            blitPass = pass
        });
        if (dest != null)
        {
            SetRenderTargetInternal(new RenderTargetIdentifier(dest));
        }
    }

    public static void Blit(RenderTargetIdentifier source, RenderTargetIdentifier dest) => Blit(source, dest, null, -1);
    public static void Blit(RenderTargetIdentifier source, RenderTargetIdentifier dest, Material mat) => Blit(source, dest, mat, -1);
    public static void Blit(RenderTargetIdentifier source, RenderTargetIdentifier dest, Material mat, int pass)
    {
        _drawCommands.Add(new DrawCommand
        {
            type = DrawCommandType.Blit,
            rtId = source,
            rtColor = dest,
            blitMaterial = mat,
            blitPass = pass
        });
        SetRenderTargetInternal(dest);
    }

    public static void SetRenderTarget(RenderTargetIdentifier rt)
    {
        SetRenderTargetInternal(rt);
        _drawCommands.Add(new DrawCommand
        {
            type = DrawCommandType.SetRenderTarget,
            rtId = rt
        });
    }

    public static void SetRenderTarget(RenderTargetIdentifier color, RenderTargetIdentifier depth)
    {
        _currentColorRT = color;
        _currentDepthRT = depth;
        _hasRenderTarget = true;
        _drawCommands.Add(new DrawCommand
        {
            type = DrawCommandType.SetRenderTarget,
            rtColor = color,
            rtDepth = depth
        });
    }

    public static void SetRenderTarget(RenderTargetIdentifier[] colors, RenderTargetIdentifier depth)
    {
        _currentColorRTs = colors;
        _currentDepthRT = depth;
        _hasRenderTarget = true;
        _drawCommands.Add(new DrawCommand
        {
            type = DrawCommandType.SetRenderTarget,
            rtColors = colors,
            rtDepth = depth
        });
    }

    public static void SetRenderTarget(RenderTargetSetup setup)
    {
        if (setup.color != null && setup.color.Length > 0)
        {
            _currentColorRT = setup.color[0];
            _currentColorRTs = setup.color;
        }
        _currentDepthRT = setup.depth;
        _hasRenderTarget = true;
        _drawCommands.Add(new DrawCommand
        {
            type = DrawCommandType.SetRenderTarget,
            rtSetup = setup
        });
    }

    private static void SetRenderTargetInternal(RenderTargetIdentifier rt)
    {
        _currentColorRT = rt;
        _currentDepthRT = default;
        _hasRenderTarget = true;
    }

    public static void ClearRenderTarget(bool clearDepth, bool clearColor, Color backgroundColor) => ClearRenderTarget(clearDepth, clearColor, backgroundColor, 1f);
    public static void ClearRenderTarget(bool clearDepth, bool clearColor, Color backgroundColor, float depth)
    {
        _drawCommands.Add(new DrawCommand
        {
            type = DrawCommandType.ClearRenderTarget,
            clearDepth = clearDepth,
            clearColor = clearColor,
            clearBackgroundColor = backgroundColor,
            clearDepthValue = depth
        });
    }

    public static RenderBuffer activeColorBuffer
    {
        get => new() { m_RT = _currentColorRT };
        set => _currentColorRT = value.m_RT;
    }

    public static RenderBuffer activeDepthBuffer
    {
        get => new() { m_RT = _currentDepthRT };
        set => _currentDepthRT = value.m_RT;
    }

    public static GraphicsTier activeTier { get; set; } = GraphicsTier.Tier1;
    public static ColorGamut activeColorGamut { get; set; } = ColorGamut.sRGB;
    internal static bool hasRenderTarget => _hasRenderTarget;
    internal static RenderTargetIdentifier currentColorRT => _currentColorRT;
    internal static RenderTargetIdentifier currentDepthRT => _currentDepthRT;

    public static string deviceName => SystemInfo.graphicsDeviceName;
    public static string deviceVersion => SystemInfo.graphicsDeviceVersion;
    public static GraphicsDeviceType graphicsDeviceType => SystemInfo.graphicsDeviceType;
    public static int minOpenGLESVersion => 30;
    public static int minComputeBufferOffsetAlignment => 256;

    public static void ExecuteCommandBuffer(CommandBuffer commandBuffer)
    {
        _drawCommands.Add(new DrawCommand
        {
            type = DrawCommandType.ExecuteCommandBuffer,
            commandBuffer = commandBuffer,
            queueType = ComputeQueueType.Default
        });
    }

    public static void ExecuteCommandBufferAsync(CommandBuffer commandBuffer, ComputeQueueType queueType)
    {
        _drawCommands.Add(new DrawCommand
        {
            type = DrawCommandType.ExecuteCommandBuffer,
            commandBuffer = commandBuffer,
            queueType = queueType
        });
    }

    public static void CopyTexture(Texture src, Texture dst)
    {
        CopyTexture(src, 0, 0, dst, 0, 0);
    }

    public static void CopyTexture(Texture src, int srcElement, Texture dst, int dstElement)
    {
        CopyTexture(src, srcElement, 0, dst, dstElement, 0);
    }

    public static void CopyTexture(Texture src, int srcElement, int srcMip, Texture dst, int dstElement, int dstMip)
    {
        _drawCommands.Add(new DrawCommand
        {
            type = DrawCommandType.CopyTexture,
            copySrc = src,
            copyDst = dst,
            copySrcElement = srcElement,
            copyDstElement = dstElement,
            copySrcMip = srcMip,
            copyDstMip = dstMip
        });
    }

    private static int _loadTickCount;
    public static void WaitOnLoadTick() { _loadTickCount++; }

    private static void FlushImmediate()
    {
        for (int i = _drawCommands.Count - 1; i >= 0; i--)
        {
            if (_drawCommands[i].isImmediate)
            {
                _drawCommands.RemoveAt(i);
                break;
            }
        }
    }
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
    private List<CommandBuffer> _commandBuffers;
    private List<DrawCommand> _drawCommands;
    private bool _submitted;
    private CullingResults _cullingResults;
    private bool _hasCullingResults;

    internal List<CommandBuffer> commandBuffers => _commandBuffers ??= new List<CommandBuffer>();
    internal bool submitted => _submitted;

    public void Submit()
    {
        _submitted = true;
        _commandBuffers?.Clear();
        _drawCommands?.Clear();
    }

    public void ExecuteCommandBuffer(CommandBuffer commandBuffer)
    {
        if (commandBuffer == null) return;
        _commandBuffers ??= new List<CommandBuffer>();
        _commandBuffers.Add(commandBuffer);
        Graphics.ExecuteCommandBuffer(commandBuffer);
    }

    public void DrawRenderers(CullingResults cullingResults, ref DrawingSettings drawingSettings, ref FilteringSettings filteringSettings)
    {
        _cullingResults = cullingResults;
        _hasCullingResults = true;
        _drawCommands ??= new List<DrawCommand>();
        _drawCommands.Add(new DrawCommand
        {
            type = DrawCommandType.DrawMesh
        });
    }

    public void Cull(ref ScriptableCullingParameters parameters, out CullingResults results)
    {
        results = new CullingResults
        {
            _visibleLights = new VisibleLight[0],
            _visibleReflectionProbes = new VisibleReflectionProbe[0],
            _lightAndReflectionProbeCount = 0,
            _visibleLightCount = 0,
            _visibleInstanceCount = 0
        };
        _cullingResults = results;
        _hasCullingResults = true;
    }
}

public struct CullingResults
{
    internal VisibleLight[] _visibleLights;
    internal VisibleReflectionProbe[] _visibleReflectionProbes;
    internal int _lightAndReflectionProbeCount;
    internal int _visibleLightCount;
    internal int _visibleInstanceCount;

    public int lightAndReflectionProbeCount => _lightAndReflectionProbeCount;
    public int visibleLightCount => _visibleLightCount;
    public int visibleInstanceCount => _visibleInstanceCount;
    public VisibleLight[] visibleLights => _visibleLights ?? Array.Empty<VisibleLight>();
    public VisibleReflectionProbe[] visibleReflectionProbes => _visibleReflectionProbes ?? Array.Empty<VisibleReflectionProbe>();
}

public struct DrawingSettings
{
    public SortingCriteria sortingCriteria { get; set; }
    public PerObjectData renderingLayerMask { get; set; }
    public bool enableDynamicBatching { get; set; }
    public bool enableInstancing { get; set; }

    public DrawingSettings(ShaderTagId shaderTagId, SortingCriteria sortingCriteria)
    {
        this.sortingCriteria = sortingCriteria;
        renderingLayerMask = PerObjectData.None;
        enableDynamicBatching = false;
        enableInstancing = false;
        _ = shaderTagId;
    }
}

public struct FilteringSettings
{
    public RenderQueueRange renderQueueRange { get; set; }
    public int layerMask { get; set; }
    public SortingLayerRange sortingLayerRange { get; set; }

    public FilteringSettings(RenderQueueRange range)
    {
        renderQueueRange = range;
        layerMask = -1;
        sortingLayerRange = SortingLayerRange.all;
    }
}

public struct SortingLayerRange
{
    public short lowerBound { get; set; }
    public short upperBound { get; set; }
    public static SortingLayerRange all => new() { lowerBound = short.MinValue, upperBound = short.MaxValue };
}

public struct RenderQueueRange
{
    public int lowerBound { get; set; }
    public int upperBound { get; set; }

    public static RenderQueueRange all => new() { lowerBound = 0, upperBound = 5000 };
    public static RenderQueueRange opaque => new() { lowerBound = 0, upperBound = 2500 };
    public static RenderQueueRange transparent => new() { lowerBound = 2501, upperBound = 5000 };
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
    }

    internal static void InvokeBeginFrameRendering(ScriptableRenderContext context) => beginFrameRendering?.Invoke(context);
    internal static void InvokeBeginCameraRendering(ScriptableRenderContext context, Camera[] cameras) => beginCameraRendering?.Invoke(context, cameras);
    internal static void InvokeEndCameraRendering(ScriptableRenderContext context, Camera[] cameras) => endCameraRendering?.Invoke(context, cameras);
    internal static void InvokeEndFrameRendering(ScriptableRenderContext context) => endFrameRendering?.Invoke(context);
}
