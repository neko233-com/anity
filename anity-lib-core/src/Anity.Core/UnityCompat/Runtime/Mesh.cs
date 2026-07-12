using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;

namespace UnityEngine;

public partial class Mesh : Object
{
    private List<Vector3> _vertices = new();
    private List<int> _triangles = new();
    private List<Vector3> _normals = new();
    private List<Vector4> _tangents = new();
    private List<Vector2> _uv = new();
    private List<Vector2> _uv2 = new();
    private List<Vector2> _uv3 = new();
    private List<Vector2> _uv4 = new();
    private List<Vector2> _uv5 = new();
    private List<Vector2> _uv6 = new();
    private List<Vector2> _uv7 = new();
    private List<Vector2> _uv8 = new();
    private List<Color> _colors = new();
    private List<Color32> _colors32 = new();
    private List<Matrix4x4> _bindposes = new();
    private List<BoneWeight> _boneWeights = new();
    private List<int[]> _subMeshIndices = new();
    private Bounds _bounds;
    private MeshTopology _topology = MeshTopology.Triangles;
    private bool _isReadable = true;
    private IndexFormat _indexFormat = IndexFormat.UInt16;
    private bool _isDynamic;
    private bool _blendShapesDirty;
    private bool _optimized;

    public string name { get; set; } = string.Empty;
    public bool isReadable { get => _isReadable; set => _isReadable = value; }
    public int vertexBufferSize { get; private set; }
    public int indexBufferSize { get; private set; }
    public IndexFormat indexFormat { get => _indexFormat; set => _indexFormat = value; }

    public Vector3[] vertices
    {
        get => _vertices.ToArray();
        set => SetVertices(value != null ? new List<Vector3>(value) : new List<Vector3>());
    }

    public Vector3[] normals
    {
        get => _normals.ToArray();
        set => SetNormals(value != null ? new List<Vector3>(value) : new List<Vector3>());
    }

    public Vector4[] tangents
    {
        get => _tangents.ToArray();
        set => _tangents = value != null ? new List<Vector4>(value) : new List<Vector4>();
    }

    public Vector2[] uv
    {
        get => _uv.ToArray();
        set => SetUVs(0, value != null ? new List<Vector2>(value) : new List<Vector2>());
    }

    public Vector2[] uv2
    {
        get => _uv2.ToArray();
        set => SetUVs(1, value != null ? new List<Vector2>(value) : new List<Vector2>());
    }

    public Vector2[] uv3
    {
        get => _uv3.ToArray();
        set => SetUVs(2, value != null ? new List<Vector2>(value) : new List<Vector2>());
    }

    public Vector2[] uv4
    {
        get => _uv4.ToArray();
        set => SetUVs(3, value != null ? new List<Vector2>(value) : new List<Vector2>());
    }

    public Vector2[] uv5
    {
        get => _uv5.ToArray();
        set => SetUVs(4, value != null ? new List<Vector2>(value) : new List<Vector2>());
    }

    public Vector2[] uv6
    {
        get => _uv6.ToArray();
        set => SetUVs(5, value != null ? new List<Vector2>(value) : new List<Vector2>());
    }

    public Vector2[] uv7
    {
        get => _uv7.ToArray();
        set => SetUVs(6, value != null ? new List<Vector2>(value) : new List<Vector2>());
    }

    public Vector2[] uv8
    {
        get => _uv8.ToArray();
        set => SetUVs(7, value != null ? new List<Vector2>(value) : new List<Vector2>());
    }

    public Color[] colors
    {
        get => _colors.ToArray();
        set => SetColors(value != null ? new List<Color>(value) : new List<Color>());
    }

    public Color32[] colors32
    {
        get => _colors32.ToArray();
        set => SetColors(value != null ? new List<Color32>(value) : new List<Color32>());
    }

    public int[] triangles
    {
        get => GetTriangles(0);
        set => SetTriangles(value, 0);
    }

    public Bounds bounds
    {
        get => _bounds;
        set => _bounds = value;
    }

    public int vertexCount => _vertices.Count;

    public int indexCount
    {
        get
        {
            int total = 0;
            foreach (var indices in _subMeshIndices)
            {
                if (indices != null) total += indices.Length;
            }
            return total;
        }
    }

    public int subMeshCount
    {
        get => Math.Max(1, _subMeshIndices.Count);
        set
        {
            if (value < 1) value = 1;
            while (_subMeshIndices.Count < value)
                _subMeshIndices.Add(Array.Empty<int>());
            while (_subMeshIndices.Count > value)
                _subMeshIndices.RemoveAt(_subMeshIndices.Count - 1);
        }
    }

    public Matrix4x4[] bindposes
    {
        get => _bindposes.ToArray();
        set => SetBindposes(value != null ? new List<Matrix4x4>(value) : new List<Matrix4x4>());
    }

