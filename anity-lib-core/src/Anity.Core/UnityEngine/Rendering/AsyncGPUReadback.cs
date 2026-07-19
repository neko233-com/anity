using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Anity.Core.Runtime.Native;
using Unity.Collections;
using Unity.Jobs;

namespace UnityEngine.Rendering;

/// <summary>Deferred GPU/graphics readback requests matching the Unity 2022 public surface.</summary>
public struct AsyncGPUReadbackRequest
{
    private readonly ReadbackState? _state;

    internal AsyncGPUReadbackRequest(ReadbackState state) => _state = state;

    public int depth => _state?.Depth ?? 0;
    public bool done => _state?.Done ?? false;
    public bool forcePlayerLoopUpdate
    {
        get => _state?.ForcePlayerLoopUpdate ?? false;
        set { if (_state != null) _state.ForcePlayerLoopUpdate = value; }
    }
    public bool hasError => _state?.HasError ?? true;
    public int height => _state?.Height ?? 0;
    public int layerCount => _state?.LayerCount ?? 0;
    public int layerDataSize => _state?.LayerDataSize ?? 0;
    public int width => _state?.Width ?? 0;

    public NativeArray<T> GetData<T>(int layer = 0) where T : struct
    {
        if (_state == null || !_state.Done)
            throw new InvalidOperationException("Async GPU readback data is not available until the request has completed.");
        return _state.GetData<T>(layer);
    }

    public void Update() => _state?.Complete(force: true);
    public void WaitForCompletion() => _state?.Complete(force: true);
}

/// <summary>Unity-compatible readback submission API. Completion is deferred until the player loop.</summary>
[Bindings.StaticAccessor("AsyncGPUReadbackManager::GetInstance()", Bindings.StaticAccessorType.Dot)]
public static class AsyncGPUReadback
{
    private static readonly object Gate = new();
    private static readonly List<ReadbackState> Pending = new();

    public static AsyncGPUReadbackRequest Request(ComputeBuffer src, int size, int offset, Action<AsyncGPUReadbackRequest> callback = null)
        => QueueBuffer(src, size, offset, callback);
    public static AsyncGPUReadbackRequest Request(ComputeBuffer src, Action<AsyncGPUReadbackRequest> callback = null)
        => QueueBuffer(src, src?.count * src?.stride ?? 0, 0, callback);
    public static AsyncGPUReadbackRequest Request(GraphicsBuffer src, int size, int offset, Action<AsyncGPUReadbackRequest> callback = null)
        => QueueBuffer(src, size, offset, callback);
    public static AsyncGPUReadbackRequest Request(GraphicsBuffer src, Action<AsyncGPUReadbackRequest> callback = null)
        => QueueBuffer(src, src?.count * src?.stride ?? 0, 0, callback);

    public static AsyncGPUReadbackRequest Request(Texture src, int mipIndex, int x, int width, int y, int height, int z, int depth, Action<AsyncGPUReadbackRequest> callback = null)
        => QueueTexture(src, mipIndex, x, width, y, height, z, depth, callback);
    public static AsyncGPUReadbackRequest Request(Texture src, int mipIndex, int x, int width, int y, int height, int z, int depth, UnityEngine.Experimental.Rendering.GraphicsFormat dstFormat, Action<AsyncGPUReadbackRequest> callback = null)
        => QueueTexture(src, mipIndex, x, width, y, height, z, depth, callback);
    public static AsyncGPUReadbackRequest Request(Texture src, int mipIndex, int x, int width, int y, int height, int z, int depth, TextureFormat dstFormat, Action<AsyncGPUReadbackRequest> callback = null)
        => QueueTexture(src, mipIndex, x, width, y, height, z, depth, callback);
    public static AsyncGPUReadbackRequest Request(Texture src, int mipIndex = 0, Action<AsyncGPUReadbackRequest> callback = null)
        => QueueTexture(src, mipIndex, 0, TextureMipWidth(src, mipIndex), 0, TextureMipHeight(src, mipIndex), 0, TextureMipDepth(src, mipIndex), callback);
    public static AsyncGPUReadbackRequest Request(Texture src, int mipIndex, UnityEngine.Experimental.Rendering.GraphicsFormat dstFormat, Action<AsyncGPUReadbackRequest> callback = null)
        => Request(src, mipIndex, callback);
    public static AsyncGPUReadbackRequest Request(Texture src, int mipIndex, TextureFormat dstFormat, Action<AsyncGPUReadbackRequest> callback = null)
        => Request(src, mipIndex, callback);

