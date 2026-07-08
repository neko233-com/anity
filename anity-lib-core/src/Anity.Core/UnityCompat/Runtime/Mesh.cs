namespace UnityEngine;

public class Mesh : Object
{
  public string name { get; set; } = string.Empty;
  public Vector3[] vertices { get; set; } = new Vector3[0];
  public Vector3[] normals { get; set; } = new Vector3[0];
  public Vector4[] tangents { get; set; } = new Vector4[0];
  public Vector2[] uv { get; set; } = new Vector2[0];
  public Vector2[] uv2 { get; set; } = new Vector2[0];
  public Vector2[] uv3 { get; set; } = new Vector2[0];
  public Vector2[] uv4 { get; set; } = new Vector2[0];
  public Color[] colors { get; set; } = new Color[0];
  public Color32[] colors32 { get; set; } = new Color32[0];
  public int[] triangles { get; set; } = new int[0];
  public Bounds bounds { get; set; }
  public int vertexCount { get; set; }
  public int subMeshCount { get; set; } = 1;
  public Matrix4x4[] bindposes { get; set; } = new Matrix4x4[0];
  public BoneWeight[] boneWeights { get; set; } = new BoneWeight[0];

  public void Clear()
  {
  }

  public void RecalculateNormals()
  {
  }

  public void RecalculateBounds()
  {
  }

  public void RecalculateTangents()
  {
  }

  public void Optimize()
  {
  }

  public int[] GetTriangles(int submesh)
  {
    _ = submesh;
    return new int[0];
  }

  public void SetTriangles(int[] triangles, int submesh)
  {
    _ = triangles;
    _ = submesh;
  }

  public Vector3[] GetVertices()
  {
    return new Vector3[0];
  }

  public void SetVertices(Vector3[] inVertices)
  {
    _ = inVertices;
  }

  public Vector3[] GetNormals()
  {
    return new Vector3[0];
  }

  public void SetNormals(Vector3[] inNormals)
  {
    _ = inNormals;
  }

  public Vector4[] GetTangents()
  {
    return new Vector4[0];
  }

  public void SetTangents(Vector4[] inTangents)
  {
    _ = inTangents;
  }

  public Vector2[] GetUVs(int channel)
  {
    _ = channel;
    return new Vector2[0];
  }

  public void SetUVs(int channel, Vector2[] uvs)
  {
    _ = channel;
    _ = uvs;
  }

  public Color[] GetColors()
  {
    return new Color[0];
  }

  public void SetColors(Color[] inColors)
  {
    _ = inColors;
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
