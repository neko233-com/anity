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
        internal Bone[] Bones = Array.Empty<Bone>();
        internal SkinVertex[] SkinVertices = Array.Empty<SkinVertex>();
        internal BoneWeight[] BoneWeights = Array.Empty<BoneWeight>();
        internal BlendShape[] BlendShapes = Array.Empty<BlendShape>();
    }

    internal sealed class Bone
    {
        internal string Name = string.Empty;
        internal int NodeIndex;
        internal float[] Bindpose = Array.Empty<float>();
    }

    internal sealed class BlendShape
    {
        internal string Name = string.Empty;
        internal BlendShapeFrame[] Frames = Array.Empty<BlendShapeFrame>();
    }

    internal sealed class BlendShapeFrame
    {
        internal float Weight;
        internal BlendShapeDelta[] Deltas = Array.Empty<BlendShapeDelta>();
    }

    internal sealed class Clip
    {
        internal string Name = string.Empty;
        internal float Duration;
        internal float FrameRate;
        internal float FirstFrame;
        internal float LastFrame;
        internal Track[] Tracks = Array.Empty<Track>();
        internal BlendShapeTrack[] BlendShapeTracks = Array.Empty<BlendShapeTrack>();
    }

    internal sealed class BlendShapeTrack
    {
        internal int NodeIndex;
        internal string Name = string.Empty;
        internal ScalarKey[] Keys = Array.Empty<ScalarKey>();
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
        internal byte importBlendShapes;
        internal int maxBonesPerVertex;
        internal float minBoneWeight;
        internal byte resampleCurves;
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
        internal int vertexCount, indexCount, subMeshCount, boneCount, skinWeightCount, blendShapeCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BoneInfo
    {
        internal IntPtr name;
        internal int nodeIndex;
        internal float m00, m01, m02, m03, m10, m11, m12, m13;
        internal float m20, m21, m22, m23, m30, m31, m32, m33;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct SkinVertex { internal int weightStart, weightCount; }

    [StructLayout(LayoutKind.Sequential)]
    internal struct BoneWeight { internal int boneIndex; internal float weight; }

    [StructLayout(LayoutKind.Sequential)]
    private struct BlendShapeInfo { internal IntPtr name; internal int frameCount; }

    [StructLayout(LayoutKind.Sequential)]
    private struct BlendShapeFrameInfo { internal float weight; }

    [StructLayout(LayoutKind.Sequential)]
    internal struct BlendShapeDelta
    {
        internal float positionX, positionY, positionZ;
        internal float normalX, normalY, normalZ;
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
        internal int trackCount, blendShapeTrackCount;
        internal float firstFrame, lastFrame;
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

    [StructLayout(LayoutKind.Sequential)]
    private struct BlendShapeTrackInfo
    {
        internal int nodeIndex;
        internal IntPtr name;
        internal int keyCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct ScalarKey { internal float time, value, inTangent, outTangent; }

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
                ValidateCount(native.boneCount, "bone");
                ValidateCount(native.skinWeightCount, "skin weight");
                ValidateCount(native.blendShapeCount, "blend shape");
                var mesh = new Mesh
                {
                    Name = Utf8(native.name),
                    Vertices = new Vertex[native.vertexCount],
                    Indices = new uint[native.indexCount],
                    SubMeshes = new SubMesh[native.subMeshCount],
                    Bones = new Bone[native.boneCount],
                    SkinVertices = native.boneCount == 0 ? Array.Empty<SkinVertex>() : new SkinVertex[native.vertexCount],
                    BoneWeights = new BoneWeight[native.skinWeightCount],
                    BlendShapes = new BlendShape[native.blendShapeCount],
                };
                Require(CopyMeshVertices(handle, meshIndex, mesh.Vertices, mesh.Vertices.Length), "mesh vertices");
                Require(CopyMeshIndices(handle, meshIndex, mesh.Indices, mesh.Indices.Length), "mesh indices");
                for (var subMeshIndex = 0; subMeshIndex < mesh.SubMeshes.Length; subMeshIndex++)
                    Require(GetSubMeshInfo(handle, meshIndex, subMeshIndex, out mesh.SubMeshes[subMeshIndex]), "submesh information");
                for (var boneIndex = 0; boneIndex < mesh.Bones.Length; boneIndex++)
                {
                    Require(GetBoneInfo(handle, meshIndex, boneIndex, out var bone), "bone information");
                    mesh.Bones[boneIndex] = new Bone
                    {
                        Name = Utf8(bone.name), NodeIndex = bone.nodeIndex,
                        Bindpose = new[] { bone.m00, bone.m01, bone.m02, bone.m03, bone.m10, bone.m11, bone.m12, bone.m13,
                            bone.m20, bone.m21, bone.m22, bone.m23, bone.m30, bone.m31, bone.m32, bone.m33 },
                    };
                }
                if (mesh.SkinVertices.Length != 0)
                    Require(CopySkinVertices(handle, meshIndex, mesh.SkinVertices, mesh.SkinVertices.Length), "skin vertices");
                Require(CopyBoneWeights(handle, meshIndex, mesh.BoneWeights, mesh.BoneWeights.Length), "bone weights");
                for (var shapeIndex = 0; shapeIndex < mesh.BlendShapes.Length; shapeIndex++)
                {
                    Require(GetBlendShapeInfo(handle, meshIndex, shapeIndex, out var shapeInfo), "blend shape information");
                    ValidateCount(shapeInfo.frameCount, "blend shape frame");
                    var shape = new BlendShape { Name = Utf8(shapeInfo.name), Frames = new BlendShapeFrame[shapeInfo.frameCount] };
                    for (var frameIndex = 0; frameIndex < shape.Frames.Length; frameIndex++)
                    {
                        Require(GetBlendShapeFrameInfo(handle, meshIndex, shapeIndex, frameIndex, out var frameInfo), "blend shape frame information");
                        var frame = new BlendShapeFrame { Weight = frameInfo.weight, Deltas = new BlendShapeDelta[native.vertexCount] };
                        Require(CopyBlendShapeFrameDeltas(handle, meshIndex, shapeIndex, frameIndex, frame.Deltas, frame.Deltas.Length), "blend shape frame deltas");
                        shape.Frames[frameIndex] = frame;
                    }
                    mesh.BlendShapes[shapeIndex] = shape;
                }
                decoded.Meshes[meshIndex] = mesh;
            }

            for (var clipIndex = 0; clipIndex < decoded.Clips.Length; clipIndex++)
            {
                Require(GetClipInfo(handle, clipIndex, out var native), "clip information");
                ValidateCount(native.trackCount, "animation track");
                ValidateCount(native.blendShapeTrackCount, "blend shape animation track");
                var clip = new Clip
                {
                    Name = Utf8(native.name), Duration = native.duration, FrameRate = native.frameRate,
                    FirstFrame = native.firstFrame, LastFrame = native.lastFrame,
                    Tracks = new Track[native.trackCount], BlendShapeTracks = new BlendShapeTrack[native.blendShapeTrackCount],
                };
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
                for (var trackIndex = 0; trackIndex < clip.BlendShapeTracks.Length; trackIndex++)
                {
                    Require(GetBlendShapeTrackInfo(handle, clipIndex, trackIndex, out var trackInfo), "blend shape animation track information");
                    ValidateCount(trackInfo.keyCount, "blend shape animation key");
                    var track = new BlendShapeTrack
                    {
                        NodeIndex = trackInfo.nodeIndex,
                        Name = Utf8(trackInfo.name),
                        Keys = new ScalarKey[trackInfo.keyCount],
                    };
                    Require(CopyBlendShapeKeys(handle, clipIndex, trackIndex, track.Keys, track.Keys.Length), "blend shape animation keys");
                    clip.BlendShapeTracks[trackIndex] = track;
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
    [DllImport(AnityNative.LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityModel_GetBoneInfo")]
    private static extern AnityNative.Result GetBoneInfo(IntPtr scene, int meshIndex, int boneIndex, out BoneInfo info);
    [DllImport(AnityNative.LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityModel_CopySkinVertices")]
    private static extern AnityNative.Result CopySkinVertices(IntPtr scene, int meshIndex, [Out] SkinVertex[] vertices, int capacity);
    [DllImport(AnityNative.LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityModel_CopyBoneWeights")]
    private static extern AnityNative.Result CopyBoneWeights(IntPtr scene, int meshIndex, [Out] BoneWeight[] weights, int capacity);
    [DllImport(AnityNative.LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityModel_GetBlendShapeInfo")]
    private static extern AnityNative.Result GetBlendShapeInfo(IntPtr scene, int meshIndex, int shapeIndex, out BlendShapeInfo info);
    [DllImport(AnityNative.LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityModel_GetBlendShapeFrameInfo")]
    private static extern AnityNative.Result GetBlendShapeFrameInfo(IntPtr scene, int meshIndex, int shapeIndex, int frameIndex, out BlendShapeFrameInfo info);
    [DllImport(AnityNative.LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityModel_CopyBlendShapeFrameDeltas")]
    private static extern AnityNative.Result CopyBlendShapeFrameDeltas(IntPtr scene, int meshIndex, int shapeIndex, int frameIndex, [Out] BlendShapeDelta[] deltas, int capacity);
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
    [DllImport(AnityNative.LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityModel_GetBlendShapeTrackInfo")]
    private static extern AnityNative.Result GetBlendShapeTrackInfo(IntPtr scene, int clipIndex, int trackIndex, out BlendShapeTrackInfo info);
    [DllImport(AnityNative.LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "AnityModel_CopyBlendShapeKeys")]
    private static extern AnityNative.Result CopyBlendShapeKeys(IntPtr scene, int clipIndex, int trackIndex, [Out] ScalarKey[] keys, int capacity);
}
