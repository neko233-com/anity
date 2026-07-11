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

    public SubmeshDescriptor(int indexStart, int indexCount, int firstVertex, int vertexCount, Bounds bounds, MeshTopology topology)
    {
        this.indexStart = indexStart;
        this.indexCount = indexCount;
        this.firstVertex = firstVertex;
        this.vertexCount = vertexCount;
        this.bounds = bounds;
        this.topology = topology;
    }
}

public struct MeshData : IDisposable
{
    private IndexFormat _indexFormat;
    private List<VertexAttributeDescriptor> _vertexAttributes;
    private int _vertexCount;
    private int _indexCount;
    private List<SubmeshDescriptor> _subMeshes;
    private bool _disposed;
    private NativeArray<byte> _vertexBuffer;
    private NativeArray<byte> _indexBuffer;

    public IndexFormat indexFormat
    {
        get => _indexFormat;
        set => _indexFormat = value;
    }

    public int vertexCount => _vertexCount;
    public int indexCount => _indexCount;
    public int subMeshCount => _subMeshes?.Count ?? 0;

    public VertexAttributeDescriptor[] GetVertexAttributes()
    {
        if (_vertexAttributes == null) return Array.Empty<VertexAttributeDescriptor>();
        return _vertexAttributes.ToArray();
    }

    public void SetVertexAttributes(VertexAttributeDescriptor[] attributes)
    {
        _vertexAttributes = attributes != null ? new List<VertexAttributeDescriptor>(attributes) : new List<VertexAttributeDescriptor>();
    }

    public void SetVertexBufferParams(int vertexCount, params VertexAttributeDescriptor[] attributes)
    {
        _vertexCount = vertexCount;
        _vertexAttributes = attributes != null ? new List<VertexAttributeDescriptor>(attributes) : new List<VertexAttributeDescriptor>();
        int vertexStride = 0;
        for (int i = 0; i < _vertexAttributes.Count; i++)
            vertexStride += _vertexAttributes[i].dimension * GetFormatSize(_vertexAttributes[i].format);
        if (vertexCount > 0 && vertexStride > 0)
        {
            if (_vertexBuffer.IsCreated) _vertexBuffer.Dispose();
            _vertexBuffer = new NativeArray<byte>(vertexCount * vertexStride, Allocator.Persistent);
        }
    }

    public void SetIndexBufferParams(int indexCount, IndexFormat format)
    {
        _indexCount = indexCount;
        _indexFormat = format;
        int indexSize = format == IndexFormat.UInt32 ? 4 : 2;
        if (indexCount > 0)
        {
            if (_indexBuffer.IsCreated) _indexBuffer.Dispose();
            _indexBuffer = new NativeArray<byte>(indexCount * indexSize, Allocator.Persistent);
        }
    }

    private static int GetFormatSize(VertexAttributeFormat format)
    {
        return format switch
        {
            VertexAttributeFormat.Float32 or VertexAttributeFormat.UInt32 or VertexAttributeFormat.SInt32 => 4,
            VertexAttributeFormat.Float16 or VertexAttributeFormat.UNorm16 or VertexAttributeFormat.SNorm16 or VertexAttributeFormat.UInt16 or VertexAttributeFormat.SInt16 => 2,
            VertexAttributeFormat.UNorm8 or VertexAttributeFormat.SNorm8 or VertexAttributeFormat.UInt8 or VertexAttributeFormat.SInt8 => 1,
            _ => 4,
        };
    }

    public void GetVertices<T>(NativeArray<T> vertices) where T : struct { }
    public void SetVertices<T>(NativeArray<T> vertices) where T : struct { }
    public void GetIndices<T>(NativeArray<T> indices, int submesh, bool applyBaseVertex = true) where T : struct { }
    public void SetIndices<T>(NativeArray<T> indices, int submesh, MeshTopology topology, bool renderNode = false) where T : struct { }

    public SubmeshDescriptor GetSubMesh(int index)
    {
        if (_subMeshes != null && index >= 0 && index < _subMeshes.Count)
            return _subMeshes[index];
        return default;
    }

    public void SetSubMesh(int index, SubmeshDescriptor subMesh)
    {
        if (_subMeshes == null) _subMeshes = new List<SubmeshDescriptor>();
        while (_subMeshes.Count <= index) _subMeshes.Add(default);
        _subMeshes[index] = subMesh;
    }