    public BoneWeight[] boneWeights
    {
        get => _boneWeights.ToArray();
        set => SetBoneWeights(value != null ? new List<BoneWeight>(value) : new List<BoneWeight>());
    }

    public MeshTopology GetTopology(int submesh) => _topology;

    public Mesh()
    {
        _subMeshIndices.Add(Array.Empty<int>());
    }

    public void Clear()
    {
        Clear(false);
    }

    public void Clear(bool keepVertexLayout)
    {
        _vertices.Clear();
        _triangles.Clear();
        _normals.Clear();
        _tangents.Clear();
        _uv.Clear();
        _uv2.Clear();
        _uv3.Clear();
        _uv4.Clear();
        _uv5.Clear();
        _uv6.Clear();
        _uv7.Clear();
        _uv8.Clear();
        _colors.Clear();
        _colors32.Clear();
        _bindposes.Clear();
        _boneWeights.Clear();
        _subMeshIndices.Clear();
        _subMeshIndices.Add(Array.Empty<int>());
        _bounds = default;
        _topology = MeshTopology.Triangles;
        vertexBufferSize = 0;
        indexBufferSize = 0;
    }

    public void ClearBlendShapes() { _blendShapesDirty = true; }

    public void MarkDynamic() { _isDynamic = true; }

    public void MarkModified()
    {
        RecalculateBounds();
    }

    public void RecalculateBounds()
    {
        if (_vertices.Count == 0)
        {
            _bounds = new Bounds(Vector3.zero, Vector3.zero);
            return;
        }

        var min = _vertices[0];
        var max = _vertices[0];
        for (int i = 1; i < _vertices.Count; i++)
        {
            var v = _vertices[i];
            if (v.x < min.x) min.x = v.x;
            if (v.y < min.y) min.y = v.y;
            if (v.z < min.z) min.z = v.z;
            if (v.x > max.x) max.x = v.x;
            if (v.y > max.y) max.y = v.y;
            if (v.z > max.z) max.z = v.z;
        }
        _bounds = new Bounds((min + max) * 0.5f, max - min);
    }

    public void RecalculateNormals()
    {
        RecalculateNormalsInternal();
    }

    private void RecalculateNormalsInternal()
    {
        _normals.Clear();
        if (_vertices.Count == 0) return;

        var normals = new Vector3[_vertices.Count];

        if (_triangles.Count >= 3)
        {
            for (int i = 0; i < _triangles.Count; i += 3)
            {
                int i0 = _triangles[i];
                int i1 = _triangles[i + 1];
                int i2 = _triangles[i + 2];

                if (i0 >= _vertices.Count || i1 >= _vertices.Count || i2 >= _vertices.Count)
                    continue;

                Vector3 v0 = _vertices[i0];
                Vector3 v1 = _vertices[i1];
                Vector3 v2 = _vertices[i2];

                Vector3 edge1 = v1 - v0;
                Vector3 edge2 = v2 - v0;
                Vector3 normal = Vector3.Cross(edge1, edge2);

                float area = normal.magnitude * 0.5f;
                normal = normal.normalized;

                normals[i0] += normal * area;
                normals[i1] += normal * area;
                normals[i2] += normal * area;
            }
        }

        for (int i = 0; i < normals.Length; i++)
        {
            normals[i] = normals[i].normalized;
        }

        _normals.AddRange(normals);
    }

    public void RecalculateTangents()
    {
        _tangents.Clear();
        if (_vertices.Count == 0) return;
        var tangents = new Vector4[_vertices.Count];
        for (int i = 0; i < tangents.Length; i++)
        {
            tangents[i] = new Vector4(1, 0, 0, 1);
        }
        _tangents.AddRange(tangents);
    }

    public void Optimize() { _optimized = true; }
    public void OptimizeIndexBuffers() { _optimized = true; }
    public void OptimizeReorderVertexBuffer() { _optimized = true; }

    public int[] GetTriangles(int submesh)
    {
        if (submesh >= 0 && submesh < _subMeshIndices.Count)
            return (int[])_subMeshIndices[submesh].Clone();
        return Array.Empty<int>();
    }

    public void GetTriangles(List<int> triangles, int submesh)
    {
        triangles?.Clear();
        triangles?.AddRange(GetTriangles(submesh));
    }

    public void SetTriangles(int[] triangles, int submesh)
    {
        SetTriangles(triangles != null ? new List<int>(triangles) : new List<int>(), submesh);
    }

    public void SetTriangles(List<int> triangles, int submesh)
    {
        if (triangles == null) triangles = new List<int>();
        while (_subMeshIndices.Count <= submesh)
            _subMeshIndices.Add(Array.Empty<int>());
        _subMeshIndices[submesh] = triangles.ToArray();
        if (submesh == 0)
        {
            _triangles.Clear();
            _triangles.AddRange(triangles);
        }
        UpdateBufferSizes();
    }