    public static AsyncGPUReadbackRequest RequestIntoNativeArray<T>(ref NativeArray<T> output, ComputeBuffer src, int size, int offset, Action<AsyncGPUReadbackRequest> callback = null) where T : struct
    {
        NativeArray<T> target = output;
        return QueueBuffer(src, size, offset, callback, bytes => CopyToNativeArray(bytes, target));
    }
    public static AsyncGPUReadbackRequest RequestIntoNativeArray<T>(ref NativeArray<T> output, ComputeBuffer src, Action<AsyncGPUReadbackRequest> callback = null) where T : struct
        => RequestIntoNativeArray(ref output, src, src?.count * src?.stride ?? 0, 0, callback);
    public static AsyncGPUReadbackRequest RequestIntoNativeArray<T>(ref NativeArray<T> output, GraphicsBuffer src, int size, int offset, Action<AsyncGPUReadbackRequest> callback = null) where T : struct
    {
        NativeArray<T> target = output;
        return QueueBuffer(src, size, offset, callback, bytes => CopyToNativeArray(bytes, target));
    }
    public static AsyncGPUReadbackRequest RequestIntoNativeArray<T>(ref NativeArray<T> output, GraphicsBuffer src, Action<AsyncGPUReadbackRequest> callback = null) where T : struct
        => RequestIntoNativeArray(ref output, src, src?.count * src?.stride ?? 0, 0, callback);
    private static AsyncGPUReadbackRequest QueueTextureIntoNativeArray<T>(ref NativeArray<T> output, Texture src, int mipIndex, int x, int width, int y, int height, int z, int depth, Action<AsyncGPUReadbackRequest> callback) where T : struct
    {
        NativeArray<T> target = output;
        return QueueTexture(src, mipIndex, x, width, y, height, z, depth, callback, bytes => CopyToNativeArray(bytes, target));
    }
    public static AsyncGPUReadbackRequest RequestIntoNativeArray<T>(ref NativeArray<T> output, Texture src, int mipIndex, int x, int width, int y, int height, int z, int depth, UnityEngine.Experimental.Rendering.GraphicsFormat dstFormat, Action<AsyncGPUReadbackRequest> callback = null) where T : struct
        => QueueTextureIntoNativeArray(ref output, src, mipIndex, x, width, y, height, z, depth, callback);
    public static AsyncGPUReadbackRequest RequestIntoNativeArray<T>(ref NativeArray<T> output, Texture src, int mipIndex, int x, int width, int y, int height, int z, int depth, TextureFormat dstFormat, Action<AsyncGPUReadbackRequest> callback = null) where T : struct
        => QueueTextureIntoNativeArray(ref output, src, mipIndex, x, width, y, height, z, depth, callback);
    public static AsyncGPUReadbackRequest RequestIntoNativeArray<T>(ref NativeArray<T> output, Texture src, int mipIndex = 0, Action<AsyncGPUReadbackRequest> callback = null) where T : struct
        => QueueTextureIntoNativeArray(ref output, src, mipIndex, 0, TextureMipWidth(src, mipIndex), 0, TextureMipHeight(src, mipIndex), 0, TextureMipDepth(src, mipIndex), callback);
    public static AsyncGPUReadbackRequest RequestIntoNativeArray<T>(ref NativeArray<T> output, Texture src, int mipIndex, UnityEngine.Experimental.Rendering.GraphicsFormat dstFormat, Action<AsyncGPUReadbackRequest> callback = null) where T : struct
        => RequestIntoNativeArray(ref output, src, mipIndex, callback);
    public static AsyncGPUReadbackRequest RequestIntoNativeArray<T>(ref NativeArray<T> output, Texture src, int mipIndex, TextureFormat dstFormat, Action<AsyncGPUReadbackRequest> callback = null) where T : struct
        => RequestIntoNativeArray(ref output, src, mipIndex, callback);