    public void SetSubMeshes(params SubmeshDescriptor[] subMeshes)
    {
        _subMeshes = subMeshes != null ? new List<SubmeshDescriptor>(subMeshes) : new List<SubmeshDescriptor>();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_vertexBuffer.IsCreated) _vertexBuffer.Dispose();
        if (_indexBuffer.IsCreated) _indexBuffer.Dispose();
        _vertexAttributes?.Clear();
        _subMeshes?.Clear();
    }
}

public struct MeshDataArray : IDisposable
{
    internal MeshData[] _data;
    private bool _disposed;

    public int length => _data?.Length ?? 0;

    public MeshData this[int index]
    {
        get => _data != null && index >= 0 && index < _data.Length ? _data[index] : default;
        set { if (_data != null && index >= 0 && index < _data.Length) _data[index] = value; }
    }

    public MeshDataArray(int length)
    {
        _data = new MeshData[length];
        _disposed = false;
        for (int i = 0; i < length; i++)
            _data[i] = new MeshData();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_data != null)
        {
            for (int i = 0; i < _data.Length; i++)
                _data[i].Dispose();
        }
        _data = null;
    }

    public static MeshDataArray Allocate(int length) => new MeshDataArray(length);
}

public partial class Mesh
{
    private List<VertexAttributeDescriptor> _vertexAttributeDescriptors = new();
    private List<SubmeshDescriptor> _submeshDescriptors = new();
    private int _meshVertexCount;
    private int _meshIndexCount;
    private float[] _uvDistribution;

    public int indexBufferTarget { get; set; }

    public void SetIndexBufferParams(int indexCount, IndexFormat format)
    {
        indexFormat = format;
        _meshIndexCount = indexCount;
    }

    public void SetVertexBufferParams(int vertexCount, params VertexAttributeDescriptor[] attributes)
    {
        _meshVertexCount = vertexCount;
        _vertexAttributeDescriptors = attributes != null ? new List<VertexAttributeDescriptor>(attributes) : new List<VertexAttributeDescriptor>();
    }

    public NativeArray<int> GetIndexData() => new NativeArray<int>(0, Allocator.Persistent);
    public NativeArray<Vector3> GetVertexData() => new NativeArray<Vector3>(0, Allocator.Persistent);

    public static MeshDataArray AllocateWritableMeshData(int meshCount) => new MeshDataArray(meshCount);

    public static void ApplyAndDisposeWritableMeshData(MeshDataArray data, Mesh[] meshes)
    {
        if (data._data == null || meshes == null) { data.Dispose(); return; }
        int count = Math.Min(data._data.Length, meshes.Length);
        for (int i = 0; i < count; i++)
        {
            if (meshes[i] == null) continue;
            var md = data._data[i];
            meshes[i]._vertexAttributeDescriptors = new List<VertexAttributeDescriptor>(md.GetVertexAttributes());
            int smc = md.subMeshCount;
            meshes[i]._submeshDescriptors = new List<SubmeshDescriptor>();
            for (int s = 0; s < smc; s++)
                meshes[i]._submeshDescriptors.Add(md.GetSubMesh(s));
            meshes[i]._meshVertexCount = md.vertexCount;
            meshes[i]._meshIndexCount = md.indexCount;
            meshes[i].indexFormat = md.indexFormat;
        }
        data.Dispose();
    }

    public static void ApplyAndDisposeWritableMeshData(MeshDataArray data, Mesh mesh, int index = 0)
    {
        if (mesh == null || data._data == null) { data.Dispose(); return; }
        if (index >= 0 && index < data._data.Length)
        {
            var md = data._data[index];
            mesh._vertexAttributeDescriptors = new List<VertexAttributeDescriptor>(md.GetVertexAttributes());
            int smc = md.subMeshCount;
            mesh._submeshDescriptors = new List<SubmeshDescriptor>();
            for (int s = 0; s < smc; s++)
                mesh._submeshDescriptors.Add(md.GetSubMesh(s));
            mesh._meshVertexCount = md.vertexCount;
            mesh._meshIndexCount = md.indexCount;
            mesh.indexFormat = md.indexFormat;
        }
        data.Dispose();
    }

