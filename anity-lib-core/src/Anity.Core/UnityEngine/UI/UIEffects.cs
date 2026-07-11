using System;
using System.Collections.Generic;

namespace UnityEngine.UI;

public abstract class BaseMeshEffect : UIBehaviour, IMeshModifier
{
    protected Graphic? graphic => GetComponent<Graphic>();

    public abstract void ModifyMesh(VertexHelper vh);

    protected override void OnEnable()
    {
        base.OnEnable();
        if (graphic is not null)
        {
            graphic.SetVerticesDirty();
        }
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        if (graphic is not null)
        {
            graphic.SetVerticesDirty();
        }
    }

    protected override void OnDidApplyAnimationProperties()
    {
        base.OnDidApplyAnimationProperties();
        if (graphic is not null)
        {
            graphic.SetVerticesDirty();
        }
    }
}

public interface IMeshModifier
{
    void ModifyMesh(VertexHelper vh);
}

public class VertexHelper : IDisposable
{
    private readonly List<Vector3> m_Positions = new();
    private readonly List<Color32> m_Colors = new();
    private readonly List<Vector2> m_Uvs = new();
    private readonly List<Vector3> m_Normals = new();
    private readonly List<Vector4> m_Tangents = new();
    private readonly List<int> m_Indices = new();

    public int currentVertCount => m_Positions.Count;
    public int currentIndexCount => m_Indices.Count;

    public VertexHelper()
    {
    }

    public VertexHelper(Mesh m)
    {
        if (m == null) return;
        m_Positions.AddRange(m.vertices);
        m_Colors.AddRange(m.colors32);
        m_Uvs.AddRange(m.uv);
        var normals = m.normals;
        if (normals != null && normals.Length > 0)
            m_Normals.AddRange(normals);
        var tangents = m.tangents;
        if (tangents != null && tangents.Length > 0)
            m_Tangents.AddRange(tangents);
        m_Indices.AddRange(m.triangles);
    }

    public void Clear()
    {
        m_Positions.Clear();
        m_Colors.Clear();
        m_Uvs.Clear();
        m_Normals.Clear();
        m_Tangents.Clear();
        m_Indices.Clear();
    }

    public void Dispose()
    {
        Clear();
    }

    public void AddVert(UIVertex v)
    {
        AddVert(v.position, v.color, v.uv0, v.normal, v.tangent);
    }

    public void AddVert(Vector3 position, Color32 color, Vector2 uv0)
    {
        AddVert(position, color, uv0, new Vector3(0f, 0f, -1f), new Vector4(1f, 0f, 0f, -1f));
    }

    public void AddVert(Vector3 position, Color32 color, Vector2 uv0, Vector3 normal, Vector4 tangent)
    {
        m_Positions.Add(position);
        m_Colors.Add(color);
        m_Uvs.Add(uv0);
        m_Normals.Add(normal);
        m_Tangents.Add(tangent);
    }

    public void AddTriangle(int idx0, int idx1, int idx2)
    {
        m_Indices.Add(idx0);
        m_Indices.Add(idx1);
        m_Indices.Add(idx2);
    }

    public void AddUIVertexQuad(UIVertex[] verts)
    {
        if (verts == null || verts.Length < 4) return;
        int startIndex = currentVertCount;
        AddVert(verts[0]);
        AddVert(verts[1]);
        AddVert(verts[2]);
        AddVert(verts[3]);
        AddTriangle(startIndex, startIndex + 1, startIndex + 2);
        AddTriangle(startIndex, startIndex + 2, startIndex + 3);
    }

    public void AddUIVertexStream(List<UIVertex> verts, List<int> indices)
    {
        if (verts == null) return;
        for (int i = 0; i < verts.Count; i++)
            AddVert(verts[i]);
        if (indices != null)
            m_Indices.AddRange(indices);
    }

    public void AddUIVertexTriangleStream(List<UIVertex> verts)
    {
        if (verts == null) return;
        int startIndex = currentVertCount;
        for (int i = 0; i < verts.Count; i++)
            AddVert(verts[i]);
        for (int i = 0; i < verts.Count; i += 3)
        {
            m_Indices.Add(startIndex + i);
            m_Indices.Add(startIndex + i + 1);
            m_Indices.Add(startIndex + i + 2);
        }
    }

    public void GetUIVertexStream(List<UIVertex> stream)
    {
        if (stream == null) return;
        stream.Clear();
        for (int i = 0; i < m_Indices.Count; i++)
        {
            int idx = m_Indices[i];
            if (idx >= 0 && idx < m_Positions.Count)
            {
                var vert = new UIVertex();
                PopulateUIVertex(ref vert, idx);
                stream.Add(vert);
            }
        }
    }