    public static AsyncGPUReadbackRequest RequestIntoNativeSlice<T>(ref NativeSlice<T> output, ComputeBuffer src, int size, int offset, Action<AsyncGPUReadbackRequest> callback = null) where T : struct
    {
        NativeSlice<T> target = output;
        return QueueBuffer(src, size, offset, callback, bytes => CopyToNativeSlice(bytes, target));
    }
    public static AsyncGPUReadbackRequest RequestIntoNativeSlice<T>(ref NativeSlice<T> output, ComputeBuffer src, Action<AsyncGPUReadbackRequest> callback = null) where T : struct
        => RequestIntoNativeSlice(ref output, src, src?.count * src?.stride ?? 0, 0, callback);
    public static AsyncGPUReadbackRequest RequestIntoNativeSlice<T>(ref NativeSlice<T> output, GraphicsBuffer src, int size, int offset, Action<AsyncGPUReadbackRequest> callback = null) where T : struct
    {
        NativeSlice<T> target = output;
        return QueueBuffer(src, size, offset, callback, bytes => CopyToNativeSlice(bytes, target));
    }
    public static AsyncGPUReadbackRequest RequestIntoNativeSlice<T>(ref NativeSlice<T> output, GraphicsBuffer src, Action<AsyncGPUReadbackRequest> callback = null) where T : struct
        => RequestIntoNativeSlice(ref output, src, src?.count * src?.stride ?? 0, 0, callback);
    private static AsyncGPUReadbackRequest QueueTextureIntoNativeSlice<T>(ref NativeSlice<T> output, Texture src, int mipIndex, int x, int width, int y, int height, int z, int depth, Action<AsyncGPUReadbackRequest> callback) where T : struct
    {
        NativeSlice<T> target = output;
        return QueueTexture(src, mipIndex, x, width, y, height, z, depth, callback, bytes => CopyToNativeSlice(bytes, target));
    }
    public static AsyncGPUReadbackRequest RequestIntoNativeSlice<T>(ref NativeSlice<T> output, Texture src, int mipIndex, int x, int width, int y, int height, int z, int depth, UnityEngine.Experimental.Rendering.GraphicsFormat dstFormat, Action<AsyncGPUReadbackRequest> callback = null) where T : struct
        => QueueTextureIntoNativeSlice(ref output, src, mipIndex, x, width, y, height, z, depth, callback);
    public static AsyncGPUReadbackRequest RequestIntoNativeSlice<T>(ref NativeSlice<T> output, Texture src, int mipIndex, int x, int width, int y, int height, int z, int depth, TextureFormat dstFormat, Action<AsyncGPUReadbackRequest> callback = null) where T : struct
        => QueueTextureIntoNativeSlice(ref output, src, mipIndex, x, width, y, height, z, depth, callback);
    public static AsyncGPUReadbackRequest RequestIntoNativeSlice<T>(ref NativeSlice<T> output, Texture src, int mipIndex = 0, Action<AsyncGPUReadbackRequest> callback = null) where T : struct
        => QueueTextureIntoNativeSlice(ref output, src, mipIndex, 0, TextureMipWidth(src, mipIndex), 0, TextureMipHeight(src, mipIndex), 0, TextureMipDepth(src, mipIndex), callback);
    public static AsyncGPUReadbackRequest RequestIntoNativeSlice<T>(ref NativeSlice<T> output, Texture src, int mipIndex, UnityEngine.Experimental.Rendering.GraphicsFormat dstFormat, Action<AsyncGPUReadbackRequest> callback = null) where T : struct
        => RequestIntoNativeSlice(ref output, src, mipIndex, callback);
    public static AsyncGPUReadbackRequest RequestIntoNativeSlice<T>(ref NativeSlice<T> output, Texture src, int mipIndex, TextureFormat dstFormat, Action<AsyncGPUReadbackRequest> callback = null) where T : struct
        => RequestIntoNativeSlice(ref output, src, mipIndex, callback);