    public int[] GetIndices(int submesh)
    {
        return GetTriangles(submesh);
    }

    public void GetIndices(List<int> indices, int submesh)
    {
        GetTriangles(indices, submesh);
    }

    public void SetIndices(int[] indices, MeshTopology topology, int submesh)
    {
        _topology = topology;
        SetTriangles(indices, submesh);
    }

    public void SetIndices(List<int> indices, MeshTopology topology, int submesh)
    {
        _topology = topology;
        SetTriangles(indices, submesh);
    }

    public void SetVertices(Vector3[] inVertices)
    {
        SetVertices(inVertices != null ? new List<Vector3>(inVertices) : new List<Vector3>());
    }

    public void SetVertices(List<Vector3> inVertices)
    {
        _vertices = inVertices ?? new List<Vector3>();
        RecalculateBounds();
        UpdateBufferSizes();
    }

    public void GetVertices(List<Vector3> vertices)
    {
        vertices?.Clear();
        vertices?.AddRange(_vertices);
    }

    public Vector3[] GetVertices() => _vertices.ToArray();

    public void SetNormals(Vector3[] inNormals)
    {
        SetNormals(inNormals != null ? new List<Vector3>(inNormals) : new List<Vector3>());
    }

    public void SetNormals(List<Vector3> inNormals)
    {
        _normals = inNormals ?? new List<Vector3>();
    }

    public void GetNormals(List<Vector3> normals)
    {
        normals?.Clear();
        normals?.AddRange(_normals);
    }

    public Vector3[] GetNormals() => _normals.ToArray();

    public void SetTangents(Vector4[] inTangents)
    {
        SetTangents(inTangents != null ? new List<Vector4>(inTangents) : new List<Vector4>());
    }

    public void SetTangents(List<Vector4> inTangents)
    {
        _tangents = inTangents ?? new List<Vector4>();
    }

    public void GetTangents(List<Vector4> tangents)
    {
        tangents?.Clear();
        tangents?.AddRange(_tangents);
    }

    public Vector4[] GetTangents() => _tangents.ToArray();

    public void SetUVs(int channel, Vector2[] uvs)
    {
        SetUVs(channel, uvs != null ? new List<Vector2>(uvs) : new List<Vector2>());
    }

    public void SetUVs(int channel, List<Vector2> uvs)
    {
        uvs ??= new List<Vector2>();
        switch (channel)
        {
            case 0: _uv = uvs; break;
            case 1: _uv2 = uvs; break;
            case 2: _uv3 = uvs; break;
            case 3: _uv4 = uvs; break;
            case 4: _uv5 = uvs; break;
            case 5: _uv6 = uvs; break;
            case 6: _uv7 = uvs; break;
            case 7: _uv8 = uvs; break;
        }
    }

    public void GetUVs(int channel, List<Vector2> uvs)
    {
        uvs?.Clear();
        switch (channel)
        {
            case 0: uvs?.AddRange(_uv); break;
            case 1: uvs?.AddRange(_uv2); break;
            case 2: uvs?.AddRange(_uv3); break;
            case 3: uvs?.AddRange(_uv4); break;
            case 4: uvs?.AddRange(_uv5); break;
            case 5: uvs?.AddRange(_uv6); break;
            case 6: uvs?.AddRange(_uv7); break;
            case 7: uvs?.AddRange(_uv8); break;
        }
    }

    public Vector2[] GetUVs(int channel)
    {
        var list = new List<Vector2>();
        GetUVs(channel, list);
        return list.ToArray();
    }

    public void SetColors(Color[] inColors)
    {
        SetColors(inColors != null ? new List<Color>(inColors) : new List<Color>());
    }

    public void SetColors(List<Color> inColors)
    {
        _colors = inColors ?? new List<Color>();
    }

    public void SetColors(Color32[] inColors)
    {
        SetColors(inColors != null ? new List<Color32>(inColors) : new List<Color32>());
    }

    public void SetColors(List<Color32> inColors)
    {
        _colors32 = inColors ?? new List<Color32>();
    }

    public void GetColors(List<Color> colors)
    {
        colors?.Clear();
        colors?.AddRange(_colors);
    }

    public void GetColors(List<Color32> colors)
    {
        colors?.Clear();
        colors?.AddRange(_colors32);
    }

    public Color[] GetColors() => _colors.ToArray();
    public Color32[] GetColors32() => _colors32.ToArray();

    public void SetBindposes(Matrix4x4[] inBindposes)
    {
        SetBindposes(inBindposes != null ? new List<Matrix4x4>(inBindposes) : new List<Matrix4x4>());
    }

    public void SetBindposes(List<Matrix4x4> inBindposes)
    {
        _bindposes = inBindposes ?? new List<Matrix4x4>();
    }

