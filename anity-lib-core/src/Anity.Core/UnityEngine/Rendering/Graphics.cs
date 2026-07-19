using System;
using System.Collections.Generic;
using Anity.Core.Runtime.Native;
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
        if (commandBuffer == null) throw new ArgumentNullException(nameof(commandBuffer));
        commandBuffer.ScheduleFences();
        _drawCommands.Add(new DrawCommand
        {
            type = DrawCommandType.ExecuteCommandBuffer,
            commandBuffer = commandBuffer,
            queueType = ComputeQueueType.Default
        });
    }

    public static void ExecuteCommandBufferAsync(CommandBuffer commandBuffer, ComputeQueueType queueType)
    {
        if (commandBuffer == null) throw new ArgumentNullException(nameof(commandBuffer));
        commandBuffer.ScheduleFences();
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
    private Anity.Core.Runtime.Native.NativeGraphicsDevice? _nativeGraphicsDevice;
    private RenderTexture? _nativeCameraTarget;
    private int _nativeCameraTargetSlice;
    private Matrix4x4 _nativeViewProjection;
    private Matrix4x4 _nativeTransparentViewProjection;
    private Matrix4x4 _nativeMotionViewProjection;
    private Matrix4x4 _nativePreviousMotionViewProjection;
    private Matrix4x4 _nativeStereoRightViewProjection;
    private Matrix4x4 _nativeStereoRightTransparentViewProjection;
    private Matrix4x4 _nativeStereoRightMotionViewProjection;
    private Matrix4x4 _nativeStereoRightPreviousMotionViewProjection;
    private bool _nativeSinglePassInstanced;
    private Vector3 _nativeCameraPosition;
    private int _nativeCameraInstanceId;
    private long _nativeCameraMotionHistoryKey;
    private bool _hasNativeCameraMotionHistoryKey;
    private long _nativeStereoRightCameraMotionHistoryKey;
    private bool _hasNativeStereoRightCameraMotionHistoryKey;
    private static readonly object s_MotionHistoryLock = new();
    // Camera instance alone is not a temporal identity in XR: a multipass
    // right-eye submission must never replace the left eye's previous VP.
    private static readonly Dictionary<long, Matrix4x4> s_PreviousCameraViewProjections = new();
    private static readonly Dictionary<int, Matrix4x4> s_PreviousRendererLocalToWorld = new();
    private static readonly Dictionary<int, Vector3[]> s_PreviousSkinnedRendererPositions = new();
    private const int MaxMotionHistoryEntries = 4096;

    internal List<CommandBuffer> commandBuffers => _commandBuffers ??= new List<CommandBuffer>();
    internal bool submitted => _submitted;

    public void Submit()
    {
        _submitted = true;
        if (_commandBuffers != null)
            foreach (CommandBuffer commandBuffer in _commandBuffers) commandBuffer.ScheduleFences();
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
        Renderer[] renderers = cullingResults.visibleRendererSnapshot;
        if ((drawingSettings.sortingSettings.criteria & SortingCriteria.BackToFront) != 0 && renderers.Length > 1)
        {
            renderers = (Renderer[])renderers.Clone();
            Vector3 cameraPosition = _nativeCameraPosition;
            Array.Sort(renderers, (left, right) =>
            {
                float leftDistance = (GetWorldBounds(left).center - cameraPosition).sqrMagnitude;
                float rightDistance = (GetWorldBounds(right).center - cameraPosition).sqrMagnitude;
                int comparison = rightDistance.CompareTo(leftDistance);
                return comparison != 0 ? comparison : left.GetInstanceID().CompareTo(right.GetInstanceID());
            });
        }
        foreach (var renderer in renderers)
        {
            if (renderer is null || renderer.gameObject is null || !renderer.enabled || !renderer.isVisible)
                continue;
            if ((filteringSettings.layerMask & (1u << renderer.gameObject.layer)) == 0u ||
                (filteringSettings.renderingLayerMask & renderer.renderingLayerMask) == 0u)
                continue;
            Mesh? mesh = renderer.gameObject.GetComponent<MeshFilter>()?.sharedMesh ??
                         renderer.gameObject.GetComponent<MeshFilter>()?.mesh ??
                         (renderer as SkinnedMeshRenderer)?.sharedMesh;
            if (mesh is null || mesh.vertexCount == 0)
                continue;
            Material[] materials = renderer.sharedMaterials;
            if (materials.Length == 0 && renderer.sharedMaterial is { } single)
                materials = new[] { single };
            Vector3[]? currentSkinnedPositions = null;
            bool submittedNativeMesh = false;
            for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
            {
                Material? material = materials[materialIndex];
                if (material is null)
                    continue;
                // Unity's -1 sentinel means "inherit shader queue", not an
                // invalid queue. Resolving it here is essential for default
                // opaque materials to survive URP's RenderQueueRange filter.
                int renderQueue = material.renderQueue >= 0
                    ? material.renderQueue
                    : material.shader?.renderQueue ?? (int)RenderQueue.Geometry;
                if (renderQueue < filteringSettings.renderQueueRange.min ||
                    renderQueue > filteringSettings.renderQueueRange.max)
                    continue;
                _drawCommands.Add(new DrawCommand
                {
                    type = DrawCommandType.DrawMesh,
                    mesh = mesh,
                    matrix = renderer.transform?.localToWorldMatrix ?? Matrix4x4.identity,
                    material = material,
                    layer = renderer.gameObject.layer,
                    submeshIndex = Math.Min(materialIndex, Math.Max(0, mesh.subMeshCount - 1)),
                    materialIndex = materialIndex,
                    castShadows = (ShadowCastingMode)(int)renderer.shadowCastingMode,
                    receiveShadows = renderer.receiveShadows,
                    probeAnchor = renderer.probeAnchor,
                    bounds = GetWorldBounds(renderer),
                    shaderPassName = new ShaderPassName(drawingSettings.GetShaderPassName(0).name)
                });
                if (_nativeGraphicsDevice is not null)
                {
                    Matrix4x4 localToWorld = renderer.transform?.localToWorldMatrix ?? Matrix4x4.identity;
                    Matrix4x4 previousLocalToWorld;
                    lock (s_MotionHistoryLock)
                    {
                        previousLocalToWorld = s_PreviousRendererLocalToWorld.TryGetValue(
                            renderer.GetInstanceID(), out var previous) ? previous : localToWorld;
                    }
                    // Unity exposes three distinct motion-generation modes:
                    // Camera carries only the camera delta, Object carries both
                    // camera and object deltas, and ForceNoMotion zeros them.
                    if (renderer.motionVectorGenerationMode != MotionVectorGenerationMode.Object)
                        previousLocalToWorld = localToWorld;
                    Vector3[] positions = mesh.vertices;
                    Vector3[] normals = mesh.normals;
                    Vector4[] tangents = mesh.tangents;
                    Vector3[]? previousSkinnedPositions = null;
                    if (renderer is SkinnedMeshRenderer skinned &&
                        NativeGraphicsDevice.TrySkinMeshVertices(mesh, skinned, out positions, out normals, out tangents))
                    {
                        currentSkinnedPositions = positions;
                        if (skinned.skinnedMotionVectors && renderer.motionVectorGenerationMode == MotionVectorGenerationMode.Object)
                        {
                            lock (s_MotionHistoryLock)
                                _ = s_PreviousSkinnedRendererPositions.TryGetValue(renderer.GetInstanceID(), out previousSkinnedPositions);
                            if (previousSkinnedPositions is not null && previousSkinnedPositions.Length != positions.Length)
                                previousSkinnedPositions = null;
                        }
                    }
                    Color[] colors = mesh.colors;
                    Color tint = material.mainColor;
                    if (tint == Color.white) tint = material.color;
                    if (colors.Length != positions.Length)
                    {
                        colors = new Color[positions.Length];
                        for (int i = 0; i < colors.Length; i++) colors[i] = tint;
                    }
                    else
                    {
                        for (int i = 0; i < colors.Length; i++)
                        {
                            Color vertexColor = colors[i];
                            colors[i] = new Color(vertexColor.r * tint.r, vertexColor.g * tint.g,
                                vertexColor.b * tint.b, vertexColor.a * tint.a);
                        }
                    }
                    Matrix4x4 previousObjectToClip = renderer.motionVectorGenerationMode == MotionVectorGenerationMode.ForceNoMotion
                        ? _nativeMotionViewProjection * localToWorld
                        : _nativePreviousMotionViewProjection * previousLocalToWorld;
                    Matrix4x4? stereoRightObjectToClip = null;
                    Matrix4x4? stereoRightMotionObjectToClip = null;
                    Matrix4x4? stereoRightPreviousObjectToClip = null;
                    if (_nativeSinglePassInstanced)
                    {
                        Matrix4x4 rightRasterViewProjection = renderQueue >= (int)RenderQueue.Transparent
                            ? _nativeStereoRightTransparentViewProjection : _nativeStereoRightViewProjection;
                        stereoRightObjectToClip = rightRasterViewProjection * localToWorld;
                        stereoRightMotionObjectToClip = _nativeStereoRightMotionViewProjection * localToWorld;
                        stereoRightPreviousObjectToClip = renderer.motionVectorGenerationMode == MotionVectorGenerationMode.ForceNoMotion
                            ? _nativeStereoRightMotionViewProjection * localToWorld
                            : _nativeStereoRightPreviousMotionViewProjection * previousLocalToWorld;
                    }
                    GetNativeMeshRasterState(material, renderQueue, out int blendMode,
                        out bool depthWriteEnabled, out bool alphaClipEnabled, out float alphaClipThreshold);
                    Texture? baseTexture = material.GetTexture("_BaseMap") ?? material.mainTexture;
                    string baseMapProperty = material.GetTexture("_BaseMap") is not null ? "_BaseMap" : "_MainTex";
                    Matrix4x4 rasterViewProjection = renderQueue >= (int)RenderQueue.Transparent
                        ? _nativeTransparentViewProjection : _nativeViewProjection;
                    submittedNativeMesh |= _nativeGraphicsDevice.TryDrawCameraMesh(_nativeCameraTarget,
                        positions, normals, colors, mesh.GetTriangles(Math.Min(materialIndex, Math.Max(0, mesh.subMeshCount - 1))),
                        rasterViewProjection * localToWorld, localToWorld.inverse.transpose,
                        previousObjectToClip, tangentObjectToWorld: localToWorld,
                        motionObjectToClip: _nativeMotionViewProjection * localToWorld,
                        targetIsCameraTarget: _nativeCameraTarget is null,
                        blendMode: blendMode, depthWriteEnabled: depthWriteEnabled,
                        writeMotionVectors: renderQueue <= (int)RenderQueue.GeometryLast,
                        depthSlice: _nativeCameraTargetSlice,
                        alphaClipEnabled: alphaClipEnabled, alphaClipThreshold: alphaClipThreshold,
                        uvs: mesh.uv, baseTexture: baseTexture,
                        baseMapScale: material.GetTextureScale(baseMapProperty),
                        baseMapOffset: material.GetTextureOffset(baseMapProperty), tangents: tangents,
                        normalMap: material.GetTexture("_BumpMap"), previousPositions: previousSkinnedPositions,
                        stereoRightObjectToClip: stereoRightObjectToClip,
                        stereoRightMotionObjectToClip: stereoRightMotionObjectToClip,
                        stereoRightPreviousObjectToClip: stereoRightPreviousObjectToClip);
                }
            }
            if (_nativeGraphicsDevice is not null && submittedNativeMesh)
            {
                Matrix4x4 localToWorld = renderer.transform?.localToWorldMatrix ?? Matrix4x4.identity;
                lock (s_MotionHistoryLock)
                {
                    s_PreviousRendererLocalToWorld[renderer.GetInstanceID()] = localToWorld;
                    if (currentSkinnedPositions is not null)
                        s_PreviousSkinnedRendererPositions[renderer.GetInstanceID()] = (Vector3[])currentSkinnedPositions.Clone();
                    TrimRendererMotionHistory();
                }
            }
        }
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

    internal void SetNativeCameraDrawState(Anity.Core.Runtime.Native.NativeGraphicsDevice? device,
        RenderTexture? target, Matrix4x4 viewProjection, Camera? camera = null,
        Matrix4x4? motionViewProjection = null, int targetSlice = 0,
        Matrix4x4? stereoRightViewProjection = null, Matrix4x4? stereoRightMotionViewProjection = null)
    {
        if (target is null && _hasNativeCameraMotionHistoryKey)
        {
            lock (s_MotionHistoryLock)
            {
                s_PreviousCameraViewProjections[_nativeCameraMotionHistoryKey] = _nativeMotionViewProjection;
                TrimCameraMotionHistory();
            }
            _nativeCameraInstanceId = 0;
            _nativeCameraMotionHistoryKey = 0;
            _hasNativeCameraMotionHistoryKey = false;
            if (_hasNativeStereoRightCameraMotionHistoryKey)
            {
                s_PreviousCameraViewProjections[_nativeStereoRightCameraMotionHistoryKey] = _nativeStereoRightMotionViewProjection;
                TrimCameraMotionHistory();
                _nativeStereoRightCameraMotionHistoryKey = 0;
                _hasNativeStereoRightCameraMotionHistoryKey = false;
            }
        }
        _nativeGraphicsDevice = device;
        _nativeCameraTarget = target;
        _nativeCameraTargetSlice = targetSlice;
        _nativeViewProjection = viewProjection;
        _nativeMotionViewProjection = motionViewProjection ?? viewProjection;
        _nativeSinglePassInstanced = stereoRightViewProjection.HasValue;
        _nativeStereoRightViewProjection = stereoRightViewProjection ?? viewProjection;
        _nativeStereoRightMotionViewProjection = stereoRightMotionViewProjection ?? _nativeStereoRightViewProjection;
        _nativeTransparentViewProjection = camera is not null && !camera.useJitteredProjectionMatrixForTransparentRendering
            ? _nativeMotionViewProjection : viewProjection;
        _nativeStereoRightTransparentViewProjection = camera is not null && !camera.useJitteredProjectionMatrixForTransparentRendering
            ? _nativeStereoRightMotionViewProjection : _nativeStereoRightViewProjection;
        _nativeCameraPosition = camera?.transform?.position ?? Vector3.zero;
        if (camera is not null)
        {
            _nativeCameraInstanceId = camera.GetInstanceID();
            _nativeCameraMotionHistoryKey = ComposeCameraMotionHistoryKey(_nativeCameraInstanceId, targetSlice);
            _hasNativeCameraMotionHistoryKey = true;
            lock (s_MotionHistoryLock)
                _nativePreviousMotionViewProjection = s_PreviousCameraViewProjections.TryGetValue(
                    _nativeCameraMotionHistoryKey, out var previous) ? previous : _nativeMotionViewProjection;
            if (_nativeSinglePassInstanced)
            {
                _nativeStereoRightCameraMotionHistoryKey = ComposeCameraMotionHistoryKey(_nativeCameraInstanceId, targetSlice + 1);
                _hasNativeStereoRightCameraMotionHistoryKey = true;
                lock (s_MotionHistoryLock)
                    _nativeStereoRightPreviousMotionViewProjection = s_PreviousCameraViewProjections.TryGetValue(
                        _nativeStereoRightCameraMotionHistoryKey, out var rightPrevious)
                        ? rightPrevious : _nativeStereoRightMotionViewProjection;
            }
            else
            {
                _nativeStereoRightPreviousMotionViewProjection = _nativeStereoRightMotionViewProjection;
            }
        }
        else
        {
            _nativePreviousMotionViewProjection = _nativeMotionViewProjection;
            _nativeStereoRightPreviousMotionViewProjection = _nativeStereoRightMotionViewProjection;
        }
    }

    private static long ComposeCameraMotionHistoryKey(int cameraInstanceId, int eyeSlice) =>
        ((long)cameraInstanceId << 32) | (uint)Math.Max(0, eyeSlice);

    private static void TrimCameraMotionHistory()
    {
        while (s_PreviousCameraViewProjections.Count > MaxMotionHistoryEntries)
        {
            using var entries = s_PreviousCameraViewProjections.GetEnumerator();
            if (!entries.MoveNext()) return;
            s_PreviousCameraViewProjections.Remove(entries.Current.Key);
        }
    }

    private static void TrimRendererMotionHistory()
    {
        while (s_PreviousRendererLocalToWorld.Count > MaxMotionHistoryEntries)
        {
            using var entries = s_PreviousRendererLocalToWorld.GetEnumerator();
            if (!entries.MoveNext()) return;
            int rendererId = entries.Current.Key;
            s_PreviousRendererLocalToWorld.Remove(rendererId);
            s_PreviousSkinnedRendererPositions.Remove(rendererId);
        }
        while (s_PreviousSkinnedRendererPositions.Count > MaxMotionHistoryEntries)
        {
            using var entries = s_PreviousSkinnedRendererPositions.GetEnumerator();
            if (!entries.MoveNext()) return;
            s_PreviousSkinnedRendererPositions.Remove(entries.Current.Key);
        }
    }

    public void Cull(ref ScriptableCullingParameters parameters, out CullingResults results)
    {
        var visible = new List<Renderer>();
        var camera = parameters.camera;
        Plane[]? frustum = camera is null ? null : GeometryUtility.CalculateFrustumPlanes(parameters.cullingMatrix);
        bool needsVelocityRasterization = false;
        foreach (var renderer in UnityEngine.Object.FindObjectsByType<Renderer>(
                     FindObjectsInactive.Exclude, FindObjectsSortMode.InstanceID))
        {
            if (renderer is null || !renderer.enabled || !renderer.isVisible || renderer.gameObject is null)
                continue;
            uint layerBit = 1u << renderer.gameObject.layer;
            if ((((uint)parameters.cullingMask) & layerBit) == 0u)
                continue;

            var bounds = GetWorldBounds(renderer);
            if (parameters.layerCullDistances is { } distances &&
                renderer.gameObject.layer < distances.Length)
            {
                float maxDistance = distances[renderer.gameObject.layer];
                if (maxDistance > 0f && bounds.SqrDistance(parameters.worldOrigin) > maxDistance * maxDistance)
                    continue;
            }
            if (frustum is not null && !GeometryUtility.TestPlanesAABB(frustum, bounds))
                continue;

            visible.Add(renderer);
            needsVelocityRasterization |= renderer.motionVectorGenerationMode == MotionVectorGenerationMode.Object;
        }

        results = new CullingResults
        {
            _visibleLights = new VisibleLight[0],
            _visibleReflectionProbes = new VisibleReflectionProbe[0],
            _lightAndReflectionProbeCount = 0,
            _visibleLightCount = 0,
            _visibleInstanceCount = visible.Count,
            _visibleRenderers = visible.ToArray(),
            visibleRenderers = new VisibleRendererList { length = visible.Count, nativePointer = IntPtr.Zero },
            velocityNeedsRasterization = needsVelocityRasterization
        };
        _cullingResults = results;
        _hasCullingResults = true;
    }

    private static Bounds GetWorldBounds(Renderer renderer)
    {
        Bounds local;
        if (renderer is SkinnedMeshRenderer skinned)
        {
            // Unity stores a skinned renderer's culling AABB in renderer-local
            // space. When an executable native skin stream is available, use
            // the current deformation so large bone/shape motion cannot be
            // rejected by the authored fallback AABB.
            if (skinned.sharedMesh is { } skinnedMesh &&
                NativeGraphicsDevice.TrySkinMeshVertices(skinnedMesh, skinned,
                    out var deformedPositions, out _, out _) && deformedPositions.Length != 0)
            {
                local = new Bounds(deformedPositions[0], Vector3.zero);
                for (int vertex = 1; vertex < deformedPositions.Length; vertex++)
                    local.Encapsulate(deformedPositions[vertex]);
            }
            else
            {
                // The shared mesh is only the initial default, so do not
                // substitute MeshFilter bounds once a caller has authored it.
                local = skinned.localBounds;
            }
        }
        else
        {
            var meshFilter = renderer.gameObject?.GetComponent<MeshFilter>();
            local = meshFilter?.sharedMesh?.bounds ?? meshFilter?.mesh?.bounds ?? renderer.localBounds;
        }
        var transform = renderer.transform;
        if (transform is null) return local;
        Vector3 min = local.min;
        Vector3 max = local.max;
        Vector3[] corners =
        {
            new(min.x, min.y, min.z), new(min.x, min.y, max.z),
            new(min.x, max.y, min.z), new(min.x, max.y, max.z),
            new(max.x, min.y, min.z), new(max.x, min.y, max.z),
            new(max.x, max.y, min.z), new(max.x, max.y, max.z)
        };
        Bounds world = new(transform.TransformPoint(corners[0]), Vector3.zero);
        for (int i = 1; i < corners.Length; i++)
            world.Encapsulate(transform.TransformPoint(corners[i]));
        return world;
    }

    private static void GetNativeMeshRasterState(Material material, int renderQueue,
        out int blendMode, out bool depthWriteEnabled, out bool alphaClipEnabled,
        out float alphaClipThreshold)
    {
        alphaClipEnabled = renderQueue >= (int)RenderQueue.AlphaTest && renderQueue <= (int)RenderQueue.GeometryLast &&
            (material.IsKeywordEnabled("_ALPHATEST_ON") || material.HasProperty("_Cutoff"));
        alphaClipThreshold = alphaClipEnabled ? Math.Clamp(material.GetFloat("_Cutoff"), 0f, 1f) : 0f;
        bool transparent = renderQueue >= (int)RenderQueue.Transparent;
        int src = material.GetInt("_SrcBlend");
        int dst = material.GetInt("_DstBlend");
        blendMode = !transparent ? 0 :
            src == (int)BlendMode.One && dst == (int)BlendMode.OneMinusSrcAlpha ? 2 :
            src == (int)BlendMode.SrcAlpha && dst == (int)BlendMode.One ? 3 :
            src == (int)BlendMode.DstColor && dst == (int)BlendMode.Zero ? 4 : 1;
        depthWriteEnabled = !transparent || material.GetInt("_ZWrite") != 0;
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
    // The managed culling bridge retains the actual renderer snapshot until
    // native scene ownership takes over; VisibleRendererList remains ABI-shaped.
    internal Renderer[] _visibleRenderers;

    public VisibleLight[] lights => _visibleLights ?? Array.Empty<VisibleLight>();
    public VisibleReflectionProbe[] reflectionProbes => _visibleReflectionProbes ?? Array.Empty<VisibleReflectionProbe>();
    public VisibleRendererList visibleRenderers;
    public bool velocityNeedsRasterization;
    public int lightAndReflectionProbeCount => _lightAndReflectionProbeCount;
    public int visibleLightCount => _visibleLightCount;
    public int visibleInstanceCount => _visibleInstanceCount;
    internal Renderer[] visibleRendererSnapshot => _visibleRenderers ?? Array.Empty<Renderer>();
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