    public static void WaitAllRequests()
    {
        ReadbackState[] states;
        lock (Gate) states = Pending.ToArray();
        foreach (ReadbackState state in states) state.Complete(force: true);
    }

    internal static void ProcessPlayerLoopUpdate()
    {
        ReadbackState[] states;
        lock (Gate) states = Pending.ToArray();
        foreach (ReadbackState state in states) state.Complete(force: false);
    }

    private static AsyncGPUReadbackRequest QueueBuffer(ComputeBuffer? src, int size, int offset, Action<AsyncGPUReadbackRequest>? callback, Func<byte[], bool>? output = null)
        => Queue(src != null && src.TryGetReadbackData(size, offset, out byte[] bytes)
                ? new ReadbackPayload(bytes, 0, 0, 1, 1) : ReadbackPayload.Error,
            callback, output);
    private static AsyncGPUReadbackRequest QueueBuffer(GraphicsBuffer? src, int size, int offset, Action<AsyncGPUReadbackRequest>? callback, Func<byte[], bool>? output = null)
        => Queue(src != null && src.TryGetReadbackData(size, offset, out byte[] bytes)
                ? new ReadbackPayload(bytes, 0, 0, 1, 1) : ReadbackPayload.Error,
            callback, output);

    private static AsyncGPUReadbackRequest QueueTexture(Texture? src, int mip, int x, int requestedWidth, int y, int requestedHeight, int z, int requestedDepth, Action<AsyncGPUReadbackRequest>? callback, Func<byte[], bool>? output = null)
    {
        if (src is Texture2D texture2D)
        {
            if (mip < 0 || mip >= texture2D.mipmapCount) return Queue(ReadbackPayload.Error, callback, output);
            int mipWidth = texture2D.GetMipWidth(mip);
            int mipHeight = texture2D.GetMipHeight(mip);
            if (z != 0 || requestedDepth != 1 || x < 0 || y < 0 || requestedWidth < 1 || requestedHeight < 1 || x + requestedWidth > mipWidth || y + requestedHeight > mipHeight)
                return Queue(ReadbackPayload.Error, callback, output);
            Color32[] pixels = texture2D.GetPixels32(mip);
            byte[] bytes = CopyRgbaRegion(pixels, mipWidth, x, requestedWidth, y, requestedHeight);
            return Queue(new ReadbackPayload(bytes, requestedWidth, requestedHeight, 1, 1), callback, output);
        }
        if (src is Texture2DArray texture2DArray)
        {
            if (mip != 0 || !IsValidRegion(texture2DArray.width, texture2DArray.height, texture2DArray.depth, x, requestedWidth, y, requestedHeight, z, requestedDepth))
                return Queue(ReadbackPayload.Error, callback, output);
            return Queue(BuildLayeredPayload(texture2DArray.width, texture2DArray.height, requestedDepth, x, requestedWidth, y, requestedHeight,
                layer => texture2DArray.GetPixels(z + layer)), callback, output);
        }
        if (src is Texture3D texture3D)
        {
            if (mip != 0 || !IsValidRegion(texture3D.width, texture3D.height, texture3D.depth, x, requestedWidth, y, requestedHeight, z, requestedDepth))
                return Queue(ReadbackPayload.Error, callback, output);
            Color[] pixels = texture3D.GetPixels();
            int layerPixels = checked(texture3D.width * texture3D.height);
            return Queue(BuildLayeredPayload(texture3D.width, texture3D.height, requestedDepth, x, requestedWidth, y, requestedHeight,
                layer => SliceLayer(pixels, layerPixels, z + layer)), callback, output);
        }
        if (src is Cubemap cubemap)
        {
            if (mip != 0 || !IsValidRegion(cubemap.width, cubemap.height, 6, x, requestedWidth, y, requestedHeight, z, requestedDepth))
                return Queue(ReadbackPayload.Error, callback, output);
            return Queue(BuildLayeredPayload(cubemap.width, cubemap.height, requestedDepth, x, requestedWidth, y, requestedHeight,
                layer => cubemap.GetPixels((UnityEngine.CubemapFace)(z + layer))), callback, output);
        }
        if (src is CubemapArray cubemapArray)
        {
            int totalFaces = checked(cubemapArray.cubemapCount * 6);
            if (mip != 0 || !IsValidRegion(cubemapArray.width, cubemapArray.height, totalFaces, x, requestedWidth, y, requestedHeight, z, requestedDepth))
                return Queue(ReadbackPayload.Error, callback, output);
            return Queue(BuildLayeredPayload(cubemapArray.width, cubemapArray.height, requestedDepth, x, requestedWidth, y, requestedHeight,
                layer =>
                {
                    int sourceLayer = z + layer;
                    return cubemapArray.GetPixels((UnityEngine.CubemapFace)(sourceLayer % 6), sourceLayer / 6);
                }), callback, output);
        }
        if (src is RenderTexture renderTexture && mip == 0 && IsValidRegion(renderTexture.width, renderTexture.height, Math.Max(1, renderTexture.volumeDepth), x, requestedWidth, y, requestedHeight, z, requestedDepth))
        {
            return Queue(() => ReadbackNativeRenderTexture(renderTexture, x, requestedWidth, y, requestedHeight, z, requestedDepth), callback, output);
        }
        return Queue(ReadbackPayload.Error, callback, output);
    }

