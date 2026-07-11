using System;
using System.Collections.Generic;
using System.Linq;
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
    private List<Vector3> _positions;
    private List<Vector3> _normals;
    private List<Vector4> _tangents;
    private List<Color> _colors;
    private List<Vector2>[] _uvChannels;
    private List<int> _indices;

    public IndexFormat indexFormat
    {
        get => _indexFormat;
        set => _indexFormat = value;
    }

    public int vertexCount => _vertexCount;
    public int indexCount => _indexCount;
    public int subMeshCount => _subMeshes?.Count ?? 0;
    public int vertexBufferCount => _vertexAttributes != null ? _vertexAttributes.Select(a => a.stream).Distinct().Count() : 1;

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
        EnsureLists(vertexCount);
    }

    private void EnsureLists(int count)
    {
        _positions = new List<Vector3>(count);
        _normals = new List<Vector3>(count);
        _tangents = new List<Vector4>(count);
        _colors = new List<Color>(count);
        _uvChannels = new List<Vector2>[8];
        for (int ch = 0; ch < 8; ch++)
        {
            _uvChannels[ch] = new List<Vector2>(count);
        }
        for (int i = 0; i < count; i++)
        {
            _positions.Add(Vector3.zero);
            _normals.Add(Vector3.up);
            _tangents.Add(new Vector4(1, 0, 0, 1));
            _colors.Add(Color.white);
            for (int ch = 0; ch < 8; ch++)
            {
                _uvChannels[ch].Add(Vector2.zero);
            }
        }
    }

    public void SetIndexBufferParams(int indexCount, IndexFormat format)
    {
        _indexCount = indexCount;
        _indexFormat = format;
        _indices = new List<int>(indexCount);
        for (int i = 0; i < indexCount; i++)
            _indices.Add(0);
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

    public NativeArray<T> GetVertexData<T>(int stream = 0) where T : struct
    {
        _ = stream;
        if (typeof(T) == typeof(Vector3))
            return new NativeArray<T>(0, Allocator.Persistent);
        return new NativeArray<T>(0, Allocator.Persistent);
    }

    public NativeArray<T> GetIndexData<T>() where T : struct
    {
        if (_indices == null) return new NativeArray<T>(0, Allocator.Persistent);
        var arr = new NativeArray<T>(_indices.Count, Allocator.Persistent);
        return arr;
    }

    public NativeArray<T> GetVertices<T>() where T : struct
    {
        if (_positions == null) return new NativeArray<T>(0, Allocator.Persistent);
        var result = new NativeArray<T>(_positions.Count, Allocator.Persistent);
        if (typeof(T) == typeof(Vector3))
        {
            for (int i = 0; i < _positions.Count; i++)
            {
                result[i] = (T)(object)_positions[i];
            }
        }
        return result;
    }

    public NativeArray<T> GetNormals<T>() where T : struct
    {
        if (_normals == null) return new NativeArray<T>(0, Allocator.Persistent);
        var result = new NativeArray<T>(_normals.Count, Allocator.Persistent);
        if (typeof(T) == typeof(Vector3))
        {
            for (int i = 0; i < _normals.Count; i++)
            {
                result[i] = (T)(object)_normals[i];
            }
        }
        return result;
    }

    public NativeArray<T> GetTangents<T>() where T : struct
    {
        if (_tangents == null) return new NativeArray<T>(0, Allocator.Persistent);
        var result = new NativeArray<T>(_tangents.Count, Allocator.Persistent);
        if (typeof(T) == typeof(Vector4))
        {
            for (int i = 0; i < _tangents.Count; i++)
            {
                result[i] = (T)(object)_tangents[i];
            }
        }
        return result;
    }

    public NativeArray<T> GetColors<T>() where T : struct
    {
        if (_colors == null) return new NativeArray<T>(0, Allocator.Persistent);
        var result = new NativeArray<T>(_colors.Count, Allocator.Persistent);
        if (typeof(T) == typeof(Color))
        {
            for (int i = 0; i < _colors.Count; i++)
            {
                result[i] = (T)(object)_colors[i];
            }
        }
        return result;
    }

    public NativeArray<T> GetUVs<T>(int channel) where T : struct
    {
        if (_uvChannels == null || channel < 0 || channel >= 8) return new NativeArray<T>(0, Allocator.Persistent);
        var uvList = _uvChannels[channel];
        if (uvList == null) return new NativeArray<T>(0, Allocator.Persistent);
        var result = new NativeArray<T>(uvList.Count, Allocator.Persistent);
        if (typeof(T) == typeof(Vector2))
        {
            for (int i = 0; i < uvList.Count; i++)
            {
                result[i] = (T)(object)uvList[i];
            }
        }
        return result;
    }

    public NativeArray<T> GetIndices<T>(int submesh) where T : struct
    {
        if (_indices == null) return new NativeArray<T>(0, Allocator.Persistent);
        var subMeshDesc = GetSubMesh(submesh);
        int start = subMeshDesc.indexStart;
        int count = subMeshDesc.indexCount > 0 ? subMeshDesc.indexCount : _indices.Count;
        if (start + count > _indices.Count) count = _indices.Count - start;
        var result = new NativeArray<T>(Math.Max(0, count), Allocator.Persistent);
        if (typeof(T) == typeof(int) || typeof(T) == typeof(ushort) || typeof(T) == typeof(uint))
        {
            for (int i = 0; i < count; i++)
            {
                result[i] = (T)Convert.ChangeType(_indices[start + i], typeof(T));
            }
        }
        return result;
    }

    public void SetSubMesh(int index, SubmeshDescriptor subMesh, MeshUpdateFlags flags = MeshUpdateFlags.Default)
    {
        _ = flags;
        if (_subMeshes == null) _subMeshes = new List<SubmeshDescriptor>();
        while (_subMeshes.Count <= index) _subMeshes.Add(default);
        _subMeshes[index] = subMesh;
    }

    public SubmeshDescriptor GetSubMesh(int index)
    {
        if (_subMeshes != null && index >= 0 && index < _subMeshes.Count)
            return _subMeshes[index];
        return default;
    }

    public void SetSubMeshes(params SubmeshDescriptor[] subMeshes)
    {
        _subMeshes = subMeshes != null ? new List<SubmeshDescriptor>(subMeshes) : new List<SubmeshDescriptor>();
    }

    public void AddSubMesh(SubmeshDescriptor subMesh, MeshUpdateFlags flags = MeshUpdateFlags.Default)
    {
        _ = flags;
        if (_subMeshes == null) _subMeshes = new List<SubmeshDescriptor>();
        _subMeshes.Add(subMesh);
    }

    internal List<Vector3> GetPositions() => _positions ?? new List<Vector3>();
    internal List<Vector3> GetNormalsList() => _normals ?? new List<Vector3>();
    internal List<Vector4> GetTangentsList() => _tangents ?? new List<Vector4>();
    internal List<Color> GetColorsList() => _colors ?? new List<Color>();
    internal List<Vector2> GetUVsList(int channel)
    {
        if (_uvChannels == null || channel < 0 || channel >= 8) return new List<Vector2>();
        return _uvChannels[channel] ?? new List<Vector2>();
    }
    internal List<int> GetIndices() => _indices ?? new List<int>();

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _positions?.Clear();
        _normals?.Clear();
        _tangents?.Clear();
        _colors?.Clear();
        if (_uvChannels != null)
        {
            for (int ch = 0; ch < 8; ch++)
            {
                _uvChannels[ch]?.Clear();
            }
        }
        _indices?.Clear();
        _vertexAttributes?.Clear();
        _subMeshes?.Clear();
    }
}

