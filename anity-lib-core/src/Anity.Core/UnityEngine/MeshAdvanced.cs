using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;

namespace UnityEngine;

public enum IndexFormat
{
    UInt16 = 0,
    UInt32 = 1,
}

public enum VertexAttribute
{
    Position = 0,
    Normal = 1,
    Tangent = 2,
    Color = 3,
    TexCoord0 = 4,
    TexCoord1 = 5,
    TexCoord2 = 6,
    TexCoord3 = 7,
    TexCoord4 = 8,
    TexCoord5 = 9,
    TexCoord6 = 10,
    TexCoord7 = 11,
    BlendWeight = 12,
    BlendIndices = 13,
}

public enum VertexAttributeFormat
{
    Float32 = 0,
    Float16 = 1,
    UNorm8 = 2,
    SNorm8 = 3,
    UNorm16 = 4,
    SNorm16 = 5,
    UInt8 = 6,
    SInt8 = 7,
    UInt16 = 8,
    SInt16 = 9,
    UInt32 = 10,
    SInt32 = 11,
}

public struct VertexAttributeDescriptor
{
    public VertexAttribute attribute;
    public VertexAttributeFormat format;
    public int dimension;
    public int stream;

    public VertexAttributeDescriptor(VertexAttribute attribute, VertexAttributeFormat format, int dimension, int stream = 0)
    {
        this.attribute = attribute;
        this.format = format;
        this.dimension = dimension;
        this.stream = stream;
    }
}

public struct SubmeshDescriptor
{
    public int indexStart;
    public int indexCount;
    public int firstVertex;
    public int vertexCount;
    public Bounds bounds;
    public MeshTopology topology;

    public SubmeshDescriptor(int indexStart, int indexCount)
    {
        this.indexStart = indexStart;
        this.indexCount = indexCount;
        firstVertex = 0;
        vertexCount = 0;
        bounds = default;
        topology = MeshTopology.Triangles;
    }

    public SubmeshDescriptor(int indexStart, int indexCount, MeshTopology topology)
    {
        this.indexStart = indexStart;
        this.indexCount = indexCount;
        firstVertex = 0;
        vertexCount = 0;
        bounds = default;
        this.topology = topology;
    }
}

public struct MeshData : IDisposable
{
    private IndexFormat _indexFormat;
    private VertexAttributeDescriptor[] _vertexAttributes;
    private int _vertexCount;
    private int _indexCount;
    private SubmeshDescriptor[] _subMeshes;

    public IndexFormat indexFormat
    {
        get => _indexFormat;
        set => _indexFormat = value;
    }

    public int vertexCount => _vertexCount;
    public int indexCount => _indexCount;
    public int subMeshCount => _subMeshes?.Length ?? 0;

    public VertexAttributeDescriptor[] GetVertexAttributes() => _vertexAttributes ?? Array.Empty<VertexAttributeDescriptor>();
    public void SetVertexAttributes(VertexAttributeDescriptor[] attributes) => _vertexAttributes = attributes;

    public void GetVertices<T>(NativeArray<T> vertices) where T : struct { }
    public void SetVertices<T>(NativeArray<T> vertices) where T : struct { }
    public void GetIndices<T>(NativeArray<T> indices, int submesh, bool applyBaseVertex = true) where T : struct { }
    public void SetIndices<T>(NativeArray<T> indices, int submesh, MeshTopology topology, bool renderNode = false) where T : struct { }

    public SubmeshDescriptor GetSubMesh(int index) => _subMeshes != null && index >= 0 && index < _subMeshes.Length ? _subMeshes[index] : default;
    public void SetSubMesh(int index, SubmeshDescriptor subMesh) { if (_subMeshes != null && index >= 0 && index < _subMeshes.Length) _subMeshes[index] = subMesh; }

    public void Dispose() { }
}

public struct MeshDataArray : IDisposable
{
    private MeshData[] _data;

    public int length => _data?.Length ?? 0;

    public MeshData this[int index]
    {
        get => _data != null && index >= 0 && index < _data.Length ? _data[index] : default;
        set { if (_data != null && index >= 0 && index < _data.Length) _data[index] = value; }
    }

    public MeshDataArray(int length)
    {
        _data = new MeshData[length];
    }

    public void Dispose() { }

    public static MeshDataArray Allocate(int length) => new MeshDataArray(length);
}

public partial class Mesh
{
    public int indexBufferTarget { get; set; }

    public void SetIndexBufferParams(int indexCount, IndexFormat format) { indexFormat = format; }
    public void SetVertexBufferParams(int vertexCount, params VertexAttributeDescriptor[] attributes) { }

    public NativeArray<int> GetIndexData() => new NativeArray<int>(0, Allocator.Persistent);
    public NativeArray<Vector3> GetVertexData() => new NativeArray<Vector3>(0, Allocator.Persistent);

    public static MeshDataArray AllocateWritableMeshData(int meshCount) => new MeshDataArray(meshCount);
    public static void ApplyAndDisposeWritableMeshData(MeshDataArray data, Mesh[] meshes) { }
    public static void ApplyAndDisposeWritableMeshData(MeshDataArray data, Mesh mesh, int index = 0) { }

    public static MeshDataArray AcquireReadOnlyMeshData(Mesh mesh) => new MeshDataArray(1);
    public static MeshDataArray AcquireReadOnlyMeshData(Mesh[] meshes) => new MeshDataArray(meshes?.Length ?? 0);

    public void SetSubMeshes(SubmeshDescriptor[] subMeshes) { }
    public void SetSubMeshes(SubmeshDescriptor[] subMeshes, bool _ = false) { }

    public void RecalculateUVDistribution(int channel) { }
    public void RecalculateUVDistribution(params int[] channels) { }

    public int GetIndexCount(int submesh) => 0;
    public int GetIndexStart(int submesh) => 0;
    public int GetBaseVertex(int submesh) => 0;
    public new SubmeshDescriptor GetSubMesh(int submesh) => default;

    public void SetIndexBufferData<T>(NativeArray<T> data, int indexStart, int indexCount, int submesh = 0) where T : struct { }
    public void SetVertexBufferData<T>(NativeArray<T> data, int start, int count, int stream = 0) where T : struct { }

    public bool HasVertexAttribute(VertexAttribute attr) => false;
    public int GetVertexAttributeDimension(VertexAttribute attr) => 0;
    public VertexAttributeFormat GetVertexAttributeFormat(VertexAttribute attr) => VertexAttributeFormat.Float32;
    public VertexAttribute[] GetVertexAttributes() => Array.Empty<VertexAttribute>();
    public VertexAttributeDescriptor[] GetVertexAttributesUnsafe() => Array.Empty<VertexAttributeDescriptor>();
}

public static class MeshUtility
{
    public static void Optimize(Mesh mesh) { mesh?.Optimize(); }
    public static void CreateMeshFromVertices(Vector3[] vertices, int[] indices, out Mesh mesh)
    {
        mesh = new Mesh { vertices = vertices, triangles = indices };
    }
    public static void CreateMeshFromVertices(CombineInstance[] combine, out Mesh mesh)
    {
        mesh = new Mesh { };
        mesh.CombineMeshes(combine);
    }
}