    private static AsyncGPUReadbackRequest Queue(ReadbackPayload payload, Action<AsyncGPUReadbackRequest>? callback, Func<byte[], bool>? output)
        => Queue(() => payload, callback, output);
    private static AsyncGPUReadbackRequest Queue(Func<ReadbackPayload> produce, Action<AsyncGPUReadbackRequest>? callback, Func<byte[], bool>? output)
    {
        var state = new ReadbackState(produce, callback, output, Retire);
        lock (Gate) Pending.Add(state);
        return new AsyncGPUReadbackRequest(state);
    }

    private static void Retire(ReadbackState state)
    {
        lock (Gate) Pending.Remove(state);
    }

    private static byte[] CopyRgbaRegion(Color32[] pixels, int sourceWidth, int x, int width, int y, int height)
    {
        byte[] bytes = new byte[checked(width * height * 4)];
        int destination = 0;
        for (int row = 0; row < height; row++)
        for (int column = 0; column < width; column++)
        {
            Color32 pixel = pixels[(y + row) * sourceWidth + x + column];
            bytes[destination++] = pixel.r; bytes[destination++] = pixel.g; bytes[destination++] = pixel.b; bytes[destination++] = pixel.a;
        }
        return bytes;
    }

    private static byte[] CopyRgbaRegion(Color[] pixels, int sourceWidth, int x, int width, int y, int height)
    {
        byte[] bytes = new byte[checked(width * height * 4)];
        int destination = 0;
        for (int row = 0; row < height; row++)
        for (int column = 0; column < width; column++)
        {
            Color32 pixel = pixels[(y + row) * sourceWidth + x + column];
            bytes[destination++] = pixel.r; bytes[destination++] = pixel.g; bytes[destination++] = pixel.b; bytes[destination++] = pixel.a;
        }
        return bytes;
    }