[Flags]
public enum MeshUpdateFlags
{
    Default = 0,
    DontValidateIndices = 1,
    DontResetBoneBounds = 2,
    DontNotifyMeshUsers = 4,
    DontRecalculateBounds = 8
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

    public static void ApplyAndDisposeWritableMeshData(MeshDataArray data, Mesh[] meshes, MeshUpdateFlags flags = MeshUpdateFlags.Default)
    {
        _ = flags;
        if (data._data == null || meshes == null) { data.Dispose(); return; }
        int count = Math.Min(data._data.Length, meshes.Length);
        for (int i = 0; i < count; i++)
        {
            if (meshes[i] == null) continue;
            var md = data._data[i];
            meshes[i]._vertexAttributeDescriptors = new List<VertexAttributeDescriptor>(md.GetVertexAttributes());
            int smc = md.subMeshCount;
            meshes[i]._submeshDescriptors = new List<SubmeshDescriptor>();
            var positions = md.GetPositions();
            var indices = md.GetIndices();
            if (positions.Count > 0)
                meshes[i].SetVertices(positions.ToArray());
            if (indices.Count > 0)
            {
                meshes[i].triangles = indices.ToArray();
            }
            for (int s = 0; s < smc; s++)
                meshes[i]._submeshDescriptors.Add(md.GetSubMesh(s));
            meshes[i]._meshVertexCount = md.vertexCount;
            meshes[i]._meshIndexCount = md.indexCount;
            meshes[i].indexFormat = md.indexFormat;
            meshes[i].RecalculateBounds();
        }
        data.Dispose();
    }

