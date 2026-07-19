using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Anity.Core.Runtime.Native;

internal static class NativeModelDecoder
{
    internal sealed class Scene
    {
        internal float FileScale;
        internal float FrameRate;
        internal Node[] Nodes = Array.Empty<Node>();
        internal Mesh[] Meshes = Array.Empty<Mesh>();
        internal Clip[] Clips = Array.Empty<Clip>();
    }

    internal sealed class Node
    {
        internal string Name = string.Empty;
        internal int ParentIndex;
        internal int MeshIndex;
        internal float PositionX, PositionY, PositionZ;
        internal float RotationX, RotationY, RotationZ, RotationW;
        internal float ScaleX, ScaleY, ScaleZ;
    }

    internal sealed class Mesh
    {
        internal string Name = string.Empty;
        internal Vertex[] Vertices = Array.Empty<Vertex>();
        internal uint[] Indices = Array.Empty<uint>();
        internal SubMesh[] SubMeshes = Array.Empty<SubMesh>();
    }

    internal sealed class Clip
    {
        internal string Name = string.Empty;
        internal float Duration;
        internal float FrameRate;
        internal Track[] Tracks = Array.Empty<Track>();
    }

    internal sealed class Track
    {
        internal int NodeIndex;
        internal VectorKey[] PositionKeys = Array.Empty<VectorKey>();
        internal QuaternionKey[] RotationKeys = Array.Empty<QuaternionKey>();
        internal VectorKey[] ScaleKeys = Array.Empty<VectorKey>();
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct Options
    {
        internal float globalScale;
        internal byte useFileUnits;
        internal byte importAnimation;
        internal byte generateMissingNormals;
        internal byte reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SceneInfo
    {
        internal int nodeCount, meshCount, clipCount;
        internal float fileScale, frameRate;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NodeInfo
    {
        internal IntPtr name;
        internal int parentIndex, meshIndex;
        internal float positionX, positionY, positionZ;
        internal float rotationX, rotationY, rotationZ, rotationW;
        internal float scaleX, scaleY, scaleZ;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct Vertex
    {
        internal float positionX, positionY, positionZ;
        internal float normalX, normalY, normalZ;
        internal float tangentX, tangentY, tangentZ, tangentW;
        internal float uvX, uvY;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MeshInfo
    {
        internal IntPtr name;
        internal int vertexCount, indexCount, subMeshCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct SubMesh
    {
        internal int indexStart, indexCount, materialIndex;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ClipInfo
    {
        internal IntPtr name;
        internal float duration, frameRate;
        internal int trackCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct TrackInfo
    {
        internal int nodeIndex, positionKeyCount, rotationKeyCount, scaleKeyCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct VectorKey
    {
        internal float time, x, y, z;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct QuaternionKey
    {
        internal float time, x, y, z, w;
    }

    internal static bool TryLoad(string path, Options options, out Scene scene, out string error)
    {
        scene = null!;
        error = string.Empty;
        var message = new StringBuilder(2048);
        IntPtr handle = IntPtr.Zero;
        try
        {
            var result = LoadFile(path, ref options, out handle, message, message.Capacity);
            if (result != AnityNative.Result.Ok || handle == IntPtr.Zero)
            {
                error = message.Length == 0 ? $"Native model decoder failed with {result}." : message.ToString();
                return false;
            }

            Require(GetSceneInfo(handle, out var info), "scene information");
            ValidateCount(info.nodeCount, "node");
            ValidateCount(info.meshCount, "mesh");
            ValidateCount(info.clipCount, "clip");
            var decoded = new Scene
            {
                FileScale = info.fileScale,
                FrameRate = info.frameRate,
                Nodes = new Node[info.nodeCount],
                Meshes = new Mesh[info.meshCount],
                Clips = new Clip[info.clipCount],
            };

            for (var nodeIndex = 0; nodeIndex < decoded.Nodes.Length; nodeIndex++)
            {
                Require(GetNodeInfo(handle, nodeIndex, out var native), "node information");
                decoded.Nodes[nodeIndex] = new Node
                {
                    Name = Utf8(native.name), ParentIndex = native.parentIndex, MeshIndex = native.meshIndex,
                    PositionX = native.positionX, PositionY = native.positionY, PositionZ = native.positionZ,
                    RotationX = native.rotationX, RotationY = native.rotationY, RotationZ = native.rotationZ, RotationW = native.rotationW,
                    ScaleX = native.scaleX, ScaleY = native.scaleY, ScaleZ = native.scaleZ,
                };
            }

            for (var meshIndex = 0; meshIndex < decoded.Meshes.Length; meshIndex++)
            {
                Require(GetMeshInfo(handle, meshIndex, out var native), "mesh information");
                ValidateCount(native.vertexCount, "vertex");
                ValidateCount(native.indexCount, "index");
                ValidateCount(native.subMeshCount, "submesh");
                var mesh = new Mesh
                {
                    Name = Utf8(native.name),
                    Vertices = new Vertex[native.vertexCount],
                    Indices = new uint[native.indexCount],
                    SubMeshes = new SubMesh[native.subMeshCount],
                };
                Require(CopyMeshVertices(handle, meshIndex, mesh.Vertices, mesh.Vertices.Length), "mesh vertices");
                Require(CopyMeshIndices(handle, meshIndex, mesh.Indices, mesh.Indices.Length), "mesh indices");
                for (var subMeshIndex = 0; subMeshIndex < mesh.SubMeshes.Length; subMeshIndex++)
                    Require(GetSubMeshInfo(handle, meshIndex, subMeshIndex, out mesh.SubMeshes[subMeshIndex]), "submesh information");
                decoded.Meshes[meshIndex] = mesh;
            }

            for (var clipIndex = 0; clipIndex < decoded.Clips.Length; clipIndex++)
            {
                Require(GetClipInfo(handle, clipIndex, out var native), "clip information");
                ValidateCount(native.trackCount, "animation track");
                var clip = new Clip { Name = Utf8(native.name), Duration = native.duration, FrameRate = native.frameRate, Tracks = new Track[native.trackCount] };
                for (var trackIndex = 0; trackIndex < clip.Tracks.Length; trackIndex++)
                {
                    Require(GetTrackInfo(handle, clipIndex, trackIndex, out var trackInfo), "animation track information");
                    ValidateCount(trackInfo.positionKeyCount, "position key");
                    ValidateCount(trackInfo.rotationKeyCount, "rotation key");
                    ValidateCount(trackInfo.scaleKeyCount, "scale key");
                    var track = new Track
                    {
                        NodeIndex = trackInfo.nodeIndex,
                        PositionKeys = new VectorKey[trackInfo.positionKeyCount],
                        RotationKeys = new QuaternionKey[trackInfo.rotationKeyCount],
                        ScaleKeys = new VectorKey[trackInfo.scaleKeyCount],
                    };
                    Require(CopyPositionKeys(handle, clipIndex, trackIndex, track.PositionKeys, track.PositionKeys.Length), "position keys");
                    Require(CopyRotationKeys(handle, clipIndex, trackIndex, track.RotationKeys, track.RotationKeys.Length), "rotation keys");
                    Require(CopyScaleKeys(handle, clipIndex, trackIndex, track.ScaleKeys, track.ScaleKeys.Length), "scale keys");
                    clip.Tracks[trackIndex] = track;
                }
                decoded.Clips[clipIndex] = clip;
            }

            scene = decoded;
            return true;
        }
        catch (Exception exception) when (exception is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
        {
            error = "anity-native model importer is unavailable: " + exception.Message;
            return false;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            return false;
        }
        finally
        {
            if (handle != IntPtr.Zero) FreeScene(handle);
        }
    }

    private static string Utf8(IntPtr pointer) => pointer == IntPtr.Zero ? string.Empty : Marshal.PtrToStringUTF8(pointer) ?? string.Empty;
    private static void Require(AnityNative.Result result, string operation)
    {
        if (result != AnityNative.Result.Ok) throw new InvalidOperationException($"Native model {operation} failed with {result}.");
    }
    private static void ValidateCount(int count, string kind)
    {
        if (count < 0 || count > 100_000_000) throw new InvalidOperationException($"Native model {kind} count is invalid: {count}.");
    }

    [DllImport(AnityNative.LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityModel_LoadFile")]
    private static extern AnityNative.Result LoadFile([MarshalAs(UnmanagedType.LPUTF8Str)] string path, ref Options options, out IntPtr scene, StringBuilder error, int errorCapacity);
    [DllImport(AnityNative.LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityModel_FreeScene")]
    private static extern void FreeScene(IntPtr scene);
    [DllImport(AnityNative.LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityModel_GetSceneInfo")]
    private static extern AnityNative.Result GetSceneInfo(IntPtr scene, out SceneInfo info);
    [DllImport(AnityNative.LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityModel_GetNodeInfo")]
    private static extern AnityNative.Result GetNodeInfo(IntPtr scene, int nodeIndex, out NodeInfo info);
    [DllImport(AnityNative.LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityModel_GetMeshInfo")]
    private static extern AnityNative.Result GetMeshInfo(IntPtr scene, int meshIndex, out MeshInfo info);
    [DllImport(AnityNative.LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityModel_CopyMeshVertices")]
    private static extern AnityNative.Result CopyMeshVertices(IntPtr scene, int meshIndex, [Out] Vertex[] vertices, int capacity);
    [DllImport(AnityNative.LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityModel_CopyMeshIndices")]
    private static extern AnityNative.Result CopyMeshIndices(IntPtr scene, int meshIndex, [Out] uint[] indices, int capacity);
    [DllImport(AnityNative.LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityModel_GetSubMeshInfo")]
    private static extern AnityNative.Result GetSubMeshInfo(IntPtr scene, int meshIndex, int subMeshIndex, out SubMesh info);
    [DllImport(AnityNative.LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityModel_GetClipInfo")]
    private static extern AnityNative.Result GetClipInfo(IntPtr scene, int clipIndex, out ClipInfo info);
    [DllImport(AnityNative.LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityModel_GetTrackInfo")]
    private static extern AnityNative.Result GetTrackInfo(IntPtr scene, int clipIndex, int trackIndex, out TrackInfo info);
    [DllImport(AnityNative.LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityModel_CopyPositionKeys")]
    private static extern AnityNative.Result CopyPositionKeys(IntPtr scene, int clipIndex, int trackIndex, [Out] VectorKey[] keys, int capacity);
    [DllImport(AnityNative.LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityModel_CopyRotationKeys")]
    private static extern AnityNative.Result CopyRotationKeys(IntPtr scene, int clipIndex, int trackIndex, [Out] QuaternionKey[] keys, int capacity);
    [DllImport(AnityNative.LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityModel_CopyScaleKeys")]
    private static extern AnityNative.Result CopyScaleKeys(IntPtr scene, int clipIndex, int trackIndex, [Out] VectorKey[] keys, int capacity);
}