    private static ReadbackPayload BuildLayeredPayload(int sourceWidth, int sourceHeight, int layerCount, int x, int width, int y, int height, Func<int, Color[]> getLayer)
    {
        try
        {
            int layerSize = checked(width * height * 4);
            byte[] data = new byte[checked(layerCount * layerSize)];
            for (int layer = 0; layer < layerCount; layer++)
            {
                Color[] pixels = getLayer(layer);
                if (pixels.Length != checked(sourceWidth * sourceHeight)) return ReadbackPayload.Error;
                Buffer.BlockCopy(CopyRgbaRegion(pixels, sourceWidth, x, width, y, height), 0, data, layer * layerSize, layerSize);
            }
            return new ReadbackPayload(data, width, height, layerCount, layerCount);
        }
        catch (OverflowException)
        {
            return ReadbackPayload.Error;
        }
    }

    private static ReadbackPayload ReadbackNativeRenderTexture(RenderTexture texture, int x, int width, int y, int height, int z, int depth)
    {
        NativeGraphicsDevice? device = NativeGraphicsDevice.Current;
        if (device == null) return ReadbackPayload.Error;
        try
        {
            int layerSize = checked(width * height * 4);
            byte[] result = new byte[checked(depth * layerSize)];
            for (int layer = 0; layer < depth; layer++)
            {
                if (!device.TryReadbackCameraRenderTargetRGBA8(texture, out byte[] source, z + layer) || source.Length != checked(texture.width * texture.height * 4))
                    return ReadbackPayload.Error;
                Buffer.BlockCopy(CopyRgbaRegion(source, texture.width, x, width, y, height), 0, result, layer * layerSize, layerSize);
            }
            return new ReadbackPayload(result, width, height, depth, depth);
        }
        catch (OverflowException)
        {
            return ReadbackPayload.Error;
        }
    }

    private static byte[] CopyRgbaRegion(byte[] pixels, int sourceWidth, int x, int width, int y, int height)
    {
        byte[] bytes = new byte[checked(width * height * 4)];
        for (int row = 0; row < height; row++)
            Buffer.BlockCopy(pixels, checked(((y + row) * sourceWidth + x) * 4), bytes, row * width * 4, width * 4);
        return bytes;
    }

    private static Color[] SliceLayer(Color[] pixels, int layerPixels, int layer)
    {
        var result = new Color[layerPixels];
        Array.Copy(pixels, checked(layer * layerPixels), result, 0, layerPixels);
        return result;
    }

    private static bool IsValidRegion(int sourceWidth, int sourceHeight, int sourceDepth, int x, int width, int y, int height, int z, int depth)
        => x >= 0 && y >= 0 && z >= 0 && width > 0 && height > 0 && depth > 0 &&
           x <= sourceWidth - width && y <= sourceHeight - height && z <= sourceDepth - depth;

    private static int TextureMipWidth(Texture? texture, int mip)
        => texture is Texture2D texture2D && mip >= 0 && mip < texture2D.mipmapCount
            ? texture2D.GetMipWidth(mip) : texture?.width ?? 0;

    private static int TextureMipHeight(Texture? texture, int mip)
        => texture is Texture2D texture2D && mip >= 0 && mip < texture2D.mipmapCount
            ? texture2D.GetMipHeight(mip) : texture?.height ?? 0;

    private static int TextureMipDepth(Texture? texture, int mip)
        => texture switch
        {
            Texture2DArray texture2DArray when mip == 0 => texture2DArray.depth,
            Texture3D texture3D when mip == 0 => texture3D.depth,
            Cubemap when mip == 0 => 6,
            CubemapArray cubemapArray when mip == 0 => cubemapArray.cubemapCount * 6,
            RenderTexture renderTexture when mip == 0 => Math.Max(1, renderTexture.volumeDepth),
            _ => 1
        };

    private static bool CopyToNativeArray<T>(byte[] bytes, NativeArray<T> destination) where T : struct
    {
        if (!ReadbackState.TryDecode(bytes, out NativeArray<T> decoded) || decoded.Length != destination.Length) return false;
        for (int index = 0; index < decoded.Length; index++) destination[index] = decoded[index];
        return true;
    }

