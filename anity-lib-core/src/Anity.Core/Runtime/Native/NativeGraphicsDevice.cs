using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.VFX;

namespace Anity.Core.Runtime.Native;

internal sealed record NativeVFXEventDispatchPlan(
    AnityNative.GraphicsVFXEventDispatchPlanInfo Info,
    AnityNative.GraphicsVFXEventDispatchBatch[] Batches,
    byte[] Records);

internal sealed record NativeVFXInitializeDispatch(
    AnityNative.GraphicsVFXInitializeDispatchInfo Info,
    byte[] Records);

internal sealed record NativeVFXParticleSystem(
    AnityNative.GraphicsVFXParticleSystemInfo Info,
    byte[] AttributeRecords,
    uint[] DeadList);

/// <summary>
/// Managed wrapper over AnityGraphics_* — Unity player/editor graphics device bootstrap.
/// </summary>
public sealed class NativeGraphicsDevice : IDisposable
{
    internal AnityNative.Result LastVFXUpdateSubmitResult { get; private set; }
    private static readonly object DevicesLock = new();
    private static readonly HashSet<NativeGraphicsDevice> LiveDevices = new();
    private readonly object _lifetimeLock = new();
    private readonly object _textureLock = new();
    private IntPtr _handle;
    private IntPtr _swapchain;
    private bool _disposed;
    private bool _disposePending;
    private int _nativeUseDepth;
    private bool _managedSwapchain;
    private NativeUICanvas? _uiCanvas;
    private readonly Dictionary<ulong, ulong> _textureStates = new();

    public static NativeGraphicsDevice? Current { get; private set; }

    public IntPtr Handle => _handle;
    public IntPtr SwapchainHandle => _swapchain;
    public bool IsValid => _handle != IntPtr.Zero || _managedSwapchain;
    public bool HasSwapchain => _swapchain != IntPtr.Zero || _managedSwapchain;
    public GraphicsDeviceType DeviceType { get; private set; }
    public bool SupportsHDR { get; private set; }
    public int Width { get; private set; }
    public int Height { get; private set; }
    public int SwapchainImageCount { get; private set; }
    public bool SwapchainHeadless { get; private set; } = true;
    public bool SwapchainHasNativeSurface { get; private set; }
    /// <summary>0=software, 1=Vulkan, 2=Metal, 3=D3D</summary>
    public int SwapchainBackendKind { get; private set; }
    /// <summary>0=none, 1=Win32, 2=Android ANativeWindow, 3=X11, 4=Wayland</summary>
    public int SwapchainSurfaceKind { get; private set; }
    public int PresentCount { get; private set; }
    public AnityNative.Result LastSwapchainResult { get; private set; } =
        AnityNative.Result.Ok;
    public AnityNative.GraphicsCameraPassInfo LastCameraPass { get; private set; }
    public bool HasAttachedUICanvas => _uiCanvas is { IsValid: true };
    internal NativeUICanvas? AttachedUICanvas => _uiCanvas is { IsValid: true } ? _uiCanvas : null;
    public AnityNative.GraphicsUIUploadStats LastUIUploadStats { get; private set; }

    /// <summary>Compile-time Vulkan surface platforms: bit0=Win32, bit1=Android, bit2=X11, bit3=Wayland.</summary>
    public static int VulkanSupportedSurfaceMask
    {
        get
        {
            if (!AnityNative.Available) return ExpectedSurfaceMaskForHost();
            try { return AnityNative.Graphics_Vulkan_GetSupportedSurfaceMask(); }
            catch { return ExpectedSurfaceMaskForHost(); }
        }
    }