    public void PopulateUIVertex(ref UIVertex vertex, int index)
    {
        if (index < 0 || index >= m_Positions.Count) return;
        vertex.position = m_Positions[index];
        vertex.color = m_Colors[index];
        vertex.uv0 = m_Uvs[index];
        vertex.normal = m_Normals.Count > index ? m_Normals[index] : new Vector3(0f, 0f, -1f);
        vertex.tangent = m_Tangents.Count > index ? m_Tangents[index] : new Vector4(1f, 0f, 0f, -1f);
    }

    public void SetUIVertex(UIVertex vertex, int index)
    {
        if (index < 0 || index >= m_Positions.Count) return;
        m_Positions[index] = vertex.position;
        m_Colors[index] = vertex.color;
        m_Uvs[index] = vertex.uv0;
        if (m_Normals.Count > index) m_Normals[index] = vertex.normal;
        if (m_Tangents.Count > index) m_Tangents[index] = vertex.tangent;
    }

    public void FillMesh(Mesh mesh)
    {
        if (mesh == null) return;
        mesh.Clear();

        if (m_Positions.Count >= 65000)
            throw new ArgumentException("Mesh can not have more than 65000 vertices");

        mesh.SetVertices(m_Positions);
        mesh.SetColors(m_Colors);
        mesh.SetUVs(0, m_Uvs);
        if (m_Normals.Count == m_Positions.Count)
            mesh.SetNormals(m_Normals);
        if (m_Tangents.Count == m_Positions.Count)
            mesh.SetTangents(m_Tangents);
        mesh.SetTriangles(m_Indices, 0);
        mesh.RecalculateBounds();
    }
}

[AddComponentMenu("UI/Effects/Outline")]
public class Outline : Shadow
{
    protected Outline() { }

    public override void ModifyMesh(VertexHelper vh)
    {
        if (!IsActive()) return;

        var verts = new List<UIVertex>();
        vh.GetUIVertexStream(verts);
        vh.Clear();

        var originalCount = verts.Count;
        ApplyShadowZeroAlloc(verts, (Color32)effectColor, 0, originalCount, effectDistance.x, effectDistance.y);
        ApplyShadowZeroAlloc(verts, (Color32)effectColor, 0, originalCount, effectDistance.x, -effectDistance.y);
        ApplyShadowZeroAlloc(verts, (Color32)effectColor, 0, originalCount, -effectDistance.x, effectDistance.y);
        ApplyShadowZeroAlloc(verts, (Color32)effectColor, 0, originalCount, -effectDistance.x, -effectDistance.y);

        vh.AddUIVertexTriangleStream(verts);
    }
}

[AddComponentMenu("UI/Effects/Shadow")]
public class Shadow : BaseMeshEffect
{
    private Color _effectColor = new(0f, 0f, 0f, 0.5f);
    private Vector2 _effectDistance = new(1f, -1f);
    private bool _useGraphicAlpha = true;

    public Color effectColor { get => _effectColor; set => _effectColor = value; }
    public Vector2 effectDistance { get => _effectDistance; set => _effectDistance = value; }
    public bool useGraphicAlpha { get => _useGraphicAlpha; set => _useGraphicAlpha = value; }

    protected void ApplyShadowZeroAlloc(List<UIVertex> verts, Color32 color, int start, int end, float x, float y)
    {
        var neededCapacity = verts.Count + end - start;
        if (verts.Capacity < neededCapacity)
        {
            verts.Capacity = neededCapacity;
        }

        for (var i = start; i < end; ++i)
        {
            var vt = verts[i];
            var shadowVt = vt;

            var v = shadowVt.position;
            v.x += x;
            v.y += y;
            shadowVt.position = v;

            var newColor = color;
            if (_useGraphicAlpha)
            {
                newColor.a = (byte)(color.a * vt.color.a / 255);
            }

            shadowVt.color = newColor;
            verts.Add(shadowVt);
        }
    }

    public override void ModifyMesh(VertexHelper vh)
    {
        if (!IsActive()) return;

        var verts = new List<UIVertex>();
        vh.GetUIVertexStream(verts);
        vh.Clear();

        var originalCount = verts.Count;
        ApplyShadowZeroAlloc(verts, (Color32)effectColor, 0, originalCount, effectDistance.x, effectDistance.y);

        vh.AddUIVertexTriangleStream(verts);
    }
}

[AddComponentMenu("UI/Effects/Position As UV1")]
public class PositionAsUV1 : BaseMeshEffect
{
    public override void ModifyMesh(VertexHelper vh)
    {
        if (!IsActive()) return;

        var vertex = new UIVertex();
        for (var i = 0; i < vh.currentVertCount; i++)
        {
            vh.PopulateUIVertex(ref vertex, i);
            vertex.uv1 = new Vector2(vertex.position.x, vertex.position.y);
            vh.SetUIVertex(vertex, i);
        }
    }
}