    private static bool CopyToNativeSlice<T>(byte[] bytes, NativeSlice<T> destination) where T : struct
    {
        if (!ReadbackState.TryDecode(bytes, out NativeArray<T> decoded) || decoded.Length != destination.Length) return false;
        for (int index = 0; index < decoded.Length; index++) destination[index] = decoded[index];
        return true;
    }
}

internal sealed class ReadbackState
{
    private readonly Func<ReadbackPayload> _produce;
    private readonly Action<AsyncGPUReadbackRequest>? _callback;
    private readonly Func<byte[], bool>? _output;
    private readonly Action<ReadbackState> _retire;
    private byte[] _data = Array.Empty<byte>();
    private bool _done;
    private bool _hasError;

    internal ReadbackState(Func<ReadbackPayload> produce, Action<AsyncGPUReadbackRequest>? callback, Func<byte[], bool>? output, Action<ReadbackState> retire)
    {
        _produce = produce;
        _callback = callback;
        _output = output;
        _retire = retire;
    }

    internal int Depth { get; private set; }
    internal bool Done => _done;
    internal bool ForcePlayerLoopUpdate { get; set; }
    internal bool HasError => _hasError;
    internal int Height { get; private set; }
    internal int LayerCount { get; private set; }
    internal int LayerDataSize { get; private set; }
    internal int Width { get; private set; }

    internal void Complete(bool force)
    {
        if (_done || (!force && !ForcePlayerLoopUpdate && Time.frameCount == 0)) return;
        try
        {
            ReadbackPayload payload = _produce();
            _hasError = payload.IsError;
            if (!_hasError)
            {
                _data = payload.Data;
                Width = payload.Width; Height = payload.Height; Depth = payload.Depth; LayerCount = payload.LayerCount; LayerDataSize = payload.Data.Length / payload.LayerCount;
                if (_output != null && !_output(_data)) _hasError = true;
            }
        }
        catch
        {
            _hasError = true;
        }
        _done = true;
        _retire(this);
        try { _callback?.Invoke(new AsyncGPUReadbackRequest(this)); } catch { }
    }

    internal NativeArray<T> GetData<T>(int layer) where T : struct
    {
        if (_hasError) throw new InvalidOperationException("The async GPU readback request completed with an error.");
        if (layer < 0 || layer >= LayerCount) throw new ArgumentOutOfRangeException(nameof(layer));
        byte[] layerData = new byte[LayerDataSize];
        Buffer.BlockCopy(_data, checked(layer * LayerDataSize), layerData, 0, LayerDataSize);
        if (!TryDecode(layerData, out NativeArray<T> data))
        {
            _hasError = true;
            throw new InvalidOperationException($"Readback data cannot be represented as {typeof(T).FullName}.");
        }
        return data;
    }

    internal static bool TryDecode<T>(byte[] bytes, out NativeArray<T> data) where T : struct
    {
        data = default;
        int elementSize;
        try { elementSize = Marshal.SizeOf<T>(); }
        catch (ArgumentException) { return false; }
        if (elementSize <= 0 || bytes.Length % elementSize != 0) return false;
        var managed = new T[bytes.Length / elementSize];
        if (bytes.Length != 0)
        {
            GCHandle handle = GCHandle.Alloc(managed, GCHandleType.Pinned);
            try { Marshal.Copy(bytes, 0, handle.AddrOfPinnedObject(), bytes.Length); }
            finally { handle.Free(); }
        }
        data = new NativeArray<T>(managed, Allocator.Temp);
        return true;
    }
}

internal readonly struct ReadbackPayload
{
    internal static readonly ReadbackPayload Error = new(Array.Empty<byte>(), 0, 0, 0, 0, true);
    internal ReadbackPayload(byte[] data, int width, int height, int depth, int layerCount, bool isError = false)
    {
        Data = data; Width = width; Height = height; Depth = depth; LayerCount = layerCount; IsError = isError;
    }
    internal byte[] Data { get; }
    internal int Width { get; }
    internal int Height { get; }
    internal int Depth { get; }
    internal int LayerCount { get; }
    internal bool IsError { get; }
}
