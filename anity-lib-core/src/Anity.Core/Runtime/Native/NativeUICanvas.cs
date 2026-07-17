using System;

namespace Anity.Core.Runtime.Native;

/// <summary>Owns persistent native Canvas render commands and their batched upload buffers.</summary>
public sealed class NativeUICanvas : IDisposable
{
    private IntPtr _handle;

    private NativeUICanvas(IntPtr handle) => _handle = handle;

    public bool IsValid => _handle != IntPtr.Zero;
    public IntPtr Handle => _handle;

    public static NativeUICanvas? TryCreate()
    {
        try
        {
            return AnityNative.UICanvas_Create(out IntPtr handle) == AnityNative.Result.Ok &&
                handle != IntPtr.Zero ? new NativeUICanvas(handle) : null;
        }
        catch (DllNotFoundException)
        {
            if (NativeRequired) throw;
            return null;
        }
        catch (EntryPointNotFoundException)
        {
            if (NativeRequired) throw;
            return null;
        }
    }

    public bool BeginFrame(ulong frameId)
        => IsValid && AnityNative.UICanvas_BeginFrame(_handle, frameId) == AnityNative.Result.Ok;

    public bool Clear()
        => IsValid && AnityNative.UICanvas_Clear(_handle) == AnityNative.Result.Ok;

    public bool Upsert(
        AnityNative.UIRenderCommandDesc desc,
        AnityNative.UIPackedVertex[] vertices,
        uint[] indices)
    {
        if (!IsValid || vertices is null || indices is null) return false;
        return AnityNative.UICanvas_UpsertCommand(
            _handle, ref desc, vertices, vertices.Length, indices, indices.Length) == AnityNative.Result.Ok;
    }

    public bool Remove(ulong rendererId)
        => IsValid && AnityNative.UICanvas_RemoveCommand(_handle, rendererId) == AnityNative.Result.Ok;

    public bool BuildBatches()
        => IsValid && AnityNative.UICanvas_BuildBatches(_handle) == AnityNative.Result.Ok;

    public AnityNative.UICanvasStats GetStats()
    {
        if (!IsValid || AnityNative.UICanvas_GetStats(_handle, out var stats) != AnityNative.Result.Ok)
            return default;
        return stats;
    }

    public AnityNative.UIBatchInfo GetBatchInfo(int batchIndex)
    {
        if (!IsValid || AnityNative.UICanvas_GetBatchInfo(_handle, batchIndex, out var info) != AnityNative.Result.Ok)
            throw new ArgumentOutOfRangeException(nameof(batchIndex));
        return info;
    }

    public AnityNative.UIPackedVertex[] GetBatchVertices(int batchIndex)
    {
        AnityNative.UIBatchInfo info = GetBatchInfo(batchIndex);
        var result = new AnityNative.UIPackedVertex[info.vertexCount];
        if (AnityNative.UICanvas_CopyBatchVertices(
                _handle, batchIndex, result, result.Length, out int written) != AnityNative.Result.Ok ||
            written != result.Length)
            throw new InvalidOperationException("Native Canvas vertex buffer copy failed.");
        return result;
    }

    public uint[] GetBatchIndices(int batchIndex)
    {
        AnityNative.UIBatchInfo info = GetBatchInfo(batchIndex);
        var result = new uint[info.indexCount];
        if (AnityNative.UICanvas_CopyBatchIndices(
                _handle, batchIndex, result, result.Length, out int written) != AnityNative.Result.Ok ||
            written != result.Length)
            throw new InvalidOperationException("Native Canvas index buffer copy failed.");
        return result;
    }

    public void Dispose()
    {
        IntPtr handle = _handle;
        _handle = IntPtr.Zero;
        if (handle != IntPtr.Zero)
            AnityNative.UICanvas_Destroy(handle);
        GC.SuppressFinalize(this);
    }

    ~NativeUICanvas()
    {
        try { Dispose(); }
        catch { }
    }

    private static bool NativeRequired
        => string.Equals(Environment.GetEnvironmentVariable("ANITY_REQUIRE_NATIVE"), "1", StringComparison.Ordinal);
}
