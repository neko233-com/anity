using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;

namespace UnityEngine.Rendering;

public struct GlobalKeyword
{
    public int index;
    public string name;
}

public struct AttachmentDescriptor
{
    public RenderTargetIdentifier loadStoreTarget;
    public RenderBufferLoadAction loadAction;
    public RenderBufferStoreAction storeAction;
    public Color clearColor;
    public float clearDepth;
    public uint clearStencil;

    public AttachmentDescriptor(RenderBufferLoadAction loadAction, RenderBufferStoreAction storeAction)
    {
        this.loadAction = loadAction;
        this.storeAction = storeAction;
        loadStoreTarget = default;
        clearColor = Color.clear;
        clearDepth = 1.0f;
        clearStencil = 0;
    }
}

public class ReflectionProbe : Behaviour
{
    public Bounds bounds;
    public Texture texture;
    public float blendDistance;
    public int importance;
    public bool boxProjection;
    public bool hdr;
}

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

    public void ExecuteCommandBufferAsync(CommandBuffer commandBuffer, ComputeQueueType queueType)
    {
        if (commandBuffer == null) return;
        _commandBuffers ??= new List<CommandBuffer>();
        _commandBuffers.Add(commandBuffer);
        Graphics.ExecuteCommandBufferAsync(commandBuffer, queueType);
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

    public void DrawRenderers(CullingResults cullingResults, ref DrawingSettings drawingSettings, ref FilteringSettings filteringSettings, ref RenderStateBlock stateBlock)
    {
        DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings);
    }

    public void DrawSkybox(Camera camera)
    {
        _drawCommands ??= new List<DrawCommand>();
    }

    public void DrawShadows(ref ShadowDrawingSettings settings)
    {
        _drawCommands ??= new List<DrawCommand>();
    }

    public void DrawGizmos(Camera camera, GizmoSubset gizmoSubset)
    {
        _drawCommands ??= new List<DrawCommand>();
    }

    public void DrawWireOverlay(Camera camera)
    {
        _drawCommands ??= new List<DrawCommand>();
    }

    public void DrawUIOverlay(Camera camera)
    {
        _drawCommands ??= new List<DrawCommand>();
    }

    public void SetupCameraProperties(Camera camera)
    {
        if (camera == null) return;
        var cmd = CommandBufferPool.Get("Setup Camera Properties");
        cmd.SetViewProjectionMatrices(camera.worldToCameraMatrix, camera.projectionMatrix);
        ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);
    }

    public void SetRenderTarget(RenderTargetIdentifier color, RenderBufferLoadAction colorLoad, RenderBufferStoreAction colorStore, RenderTargetIdentifier depth, RenderBufferLoadAction depthLoad, RenderBufferStoreAction depthStore)
    {
        Graphics.SetRenderTarget(color, depth);
    }

    public void BeginRenderPass(int width, int height, int samples, NativeArray<AttachmentDescriptor> attachments, int depthAttachmentIndex)
    {
        _drawCommands ??= new List<DrawCommand>();
    }

    public void EndRenderPass()
    {
    }

    public void EnableKeyword(ref GlobalKeyword keyword)
    {
    }

    public void DisableKeyword(ref GlobalKeyword keyword)
    {
    }

    public void SetGlobalFloat(int nameID, float value) => Shader.SetGlobalFloat(nameID, value);
    public void SetGlobalFloat(string name, float value) => Shader.SetGlobalFloat(name, value);
    public void SetGlobalInt(int nameID, int value) => Shader.SetGlobalInt(nameID, value);
    public void SetGlobalInt(string name, int value) => Shader.SetGlobalInt(name, value);
    public void SetGlobalVector(int nameID, Vector4 value) => Shader.SetGlobalVector(nameID, value);
    public void SetGlobalVector(string name, Vector4 value) => Shader.SetGlobalVector(name, value);
    public void SetGlobalColor(int nameID, Color value) => Shader.SetGlobalColor(nameID, value);
    public void SetGlobalColor(string name, Color value) => Shader.SetGlobalColor(name, value);
    public void SetGlobalMatrix(int nameID, Matrix4x4 value) => Shader.SetGlobalMatrix(nameID, value);
    public void SetGlobalMatrix(string name, Matrix4x4 value) => Shader.SetGlobalMatrix(name, value);
    public void SetGlobalTexture(int nameID, RenderTargetIdentifier value) { }
    public void SetGlobalTexture(string name, RenderTargetIdentifier value) { }

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

public enum GizmoSubset
{
    PreImageEffects,
    PostImageEffects
}

public struct RenderingLayerMask
{
    public uint value;

    public RenderingLayerMask(uint value)
    {
        this.value = value;
    }