    public static int ExpectedSurfaceMaskForHost()
    {
        // Managed fallback when native absent — report host OS compile expectations
        if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
                System.Runtime.InteropServices.OSPlatform.Windows))
            return 1; // Win32
        if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
                System.Runtime.InteropServices.OSPlatform.Linux))
            return 4 | 8; // X11 | Wayland capability
        // Android / other: mask reported by native when present
        return 0;
    }

    public static NativeGraphicsDevice Create(
        GraphicsDeviceType preferred,
        int width,
        int height,
        bool hdr,
        int msaa = 1,
        bool vsync = true,
        IntPtr nativeWindow = default)
    {
        var dev = new NativeGraphicsDevice();
        if (AnityNative.Available)
        {
            var desc = new AnityNative.GraphicsDeviceDesc
            {
                preferred = (int)Map(preferred),
                width = width,
                height = height,
                hdrEnabled = hdr ? 1 : 0,
                msaaSamples = msaa,
                vsync = vsync ? 1 : 0,
                nativeWindow = nativeWindow
            };
            try
            {
                if (AnityNative.Graphics_CreateDevice(ref desc, out var h) == AnityNative.Result.Ok && h != IntPtr.Zero)
                {
                    dev._handle = h;
                    dev.DeviceType = MapBack(AnityNative.Graphics_GetDeviceType(h));
                    dev.SupportsHDR = AnityNative.Graphics_SupportsHDR(h) != 0;
                    dev.Width = width;
                    dev.Height = height;
                    lock (DevicesLock)
                    {
                        LiveDevices.Add(dev);
                        Current = dev;
                    }
                    SystemInfo.overrideGraphicsDeviceType = dev.DeviceType;
                    return dev;
                }
            }
            catch
            {
                AnityNative.MarkUnavailable();
            }
        }

        // Managed fallback device record
        dev.DeviceType = preferred;
        dev.SupportsHDR = hdr;
        dev.Width = width;
        dev.Height = height;
        SystemInfo.overrideGraphicsDeviceType = preferred;
        lock (DevicesLock)
        {
            LiveDevices.Add(dev);
            Current = dev;
        }
        return dev;
    }

    internal bool ExecuteWhileAlive(Action action)
    {
        if (action is null) throw new ArgumentNullException(nameof(action));
        lock (_lifetimeLock)
        {
            if (_disposed) return false;
            _nativeUseDepth++;
            try
            {
                action();
                return true;
            }
            finally
            {
                _nativeUseDepth--;
                if (_nativeUseDepth == 0 && _disposePending)
                    DisposeCore();
            }
        }
    }

    public void BeginFrame()
    {
        CanvasNativeRenderBridge.Flush(this);
        EnsureAttachedCanvasValid();
        if (_handle != IntPtr.Zero && AnityNative.Available)
            AnityNative.Graphics_BeginFrame(_handle);
    }

    public void EndFrame()
    {
        EnsureAttachedCanvasValid();
        if (_handle != IntPtr.Zero && AnityNative.Available)
        {
            AnityNative.Graphics_EndFrame(_handle);
            RefreshUIUploadStats();
        }
    }

    /// <summary>
    /// Records one validated camera render-pass contract in the native graphics
    /// control plane. Backend encoders consume this state as target support is
    /// brought online; callers can inspect the returned native sequence now.
    /// </summary>
    public bool TryRecordCameraPass(
        ulong targetId, int targetWidth, int targetHeight, Rect viewport,
        Color clearColor, bool clearDepth, bool clearColorEnabled,
        int msaaSamples, bool isFinal, bool hdr, bool isCameraTarget = true,
        float clearDepthValue = 1f, int depthSlice = 0, int depthSliceCount = 1)
    {
        if (_disposed || _handle == IntPtr.Zero || !AnityNative.Available) return false;
        var flags = AnityNative.GraphicsCameraPassFlags.StoreColor |
                    AnityNative.GraphicsCameraPassFlags.StoreDepth;
        if (clearColorEnabled) flags |= AnityNative.GraphicsCameraPassFlags.ClearColor;
        if (clearDepth) flags |= AnityNative.GraphicsCameraPassFlags.ClearDepth;
        if (isFinal) flags |= AnityNative.GraphicsCameraPassFlags.Final;
        if (hdr) flags |= AnityNative.GraphicsCameraPassFlags.Hdr;
        if (isCameraTarget) flags |= AnityNative.GraphicsCameraPassFlags.TargetIsCameraTarget;
        var desc = new AnityNative.GraphicsCameraPassDesc
        {
            targetId = targetId,
            targetWidth = targetWidth,
            targetHeight = targetHeight,
            viewportX = viewport.x,
            viewportY = viewport.y,
            viewportWidth = viewport.width,
            viewportHeight = viewport.height,
            clearR = clearColor.r,
            clearG = clearColor.g,
            clearB = clearColor.b,
            clearA = clearColor.a,
            clearDepth = clearDepthValue,
            msaaSamples = msaaSamples,
            depthSlice = depthSlice,
            depthSliceCount = depthSliceCount,
            flags = flags
        };
        try
        {
            if (AnityNative.Graphics_RecordCameraPass(_handle, ref desc, out var info) != AnityNative.Result.Ok)
                return false;
            LastCameraPass = info;
            return true;
        }
        catch
        {
            if (NativeRequired) throw;
            return false;
        }
    }

    /// <summary>Ensures a native offscreen attachment for <see cref="Camera.targetTexture"/>.</summary>
    internal bool EnsureCameraRenderTarget(RenderTexture? target)
    {
        if (target is null || target.width <= 0 || target.height <= 0 ||
            _disposed || _handle == IntPtr.Zero || !AnityNative.Available) return false;
        if (!target.IsCreated()) target.Create();
        int samples = Math.Max(1, target.msaaSamples);
        var desc = new AnityNative.GraphicsCameraRenderTargetDesc
        {
            targetId = unchecked((ulong)(uint)target.GetInstanceID()),
            width = target.width,
            height = target.height,
            msaaSamples = samples,
            hdrEnabled = IsHdrRenderTexture(target) ? 1 : 0,
            dimension = (int)target.dimension,
            volumeDepth = Math.Max(1, target.volumeDepth),
            colorFormat = target.graphicsFormat == GraphicsFormat.R8G8B8A8_SNorm ? 1 :
                (target.graphicsFormat == GraphicsFormat.R16G16_SFloat || target.format == RenderTextureFormat.RGHalf ? 2 : 0)
        };
        try
        {
            return AnityNative.Graphics_EnsureCameraRenderTarget(_handle, ref desc) ==
                AnityNative.Result.Ok;
        }
        catch
        {
            if (NativeRequired) throw;
            return false;
        }
    }

    internal bool ReleaseCameraRenderTarget(RenderTexture? target)
    {
        if (target is null || _disposed || _handle == IntPtr.Zero || !AnityNative.Available)
            return false;
        try
        {
            return AnityNative.Graphics_DestroyCameraRenderTarget(
                _handle, unchecked((ulong)(uint)target.GetInstanceID())) == AnityNative.Result.Ok;
        }
        catch
        {
            if (NativeRequired) throw;
            return false;
        }
    }

    internal bool TryDrawCameraMesh(RenderTexture? target, Vector3[] positions,
        Vector3[] normals, Color[] colors, int[] indices, Matrix4x4 objectToClip,
        Matrix4x4? normalObjectToWorld = null, Matrix4x4? previousObjectToClip = null,
        Matrix4x4? tangentObjectToWorld = null, Matrix4x4? motionObjectToClip = null,
        bool targetIsCameraTarget = false, int blendMode = 0, bool depthWriteEnabled = true,
        bool writeMotionVectors = true, int depthSlice = 0,
        bool alphaClipEnabled = false, float alphaClipThreshold = 0.5f,
        Vector2[]? uvs = null, Texture? baseTexture = null, Vector2? baseMapScale = null,
        Vector2? baseMapOffset = null, Vector4[]? tangents = null, Texture? normalMap = null,
        Vector3[]? previousPositions = null, Matrix4x4? stereoRightObjectToClip = null,
        Matrix4x4? stereoRightMotionObjectToClip = null,
        Matrix4x4? stereoRightPreviousObjectToClip = null)
    {
        if ((target is null && !targetIsCameraTarget) || positions is null || indices is null || positions.Length == 0 ||
            indices.Length == 0 || indices.Length % 3 != 0 || _disposed || _handle == IntPtr.Zero ||
            !AnityNative.Available || blendMode < 0 || blendMode > 4 || !float.IsFinite(alphaClipThreshold) ||
            depthSlice < 0 ||
            (target is not null && !EnsureCameraRenderTarget(target))) return false;
        if (stereoRightObjectToClip.HasValue &&
            (target is null || target.dimension != UnityEngine.TextureDimension.Tex2DArray || target.volumeDepth < depthSlice + 2))
            return false;
        Matrix4x4 normalMatrix = normalObjectToWorld ?? Matrix4x4.identity;
        Matrix4x4 tangentMatrix = tangentObjectToWorld ?? normalMatrix;
        float tangentHandedness = tangentMatrix.determinant < 0f ? -1f : 1f;
        var vertices = new AnityNative.GraphicsMeshVertex[positions.Length];
        for (int i = 0; i < vertices.Length; i++)
        {
            var p = positions[i]; var localNormal = i < normals.Length ? normals[i] : Vector3.forward;
            var previousPosition = previousPositions is not null && i < previousPositions.Length ? previousPositions[i] : p;
            var uv = uvs is not null && i < uvs.Length ? uvs[i] : Vector2.zero;
            var n = normalMatrix.MultiplyVector(localNormal).normalized;
            if (n.sqrMagnitude < 1e-12f) n = Vector3.forward;
            var localTangent = tangents is not null && i < tangents.Length ? tangents[i] : new Vector4(1f, 0f, 0f, 1f);
            var tangent = tangentMatrix.MultiplyVector(new Vector3(localTangent.x, localTangent.y, localTangent.z));
            tangent = (tangent - n * Vector3.Dot(n, tangent)).normalized;
            if (tangent.sqrMagnitude < 1e-12f)
                tangent = Vector3.Cross(Math.Abs(n.z) < .999f ? Vector3.forward : Vector3.up, n).normalized;
            var c = i < colors.Length ? colors[i] : Color.white;
            vertices[i] = new AnityNative.GraphicsMeshVertex { px = p.x, py = p.y, pz = p.z,
                ppx = previousPosition.x, ppy = previousPosition.y, ppz = previousPosition.z,
                nx = n.x, ny = n.y, nz = n.z, tx = tangent.x, ty = tangent.y, tz = tangent.z, tw = localTangent.w * tangentHandedness,
                u = uv.x, v = uv.y, r = c.r, g = c.g, b = c.b, a = c.a };
        }
        var nativeIndices = new uint[indices.Length];
        for (int i = 0; i < indices.Length; i++)
            if (indices[i] < 0 || indices[i] >= vertices.Length) return false;
            else nativeIndices[i] = (uint)indices[i];
        var vertexPin = GCHandle.Alloc(vertices, GCHandleType.Pinned);
        var indexPin = GCHandle.Alloc(nativeIndices, GCHandleType.Pinned);
        try
        {
            Matrix4x4 resolvedMotionObjectToClip = motionObjectToClip ?? objectToClip;
            Matrix4x4 resolvedPreviousObjectToClip = previousObjectToClip ?? objectToClip;
            Matrix4x4 rightObjectToClip = stereoRightObjectToClip ?? objectToClip;
            Matrix4x4 rightMotionObjectToClip = stereoRightMotionObjectToClip ?? resolvedMotionObjectToClip;
            Matrix4x4 rightPreviousObjectToClip = stereoRightPreviousObjectToClip ?? resolvedPreviousObjectToClip;
            var desc = new AnityNative.GraphicsCameraMeshDrawDesc
            {
                targetId = target is null ? 0UL : unchecked((ulong)(uint)target.GetInstanceID()),
                targetIsCameraTarget = targetIsCameraTarget ? 1 : 0,
                blendMode = blendMode,
                depthWriteEnabled = depthWriteEnabled ? 1 : 0,
                writeMotionVectors = writeMotionVectors ? 1 : 0,
                depthSlice = depthSlice,
                alphaClipEnabled = alphaClipEnabled ? 1 : 0,
                alphaClipThreshold = alphaClipThreshold,
                baseTextureId = baseTexture is not null && EnsureTexture(baseTexture)
                    ? unchecked((ulong)(uint)baseTexture.GetInstanceID()) : 0UL,
                baseMapST = new[] { (baseMapScale ?? Vector2.one).x, (baseMapScale ?? Vector2.one).y,
                    (baseMapOffset ?? Vector2.zero).x, (baseMapOffset ?? Vector2.zero).y },
                normalMapTextureId = normalMap is not null && EnsureTexture(normalMap)
                    ? unchecked((ulong)(uint)normalMap.GetInstanceID()) : 0UL,
                vertices = vertexPin.AddrOfPinnedObject(), vertexCount = vertices.Length,
                indices = indexPin.AddrOfPinnedObject(), indexCount = nativeIndices.Length,
                // Metal float4x4 constants are column-major; Matrix4x4's
                // field order is row-major, so transpose at the ABI edge.
                objectToClip = ToMetalMatrix(objectToClip),
                motionObjectToClip = ToMetalMatrix(resolvedMotionObjectToClip),
                previousObjectToClip = ToMetalMatrix(resolvedPreviousObjectToClip),
                hasPreviousObjectToClip = previousObjectToClip.HasValue ? 1 : 0,
                stereoInstanceCount = stereoRightObjectToClip.HasValue ? 2 : 1,
                stereoObjectToClip = CombineMetalMatrices(objectToClip, rightObjectToClip),
                stereoMotionObjectToClip = CombineMetalMatrices(resolvedMotionObjectToClip, rightMotionObjectToClip),
                stereoPreviousObjectToClip = CombineMetalMatrices(resolvedPreviousObjectToClip, rightPreviousObjectToClip)
            };
            return AnityNative.Graphics_DrawCameraMesh(_handle, ref desc) == AnityNative.Result.Ok;
        }
        catch { if (NativeRequired) throw; return false; }
        finally { indexPin.Free(); vertexPin.Free(); }
    }

    /// <summary>
    /// Runs the authoritative native four-weight skinning kernel.  Output stays
    /// in renderer-local space so the normal mesh submission path can apply the
    /// renderer's current/previous object transforms consistently.
    /// </summary>
    internal static bool TrySkinMeshVertices(Mesh mesh, SkinnedMeshRenderer renderer,
        out Vector3[] positions, out Vector3[] normals, out Vector4[] tangents)
    {
        positions = mesh.vertices; normals = mesh.normals; tangents = mesh.tangents;
        BoneWeight[] weights = mesh.boneWeights;
        Matrix4x4[] bindposes = mesh.bindposes;
        Transform[] bones = renderer.bones;
        Transform? rendererTransform = renderer.transform;
        if (!AnityNative.Available || rendererTransform is null || positions.Length == 0 ||
            weights.Length != positions.Length || bones.Length == 0 || bindposes.Length == 0)
            return false;
        int boneCount = Math.Min(bones.Length, bindposes.Length);
        if (boneCount <= 0) return false;
        var source = new AnityNative.GraphicsSkinVertex[positions.Length];
        var nativeWeights = new AnityNative.GraphicsBoneWeight[positions.Length];
        var matrices = new float[boneCount * 16];
        var output = new AnityNative.GraphicsSkinVertex[positions.Length];
        for (int i = 0; i < positions.Length; i++)
        {
            Vector3 normal = i < normals.Length ? normals[i] : Vector3.forward;
            Vector4 tangent = i < tangents.Length ? tangents[i] : new Vector4(1f, 0f, 0f, 1f);
            Vector3 position = positions[i]; BoneWeight weight = weights[i];
            source[i] = new AnityNative.GraphicsSkinVertex { px = position.x, py = position.y, pz = position.z,
                nx = normal.x, ny = normal.y, nz = normal.z, tx = tangent.x, ty = tangent.y, tz = tangent.z, tw = tangent.w };
            nativeWeights[i] = new AnityNative.GraphicsBoneWeight { w0 = weight.weight0, w1 = weight.weight1,
                w2 = weight.weight2, w3 = weight.weight3, i0 = weight.boneIndex0, i1 = weight.boneIndex1,
                i2 = weight.boneIndex2, i3 = weight.boneIndex3 };
        }
        if (!TryApplyBlendShapeDeltas(mesh, renderer, source)) return false;
        bool useVariableInfluences = mesh.TryGetVariableBoneWeights(out var bonesPerVertex, out var allBoneWeights);
        if (useVariableInfluences && (bonesPerVertex.Length != source.Length || allBoneWeights.Length == 0)) return false;
        var variableWeights = new AnityNative.GraphicsBoneWeight1[allBoneWeights.Length];
        for (int i = 0; i < variableWeights.Length; i++)
            variableWeights[i] = new AnityNative.GraphicsBoneWeight1 { weight = allBoneWeights[i].weight, boneIndex = allBoneWeights[i].boneIndex };
        Matrix4x4 worldToRenderer = rendererTransform.worldToLocalMatrix;
        for (int boneIndex = 0; boneIndex < boneCount; boneIndex++)
        {
            Transform? bone = bones[boneIndex];
            if (bone is null) return false;
            CopyUnityMatrix(worldToRenderer * bone.localToWorldMatrix * bindposes[boneIndex], matrices, boneIndex * 16);
        }
        var sourcePin = GCHandle.Alloc(source, GCHandleType.Pinned);
        var weightsPin = GCHandle.Alloc(nativeWeights, GCHandleType.Pinned);
        var matricesPin = GCHandle.Alloc(matrices, GCHandleType.Pinned);
        var outputPin = GCHandle.Alloc(output, GCHandleType.Pinned);
        var variableCountsPin = useVariableInfluences ? GCHandle.Alloc(bonesPerVertex, GCHandleType.Pinned) : default;
        var variableWeightsPin = useVariableInfluences ? GCHandle.Alloc(variableWeights, GCHandleType.Pinned) : default;
        try
        {
            var desc = new AnityNative.GraphicsSkinMeshDesc
            {
                sourceVertices = sourcePin.AddrOfPinnedObject(), boneWeights = weightsPin.AddrOfPinnedObject(),
                vertexCount = source.Length, boneMatrices = matricesPin.AddrOfPinnedObject(), boneCount = boneCount,
                outVertices = outputPin.AddrOfPinnedObject(), outVertexCount = output.Length,
                bonesPerVertex = useVariableInfluences ? variableCountsPin.AddrOfPinnedObject() : IntPtr.Zero,
                allBoneWeights = useVariableInfluences ? variableWeightsPin.AddrOfPinnedObject() : IntPtr.Zero,
                allBoneWeightCount = variableWeights.Length, maxInfluences = ResolveSkinInfluenceLimit(renderer)
            };
            if (AnityNative.Graphics_SkinMeshVertices(ref desc) != AnityNative.Result.Ok) return false;
        }
        catch { return false; }
        finally
        {
            if (variableWeightsPin.IsAllocated) variableWeightsPin.Free();
            if (variableCountsPin.IsAllocated) variableCountsPin.Free();
            outputPin.Free(); matricesPin.Free(); weightsPin.Free(); sourcePin.Free();
        }
        normals = new Vector3[output.Length]; tangents = new Vector4[output.Length]; positions = new Vector3[output.Length];
        for (int i = 0; i < output.Length; i++)
        {
            AnityNative.GraphicsSkinVertex vertex = output[i];
            positions[i] = new Vector3(vertex.px, vertex.py, vertex.pz);
            normals[i] = new Vector3(vertex.nx, vertex.ny, vertex.nz);
            tangents[i] = new Vector4(vertex.tx, vertex.ty, vertex.tz, vertex.tw);
        }
        return true;
    }

    private static int ResolveSkinInfluenceLimit(SkinnedMeshRenderer renderer)
    {
        if (renderer.skinWeight == SkinQuality.Bone1) return 1;
        if (renderer.skinWeight == SkinQuality.Bone2) return 2;
        if (renderer.skinWeight == SkinQuality.Bone4) return 4;
        return QualitySettings.skinWeights switch
        {
            SkinWeights.OneBone => 1,
            SkinWeights.TwoBones => 2,
            SkinWeights.FourBones => 4,
            SkinWeights.None => 1,
            _ => 8
        };
    }

    private static bool TryApplyBlendShapeDeltas(Mesh mesh, SkinnedMeshRenderer renderer,
        AnityNative.GraphicsSkinVertex[] source)
    {
        int totalShapeCount = mesh.blendShapeCount;
        if (totalShapeCount == 0) return true;
        var activeDeltas = new List<AnityNative.GraphicsSkinVertex[]>();
        for (int shapeIndex = 0; shapeIndex < totalShapeCount; shapeIndex++)
        {
            float weight = renderer.GetBlendShapeWeight(shapeIndex);
            if (weight == 0f) continue;
            if (!mesh.TryEvaluateBlendShape(shapeIndex, weight, out var positionDeltas,
                    out var normalDeltas, out var tangentDeltas) || positionDeltas.Length != source.Length)
                return false;
            var nativeDeltas = new AnityNative.GraphicsSkinVertex[source.Length];
            for (int vertex = 0; vertex < nativeDeltas.Length; vertex++)
            {
                Vector3 position = positionDeltas[vertex];
                Vector3 normal = vertex < normalDeltas.Length ? normalDeltas[vertex] : Vector3.zero;
                Vector3 tangent = vertex < tangentDeltas.Length ? tangentDeltas[vertex] : Vector3.zero;
                nativeDeltas[vertex] = new AnityNative.GraphicsSkinVertex { px = position.x, py = position.y, pz = position.z,
                    nx = normal.x, ny = normal.y, nz = normal.z, tx = tangent.x, ty = tangent.y, tz = tangent.z, tw = 0f };
            }
            activeDeltas.Add(nativeDeltas);
        }
        if (activeDeltas.Count == 0) return true;
        var flattened = new AnityNative.GraphicsSkinVertex[activeDeltas.Count * source.Length];
        for (int shape = 0; shape < activeDeltas.Count; shape++)
            Array.Copy(activeDeltas[shape], 0, flattened, shape * source.Length, source.Length);
        var blended = new AnityNative.GraphicsSkinVertex[source.Length];
        var sourcePin = GCHandle.Alloc(source, GCHandleType.Pinned);
        var deltasPin = GCHandle.Alloc(flattened, GCHandleType.Pinned);
        var outputPin = GCHandle.Alloc(blended, GCHandleType.Pinned);
        try
        {
            var desc = new AnityNative.GraphicsBlendShapeDesc
            {
                sourceVertices = sourcePin.AddrOfPinnedObject(), shapeDeltas = deltasPin.AddrOfPinnedObject(),
                vertexCount = source.Length, shapeCount = activeDeltas.Count,
                outVertices = outputPin.AddrOfPinnedObject(), outVertexCount = blended.Length
            };
            if (AnityNative.Graphics_ApplyBlendShapeDeltas(ref desc) != AnityNative.Result.Ok) return false;
            Array.Copy(blended, source, source.Length);
            return true;
        }
        catch { return false; }
        finally { outputPin.Free(); deltasPin.Free(); sourcePin.Free(); }
    }

    private static void CopyUnityMatrix(Matrix4x4 value, float[] destination, int offset)
    {
        destination[offset] = value.m00; destination[offset + 1] = value.m01; destination[offset + 2] = value.m02; destination[offset + 3] = value.m03;
        destination[offset + 4] = value.m10; destination[offset + 5] = value.m11; destination[offset + 6] = value.m12; destination[offset + 7] = value.m13;
        destination[offset + 8] = value.m20; destination[offset + 9] = value.m21; destination[offset + 10] = value.m22; destination[offset + 11] = value.m23;
        destination[offset + 12] = value.m30; destination[offset + 13] = value.m31; destination[offset + 14] = value.m32; destination[offset + 15] = value.m33;
    }

    private static float[] ToMetalMatrix(Matrix4x4 value) => new[]
    {
        value.m00, value.m10, value.m20, value.m30,
        value.m01, value.m11, value.m21, value.m31,
        value.m02, value.m12, value.m22, value.m32,
        value.m03, value.m13, value.m23, value.m33
    };

    private static float[] CombineMetalMatrices(Matrix4x4 left, Matrix4x4 right)
    {
        var result = new float[32];
        Array.Copy(ToMetalMatrix(left), 0, result, 0, 16);
        Array.Copy(ToMetalMatrix(right), 0, result, 16, 16);
        return result;
    }

    internal static void ReleaseCameraRenderTargetFromAll(RenderTexture target)
    {
        NativeGraphicsDevice[] devices;
        lock (DevicesLock)
        {
            devices = new NativeGraphicsDevice[LiveDevices.Count];
            LiveDevices.CopyTo(devices);
        }
        foreach (NativeGraphicsDevice device in devices)
            device.ReleaseCameraRenderTarget(target);
    }

    internal bool TryReadbackCameraRenderTargetRGBA8(RenderTexture? target, out byte[] pixels, int depthSlice = 0)
    {
        pixels = Array.Empty<byte>();
        if (target is null || _disposed || _handle == IntPtr.Zero || !AnityNative.Available ||
            target.width <= 0 || target.height <= 0 || depthSlice < 0 || depthSlice >= Math.Max(1, target.volumeDepth)) return false;
        try
        {
            var result = new byte[checked(target.width * target.height * 4)];
            AnityNative.Result readbackResult = depthSlice == 0
                ? AnityNative.Graphics_ReadbackCameraRenderTargetRGBA8(
                    _handle, unchecked((ulong)(uint)target.GetInstanceID()), result, result.Length, out int written)
                : AnityNative.Graphics_ReadbackCameraRenderTargetSliceRGBA8(
                    _handle, unchecked((ulong)(uint)target.GetInstanceID()), depthSlice, result, result.Length, out written);
            if (readbackResult != AnityNative.Result.Ok ||
                written != result.Length) return false;
            pixels = result;
            return true;
        }
        catch
        {
            if (NativeRequired) throw;
            return false;
        }
    }

    /// <summary>Explicit ACES-to-sRGB readback for a native HDR Camera.targetTexture.</summary>
    internal bool TryReadbackCameraRenderTargetToneMappedRGBA8(RenderTexture? target, out byte[] pixels)
        => TryReadbackCameraRenderTargetToneMappedRGBA8(target, out pixels, 0);

    /// <summary>Explicit ACES-to-sRGB readback for one HDR Texture2DArray eye layer.</summary>
    internal bool TryReadbackCameraRenderTargetToneMappedRGBA8(RenderTexture? target, out byte[] pixels, int depthSlice)
    {
        pixels = Array.Empty<byte>();
        if (target is null || _disposed || _handle == IntPtr.Zero || !AnityNative.Available ||
            target.width <= 0 || target.height <= 0 || depthSlice < 0 ||
            depthSlice >= Math.Max(1, target.volumeDepth)) return false;
        try
        {
            var result = new byte[checked(target.width * target.height * 4)];
            if (AnityNative.Graphics_ReadbackCameraRenderTargetToneMappedSliceRGBA8(
                    _handle, unchecked((ulong)(uint)target.GetInstanceID()), depthSlice,
                    result, result.Length, out int written) != AnityNative.Result.Ok ||
                written != result.Length) return false;
            pixels = result;
            return true;
        }
        catch
        {
            if (NativeRequired) throw;
            return false;
        }
    }

    /// <summary>Copies a resolved camera color attachment without a CPU readback.</summary>
    internal bool TryCopyCameraRenderTargetColor(RenderTexture? source, bool sourceIsCameraTarget, RenderTexture? destination,
        int sourceSlice = 0, int destinationSlice = 0)
    {
        if (destination is null || _disposed || _handle == IntPtr.Zero || !AnityNative.Available ||
            (!sourceIsCameraTarget && source is null)) return false;
        try
        {
            if (sourceSlice < 0 || destinationSlice < 0 || destinationSlice >= Math.Max(1, destination.volumeDepth) ||
                (!sourceIsCameraTarget && source is not null && sourceSlice >= Math.Max(1, source.volumeDepth))) return false;
            ulong sourceId = source is null ? 0UL : unchecked((ulong)(uint)source.GetInstanceID());
            ulong destinationId = unchecked((ulong)(uint)destination.GetInstanceID());
            AnityNative.Result result = sourceSlice == 0 && destinationSlice == 0
                ? AnityNative.Graphics_CopyCameraRenderTargetColor(_handle, sourceId, sourceIsCameraTarget ? 1 : 0, destinationId)
                : AnityNative.Graphics_CopyCameraRenderTargetColorSlice(_handle, sourceId, sourceIsCameraTarget ? 1 : 0,
                    sourceSlice, destinationSlice, destinationId);
            return result == AnityNative.Result.Ok;
        }
        catch
        {
            if (NativeRequired) throw;
            return false;
        }
    }

    /// <summary>Writes native camera depth into the red channel of a color target on the GPU.</summary>
    internal bool TryCopyCameraRenderTargetDepthToColor(RenderTexture? source, bool sourceIsCameraTarget, RenderTexture? destination,
        int sourceSlice = 0, int destinationSlice = 0)
    {
        if (destination is null || _disposed || _handle == IntPtr.Zero || !AnityNative.Available ||
            (!sourceIsCameraTarget && source is null)) return false;
        try
        {
            if (sourceSlice < 0 || destinationSlice < 0 || destinationSlice >= Math.Max(1, destination.volumeDepth) ||
                (!sourceIsCameraTarget && source is not null && sourceSlice >= Math.Max(1, source.volumeDepth))) return false;
            ulong sourceId = source is null ? 0UL : unchecked((ulong)(uint)source.GetInstanceID());
            ulong destinationId = unchecked((ulong)(uint)destination.GetInstanceID());
            AnityNative.Result result = sourceSlice == 0 && destinationSlice == 0
                ? AnityNative.Graphics_CopyCameraRenderTargetDepthToColor(_handle, sourceId, sourceIsCameraTarget ? 1 : 0, destinationId)
                : AnityNative.Graphics_CopyCameraRenderTargetDepthToColorSlice(_handle, sourceId, sourceIsCameraTarget ? 1 : 0,
                    sourceSlice, destinationSlice, destinationId);
            return result == AnityNative.Result.Ok;
        }
        catch
        {
            if (NativeRequired) throw;
            return false;
        }
    }

    /// <summary>Copies the native mesh-normal attachment to a color target on the GPU.</summary>
    internal bool TryCopyCameraRenderTargetNormalsToColor(RenderTexture? source, bool sourceIsCameraTarget, RenderTexture? destination,
        int sourceSlice = 0, int destinationSlice = 0)
    {
        if (destination is null || _disposed || _handle == IntPtr.Zero || !AnityNative.Available ||
            (!sourceIsCameraTarget && source is null)) return false;
        try
        {
            if (sourceSlice < 0 || destinationSlice < 0 || destinationSlice >= Math.Max(1, destination.volumeDepth) ||
                (!sourceIsCameraTarget && source is not null && sourceSlice >= Math.Max(1, source.volumeDepth))) return false;
            ulong sourceId = source is null ? 0UL : unchecked((ulong)(uint)source.GetInstanceID());
            ulong destinationId = unchecked((ulong)(uint)destination.GetInstanceID());
            AnityNative.Result result = sourceSlice == 0 && destinationSlice == 0
                ? AnityNative.Graphics_CopyCameraRenderTargetNormalsToColor(_handle, sourceId, sourceIsCameraTarget ? 1 : 0, destinationId)
                : AnityNative.Graphics_CopyCameraRenderTargetNormalsToColorSlice(_handle, sourceId, sourceIsCameraTarget ? 1 : 0,
                    sourceSlice, destinationSlice, destinationId);
            return result == AnityNative.Result.Ok;
        }
        catch
        {
            if (NativeRequired) throw;
            return false;
        }
    }

    /// <summary>Copies the native RG16Float motion attachment to a matching transient target.</summary>
    internal bool TryCopyCameraRenderTargetMotionToColor(RenderTexture? source, bool sourceIsCameraTarget, RenderTexture? destination,
        int sourceSlice = 0, int destinationSlice = 0)
    {
        if (destination is null || _disposed || _handle == IntPtr.Zero || !AnityNative.Available ||
            (!sourceIsCameraTarget && source is null)) return false;
        try
        {
            if (sourceSlice < 0 || destinationSlice < 0 || destinationSlice >= Math.Max(1, destination.volumeDepth) ||
                (!sourceIsCameraTarget && source is not null && sourceSlice >= Math.Max(1, source.volumeDepth))) return false;
            ulong sourceId = source is null ? 0UL : unchecked((ulong)(uint)source.GetInstanceID());
            ulong destinationId = unchecked((ulong)(uint)destination.GetInstanceID());
            AnityNative.Result result = sourceSlice == 0 && destinationSlice == 0
                ? AnityNative.Graphics_CopyCameraRenderTargetMotionToColor(_handle, sourceId, sourceIsCameraTarget ? 1 : 0, destinationId)
                : AnityNative.Graphics_CopyCameraRenderTargetMotionToColorSlice(_handle, sourceId, sourceIsCameraTarget ? 1 : 0,
                    sourceSlice, destinationSlice, destinationId);
            return result == AnityNative.Result.Ok;
        }
        catch
        {
            if (NativeRequired) throw;
            return false;
        }
    }

    /// <summary>Runs the native backend's final URP HDR grade for a camera render target.</summary>
    internal bool TryProcessCameraRenderTargetHDR(RenderTexture? target, AnityNative.HDRColorGrade grade)
    {
        if (target is null || _disposed || _handle == IntPtr.Zero || !AnityNative.Available)
            return false;
        try
        {
            return AnityNative.Graphics_ProcessCameraRenderTargetHDR(
                _handle, unchecked((ulong)(uint)target.GetInstanceID()), ref grade) ==
                AnityNative.Result.Ok;
        }
        catch
        {
            if (NativeRequired) throw;
            return false;
        }
    }

    internal bool TryGetHDRPostProcessStats(out AnityNative.GraphicsHDRPostProcessStats stats)
    {
        stats = default;
        return !_disposed && _handle != IntPtr.Zero && AnityNative.Available &&
               AnityNative.Graphics_GetHDRPostProcessStats(_handle, out stats) ==
               AnityNative.Result.Ok;
    }

    private static bool IsHdrRenderTexture(RenderTexture target) => target.format is
        RenderTextureFormat.DefaultHDR or RenderTextureFormat.ARGBHalf or
        RenderTextureFormat.ARGBFloat or RenderTextureFormat.RGB111110Float;

    /// <summary>
    /// Attaches a persistent native Canvas queue to this device. The device keeps the managed
    /// canvas alive but does not own or dispose it. Passing null detaches the current queue.
    /// </summary>
    public bool AttachUICanvas(NativeUICanvas? canvas)
    {
        if (_disposed || (canvas is not null && !canvas.IsValid)) return false;
        if (_handle != IntPtr.Zero && AnityNative.Available)
        {
            try
            {
                if (AnityNative.Graphics_SetUICanvas(
                        _handle, canvas?.Handle ?? IntPtr.Zero) != AnityNative.Result.Ok)
                    return false;
            }
            catch
            {
                if (NativeRequired) throw;
                return false;
            }
        }
        _uiCanvas = canvas;
        return true;
    }

    /// <summary>Immediately builds and uploads one Canvas queue without changing attachment.</summary>
    public bool SubmitUICanvas(NativeUICanvas canvas)
    {
        if (_disposed || canvas is null || !canvas.IsValid ||
            _handle == IntPtr.Zero || !AnityNative.Available) return false;
        try
        {
            bool submitted = AnityNative.Graphics_SubmitUICanvas(
                _handle, canvas.Handle) == AnityNative.Result.Ok;
            if (submitted) RefreshUIUploadStats();
            return submitted;
        }
        catch
        {
            if (NativeRequired) throw;
            return false;
        }
    }

    internal bool EnsureTexture(Texture? texture)
    {
        lock (_textureLock)
        {
            if (_disposed || texture is not Texture2D texture2D ||
                _handle == IntPtr.Zero || !AnityNative.Available) return false;
            ulong textureId = unchecked((ulong)(uint)texture.GetInstanceID());
            ulong state = TextureState(texture2D);
            if (_textureStates.TryGetValue(textureId, out ulong uploaded) && uploaded == state &&
                AnityNative.Graphics_GetTextureInfo(_handle, textureId, out _) == AnityNative.Result.Ok)
                return true;
            byte[] pixels = texture2D.GetNativeRgba32();
            int expectedBytes = 0;
            for (int mip = 0; mip < texture.mipmapCount; mip++)
                expectedBytes = checked(expectedBytes +
                    Math.Max(1, texture.width >> mip) * Math.Max(1, texture.height >> mip) * 4);
            if (pixels.Length != expectedBytes) return false;
            var desc = new AnityNative.GraphicsTextureDesc
            {
                textureId = textureId,
                revision = texture.nativeRevision,
                width = texture.width,
                height = texture.height,
                mipCount = Math.Max(1, texture.mipmapCount),
                filterMode = (int)texture.filterMode,
                wrapU = (int)texture.wrapModeU,
                wrapV = (int)texture.wrapModeV,
                linear = texture2D.linear ? 1 : 0,
                mipMapBias = texture.mipMapBias,
                anisoLevel = EffectiveAnisoLevel(texture)
            };
            if (AnityNative.Graphics_UploadTextureRGBA8(
                    _handle, ref desc, pixels, pixels.Length) != AnityNative.Result.Ok)
                return false;
            _textureStates[textureId] = state;
            return true;
        }
    }

    internal bool ReleaseTexture(Texture texture)
    {
        if (texture is null) return false;
        lock (_textureLock)
        {
            if (_disposed) return false;
            ulong textureId = unchecked((ulong)(uint)texture.GetInstanceID());
            _textureStates.Remove(textureId);
            return _handle != IntPtr.Zero && AnityNative.Available &&
                AnityNative.Graphics_DestroyTexture(_handle, textureId) == AnityNative.Result.Ok;
        }
    }

    internal static void ReleaseTextureFromAll(Texture texture)
    {
        NativeGraphicsDevice[] devices;
        lock (DevicesLock)
        {
            devices = new NativeGraphicsDevice[LiveDevices.Count];
            LiveDevices.CopyTo(devices);
        }
        foreach (NativeGraphicsDevice device in devices)
            device.ReleaseTexture(texture);
    }

    internal bool TryGetTextureInfo(Texture texture, out AnityNative.GraphicsTextureInfo info)
    {
        info = default;
        if (texture is null || _handle == IntPtr.Zero || !AnityNative.Available) return false;
        ulong textureId = unchecked((ulong)(uint)texture.GetInstanceID());
        return AnityNative.Graphics_GetTextureInfo(_handle, textureId, out info) == AnityNative.Result.Ok;
    }

    internal IntPtr GetNativeTexturePtr(Texture texture)
    {
        if (!EnsureTexture(texture)) return IntPtr.Zero;
        ulong textureId = unchecked((ulong)(uint)texture.GetInstanceID());
        return AnityNative.Graphics_GetTextureNativeHandle(_handle, textureId);
    }

    internal bool UploadVFXEvent(
        ulong effectId,
        int eventNameId,
        ulong sequence,
        byte[] payload,
        int strideWords,
        int recordCount)
    {
        if (_disposed || effectId == 0 || sequence == 0 || payload is null ||
            _handle == IntPtr.Zero || !AnityNative.Available) return false;
        var desc = new AnityNative.GraphicsVFXEventUploadDesc
        {
            effectId = effectId,
            sequence = sequence,
            eventNameId = eventNameId,
            recordCount = recordCount,
            strideBytes = checked(strideWords * sizeof(uint))
        };
        try
        {
            return AnityNative.Graphics_UploadVFXEventRecords(
                _handle, ref desc, payload, payload.Length) == AnityNative.Result.Ok;
        }
        catch (EntryPointNotFoundException)
        {
            if (NativeRequired) throw;
            return false;
        }
    }

    internal static void UploadVFXEventFromAll(
        ulong effectId,
        int eventNameId,
        ulong sequence,
        byte[] payload,
        int strideWords,
        int recordCount)
    {
        NativeGraphicsDevice[] devices;
        lock (DevicesLock)
        {
            devices = new NativeGraphicsDevice[LiveDevices.Count];
            LiveDevices.CopyTo(devices);
        }
        foreach (NativeGraphicsDevice device in devices)
            device.UploadVFXEvent(effectId, eventNameId, sequence, payload, strideWords, recordCount);
    }

    internal bool TryGetVFXEventUploadInfo(ulong effectId, out AnityNative.GraphicsVFXEventUploadInfo info)
    {
        info = default;
        return !_disposed && effectId != 0 && _handle != IntPtr.Zero && AnityNative.Available &&
            AnityNative.Graphics_GetVFXEventUploadInfo(_handle, effectId, out info) == AnityNative.Result.Ok;
    }

    internal bool TryReadbackVFXEventRecords(ulong effectId, out byte[] records)
    {
        records = Array.Empty<byte>();
        if (!TryGetVFXEventUploadInfo(effectId, out AnityNative.GraphicsVFXEventUploadInfo info))
            return false;
        byte[] bytes = new byte[info.byteCount];
        if (AnityNative.Graphics_ReadbackVFXEventRecords(
                _handle, effectId, bytes, bytes.Length, out int written) != AnityNative.Result.Ok ||
            written != bytes.Length) return false;
        records = bytes;
        return true;
    }

    internal bool TryGetVFXEventDispatchPlan(
        ulong effectId,
        out NativeVFXEventDispatchPlan? plan)
    {
        plan = null;
        if (_disposed || effectId == 0 || _handle == IntPtr.Zero || !AnityNative.Available ||
            AnityNative.Graphics_GetVFXEventDispatchPlanInfo(
                _handle, effectId, out AnityNative.GraphicsVFXEventDispatchPlanInfo info) != AnityNative.Result.Ok)
            return false;
        if (info.effectId != effectId || info.firstSequence == 0 || info.lastSequence < info.firstSequence ||
            info.batchCount <= 0 || info.recordCount < 0 || info.strideBytes < 0 || info.byteCount < 0 ||
            info.byteCount != checked(info.recordCount * info.strideBytes))
            throw new InvalidOperationException("Native VFX input event dispatch plan is invalid.");

        var batches = new AnityNative.GraphicsVFXEventDispatchBatch[info.batchCount];
        if (AnityNative.Graphics_CopyVFXEventDispatchBatches(
                _handle, effectId, info.lastSequence, batches, batches.Length, out int batchWritten) !=
                AnityNative.Result.Ok || batchWritten != batches.Length)
            return false;
        var records = new byte[info.byteCount];
        if (AnityNative.Graphics_CopyVFXEventDispatchRecords(
                _handle, effectId, info.lastSequence, records, records.Length, out int recordBytesWritten) !=
                AnityNative.Result.Ok || recordBytesWritten != records.Length)
            return false;

        int expectedStart = 0;
        ulong previousSequence = 0;
        foreach (AnityNative.GraphicsVFXEventDispatchBatch batch in batches)
        {
            if (batch.sequence <= previousSequence || batch.startEventIndex != expectedStart ||
                batch.recordCount < 0 || batch.strideBytes < 0 ||
                (batch.recordCount > 0 && batch.strideBytes != info.strideBytes) ||
                (batch.recordCount == 0 && batch.strideBytes != 0))
                throw new InvalidOperationException("Native VFX input event dispatch batch is invalid.");
            previousSequence = batch.sequence;
            expectedStart = checked(expectedStart + batch.recordCount);
        }
        if (batches[0].sequence != info.firstSequence ||
            batches[^1].sequence != info.lastSequence || expectedStart != info.recordCount)
            throw new InvalidOperationException("Native VFX input event dispatch prefix sum is invalid.");
        plan = new NativeVFXEventDispatchPlan(info, batches, records);
        return true;
    }

    internal bool ConsumeVFXEventDispatchPlan(ulong effectId, ulong throughSequence)
        => !_disposed && effectId != 0 && throughSequence != 0 && _handle != IntPtr.Zero &&
           AnityNative.Available && AnityNative.Graphics_ConsumeVFXEventDispatchPlan(
               _handle, effectId, throughSequence) == AnityNative.Result.Ok;

    internal bool SubmitVFXInitializeDispatch(
        AnityNative.GraphicsVFXInitializeDispatchDesc desc,
        byte[] sourceRecords)
        => SubmitVFXInitializeDispatches(new[] { desc }, sourceRecords);

    internal bool SubmitVFXInitializeDispatches(
        AnityNative.GraphicsVFXInitializeDispatchDesc[] descs,
        byte[] sourceRecords)
    {
        if (_disposed || descs is null || descs.Length == 0 || descs.Length > 4096 ||
            sourceRecords is null || _handle == IntPtr.Zero || !AnityNative.Available)
            return false;
        foreach (AnityNative.GraphicsVFXInitializeDispatchDesc desc in descs)
        {
            if (desc.effectId == 0 || desc.sequence == 0 ||
                desc.initializeContextId == 0 || desc.startEventIndex < 0 ||
                desc.recordCount <= 0 || desc.strideBytes <= 0 ||
                (desc.strideBytes & 3) != 0)
                return false;
            long required = checked(
                ((long)desc.startEventIndex + desc.recordCount) * desc.strideBytes);
            if (required > sourceRecords.Length) return false;
        }
        return AnityNative.Graphics_SubmitVFXInitializeDispatches(
            _handle, descs, descs.Length, sourceRecords, sourceRecords.Length) == AnityNative.Result.Ok;
    }

    internal bool SubmitVFXInitializeKernels(
        AnityNative.GraphicsVFXInitializeDispatchDesc[] dispatches,
        VFXRuntimeInitializeKernelData?[] kernels,
        byte[] sourceRecords,
        uint systemSeed)
        => SubmitOrBeginVFXInitializeKernels(
            dispatches, kernels, sourceRecords, systemSeed,
            asynchronous: false, out _);

    internal bool BeginVFXInitializeKernels(
        AnityNative.GraphicsVFXInitializeDispatchDesc[] dispatches,
        VFXRuntimeInitializeKernelData?[] kernels,
        byte[] sourceRecords,
        uint systemSeed,
        out ulong ticketId)
        => SubmitOrBeginVFXInitializeKernels(
            dispatches, kernels, sourceRecords, systemSeed,
            asynchronous: true, out ticketId);

    private bool SubmitOrBeginVFXInitializeKernels(
        AnityNative.GraphicsVFXInitializeDispatchDesc[] dispatches,
        VFXRuntimeInitializeKernelData?[] kernels,
        byte[] sourceRecords,
        uint systemSeed,
        bool asynchronous,
        out ulong ticketId)
    {
        ticketId = 0;
        if (_disposed || dispatches is null || kernels is null ||
            dispatches.Length == 0 || dispatches.Length != kernels.Length ||
            dispatches.Length > 4096 || sourceRecords is null ||
            _handle == IntPtr.Zero || !AnityNative.Available)
            return false;
        var nativeKernels = new AnityNative.GraphicsVFXInitializeKernelDesc[kernels.Length];
        var attributes = new List<AnityNative.GraphicsVFXInitializeAttributeDesc>();
        var operations = new List<AnityNative.GraphicsVFXInitializeOperationDesc>();
        for (int index = 0; index < dispatches.Length; index++)
        {
            AnityNative.GraphicsVFXInitializeDispatchDesc dispatch = dispatches[index];
            if (dispatch.effectId == 0 || dispatch.sequence == 0 ||
                dispatch.initializeContextId == 0 || dispatch.startEventIndex < 0 ||
                dispatch.recordCount <= 0 || dispatch.strideBytes <= 0 ||
                (dispatch.strideBytes & 3) != 0 ||
                checked(((long)dispatch.startEventIndex + dispatch.recordCount) * dispatch.strideBytes) >
                    sourceRecords.Length)
                return false;
            VFXRuntimeInitializeKernelData? kernel = kernels[index];
            if (kernel is null)
            {
                nativeKernels[index] = default;
                continue;
            }
            if (kernel.ContextId != dispatch.initializeContextId ||
                kernel.ParticleCapacity == 0 || kernel.ParticleCapacity > int.MaxValue ||
                kernel.AttributeStrideWords <= 0 ||
                kernel.SourceStrideWords * sizeof(uint) != dispatch.strideBytes)
                return false;
            int attributeStart = attributes.Count;
            foreach (VFXRuntimeInitializeAttributeData attribute in kernel.Attributes)
            {
                uint[] defaults = PadWords(attribute.DefaultWords);
                attributes.Add(new AnityNative.GraphicsVFXInitializeAttributeDesc
                {
                    offsetBytes = checked(attribute.Layout.OffsetWords * sizeof(uint)),
                    componentCount = attribute.Layout.SizeWords,
                    valueType = (int)attribute.Layout.ValueType,
                    semantic = string.Equals(attribute.Layout.Name, "alive", StringComparison.Ordinal) ? 1 : 0,
                    default0 = defaults[0], default1 = defaults[1],
                    default2 = defaults[2], default3 = defaults[3]
                });
            }
            int operationStart = operations.Count;
            foreach (VFXRuntimeInitializeOperationData operation in kernel.Operations)
            {
                uint[] valueA = PadWords(operation.ValueA);
                uint[] valueB = PadWords(operation.ValueB);
                operations.Add(new AnityNative.GraphicsVFXInitializeOperationDesc
                {
                    targetOffsetBytes = checked(operation.TargetOffsetWords * sizeof(uint)),
                    sourceOffsetBytes = operation.SourceOffsetWords < 0
                        ? -1 : checked(operation.SourceOffsetWords * sizeof(uint)),
                    componentCount = VFXRuntimeAssetData.WordCount(operation.ValueType),
                    valueType = (int)operation.ValueType,
                    valueSource = (int)operation.ValueSource,
                    composition = (int)operation.Composition,
                    randomMode = (int)operation.RandomMode,
                    valueA0 = valueA[0], valueA1 = valueA[1],
                    valueA2 = valueA[2], valueA3 = valueA[3],
                    valueB0 = valueB[0], valueB1 = valueB[1],
                    valueB2 = valueB[2], valueB3 = valueB[3],
                    blendFactorBits = operation.BlendFactorBits
                });
            }
            nativeKernels[index] = new AnityNative.GraphicsVFXInitializeKernelDesc
            {
                version = kernel.SpawnCountSourceOffsetWords >= 0 ? 2u : 1u,
                flags = kernel.UsesDeadList ? 1u : 0u,
                particleCapacity = checked((int)kernel.ParticleCapacity),
                attributeStrideBytes = checked(kernel.AttributeStrideWords * sizeof(uint)),
                sourceStrideBytes = checked(kernel.SourceStrideWords * sizeof(uint)),
                attributeStart = attributeStart,
                attributeCount = kernel.Attributes.Count,
                operationStart = operationStart,
                operationCount = kernel.Operations.Count,
                spawnCountSourceOffsetBytes = kernel.SpawnCountSourceOffsetWords < 0
                    ? -1 : checked(kernel.SpawnCountSourceOffsetWords * sizeof(uint)),
                systemSeed = systemSeed
            };
        }
        AnityNative.GraphicsVFXInitializeAttributeDesc[] nativeAttributes =
            attributes.ToArray();
        AnityNative.GraphicsVFXInitializeOperationDesc[] nativeOperations =
            operations.ToArray();
        return asynchronous
            ? AnityNative.Graphics_BeginVFXInitializeKernels(
                _handle, dispatches, nativeKernels, dispatches.Length,
                nativeAttributes, nativeAttributes.Length,
                nativeOperations, nativeOperations.Length,
                sourceRecords, sourceRecords.Length, out ticketId) ==
              AnityNative.Result.Ok
            : AnityNative.Graphics_SubmitVFXInitializeKernels(
                _handle, dispatches, nativeKernels, dispatches.Length,
                nativeAttributes, nativeAttributes.Length,
                nativeOperations, nativeOperations.Length,
                sourceRecords, sourceRecords.Length) == AnityNative.Result.Ok;
    }

    internal bool TryGetVFXInitializeTicketInfo(
        ulong ticketId,
        out AnityNative.GraphicsVFXInitializeTicketInfo info)
    {
        info = default;
        return !_disposed && ticketId != 0 && _handle != IntPtr.Zero &&
               AnityNative.Available &&
               AnityNative.Graphics_GetVFXInitializeTicketInfo(
                   _handle, ticketId, out info) == AnityNative.Result.Ok;
    }

    internal bool CompleteVFXInitializeKernels(ulong ticketId)
        => !_disposed && ticketId != 0 && _handle != IntPtr.Zero &&
           AnityNative.Available &&
           AnityNative.Graphics_CompleteVFXInitializeKernels(
               _handle, ticketId) == AnityNative.Result.Ok;

    internal bool CancelVFXInitializeKernels(ulong ticketId)
        => !_disposed && ticketId != 0 && _handle != IntPtr.Zero &&
           AnityNative.Available &&
           AnityNative.Graphics_CancelVFXInitializeKernels(
               _handle, ticketId) == AnityNative.Result.Ok;

    internal bool DispatchVFXUpdateKernels(
        ulong effectId,
        IReadOnlyList<VFXRuntimeUpdateKernelData> kernels,
        float deltaTime,
        uint systemSeed)
    {
        return SubmitVFXUpdateKernels(
            effectId, kernels, deltaTime, systemSeed,
            asynchronous: false, null, out _);
    }

    internal bool BeginVFXUpdateKernels(
        ulong effectId,
        IReadOnlyList<VFXRuntimeUpdateKernelData> kernels,
        float deltaTime,
        uint systemSeed,
        out ulong ticketId)
        => SubmitVFXUpdateKernels(
            effectId, kernels, deltaTime, systemSeed,
            asynchronous: true, null, out ticketId);

    internal bool BeginVFXUpdateKernels(
        ulong effectId,
        IReadOnlyList<VFXRuntimeUpdateKernelData> kernels,
        IReadOnlyList<VFXParticleCullingBounds?> automaticBounds,
        float deltaTime,
        uint systemSeed,
        out ulong ticketId)
        => SubmitVFXUpdateKernels(
            effectId, kernels, deltaTime, systemSeed,
            asynchronous: true, automaticBounds, out ticketId);

    private bool SubmitVFXUpdateKernels(
        ulong effectId,
        IReadOnlyList<VFXRuntimeUpdateKernelData> kernels,
        float deltaTime,
        uint systemSeed,
        bool asynchronous,
        IReadOnlyList<VFXParticleCullingBounds?>? automaticBounds,
        out ulong ticketId)
    {
        ticketId = 0;
        if (_disposed || effectId == 0 || kernels is null || kernels.Count > 4096 ||
            _handle == IntPtr.Zero || !AnityNative.Available)
            return false;
        if (kernels.Count == 0 ||
            (automaticBounds is not null && automaticBounds.Count != kernels.Count))
            return false;
        var nativeKernels = new AnityNative.GraphicsVFXUpdateKernelDesc[kernels.Count];
        var nativeBounds = automaticBounds is null
            ? null
            : new AnityNative.GraphicsVFXBoundsReductionDesc[kernels.Count];
        var operations = new List<AnityNative.GraphicsVFXUpdateOperationDesc>();
        for (int index = 0; index < kernels.Count; index++)
        {
            VFXRuntimeUpdateKernelData kernel = kernels[index];
            if (kernel.ContextId == 0 || string.IsNullOrEmpty(kernel.ParticleSystemName) ||
                kernel.ParticleCapacity == 0 || kernel.ParticleCapacity > int.MaxValue ||
                kernel.AttributeStrideWords <= 0 || kernel.Operations.Count == 0)
                return false;
            int operationStart = operations.Count;
            foreach (VFXRuntimeUpdateOperationData operation in kernel.Operations)
            {
                uint[] valueA = PadWords(operation.ValueA);
                uint[] valueB = PadWords(operation.ValueB);
                operations.Add(new AnityNative.GraphicsVFXUpdateOperationDesc
                {
                    kind = (int)operation.Kind,
                    targetOffsetBytes = ByteOffset(operation.TargetOffsetWords),
                    sourceAOffsetBytes = ByteOffset(operation.SourceAOffsetWords),
                    sourceBOffsetBytes = ByteOffset(operation.SourceBOffsetWords),
                    auxiliaryOffset0Bytes = ByteOffset(operation.AuxiliaryOffset0Words),
                    auxiliaryOffset1Bytes = ByteOffset(operation.AuxiliaryOffset1Words),
                    componentCount = VFXRuntimeAssetData.WordCount(operation.ValueType),
                    valueType = (int)operation.ValueType,
                    composition = (int)operation.Composition,
                    randomMode = (int)operation.RandomMode,
                    flags = operation.ReadSourceSnapshot ? 1 : 0,
                    valueA0 = valueA[0], valueA1 = valueA[1],
                    valueA2 = valueA[2], valueA3 = valueA[3],
                    valueB0 = valueB[0], valueB1 = valueB[1],
                    valueB2 = valueB[2], valueB3 = valueB[3],
                    blendFactorBits = operation.BlendFactorBits
                });
            }
            nativeKernels[index] = new AnityNative.GraphicsVFXUpdateKernelDesc
            {
                version = 1,
                flags = (kernel.UsesDeadList ? 1u : 0u) |
                        (kernel.SkipZeroDeltaUpdate ? 2u : 0u),
                effectId = effectId,
                contextId = kernel.ContextId,
                particleSystemId = Shader.PropertyToID(kernel.ParticleSystemName),
                particleCapacity = checked((int)kernel.ParticleCapacity),
                attributeStrideBytes = checked(kernel.AttributeStrideWords * sizeof(uint)),
                operationStart = operationStart,
                operationCount = kernel.Operations.Count,
                aliveOffsetBytes = ByteOffset(kernel.AliveOffsetWords),
                seedOffsetBytes = ByteOffset(kernel.SeedOffsetWords),
                deltaTime = deltaTime,
                systemSeed = systemSeed
            };
            if (nativeBounds is not null &&
                automaticBounds![index] is VFXParticleCullingBounds bounds)
            {
                if (!bounds.HasAutomaticBounds || bounds.HasStaticBounds ||
                    bounds.PositionOffsetWords < 0 || bounds.AliveOffsetWords < 0)
                    return false;
                nativeBounds[index] = new AnityNative.GraphicsVFXBoundsReductionDesc
                {
                    effectId = effectId,
                    particleSystemId = Shader.PropertyToID(kernel.ParticleSystemName),
                    positionOffsetBytes = ByteOffset(bounds.PositionOffsetWords),
                    aliveOffsetBytes = ByteOffset(bounds.AliveOffsetWords),
                    sizeOffsetBytes = ByteOffset(bounds.SizeOffsetWords),
                    scaleXOffsetBytes = ByteOffset(bounds.ScaleXOffsetWords),
                    scaleYOffsetBytes = ByteOffset(bounds.ScaleYOffsetWords),
                    scaleZOffsetBytes = ByteOffset(bounds.ScaleZOffsetWords),
                    paddingX = bounds.AutomaticPadding.x,
                    paddingY = bounds.AutomaticPadding.y,
                    paddingZ = bounds.AutomaticPadding.z,
                    boundsInWorldSpace = bounds.WorldSpace ? 1 : 0
                };
            }
        }
        AnityNative.GraphicsVFXUpdateOperationDesc[] nativeOperations = operations.ToArray();
        if (!asynchronous)
        {
            LastVFXUpdateSubmitResult = AnityNative.Graphics_DispatchVFXUpdateKernels(
                _handle, nativeKernels, nativeKernels.Length,
                nativeOperations, nativeOperations.Length);
            return LastVFXUpdateSubmitResult == AnityNative.Result.Ok;
        }
        AnityNative.Result beginResult = nativeBounds is null
            ? AnityNative.Graphics_BeginVFXUpdateKernels(
                _handle, nativeKernels, nativeKernels.Length,
                nativeOperations, nativeOperations.Length, out ticketId)
            : AnityNative.Graphics_BeginVFXUpdateKernelsWithBounds(
                _handle, nativeKernels, nativeKernels.Length,
                nativeOperations, nativeOperations.Length,
                nativeBounds, nativeBounds.Length, out ticketId);
        LastVFXUpdateSubmitResult = beginResult;
        return beginResult == AnityNative.Result.Ok && ticketId != 0;

        static int ByteOffset(int words)
            => words < 0 ? -1 : checked(words * sizeof(uint));
    }

    internal bool TryGetVFXUpdateTicketInfo(
        ulong ticketId,
        out AnityNative.GraphicsVFXUpdateTicketInfo info)
    {
        info = default;
        return !_disposed && ticketId != 0 && _handle != IntPtr.Zero &&
               AnityNative.Available &&
               AnityNative.Graphics_GetVFXUpdateTicketInfo(
                   _handle, ticketId, out info) == AnityNative.Result.Ok;
    }

    internal bool CompleteVFXUpdateKernels(ulong ticketId)
        => !_disposed && ticketId != 0 && _handle != IntPtr.Zero &&
           AnityNative.Available &&
           AnityNative.Graphics_CompleteVFXUpdateKernels(
               _handle, ticketId) == AnityNative.Result.Ok;

    internal bool CancelVFXUpdateKernels(ulong ticketId)
        => !_disposed && ticketId != 0 && _handle != IntPtr.Zero &&
           AnityNative.Available &&
           AnityNative.Graphics_CancelVFXUpdateKernels(
               _handle, ticketId) == AnityNative.Result.Ok;

    internal bool InjectVFXFailure(
        AnityNative.GraphicsVFXFailurePoint failurePoint, int failureCount = 1)
        => !_disposed && failureCount >= 0 && failureCount <= 1024 &&
           _handle != IntPtr.Zero && AnityNative.Available &&
           AnityNative.Graphics_SetVFXFailureInjection(
               _handle, failurePoint, failureCount) == AnityNative.Result.Ok;

    internal bool TryGetVFXParticleSystem(
        ulong effectId,
        int particleSystemId,
        out NativeVFXParticleSystem? particleSystem)
    {
        particleSystem = null;
        if (_disposed || effectId == 0 || particleSystemId == 0 ||
            _handle == IntPtr.Zero || !AnityNative.Available ||
            AnityNative.Graphics_GetVFXParticleSystemInfo(
                _handle, effectId, particleSystemId,
                out AnityNative.GraphicsVFXParticleSystemInfo info) != AnityNative.Result.Ok)
            return false;
        if (info.effectId != effectId || info.particleSystemId != particleSystemId ||
            info.capacity <= 0 || info.attributeStrideBytes <= 0 ||
            info.aliveCount < 0 || info.aliveCount > info.capacity ||
            info.deadCount < 0 || info.deadCount > info.capacity ||
            info.backendKind is < 0 or > 3)
            throw new InvalidOperationException("Native VFX particle system state is invalid.");
        var records = new byte[checked(info.capacity * info.attributeStrideBytes)];
        if (AnityNative.Graphics_ReadbackVFXParticleSystem(
                _handle, effectId, particleSystemId, records, records.Length,
                out int recordBytes) != AnityNative.Result.Ok || recordBytes != records.Length)
            return false;
        // Readback is an explicit CPU dependency and can retire a committed
        // Initialize -> Update chain. Refresh metadata after that retirement;
        // the first non-blocking info query may still describe the predecessor
        // generation (notably its alive/dead counts).
        if (AnityNative.Graphics_GetVFXParticleSystemInfo(
                _handle, effectId, particleSystemId,
                out AnityNative.GraphicsVFXParticleSystemInfo completedInfo) !=
                    AnityNative.Result.Ok ||
            completedInfo.effectId != effectId ||
            completedInfo.particleSystemId != particleSystemId ||
            completedInfo.capacity != info.capacity ||
            completedInfo.attributeStrideBytes != info.attributeStrideBytes ||
            completedInfo.aliveCount < 0 ||
            completedInfo.aliveCount > completedInfo.capacity ||
            completedInfo.deadCount < 0 ||
            completedInfo.deadCount > completedInfo.capacity ||
            completedInfo.backendKind is < 0 or > 3)
            return false;
        info = completedInfo;
        var deadList = new uint[info.deadCount];
        if (AnityNative.Graphics_ReadbackVFXParticleDeadList(
                _handle, effectId, particleSystemId, deadList, deadList.Length,
                out int deadWritten) != AnityNative.Result.Ok || deadWritten != deadList.Length)
            return false;
        particleSystem = new NativeVFXParticleSystem(info, records, deadList);
        return true;
    }

    internal bool TryGetVFXParticleSystemInfo(
        ulong effectId,
        int particleSystemId,
        out AnityNative.GraphicsVFXParticleSystemInfo info)
    {
        info = default;
        return !_disposed && effectId != 0 && particleSystemId != 0 &&
               _handle != IntPtr.Zero && AnityNative.Available &&
               AnityNative.Graphics_GetVFXParticleSystemInfo(
                   _handle, effectId, particleSystemId, out info) ==
               AnityNative.Result.Ok;
    }

    internal bool TryGetVFXUpdateBackendStats(
        ulong effectId,
        int particleSystemId,
        out AnityNative.GraphicsVFXUpdateBackendStats stats)
    {
        stats = default;
        return !_disposed && effectId != 0 && particleSystemId != 0 &&
               _handle != IntPtr.Zero && AnityNative.Available &&
               AnityNative.Graphics_GetVFXUpdateBackendStats(
                   _handle, effectId, particleSystemId, out stats) == AnityNative.Result.Ok;
    }

    internal bool TryReduceVFXParticleBounds(
        ulong effectId,
        int particleSystemId,
        VFXParticleCullingBounds metadata,
        out Bounds bounds,
        out int backendKind,
        out ulong generation)
    {
        bounds = default;
        backendKind = 0;
        generation = 0;
        if (_disposed || effectId == 0 || particleSystemId == 0 ||
            !metadata.HasAutomaticBounds || metadata.PositionOffsetWords < 0 ||
            _handle == IntPtr.Zero || !AnityNative.Available)
            return false;
        var desc = new AnityNative.GraphicsVFXBoundsReductionDesc
        {
            effectId = effectId,
            particleSystemId = particleSystemId,
            positionOffsetBytes = checked(metadata.PositionOffsetWords * sizeof(uint)),
            aliveOffsetBytes = ByteOffset(metadata.AliveOffsetWords),
            sizeOffsetBytes = ByteOffset(metadata.SizeOffsetWords),
            scaleXOffsetBytes = ByteOffset(metadata.ScaleXOffsetWords),
            scaleYOffsetBytes = ByteOffset(metadata.ScaleYOffsetWords),
            scaleZOffsetBytes = ByteOffset(metadata.ScaleZOffsetWords),
            paddingX = metadata.AutomaticPadding.x,
            paddingY = metadata.AutomaticPadding.y,
            paddingZ = metadata.AutomaticPadding.z,
            boundsInWorldSpace = metadata.WorldSpace ? 1 : 0
        };
        if (AnityNative.Graphics_ReduceVFXParticleBounds(
                _handle, ref desc,
                out AnityNative.GraphicsVFXBoundsReductionResult result) !=
            AnityNative.Result.Ok)
            return false;
        if (result.effectId != effectId || result.particleSystemId != particleSystemId ||
            result.valid is < 0 or > 1 ||
            result.boundsInWorldSpace != desc.boundsInWorldSpace ||
            result.backendKind is not (0 or 2) || result.generation == 0)
            throw new InvalidOperationException("Native VFX Automatic bounds result is invalid.");
        backendKind = result.backendKind;
        generation = result.generation;
        if (result.valid == 0) return false;
        if (!float.IsFinite(result.centerX) || !float.IsFinite(result.centerY) ||
            !float.IsFinite(result.centerZ) || !float.IsFinite(result.extentsX) ||
            !float.IsFinite(result.extentsY) || !float.IsFinite(result.extentsZ) ||
            result.extentsX < 0f || result.extentsY < 0f || result.extentsZ < 0f)
            throw new InvalidOperationException("Native VFX Automatic bounds contain invalid values.");
        bounds = new Bounds(
            new Vector3(result.centerX, result.centerY, result.centerZ),
            new Vector3(result.extentsX, result.extentsY, result.extentsZ) * 2f);
        return true;

        static int ByteOffset(int words)
            => words < 0 ? -1 : checked(words * sizeof(uint));
    }

    internal bool SetVFXSpawnerPrograms(
        ulong effectId,
        IReadOnlyCollection<VFXRuntimeSpawnerProgramData> programs)
    {
        if (_disposed || effectId == 0 || programs is null || programs.Count > 4096 ||
            _handle == IntPtr.Zero || !AnityNative.Available)
            return false;
        var nativePrograms = new AnityNative.GraphicsVFXSpawnerProgramDesc[programs.Count];
        var nativeBlocks = new List<AnityNative.GraphicsVFXSpawnerBlockDesc>();
        int programIndex = 0;
        foreach (VFXRuntimeSpawnerProgramData program in programs)
        {
            int blockStart = nativeBlocks.Count;
            foreach (VFXRuntimeSpawnerBlockData block in program.Blocks)
            {
                uint[] valueA = block.ValueA.Concat(Enumerable.Repeat(0u, 4)).Take(4).ToArray();
                uint[] valueB = block.ValueB.Concat(Enumerable.Repeat(0u, 4)).Take(4).ToArray();
                nativeBlocks.Add(new AnityNative.GraphicsVFXSpawnerBlockDesc
                {
                    blockId = block.BlockId,
                    kind = (int)block.Kind,
                    periodic = block.Periodic ? 1 : 0,
                    valueMin = block.ValueMin,
                    valueMax = block.ValueMax,
                    periodMin = block.PeriodMin,
                    periodMax = block.PeriodMax,
                    targetOffsetWords = block.TargetOffsetWords,
                    valueType = (int)block.TargetValueType,
                    randomMode = (int)block.RandomMode,
                    valueWordCount = block.ValueA.Count,
                    valueA0 = valueA[0],
                    valueA1 = valueA[1],
                    valueA2 = valueA[2],
                    valueA3 = valueA[3],
                    valueB0 = valueB[0],
                    valueB1 = valueB[1],
                    valueB2 = valueB[2],
                    valueB3 = valueB[3]
                });
            }
            nativePrograms[programIndex] = new AnityNative.GraphicsVFXSpawnerProgramDesc
            {
                version = program.Blocks.Any(block =>
                    block.Kind == VFXRuntimeSpawnerBlockKind.CustomCallback) ? 5u : 4u,
                eventStrideWords = checked((uint)program.EventStrideWords),
                effectId = effectId,
                contextId = program.ContextId,
                systemId = Shader.PropertyToID(program.SystemName),
                blockStart = blockStart,
                blockCount = program.Blocks.Count,
                loopDurationMode = (int)program.LoopDurationMode,
                loopCountMode = (int)program.LoopCountMode,
                delayBeforeLoopMode = (int)program.DelayBeforeLoopMode,
                delayAfterLoopMode = (int)program.DelayAfterLoopMode,
                loopDurationMin = program.LoopDurationMin,
                loopDurationMax = program.LoopDurationMax,
                loopCountMin = program.LoopCountMin,
                loopCountMax = program.LoopCountMax,
                delayBeforeLoopMin = program.DelayBeforeLoopMin,
                delayBeforeLoopMax = program.DelayBeforeLoopMax,
                delayAfterLoopMin = program.DelayAfterLoopMin,
                delayAfterLoopMax = program.DelayAfterLoopMax
            };
            programIndex++;
        }
        return nativeBlocks.Count <= 65536 &&
               AnityNative.Graphics_SetVFXSpawnerPrograms(
                   _handle, effectId, nativePrograms, nativePrograms.Length,
                   nativeBlocks.ToArray(), nativeBlocks.Count) == AnityNative.Result.Ok;
    }

    internal bool BeginVFXFrame(out uint frameIndex)
    {
        frameIndex = 0;
        return !_disposed && _handle != IntPtr.Zero && AnityNative.Available &&
               AnityNative.Graphics_BeginVFXFrame(
                   _handle, out frameIndex) == AnityNative.Result.Ok;
    }

    internal bool BeginVFXPlayerLoopFrame(
        ulong playerLoopToken, out uint frameIndex, out bool beganFrame)
    {
        frameIndex = 0;
        beganFrame = false;
        if (_disposed || playerLoopToken == 0 || _handle == IntPtr.Zero ||
            !AnityNative.Available ||
            AnityNative.Graphics_BeginVFXPlayerLoopFrame(
                _handle, playerLoopToken, out frameIndex,
                out int began) != AnityNative.Result.Ok ||
            began is < 0 or > 1)
            return false;
        beganFrame = began == 1;
        return true;
    }

    internal bool BeginVFXCullingFrame(
        ulong playerLoopToken,
        AnityNative.GraphicsVFXCullingBounds[] bounds)
        => !_disposed && playerLoopToken != 0 && bounds is not null &&
           _handle != IntPtr.Zero && AnityNative.Available &&
           AnityNative.Graphics_BeginVFXCullingFrame(
               _handle, playerLoopToken, bounds, bounds.Length) ==
           AnityNative.Result.Ok;

    internal bool SubmitVFXCullingCamera(
        ulong playerLoopToken,
        AnityNative.GraphicsVFXCullingCamera camera)
        => !_disposed && playerLoopToken != 0 && camera.cameraId != 0 &&
           _handle != IntPtr.Zero && AnityNative.Available &&
           AnityNative.Graphics_SubmitVFXCullingCamera(
               _handle, playerLoopToken, ref camera) == AnityNative.Result.Ok;

    internal bool CompleteVFXCullingFrame(ulong playerLoopToken)
        => !_disposed && playerLoopToken != 0 && _handle != IntPtr.Zero &&
           AnityNative.Available &&
           AnityNative.Graphics_CompleteVFXCullingFrame(
               _handle, playerLoopToken) == AnityNative.Result.Ok;

    internal bool TryGetVFXCullingState(
        ulong effectId, out AnityNative.GraphicsVFXCullingState state)
    {
        state = default;
        return !_disposed && effectId != 0 && _handle != IntPtr.Zero &&
               AnityNative.Available &&
               AnityNative.Graphics_GetVFXCullingState(
                   _handle, effectId, out state) == AnityNative.Result.Ok;
    }

    internal bool SetVFXPlanarOutputs(
        ulong effectId,
        IReadOnlyList<VFXRuntimePlanarOutputData> outputs,
        VisualEffectAsset asset)
    {
        if (_disposed || effectId == 0 || outputs is null || asset is null ||
            _handle == IntPtr.Zero || !AnityNative.Available)
            return false;
        try
        {
            var nativeOutputs = new AnityNative.GraphicsVFXPlanarOutputDesc[outputs.Count];
            for (int index = 0; index < outputs.Count; index++)
            {
                VFXRuntimePlanarOutputData output = outputs[index];
                int systemId = Shader.PropertyToID(output.ParticleSystemName);
                if (!asset.TryGetParticleSystemInfo(systemId, out VFXParticleSystemInfo system))
                    return false;
                int Offset(string name)
                {
                    VFXRuntimeAttributeData attribute = output.Attributes.Single(candidate =>
                        string.Equals(candidate.Name, name, StringComparison.Ordinal));
                    return checked(attribute.OffsetWords * sizeof(uint));
                }
                nativeOutputs[index] = new AnityNative.GraphicsVFXPlanarOutputDesc
                {
                    version = 1,
                    flags = (output.RuntimeExecutable ? 1u : 0u) |
                            (output.AlphaClipping ? 2u : 0u) |
                            (output.RequiresSorting ? 4u : 0u) |
                            (output.IndirectDraw ? 8u : 0u),
                    effectId = effectId,
                    contextId = output.ContextId,
                    particleSystemId = systemId,
                    primitiveType = output.PrimitiveType,
                    particleCapacity = checked((int)system.capacity),
                    attributeStrideBytes = checked(output.AttributeStrideWords * sizeof(uint)),
                    aliveOffsetBytes = Offset("alive"),
                    positionOffsetBytes = Offset("position"),
                    colorOffsetBytes = Offset("color"),
                    alphaOffsetBytes = Offset("alpha"),
                    axisXOffsetBytes = Offset("axisX"),
                    axisYOffsetBytes = Offset("axisY"),
                    axisZOffsetBytes = Offset("axisZ"),
                    angleXOffsetBytes = Offset("angleX"),
                    angleYOffsetBytes = Offset("angleY"),
                    angleZOffsetBytes = Offset("angleZ"),
                    pivotXOffsetBytes = Offset("pivotX"),
                    pivotYOffsetBytes = Offset("pivotY"),
                    pivotZOffsetBytes = Offset("pivotZ"),
                    sizeOffsetBytes = Offset("size"),
                    scaleXOffsetBytes = Offset("scaleX"),
                    scaleYOffsetBytes = Offset("scaleY"),
                    scaleZOffsetBytes = Offset("scaleZ"),
                    uvMode = output.UvMode,
                    blendMode = output.BlendMode,
                    cullMode = output.CullMode,
                    zWrite = output.ZWrite ? 1 : 0,
                    zTest = output.ZTest,
                    renderQueue = ParseVFXRenderQueue(output.RenderQueue)
                };
            }
            return AnityNative.Graphics_SetVFXPlanarOutputs(
                       _handle, effectId, nativeOutputs, nativeOutputs.Length) ==
                   AnityNative.Result.Ok;
        }
        catch (Exception exception) when (exception is OverflowException or InvalidOperationException or FormatException)
        {
            return false;
        }
    }

    internal static int ParseVFXRenderQueue(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new FormatException("VFX render queue is empty.");
        string queue = value.Trim();
        int separator = -1;
        for (int index = 1; index < queue.Length; index++)
        {
            if (queue[index] is '+' or '-')
            {
                separator = index;
                break;
            }
        }
        string name = separator < 0 ? queue : queue[..separator];
        int baseQueue = name switch
        {
            "Background" => 1000,
            "Geometry" => 2000,
            "AlphaTest" => 2450,
            "GeometryLast" => 2500,
            "Transparent" => 3000,
            "Overlay" => 4000,
            _ => throw new FormatException($"Unknown VFX render queue '{value}'.")
        };
        int offset = separator < 0
            ? 0
            : int.Parse(queue.AsSpan(separator), NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture);
        int result = checked(baseQueue + offset);
        if (result is < 0 or > 5000)
            throw new FormatException($"VFX render queue '{value}' is outside Unity's range.");
        return result;
    }

    internal bool TryGetVFXPlanarOutputCount(ulong effectId, out int outputCount)
    {
        outputCount = 0;
        return !_disposed && effectId != 0 && _handle != IntPtr.Zero &&
               AnityNative.Available &&
               AnityNative.Graphics_GetVFXPlanarOutputCount(
                   _handle, effectId, out outputCount) == AnityNative.Result.Ok;
    }

    internal bool DrawVFXPlanarOutputs(
        ulong effectId,
        Matrix4x4 localToWorld,
        Matrix4x4 worldToClip,
        Camera camera,
        bool clear,
        out AnityNative.GraphicsVFXPlanarDrawInfo info)
    {
        info = default;
        if (_disposed || effectId == 0 || camera is null ||
            _handle == IntPtr.Zero || !AnityNative.Available)
            return false;
        var descriptor = new AnityNative.GraphicsVFXPlanarCameraDesc
        {
            cameraId = unchecked((ulong)(uint)camera.GetInstanceID()),
            localToWorld00 = localToWorld.m00, localToWorld01 = localToWorld.m01,
            localToWorld02 = localToWorld.m02, localToWorld03 = localToWorld.m03,
            localToWorld10 = localToWorld.m10, localToWorld11 = localToWorld.m11,
            localToWorld12 = localToWorld.m12, localToWorld13 = localToWorld.m13,
            localToWorld20 = localToWorld.m20, localToWorld21 = localToWorld.m21,
            localToWorld22 = localToWorld.m22, localToWorld23 = localToWorld.m23,
            localToWorld30 = localToWorld.m30, localToWorld31 = localToWorld.m31,
            localToWorld32 = localToWorld.m32, localToWorld33 = localToWorld.m33,
            worldToClip00 = worldToClip.m00, worldToClip01 = worldToClip.m01,
            worldToClip02 = worldToClip.m02, worldToClip03 = worldToClip.m03,
            worldToClip10 = worldToClip.m10, worldToClip11 = worldToClip.m11,
            worldToClip12 = worldToClip.m12, worldToClip13 = worldToClip.m13,
            worldToClip20 = worldToClip.m20, worldToClip21 = worldToClip.m21,
            worldToClip22 = worldToClip.m22, worldToClip23 = worldToClip.m23,
            worldToClip30 = worldToClip.m30, worldToClip31 = worldToClip.m31,
            worldToClip32 = worldToClip.m32, worldToClip33 = worldToClip.m33,
            cullingMask = camera.cullingMask,
            cameraType = (int)camera.cameraType,
            flags = clear ? 1 : 0
        };
        return descriptor.cameraId != 0 &&
               AnityNative.Graphics_DrawVFXPlanarOutputs(
                   _handle, effectId, ref descriptor, out info) == AnityNative.Result.Ok;
    }

    internal bool DrawVFXPlanarCamera(
        Camera camera,
        Matrix4x4 worldToClip,
        IReadOnlyList<VisualEffect> effects,
        bool clear,
        out AnityNative.GraphicsVFXPlanarCameraDrawInfo info)
    {
        info = default;
        if (_disposed || camera is null || effects is null || effects.Count > 4096 ||
            _handle == IntPtr.Zero || !AnityNative.Available)
            return false;
        ulong cameraId = unchecked((ulong)(uint)camera.GetInstanceID());
        if (cameraId == 0) return false;
        var descriptor = new AnityNative.GraphicsVFXPlanarCameraBatchDesc
        {
            cameraId = cameraId,
            worldToClip00 = worldToClip.m00, worldToClip01 = worldToClip.m01,
            worldToClip02 = worldToClip.m02, worldToClip03 = worldToClip.m03,
            worldToClip10 = worldToClip.m10, worldToClip11 = worldToClip.m11,
            worldToClip12 = worldToClip.m12, worldToClip13 = worldToClip.m13,
            worldToClip20 = worldToClip.m20, worldToClip21 = worldToClip.m21,
            worldToClip22 = worldToClip.m22, worldToClip23 = worldToClip.m23,
            worldToClip30 = worldToClip.m30, worldToClip31 = worldToClip.m31,
            worldToClip32 = worldToClip.m32, worldToClip33 = worldToClip.m33,
            cullingMask = camera.cullingMask,
            cameraType = (int)camera.cameraType,
            flags = clear ? 1 : 0
        };
        var nativeEffects = new AnityNative.GraphicsVFXPlanarEffectDesc[effects.Count];
        for (int index = 0; index < effects.Count; index++)
        {
            VisualEffect effect = effects[index];
            if (effect is null) return false;
            Matrix4x4 localToWorld = effect.transform?.localToWorldMatrix ?? Matrix4x4.identity;
            nativeEffects[index] = new AnityNative.GraphicsVFXPlanarEffectDesc
            {
                effectId = unchecked((ulong)(uint)effect.GetInstanceID()),
                localToWorld00 = localToWorld.m00, localToWorld01 = localToWorld.m01,
                localToWorld02 = localToWorld.m02, localToWorld03 = localToWorld.m03,
                localToWorld10 = localToWorld.m10, localToWorld11 = localToWorld.m11,
                localToWorld12 = localToWorld.m12, localToWorld13 = localToWorld.m13,
                localToWorld20 = localToWorld.m20, localToWorld21 = localToWorld.m21,
                localToWorld22 = localToWorld.m22, localToWorld23 = localToWorld.m23,
                localToWorld30 = localToWorld.m30, localToWorld31 = localToWorld.m31,
                localToWorld32 = localToWorld.m32, localToWorld33 = localToWorld.m33,
                layer = effect.gameObject?.layer ?? 0,
                sortOrder = index
            };
        }
        return AnityNative.Graphics_DrawVFXPlanarCamera(
                   _handle, ref descriptor, nativeEffects, nativeEffects.Length,
                   out info) == AnityNative.Result.Ok;
    }

    internal bool TryGetVFXPlanarSubmissionStats(
        out AnityNative.GraphicsVFXPlanarSubmissionStats stats)
    {
        stats = default;
        return !_disposed && _handle != IntPtr.Zero && AnityNative.Available &&
               AnityNative.Graphics_GetVFXPlanarSubmissionStats(
                   _handle, out stats) == AnityNative.Result.Ok;
    }

    internal AnityNative.Result WaitForVFXPlanarSubmissions(
        ulong throughSubmissionId = 0, int timeoutMilliseconds = -1)
        => _disposed || _handle == IntPtr.Zero || !AnityNative.Available
            ? AnityNative.Result.InvalidArg
            : AnityNative.Graphics_WaitForVFXPlanarSubmissions(
                _handle, throughSubmissionId, timeoutMilliseconds);

    internal bool PrepareVFXEffectFrame(
        ulong effectId, uint frameIndex, float gameDeltaTime, float playRate,
        float fixedTimeStep, float maxDeltaTime, bool paused,
        out AnityNative.GraphicsVFXFrameState state)
    {
        state = default;
        return !_disposed && effectId != 0 &&
               float.IsFinite(gameDeltaTime) && gameDeltaTime >= 0f &&
               float.IsFinite(playRate) && playRate >= 0f &&
               float.IsFinite(fixedTimeStep) && fixedTimeStep > 0f &&
               float.IsFinite(maxDeltaTime) && maxDeltaTime > 0f &&
               _handle != IntPtr.Zero && AnityNative.Available &&
               AnityNative.Graphics_PrepareVFXEffectFrame(
                   _handle, effectId, frameIndex, gameDeltaTime, playRate,
                   fixedTimeStep, maxDeltaTime, paused ? 1 : 0,
                   out state) == AnityNative.Result.Ok;
    }

    internal bool CommitVFXEffectFrame(
        ulong effectId, uint frameIndex,
        out AnityNative.GraphicsVFXFrameState state)
    {
        state = default;
        return !_disposed && effectId != 0 && _handle != IntPtr.Zero &&
               AnityNative.Available &&
               AnityNative.Graphics_CommitVFXEffectFrame(
                   _handle, effectId, frameIndex, out state) == AnityNative.Result.Ok;
    }

    internal bool PrepareVFXEffectManualFrame(
        ulong effectId, uint frameIndex, float stepDeltaTime,
        out AnityNative.GraphicsVFXFrameState state)
    {
        state = default;
        return !_disposed && effectId != 0 && _handle != IntPtr.Zero &&
               AnityNative.Available &&
               AnityNative.Graphics_PrepareVFXEffectManualFrame(
                   _handle, effectId, frameIndex, stepDeltaTime,
                   out state) == AnityNative.Result.Ok;
    }

    internal bool AbortVFXEffectFrame(ulong effectId, uint frameIndex)
        => !_disposed && effectId != 0 && _handle != IntPtr.Zero &&
           AnityNative.Available && AnityNative.Graphics_AbortVFXEffectFrame(
               _handle, effectId, frameIndex) == AnityNative.Result.Ok;

    internal bool TryGetVFXEffectFrameState(
        ulong effectId, out AnityNative.GraphicsVFXFrameState state)
    {
        state = default;
        return !_disposed && effectId != 0 && _handle != IntPtr.Zero &&
               AnityNative.Available &&
               AnityNative.Graphics_GetVFXEffectFrameState(
                   _handle, effectId, out state) == AnityNative.Result.Ok;
    }

    internal bool ResetVFXEffectFrameState(ulong effectId)
        => !_disposed && effectId != 0 && _handle != IntPtr.Zero &&
           AnityNative.Available && AnityNative.Graphics_ResetVFXEffectFrameState(
               _handle, effectId) == AnityNative.Result.Ok;

    internal bool TryGetVFXSpawnerEventRecord(
        ulong effectId, long contextId, int strideWords, out byte[] record)
    {
        record = Array.Empty<byte>();
        if (_disposed || effectId == 0 || contextId == 0 || strideWords <= 0 ||
            strideWords > 1024 * 1024 || _handle == IntPtr.Zero || !AnityNative.Available)
            return false;
        var buffer = new byte[checked(strideWords * sizeof(uint))];
        if (AnityNative.Graphics_ReadVFXSpawnerEventRecord(
                _handle, effectId, contextId, buffer, buffer.Length, out int written) !=
            AnityNative.Result.Ok || written != buffer.Length)
            return false;
        record = buffer;
        return true;
    }

    internal bool SetVFXSpawnerEventRecordDefaults(
        ulong effectId, long contextId, byte[] record)
        => !_disposed && effectId != 0 && contextId != 0 && record is not null &&
           _handle != IntPtr.Zero && AnityNative.Available &&
           AnityNative.Graphics_SetVFXSpawnerEventRecordDefaults(
               _handle, effectId, contextId, record, record.Length) == AnityNative.Result.Ok;

    internal bool ControlVFXSpawner(
        ulong effectId, long contextId, bool play, uint seed, bool resetSeed,
        out AnityNative.GraphicsVFXSpawnerState state)
    {
        state = default;
        return !_disposed && effectId != 0 && contextId != 0 &&
               _handle != IntPtr.Zero && AnityNative.Available &&
               AnityNative.Graphics_ControlVFXSpawner(
                   _handle, effectId, contextId, play ? 1 : 0, seed, resetSeed ? 1 : 0) ==
                   AnityNative.Result.Ok &&
               AnityNative.Graphics_GetVFXSpawnerState(
                   _handle, effectId, contextId, out state) == AnityNative.Result.Ok;
    }

    internal bool ControlVFXSpawner(
        ulong effectId, long contextId, bool play, uint seed, bool resetSeed,
        AnityNative.GraphicsVFXSpawnerCallback callback, IntPtr userData,
        out AnityNative.GraphicsVFXSpawnerState state)
    {
        state = default;
        return !_disposed && effectId != 0 && contextId != 0 && callback is not null &&
               _handle != IntPtr.Zero && AnityNative.Available &&
               AnityNative.Graphics_ControlVFXSpawnerWithCallbacks(
                   _handle, effectId, contextId, play ? 1 : 0, seed, resetSeed ? 1 : 0,
                   callback, userData) == AnityNative.Result.Ok &&
               AnityNative.Graphics_GetVFXSpawnerState(
                   _handle, effectId, contextId, out state) == AnityNative.Result.Ok;
    }

    internal bool TickVFXSpawners(
        ulong effectId, float deltaTime, int stateCapacity,
        out AnityNative.GraphicsVFXSpawnerState[] states,
        bool allowUnsafeDeltaTime = false)
    {
        states = Array.Empty<AnityNative.GraphicsVFXSpawnerState>();
        if (_disposed || effectId == 0 ||
            (!allowUnsafeDeltaTime && (!float.IsFinite(deltaTime) || deltaTime < 0f)) ||
            stateCapacity < 0 || stateCapacity > 4096 || _handle == IntPtr.Zero ||
            !AnityNative.Available)
            return false;
        var buffer = new AnityNative.GraphicsVFXSpawnerState[stateCapacity];
        if (AnityNative.Graphics_TickVFXSpawners(
                _handle, effectId, deltaTime, buffer, buffer.Length, out int written) !=
            AnityNative.Result.Ok || written < 0 || written > buffer.Length)
            return false;
        if (written == buffer.Length)
        {
            states = buffer;
            return true;
        }
        states = new AnityNative.GraphicsVFXSpawnerState[written];
        Array.Copy(buffer, states, written);
        return true;
    }

    internal bool TickVFXSpawners(
        ulong effectId, float deltaTime, int stateCapacity,
        AnityNative.GraphicsVFXSpawnerCallback callback, IntPtr userData,
        out AnityNative.GraphicsVFXSpawnerState[] states,
        bool allowUnsafeDeltaTime = false)
    {
        states = Array.Empty<AnityNative.GraphicsVFXSpawnerState>();
        if (_disposed || effectId == 0 ||
            (!allowUnsafeDeltaTime && (!float.IsFinite(deltaTime) || deltaTime < 0f)) ||
            stateCapacity < 0 || stateCapacity > 4096 || callback is null ||
            _handle == IntPtr.Zero || !AnityNative.Available)
            return false;
        var buffer = new AnityNative.GraphicsVFXSpawnerState[stateCapacity];
        if (AnityNative.Graphics_TickVFXSpawnersWithCallbacks(
                _handle, effectId, deltaTime, buffer, buffer.Length, out int written,
                callback, userData) != AnityNative.Result.Ok ||
            written < 0 || written > buffer.Length)
            return false;
        if (written == buffer.Length)
        {
            states = buffer;
            return true;
        }
        states = new AnityNative.GraphicsVFXSpawnerState[written];
        Array.Copy(buffer, states, written);
        return true;
    }

    internal bool TryGetVFXSpawnerState(
        ulong effectId, long contextId,
        out AnityNative.GraphicsVFXSpawnerState state)
    {
        state = default;
        return !_disposed && effectId != 0 && contextId != 0 &&
               _handle != IntPtr.Zero && AnityNative.Available &&
               AnityNative.Graphics_GetVFXSpawnerState(
                   _handle, effectId, contextId, out state) == AnityNative.Result.Ok;
    }

    private static uint[] PadWords(IReadOnlyList<uint> source)
    {
        if (source is null || source.Count > 4)
            throw new InvalidDataException("VFX Initialize operand exceeds four words.");
        var result = new uint[4];
        for (int index = 0; index < source.Count; index++) result[index] = source[index];
        return result;
    }

    internal bool ClearVFXEffectState(ulong effectId)
        => !_disposed && effectId != 0 && _handle != IntPtr.Zero &&
           AnityNative.Available && AnityNative.Graphics_ClearVFXEffectState(
               _handle, effectId) == AnityNative.Result.Ok;

    internal static void ClearVFXEffectStateFromAll(ulong effectId)
    {
        if (effectId == 0) return;
        NativeGraphicsDevice[] devices;
        lock (DevicesLock)
        {
            devices = new NativeGraphicsDevice[LiveDevices.Count];
            LiveDevices.CopyTo(devices);
        }
        foreach (NativeGraphicsDevice device in devices)
        {
            if (device._handle == IntPtr.Zero || !AnityNative.Available) continue;
            if (!device.ClearVFXEffectState(effectId) && NativeRequired)
                throw new InvalidOperationException("Native VFX effect state teardown failed.");
        }
    }

    internal bool TryGetVFXInitializeDispatch(
        ulong effectId,
        long initializeContextId,
        out NativeVFXInitializeDispatch? dispatch)
    {
        dispatch = null;
        if (_disposed || effectId == 0 || initializeContextId == 0 ||
            _handle == IntPtr.Zero || !AnityNative.Available ||
            AnityNative.Graphics_GetVFXInitializeDispatchInfo(
                _handle, effectId, initializeContextId,
                out AnityNative.GraphicsVFXInitializeDispatchInfo info) != AnityNative.Result.Ok)
            return false;
        if (info.desc.effectId != effectId ||
            info.desc.initializeContextId != initializeContextId ||
            info.desc.sequence == 0 || info.desc.startEventIndex < 0 ||
            info.desc.recordCount <= 0 || info.desc.strideBytes <= 0 ||
            info.sourceByteCount < 0 || info.outputByteCount !=
                checked(info.desc.recordCount * info.desc.strideBytes) ||
            info.backendKind is < 0 or > 3)
            throw new InvalidOperationException("Native VFX Initialize dispatch is invalid.");
        var records = new byte[info.outputByteCount];
        if (AnityNative.Graphics_ReadbackVFXInitializeDispatch(
                _handle, effectId, initializeContextId, records, records.Length,
                out int written) != AnityNative.Result.Ok || written != records.Length)
            return false;
        dispatch = new NativeVFXInitializeDispatch(info, records);
        return true;
    }

    internal bool EnqueueVFXOutputEvent(
        ulong effectId,
        int eventNameId,
        ulong sequence,
        byte[] payload,
        int strideWords,
        int recordCount)
    {
        if (_disposed || effectId == 0 || sequence == 0 || payload is null ||
            strideWords < 0 || recordCount < 0 || _handle == IntPtr.Zero || !AnityNative.Available)
            return false;
        var desc = new AnityNative.GraphicsVFXEventUploadDesc
        {
            effectId = effectId,
            sequence = sequence,
            eventNameId = eventNameId,
            recordCount = recordCount,
            strideBytes = checked(strideWords * sizeof(uint))
        };
        return AnityNative.Graphics_EnqueueVFXOutputEventRecords(
            _handle, ref desc, payload, payload.Length) == AnityNative.Result.Ok;
    }

    internal bool TryGetVFXOutputEventCount(ulong effectId, out int count)
    {
        count = 0;
        return !_disposed && effectId != 0 && _handle != IntPtr.Zero && AnityNative.Available &&
            AnityNative.Graphics_GetVFXOutputEventCount(_handle, effectId, out count) == AnityNative.Result.Ok;
    }

    internal bool TryPeekVFXOutputEvent(
        ulong effectId,
        out AnityNative.GraphicsVFXEventUploadInfo info)
    {
        info = default;
        return !_disposed && effectId != 0 && _handle != IntPtr.Zero && AnityNative.Available &&
            AnityNative.Graphics_PeekVFXOutputEventInfo(_handle, effectId, out info) == AnityNative.Result.Ok;
    }

    internal bool TryDequeueVFXOutputEvent(
        ulong effectId,
        ulong expectedSequence,
        int byteCount,
        out byte[] records)
    {
        records = Array.Empty<byte>();
        if (_disposed || effectId == 0 || expectedSequence == 0 || byteCount < 0 ||
            _handle == IntPtr.Zero || !AnityNative.Available) return false;
        var bytes = new byte[byteCount];
        if (AnityNative.Graphics_DequeueVFXOutputEventRecords(
                _handle, effectId, expectedSequence, bytes, bytes.Length, out int written) != AnityNative.Result.Ok ||
            written != bytes.Length) return false;
        records = bytes;
        return true;
    }

    private static ulong TextureState(Texture2D texture)
    {
        const ulong offset = 1469598103934665603UL;
        const ulong prime = 1099511628211UL;
        ulong state = offset;
        static ulong Mix(ulong current, ulong value) => (current ^ value) * prime;
        state = Mix(state, texture.nativeRevision);
        state = Mix(state, unchecked((uint)texture.width));
        state = Mix(state, unchecked((uint)texture.height));
        state = Mix(state, unchecked((uint)texture.mipmapCount));
        state = Mix(state, unchecked((uint)texture.format));
        state = Mix(state, unchecked((uint)texture.filterMode));
        state = Mix(state, unchecked((uint)texture.wrapModeU));
        state = Mix(state, unchecked((uint)texture.wrapModeV));
        state = Mix(state, texture.linear ? 1UL : 0UL);
        state = Mix(state, unchecked((uint)BitConverter.SingleToInt32Bits(texture.mipMapBias)));
        state = Mix(state, unchecked((uint)EffectiveAnisoLevel(texture)));
        return state;
    }

    private static int EffectiveAnisoLevel(Texture texture)
    {
        int requested = Math.Clamp(texture.anisoLevel, 1, 16);
        return QualitySettings.anisotropicFiltering switch
        {
            AnisotropicFiltering.Disable => 1,
            AnisotropicFiltering.ForceEnable => Math.Max(
                requested, Math.Clamp(QualitySettings.anisotropicFilteringLevel, 1, 16)),
            _ => requested
        };
    }

    private void RefreshUIUploadStats()
    {
        if (_handle != IntPtr.Zero &&
            AnityNative.Graphics_GetUIUploadStats(_handle, out var stats) == AnityNative.Result.Ok)
            LastUIUploadStats = stats;
    }

    private void EnsureAttachedCanvasValid()
    {
        if (_uiCanvas is not null && !_uiCanvas.IsValid)
            AttachUICanvas(null);
    }

    public void Present()
    {
        if (_swapchain != IntPtr.Zero && AnityNative.Available)
        {
            LastSwapchainResult =
                AnityNative.Graphics_PresentSwapchain(_swapchain);
            if (LastSwapchainResult != AnityNative.Result.DeviceLost)
                PresentCount++;
            return;
        }
        if (_handle != IntPtr.Zero && AnityNative.Available)
        {
            LastSwapchainResult = AnityNative.Graphics_Present(_handle);
            if (LastSwapchainResult != AnityNative.Result.DeviceLost)
                PresentCount++;
            return;
        }
        // managed headless present
        LastSwapchainResult = AnityNative.Result.Ok;
        PresentCount++;
    }

    /// <summary>Create headless or windowed swapchain (Metal/Vulkan/D3D/null path).</summary>
    public bool CreateSwapchain(int width = 0, int height = 0, int imageCount = 2, bool vsync = true, bool hdr = false, IntPtr nativeWindow = default)
    {
        int w = width > 0 ? width : Width;
        int h = height > 0 ? height : Height;
        if (_handle != IntPtr.Zero && AnityNative.Available)
        {
            try
            {
                var desc = new AnityNative.SwapchainDesc
                {
                    width = w,
                    height = h,
                    imageCount = imageCount,
                    vsync = vsync ? 1 : 0,
                    hdr = hdr ? 1 : 0,
                    nativeWindow = nativeWindow
                };
                LastSwapchainResult =
                    AnityNative.Graphics_CreateSwapchain(_handle, ref desc, out var sc);
                if (LastSwapchainResult == AnityNative.Result.Ok && sc != IntPtr.Zero)
                {
                    _swapchain = sc;
                    SwapchainImageCount = AnityNative.Graphics_GetSwapchainImageCount(sc);
                    Width = AnityNative.Graphics_GetSwapchainWidth(sc);
                    Height = AnityNative.Graphics_GetSwapchainHeight(sc);
                    SwapchainHeadless = AnityNative.Graphics_IsSwapchainHeadless(sc) != 0;
                    try
                    {
                        SwapchainHasNativeSurface = AnityNative.Graphics_SwapchainHasNativeSurface(sc) != 0;
                        SwapchainBackendKind = AnityNative.Graphics_GetSwapchainBackendKind(sc);
                        SwapchainSurfaceKind = AnityNative.Graphics_GetSwapchainSurfaceKind(sc);
                    }
                    catch
                    {
                        SwapchainHasNativeSurface = false;
                        SwapchainBackendKind = DeviceType == GraphicsDeviceType.Vulkan ? 1
                            : DeviceType == GraphicsDeviceType.Metal ? 2 : 0;
                        SwapchainSurfaceKind = 0;
                    }
                    return true;
                }
                // Once a native device accepted creation, surface every native
                // swapchain validation/capability failure to the caller. Falling
                // through to a managed headless swapchain would turn an invalid
                // Metal/Vulkan attachment into a false success.
                return false;
            }
            catch
            {
                AnityNative.MarkUnavailable();
            }
        }

        // Native-required gates must distinguish unsupported native surfaces
        // (for example an invalid MSAA attachment) from a managed fallback.
        if (NativeRequired) return false;

        // Managed headless swapchain (native lib missing or create failed)
        _managedSwapchain = true;
        LastSwapchainResult = AnityNative.Result.Ok;
        Width = w > 0 ? w : 1280;
        Height = h > 0 ? h : 720;
        SwapchainImageCount = imageCount > 0 ? imageCount : 2;
        SwapchainHeadless = nativeWindow == IntPtr.Zero;
        SwapchainHasNativeSurface = false;
        SwapchainBackendKind = preferredKind(DeviceType);
        SwapchainSurfaceKind = 0;
        return true;
    }

    private static int preferredKind(GraphicsDeviceType t) => t switch
    {
        GraphicsDeviceType.Vulkan => 1,
        GraphicsDeviceType.Metal => 2,
        GraphicsDeviceType.Direct3D11 or GraphicsDeviceType.Direct3D12 => 3,
        _ => 0
    };

    public int AcquireNextImage()
    {
        if (_swapchain != IntPtr.Zero && AnityNative.Available)
        {
            LastSwapchainResult =
                AnityNative.Graphics_AcquireNextImage(_swapchain, out int idx);
            if (LastSwapchainResult == AnityNative.Result.Ok)
                return idx;
            if (LastSwapchainResult == AnityNative.Result.DeviceLost)
                return -1;
        }
        LastSwapchainResult = AnityNative.Result.Ok;
        return PresentCount % Math.Max(1, SwapchainImageCount);
    }

    /// <summary>Reads a headless backend target as tightly packed top-to-bottom RGBA8.</summary>
    public bool TryReadbackSwapchainRGBA8(out byte[] pixels)
    {
        pixels = Array.Empty<byte>();
        if (_disposed || _swapchain == IntPtr.Zero || !AnityNative.Available ||
            Width <= 0 || Height <= 0) return false;
        try
        {
            int required = checked(Width * Height * 4);
            var result = new byte[required];
            LastSwapchainResult = AnityNative.Graphics_ReadbackSwapchainRGBA8(
                    _swapchain, result, result.Length, out int written);
            if (LastSwapchainResult != AnityNative.Result.Ok ||
                written != result.Length) return false;
            pixels = result;
            return true;
        }
        catch
        {
            if (NativeRequired) throw;
            return false;
        }
    }

    /// <summary>Explicit ACES-to-sRGB readback for a headless HDR swapchain.</summary>
    public bool TryReadbackSwapchainToneMappedRGBA8(out byte[] pixels)
    {
        pixels = Array.Empty<byte>();
        if (_disposed || _swapchain == IntPtr.Zero || !AnityNative.Available ||
            Width <= 0 || Height <= 0) return false;
        try
        {
            var result = new byte[checked(Width * Height * 4)];
            LastSwapchainResult = AnityNative.Graphics_ReadbackSwapchainToneMappedRGBA8(
                _swapchain, result, result.Length, out int written);
            if (LastSwapchainResult != AnityNative.Result.Ok || written != result.Length)
                return false;
            pixels = result;
            return true;
        }
        catch
        {
            if (NativeRequired) throw;
            return false;
        }
    }

    /// <summary>Runs the native backend's final URP HDR grade for the active swapchain.</summary>
    public bool TryProcessSwapchainHDR(AnityNative.HDRColorGrade grade)
    {
        if (_disposed || _swapchain == IntPtr.Zero || !AnityNative.Available) return false;
        try
        {
            LastSwapchainResult = AnityNative.Graphics_ProcessSwapchainHDR(_swapchain, ref grade);
            return LastSwapchainResult == AnityNative.Result.Ok;
        }
        catch
        {
            if (NativeRequired) throw;
            return false;
        }
    }

    public void Dispose()
    {
        lock (_lifetimeLock)
        {
            if (_disposed) return;
            if (_nativeUseDepth != 0)
            {
                _disposePending = true;
                return;
            }
            DisposeCore();
        }
    }

    private void DisposeCore()
    {
        if (_disposed) return;
        CanvasNativeRenderBridge.OnDeviceDisposed(this);
        _disposed = true;
        _disposePending = false;
        if (_handle != IntPtr.Zero && AnityNative.Available)
        {
            try { AnityNative.Graphics_SetUICanvas(_handle, IntPtr.Zero); } catch { }
        }
        _uiCanvas = null;
        lock (_textureLock)
        {
            _textureStates.Clear();
            if (_swapchain != IntPtr.Zero && AnityNative.Available)
            {
                try { AnityNative.Graphics_DestroySwapchain(_swapchain); } catch { }
                _swapchain = IntPtr.Zero;
            }
            if (_handle != IntPtr.Zero && AnityNative.Available)
            {
                AnityNative.Graphics_DestroyDevice(_handle);
                _handle = IntPtr.Zero;
            }
        }
        _managedSwapchain = false;
        lock (DevicesLock)
        {
            LiveDevices.Remove(this);
            if (Current == this) Current = null;
        }
    }

    private static AnityNative.GraphicsDeviceTypeNative Map(GraphicsDeviceType t) => t switch
    {
        GraphicsDeviceType.Direct3D11 => AnityNative.GraphicsDeviceTypeNative.D3D11,
        GraphicsDeviceType.Direct3D12 => AnityNative.GraphicsDeviceTypeNative.D3D12,
        GraphicsDeviceType.Vulkan => AnityNative.GraphicsDeviceTypeNative.Vulkan,
        GraphicsDeviceType.Metal => AnityNative.GraphicsDeviceTypeNative.Metal,
        GraphicsDeviceType.OpenGLES3 => AnityNative.GraphicsDeviceTypeNative.OpenGLES3,
        GraphicsDeviceType.OpenGLES2 => AnityNative.GraphicsDeviceTypeNative.OpenGLES2,
        GraphicsDeviceType.OpenGLCore => AnityNative.GraphicsDeviceTypeNative.OpenGLCore,
        GraphicsDeviceType.WebGL2 => AnityNative.GraphicsDeviceTypeNative.WebGL2,
        _ => AnityNative.GraphicsDeviceTypeNative.Null
    };

    private static GraphicsDeviceType MapBack(int t) => t switch
    {
        2 => GraphicsDeviceType.Direct3D11,
        18 => GraphicsDeviceType.Direct3D12,
        21 => GraphicsDeviceType.Vulkan,
        16 => GraphicsDeviceType.Metal,
        11 => GraphicsDeviceType.OpenGLES3,
        8 => GraphicsDeviceType.OpenGLES2,
        17 => GraphicsDeviceType.OpenGLCore,
        28 => GraphicsDeviceType.WebGL2,
        _ => GraphicsDeviceType.Null
    };

    private static bool NativeRequired
        => string.Equals(Environment.GetEnvironmentVariable("ANITY_REQUIRE_NATIVE"), "1", StringComparison.Ordinal);
}