    public static void ApplyAndDisposeWritableMeshData(MeshDataArray data, Mesh mesh, int index = 0, MeshUpdateFlags flags = MeshUpdateFlags.Default)
    {
        _ = flags;
        if (mesh == null || data._data == null) { data.Dispose(); return; }
        if (index >= 0 && index < data._data.Length)
        {
            var md = data._data[index];
            mesh._vertexAttributeDescriptors = new List<VertexAttributeDescriptor>(md.GetVertexAttributes());
            int smc = md.subMeshCount;
            mesh._submeshDescriptors = new List<SubmeshDescriptor>();
            var positions = md.GetPositions();
            var indices = md.GetIndices();
            if (positions.Count > 0)
                mesh.SetVertices(positions.ToArray());
            if (indices.Count > 0)
                mesh.triangles = indices.ToArray();
            for (int s = 0; s < smc; s++)
                mesh._submeshDescriptors.Add(md.GetSubMesh(s));
            mesh._meshVertexCount = md.vertexCount;
            mesh._meshIndexCount = md.indexCount;
            mesh.indexFormat = md.indexFormat;
            mesh.RecalculateBounds();
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

    public void SetIndexBufferData<T>(NativeArray<T> data, int indexStart, int indexCount, int submesh = 0) where T : struct
    {
        if (!data.IsCreated) return;
        int copyCount = Math.Min(indexCount, data.Length);
        if (copyCount <= 0) return;

        while (_subMeshIndices.Count <= submesh)
            _subMeshIndices.Add(Array.Empty<int>());

        int[] newIndices = new int[indexStart + copyCount];
        if (submesh < _subMeshIndices.Count && _subMeshIndices[submesh] != null)
        {
            int existingLen = _subMeshIndices[submesh].Length;
            if (existingLen > 0)
                Array.Copy(_subMeshIndices[submesh], newIndices, Math.Min(existingLen, indexStart + copyCount));
        }

        var srcArray = data.ToArray();
        for (int i = 0; i < copyCount && indexStart + i < newIndices.Length; i++)
        {
            newIndices[indexStart + i] = Convert.ToInt32(srcArray[i]);
        }

        _subMeshIndices[submesh] = newIndices;
        if (submesh == 0)
        {
            _triangles.Clear();
            _triangles.AddRange(newIndices);
        }
        UpdateBufferSizes();
    }

    public void SetVertexBufferData<T>(NativeArray<T> data, int start, int count, int stream = 0) where T : struct
    {
        if (!data.IsCreated) return;
        int copyCount = Math.Min(count, data.Length);
        if (copyCount <= 0) return;

        var srcArray = data.ToArray();

        if (typeof(T) == typeof(Vector3))
        {
            if (stream == 0)
            {
                while (_vertices.Count < start + copyCount)
                    _vertices.Add(Vector3.zero);
                for (int i = 0; i < copyCount && start + i < _vertices.Count; i++)
                    _vertices[start + i] = (Vector3)(object)srcArray[i]!;
                RecalculateBounds();
            }
            else if (stream == 1)
            {
                while (_normals.Count < start + copyCount)
                    _normals.Add(Vector3.up);
                for (int i = 0; i < copyCount && start + i < _normals.Count; i++)
                    _normals[start + i] = (Vector3)(object)srcArray[i]!;
            }
        }
        else if (typeof(T) == typeof(Vector2))
        {
            var uvList = stream switch
            {
                0 => _uv,
                1 => _uv2,
                2 => _uv3,
                3 => _uv4,
                _ => null
            };
            if (uvList != null)
            {
                while (uvList.Count < start + copyCount)
                    uvList.Add(Vector2.zero);
                for (int i = 0; i < copyCount && start + i < uvList.Count; i++)
                    uvList[start + i] = (Vector2)(object)srcArray[i]!;
            }
        }
        else if (typeof(T) == typeof(Vector4))
        {
            while (_tangents.Count < start + copyCount)
                _tangents.Add(new Vector4(1, 0, 0, 1));
            for (int i = 0; i < copyCount && start + i < _tangents.Count; i++)
                _tangents[start + i] = (Vector4)(object)srcArray[i]!;
        }
        else if (typeof(T) == typeof(Color))
        {
            while (_colors.Count < start + copyCount)
                _colors.Add(Color.white);
            for (int i = 0; i < copyCount && start + i < _colors.Count; i++)
                _colors[start + i] = (Color)(object)srcArray[i]!;
        }
        else if (typeof(T) == typeof(Color32))
        {
            while (_colors32.Count < start + copyCount)
                _colors32.Add(new Color32(255, 255, 255, 255));
            for (int i = 0; i < copyCount && start + i < _colors32.Count; i++)
                _colors32[start + i] = (Color32)(object)srcArray[i]!;
        }

        UpdateBufferSizes();
    }

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