    public static implicit operator uint(RenderingLayerMask mask) => mask.value;
    public static implicit operator RenderingLayerMask(uint value) => new RenderingLayerMask(value);

    public static RenderingLayerMask GetMask(params string[] layerNames) => new RenderingLayerMask(0xFFFFFFFF);
    public static int NameToLayer(string layerName) => 0;
    public static string LayerToName(int layer) => "Default";
}

public struct VisibleRendererList
{
    public int length;
    public IntPtr nativePointer;
}

public struct CullingResults
{
    internal VisibleLight[] _visibleLights;
    internal VisibleReflectionProbe[] _visibleReflectionProbes;
    internal int _lightAndReflectionProbeCount;
    internal int _visibleLightCount;
    internal int _visibleInstanceCount;

    public VisibleLight[] lights => _visibleLights ?? Array.Empty<VisibleLight>();
    public VisibleReflectionProbe[] reflectionProbes => _visibleReflectionProbes ?? Array.Empty<VisibleReflectionProbe>();
    public VisibleRendererList visibleRenderers;
    public bool velocityNeedsRasterization;
    public int lightAndReflectionProbeCount => _lightAndReflectionProbeCount;
    public int visibleLightCount => _visibleLightCount;
    public int visibleInstanceCount => _visibleInstanceCount;
    public VisibleLight[] visibleLights => _visibleLights ?? Array.Empty<VisibleLight>();
    public VisibleReflectionProbe[] visibleReflectionProbes => _visibleReflectionProbes ?? Array.Empty<VisibleReflectionProbe>();
    public NativeArray<VisibleLight> visibleLightsNativeArray => new NativeArray<VisibleLight>(_visibleLights ?? Array.Empty<VisibleLight>(), Allocator.Invalid);

    public int[] GetLightIndexMap()
    {
        if (_visibleLights == null) return Array.Empty<int>();
        var map = new int[_visibleLights.Length];
        for (int i = 0; i < map.Length; i++) map[i] = i;
        return map;
    }

    public bool GetShadowCasterBounds(int lightIndex, out Bounds outBounds)
    {
        outBounds = default;
        return false;
    }

    public bool ComputeDirectionalShadowMatricesAndCullingPrimitives(int lightIndex, int splitCount, int splitIndex, Vector3 splitRatio, int shadowResolution, float shadowNearPlaneOffset, out Matrix4x4 viewMatrix, out Matrix4x4 projMatrix, out ShadowSplitData splitData)
    {
        viewMatrix = Matrix4x4.identity;
        projMatrix = Matrix4x4.identity;
        splitData = default;
        return false;
    }

    public bool GetPointShadowMatricesAndCullingPrimitives(int lightIndex, CubemapFace cubemapFace, float fovBias, out Matrix4x4 viewMatrix, out Matrix4x4 projMatrix, out ShadowSplitData splitData)
    {
        viewMatrix = Matrix4x4.identity;
        projMatrix = Matrix4x4.identity;
        splitData = default;
        return false;
    }

    public bool GetSpotShadowMatricesAndCullingPrimitives(int lightIndex, out Matrix4x4 viewMatrix, out Matrix4x4 projMatrix, out ShadowSplitData splitData)
    {
        viewMatrix = Matrix4x4.identity;
        projMatrix = Matrix4x4.identity;
        splitData = default;
        return false;
    }

    public bool GetReflectionProbeBounds(out Bounds outBounds)
    {
        outBounds = default;
        return false;
    }
}

public struct DrawingSettings
{
    private ShaderTagId[] m_ShaderTagIds;
    public SortingSettings sortingSettings { get; }
    public PerObjectData perObjectData { get; set; }
    public bool enableDynamicBatching { get; set; }
    public bool enableInstancing { get; set; }
    public int mainLightIndex { get; set; }
    public Material overrideMaterial { get; set; }
    public int overrideMaterialPassIndex { get; set; }
    public int shaderPassCount => m_ShaderTagIds?.Length ?? 0;

    public DrawingSettings(ShaderTagId shaderTagId, SortingSettings sortingSettings)
    {
        this.sortingSettings = sortingSettings;
        perObjectData = PerObjectData.None;
        enableDynamicBatching = false;
        enableInstancing = false;
        mainLightIndex = -1;
        overrideMaterial = null;
        overrideMaterialPassIndex = 0;
        m_ShaderTagIds = new[] { shaderTagId };
    }

    public void SetShaderPassName(int index, ShaderTagId shaderPassName)
    {
        if (m_ShaderTagIds == null)
            m_ShaderTagIds = new ShaderTagId[index + 1];
        else if (index >= m_ShaderTagIds.Length)
            Array.Resize(ref m_ShaderTagIds, index + 1);
        m_ShaderTagIds[index] = shaderPassName;
    }