    public void GetBindposes(List<Matrix4x4> bindposes)
    {
        bindposes?.Clear();
        bindposes?.AddRange(_bindposes);
    }

    public Matrix4x4[] GetBindposes() => _bindposes.ToArray();

    public void SetBoneWeights(BoneWeight[] inBoneWeights)
    {
        SetBoneWeights(inBoneWeights != null ? new List<BoneWeight>(inBoneWeights) : new List<BoneWeight>());
    }

    public void SetBoneWeights(List<BoneWeight> inBoneWeights)
    {
        _boneWeights = inBoneWeights ?? new List<BoneWeight>();
    }

    public void GetBoneWeights(List<BoneWeight> boneWeights)
    {
        boneWeights?.Clear();
        boneWeights?.AddRange(_boneWeights);
    }

    public BoneWeight[] GetBoneWeights() => _boneWeights.ToArray();

    public void UploadMeshData(bool markNoLongerReadable)
    {
        UpdateBufferSizes();
        if (markNoLongerReadable)
            _isReadable = false;
    }

    public NativeArray<T> GetVertexBufferStream<T>(int stream) where T : struct
    {
        _ = stream;
        return new NativeArray<T>(0, Allocator.Persistent);
    }

    private void UpdateBufferSizes()
    {
        int uvSize = 0;
        uvSize += _uv.Count * 8;
        uvSize += _uv2.Count * 8;
        uvSize += _uv3.Count * 8;
        uvSize += _uv4.Count * 8;
        uvSize += _uv5.Count * 8;
        uvSize += _uv6.Count * 8;
        uvSize += _uv7.Count * 8;
        uvSize += _uv8.Count * 8;
        vertexBufferSize = _vertices.Count * 12 + _normals.Count * 12 + _tangents.Count * 16 + uvSize;
        indexBufferSize = _triangles.Count * (indexFormat == IndexFormat.UInt32 ? 4 : 2);
    }

    public void CombineMeshes(CombineInstance[] combine)
    {
        CombineMeshes(combine, false, false);
    }

    public void CombineMeshes(CombineInstance[] combine, bool mergeSubMeshes)
    {
        CombineMeshes(combine, mergeSubMeshes, false);
    }

    public void CombineMeshes(CombineInstance[] combine, bool mergeSubMeshes, bool useMatrices)
    {
        CombineMeshes(combine, mergeSubMeshes, useMatrices, false);
    }

    public void CombineMeshes(CombineInstance[] combine, bool mergeSubMeshes, bool useMatrices, bool hasLightmapData)
    {
        if (combine == null || combine.Length == 0) return;

        var newVerts = new List<Vector3>();
        var newNorms = new List<Vector3>();
        var newUvs = new List<Vector2>();
        var newTris = new List<int>();

        int vertexOffset = 0;

        foreach (var ci in combine)
        {
            if (ci.mesh == null) continue;

            var mesh = ci.mesh;
            var matrix = useMatrices ? ci.transform : Matrix4x4.identity;

            for (int i = 0; i < mesh.vertexCount; i++)
            {
                Vector3 v = matrix.MultiplyPoint(mesh._vertices[i]);
                newVerts.Add(v);

                if (mesh._normals.Count > i)
                {
                    Vector3 n = matrix.MultiplyVector(mesh._normals[i]);
                    n = n.normalized;
                    newNorms.Add(n);
                }

                if (mesh._uv.Count > i)
                {
                    newUvs.Add(mesh._uv[i]);
                }
            }

            var tris = mesh._triangles;
            foreach (var idx in tris)
            {
                newTris.Add(idx + vertexOffset);
            }

            vertexOffset += mesh.vertexCount;
        }

        Clear();
        _vertices.AddRange(newVerts);
        _normals.AddRange(newNorms);
        _uv.AddRange(newUvs);
        _triangles.AddRange(newTris);
        _subMeshIndices[0] = newTris.ToArray();
        RecalculateBounds();
        UpdateBufferSizes();
    }
}

public struct CombineInstance
{
    public int meshInstanceID;
    public Mesh mesh;
    public Matrix4x4 transform;
    public int subMeshIndex;
    public bool lightmapScaleOffset;
    public Vector4 lightmapScaleOffsetValue;
    public bool realtimeLightmapScaleOffset;
    public Vector4 realtimeLightmapScaleOffsetValue;
}

public struct BoneWeight
{
    public float weight0 { get; set; }
    public float weight1 { get; set; }
    public float weight2 { get; set; }
    public float weight3 { get; set; }
    public int boneIndex0 { get; set; }
    public int boneIndex1 { get; set; }
    public int boneIndex2 { get; set; }
    public int boneIndex3 { get; set; }
}

public enum MeshTopology
{
    Triangles = 0,
    Quads = 2,
    Lines = 3,
    LineStrip = 4,
    Points = 5
}
