namespace UnityEngine;

public class Mesh : Object
{
  private Vector3[] _vertices = new Vector3[0];
  private int[] _triangles = new int[0];
  private Vector3[] _normals = new Vector3[0];
  private Vector2[] _uv = new Vector2[0];

  public string name { get; set; } = string.Empty;
  public Vector3[] vertices { get => _vertices; set => _vertices = value ?? new Vector3[0]; }
  public Vector3[] normals { get => _normals; set => _normals = value ?? new Vector3[0]; }
  public Vector4[] tangents { get; set; } = new Vector4[0];
  public Vector2[] uv { get => _uv; set => _uv = value ?? new Vector2[0]; }
  public Vector2[] uv2 { get; set; } = new Vector2[0];
  public Vector2[] uv3 { get; set; } = new Vector2[0];
  public Vector2[] uv4 { get; set; } = new Vector2[0];
  public Color[] colors { get; set; } = new Color[0];
  public Color32[] colors32 { get; set; } = new Color32[0];
  public int[] triangles { get => _triangles; set => _triangles = value ?? new int[0]; }
  public Bounds bounds { get; set; }
  public int vertexCount => _vertices.Length;
  public int subMeshCount { get; set; } = 1;
  public Matrix4x4[] bindposes { get; set; } = new Matrix4x4[0];
  public BoneWeight[] boneWeights { get; set; } = new BoneWeight[0];

  public void Clear()
  {
    _vertices = new Vector3[0];
    _triangles = new int[0];
    _normals = new Vector3[0];
    _uv = new Vector2[0];
    tangents = new Vector4[0];
    uv2 = new Vector2[0];
    uv3 = new Vector2[0];
    uv4 = new Vector2[0];
    colors = new Color[0];
    colors32 = new Color32[0];
    bindposes = new Matrix4x4[0];
    boneWeights = new BoneWeight[0];
    subMeshCount = 1;
    bounds = default;
  }

  public void RecalculateNormals()
  {
  }

  public void RecalculateBounds()
  {
    if (_vertices.Length == 0)
    {
      bounds = new Bounds(Vector3.zero, Vector3.zero);
      return;
    }

    var min = _vertices[0];
    var max = _vertices[0];
    for (int i = 1; i < _vertices.Length; i++)
    {
      var v = _vertices[i];
      if (v.x < min.x) min.x = v.x;
      if (v.y < min.y) min.y = v.y;
      if (v.z < min.z) min.z = v.z;
      if (v.x > max.x) max.x = v.x;
      if (v.y > max.y) max.y = v.y;
      if (v.z > max.z) max.z = v.z;
    }
    bounds = new Bounds((min + max) * 0.5f, max - min);
  }

  public void RecalculateTangents()
  {
  }

  public void Optimize()
  {
  }

  public int[] GetTriangles(int submesh)
  {
    return _triangles;
  }

  public void SetTriangles(int[] triangles, int submesh)
  {
    _triangles = triangles ?? new int[0];
  }

  public Vector3[] GetVertices()
  {
    return _vertices;
  }

  public void SetVertices(Vector3[] inVertices)
  {
    _vertices = inVertices ?? new Vector3[0];
  }

  public Vector3[] GetNormals()
  {
    return _normals;
  }

  public void SetNormals(Vector3[] inNormals)
  {
    _normals = inNormals ?? new Vector3[0];
  }

  public Vector4[] GetTangents()
  {
    return tangents;
  }

  public void SetTangents(Vector4[] inTangents)
  {
    tangents = inTangents ?? new Vector4[0];
  }

  public Vector2[] GetUVs(int channel)
  {
    return _uv;
  }

  public void SetUVs(int channel, Vector2[] uvs)
  {
    _uv = uvs ?? new Vector2[0];
  }

  public Color[] GetColors()
  {
    return colors;
  }

  public void SetColors(Color[] inColors)
  {
    colors = inColors ?? new Color[0];
  }

  public void MarkDynamic()
  {
  }

  public void UploadMeshData(bool markNoLongerReadable)
  {
    _ = markNoLongerReadable;
  }

  public void OptimizeIndexBuffers()
  {
  }

  public void OptimizeReorderVertexBuffer()
  {
  }
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