    public ShaderTagId GetShaderPassName(int index)
    {
        if (m_ShaderTagIds != null && index >= 0 && index < m_ShaderTagIds.Length)
            return m_ShaderTagIds[index];
        return default;
    }
}

public struct FilteringSettings
{
    public RenderQueueRange renderQueueRange { get; set; }
    public uint layerMask { get; set; }
    public uint renderingLayerMask { get; set; }
    public SortingLayerRange sortingLayerRange { get; set; }
    public bool excludeMotionVectorObjects { get; set; }

    public FilteringSettings(RenderQueueRange? range = null)
    {
        renderQueueRange = range ?? RenderQueueRange.all;
        layerMask = uint.MaxValue;
        renderingLayerMask = uint.MaxValue;
        sortingLayerRange = SortingLayerRange.all;
        excludeMotionVectorObjects = false;
    }

    public FilteringSettings(RenderQueueRange range, uint layerMask)
    {
        renderQueueRange = range;
        this.layerMask = layerMask;
        renderingLayerMask = uint.MaxValue;
        sortingLayerRange = SortingLayerRange.all;
        excludeMotionVectorObjects = false;
    }

    public FilteringSettings(RenderQueueRange range, uint layerMask, uint renderingLayerMask)
    {
        renderQueueRange = range;
        this.layerMask = layerMask;
        this.renderingLayerMask = renderingLayerMask;
        sortingLayerRange = SortingLayerRange.all;
        excludeMotionVectorObjects = false;
    }
}

public struct SortingSettings
{
    private Camera _camera;

    public SortingCriteria criteria { get; set; }
    public Vector3 customAxis { get; set; }
    public DistanceMetric distanceMetric { get; set; }

    public SortingSettings(Camera camera)
    {
        _camera = camera;
        criteria = SortingCriteria.CommonOpaque;
        customAxis = Vector3.forward;
        distanceMetric = camera != null && camera.orthographic ? DistanceMetric.Orthographic : DistanceMetric.Default;
    }

    public SortingSettings(SortingCriteria criteria)
    {
        _camera = null;
        this.criteria = criteria;
        customAxis = Vector3.forward;
        distanceMetric = DistanceMetric.Default;
    }

    public static implicit operator SortingSettings(SortingCriteria criteria) => new SortingSettings(criteria);
}

public struct SortingLayerRange
{
    public int min { get; set; }
    public int max { get; set; }

    public static readonly SortingLayerRange all = new SortingLayerRange { min = short.MinValue, max = short.MaxValue };

    public SortingLayerRange(int min, int max)
    {
        this.min = min;
        this.max = max;
    }
}

public struct RenderQueueRange
{
    public int min { get; set; }
    public int max { get; set; }

    public static readonly RenderQueueRange all = new RenderQueueRange(0, 5000);
    public static readonly RenderQueueRange opaque = new RenderQueueRange(0, 2500);
    public static readonly RenderQueueRange transparent = new RenderQueueRange(2501, 5000);

    public RenderQueueRange(int min, int max)
    {
        this.min = min;
        this.max = max;
    }
}

public struct ShaderTagId : IEquatable<ShaderTagId>
{
    public string name;
    private int _id;

    public int id => _id;

    public ShaderTagId(string name)
    {
        this.name = name;
        _id = Shader.PropertyToID(name ?? string.Empty);
    }

    public bool Equals(ShaderTagId other)
    {
        return _id == other._id;
    }

    public override bool Equals(object obj)
    {
        return obj is ShaderTagId other && Equals(other);
    }

    public override int GetHashCode()
    {
        return _id;
    }

    public static bool operator ==(ShaderTagId left, ShaderTagId right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(ShaderTagId left, ShaderTagId right)
    {
        return !left.Equals(right);
    }

    public static implicit operator ShaderTagId(string name) => new(name);

    public static readonly ShaderTagId SRPDefaultUnlit = new("SRPDefaultUnlit");
    public static readonly ShaderTagId UniversalForward = new("UniversalForward");
    public static readonly ShaderTagId UniversalGBuffer = new("UniversalGBuffer");
    public static readonly ShaderTagId UniversalForwardOnly = new("UniversalForwardOnly");
    public static readonly ShaderTagId ShadowCaster = new("ShadowCaster");
    public static readonly ShaderTagId DepthNormals = new("DepthNormals");
    public static readonly ShaderTagId DepthOnly = new("DepthOnly");
    public static readonly ShaderTagId Meta = new("Meta");
    public static readonly ShaderTagId LightweightForward = new("LightweightForward");
    public static readonly ShaderTagId _RenderOpaqueForwardOnly = new("_RenderOpaqueForwardOnly");
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