    public static MeshDataArray AcquireReadOnlyMeshData(Mesh mesh)
    {
        var arr = new MeshDataArray(1);
        if (mesh != null)
        {
            arr[0].SetVertexBufferParams(mesh.vertexCount, mesh.GetVertexAttributesUnsafe());
        }
        return arr;
    }

    public static MeshDataArray AcquireReadOnlyMeshData(Mesh[] meshes)
    {
        int count = meshes?.Length ?? 0;
        var arr = new MeshDataArray(count);
        if (meshes != null)
        {
            for (int i = 0; i < count; i++)
            {
                if (meshes[i] != null)
                    arr[i].SetVertexBufferParams(meshes[i].vertexCount, meshes[i].GetVertexAttributesUnsafe());
            }
        }
        return arr;
    }

    public void SetSubMeshes(SubmeshDescriptor[] subMeshes)
    {
        _submeshDescriptors = subMeshes != null ? new List<SubmeshDescriptor>(subMeshes) : new List<SubmeshDescriptor>();
        if (_subMeshIndices.Count < _submeshDescriptors.Count)
        {
            while (_subMeshIndices.Count < _submeshDescriptors.Count)
                _subMeshIndices.Add(Array.Empty<int>());
        }
    }

    public void SetSubMeshes(SubmeshDescriptor[] subMeshes, bool markDynamic)
    {
        SetSubMeshes(subMeshes);
        _ = markDynamic;
    }

    public void RecalculateUVDistribution(int channel)
    {
        RecalculateUVDistribution(new[] { channel });
    }

    public void RecalculateUVDistribution(params int[] channels)
    {
        if (channels == null || channels.Length == 0) return;
        _uvDistribution = new float[channels.Length];
        var uvs = GetUVs(0);
        if (uvs.Length == 0 || _vertices.Count == 0) return;
        for (int c = 0; c < channels.Length; c++)
        {
            float total = 0f;
            var channelUvs = GetUVs(channels[c]);
            if (channelUvs.Length > 1)
            {
                for (int i = 1; i < channelUvs.Length; i++)
                {
                    total += Vector2.Distance(channelUvs[i - 1], channelUvs[i]);
                }
            }
            _uvDistribution[c] = total / Math.Max(1, channelUvs.Length);
        }
    }

    public int GetIndexCount(int submesh)
    {
        if (submesh >= 0 && submesh < _submeshDescriptors.Count)
            return _submeshDescriptors[submesh].indexCount;
        if (submesh >= 0 && submesh < _subMeshIndices.Count)
            return _subMeshIndices[submesh].Length;
        return 0;
    }

    public int GetIndexStart(int submesh)
    {
        if (submesh >= 0 && submesh < _submeshDescriptors.Count)
            return _submeshDescriptors[submesh].indexStart;
        return 0;
    }

    public int GetBaseVertex(int submesh)
    {
        if (submesh >= 0 && submesh < _submeshDescriptors.Count)
            return _submeshDescriptors[submesh].firstVertex;
        return 0;
    }

    public SubmeshDescriptor GetSubMesh(int submesh)
    {
        if (submesh >= 0 && submesh < _submeshDescriptors.Count)
            return _submeshDescriptors[submesh];
        return default;
    }

    public void SetIndexBufferData<T>(NativeArray<T> data, int indexStart, int indexCount, int submesh = 0) where T : struct { }
    public void SetVertexBufferData<T>(NativeArray<T> data, int start, int count, int stream = 0) where T : struct { }

    public bool HasVertexAttribute(VertexAttribute attr) => _vertexAttributeDescriptors.Exists(d => d.attribute == attr);

    public int GetVertexAttributeDimension(VertexAttribute attr)
    {
        var d = _vertexAttributeDescriptors.Find(x => x.attribute == attr);
        return d.dimension;
    }

    public VertexAttributeFormat GetVertexAttributeFormat(VertexAttribute attr)
    {
        var d = _vertexAttributeDescriptors.Find(x => x.attribute == attr);
        return d.format;
    }

    public VertexAttribute[] GetVertexAttributes()
    {
        var attrs = new VertexAttribute[_vertexAttributeDescriptors.Count];
        for (int i = 0; i < _vertexAttributeDescriptors.Count; i++)
            attrs[i] = _vertexAttributeDescriptors[i].attribute;
        return attrs;
    }

    public VertexAttributeDescriptor[] GetVertexAttributesUnsafe()
    {
        return _vertexAttributeDescriptors.ToArray();
    }
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
