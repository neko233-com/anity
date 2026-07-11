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
    private readonly List<UIVertex> _verts = new();

    public int currentVertCount => _verts.Count;
    public int currentIndexCount => (_verts.Count / 4) * 6;

    public void Clear()
    {
        _verts.Clear();
        _indices.Clear();
    }

    public void AddVert(Vector3 position, Color32 color, Vector4 uv0)
    {
        AddVert(position, color, uv0, Vector4.zero, Vector3.zero, Vector4.zero);
    }

    public void AddVert(Vector3 position, Color32 color, Vector4 uv0, Vector4 uv1, Vector3 normal, Vector4 tangent)
    {
        _verts.Add(new UIVertex
        {
            position = position,
            color = color,
            uv0 = uv0,
            uv1 = uv1,
            normal = normal,
            tangent = tangent
        });
    }

    private readonly List<int> _indices = new();

    public void AddTriangle(int idx0, int idx1, int idx2)
    {
        _indices.Add(idx0);
        _indices.Add(idx1);
        _indices.Add(idx2);
    }

    public void AddUIVertexTriangleStream(List<UIVertex> verts)
    {
        if (verts is null) return;
        _verts.AddRange(verts);
    }

    public void GetUIVertexStream(List<UIVertex> stream)
    {
        if (stream is null) return;
        stream.Clear();
        for (var i = 0; i < _indices.Count; i++)
        {
            var idx = _indices[i];
            if (idx >= 0 && idx < _verts.Count)
                stream.Add(_verts[idx]);
        }
    }

    public void PopulateUIVertex(ref UIVertex vertex, int index)
    {
        if (index < 0 || index >= _verts.Count) return;
        vertex = _verts[index];
    }

    public void SetUIVertex(UIVertex vertex, int index)
    {
        if (index < 0 || index >= _verts.Count) return;
        _verts[index] = vertex;
    }

    public void FillMesh(Mesh mesh)
    {
        if (mesh is null) return;
        var verts = new List<UIVertex>();
        GetUIVertexStream(verts);
        var positions = new Vector3[verts.Count];
        var colors = new Color32[verts.Count];
        var uvs = new Vector2[verts.Count];
        var normals = new Vector3[verts.Count];
        var tangents = new Vector4[verts.Count];
        for (var i = 0; i < verts.Count; i++)
        {
            positions[i] = verts[i].position;
            colors[i] = verts[i].color;
            uvs[i] = new Vector2(verts[i].uv0.x, verts[i].uv0.y);
            normals[i] = verts[i].normal;
            tangents[i] = verts[i].tangent;
        }
        var indicesArray = new int[verts.Count];
        for (var i = 0; i < verts.Count; i++)
            indicesArray[i] = i;
        mesh.vertices = positions;
        mesh.normals = normals;
        mesh.tangents = tangents;
        mesh.colors32 = colors;
        mesh.uv = uvs;
        mesh.triangles = indicesArray;
    }

    public void Dispose()
    {
        Clear();
        _indices.Clear();
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

        var start = 0;
        var count = verts.Count;
        ApplyShadowZeroAlloc(verts, (Color32)effectColor, start, verts.Count, effectDistance.x, effectDistance.y);
        ApplyShadowZeroAlloc(verts, (Color32)effectColor, start, verts.Count, effectDistance.x, -effectDistance.y);
        ApplyShadowZeroAlloc(verts, (Color32)effectColor, start, verts.Count, -effectDistance.x, effectDistance.y);
        ApplyShadowZeroAlloc(verts, (Color32)effectColor, start, verts.Count, -effectDistance.x, -effectDistance.y);
        _ = count;

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
            verts.Add(vt);

            var v = vt.position;
            v.x += x;
            v.y += y;
            vt.position = v;

            var newColor = color;
            if (_useGraphicAlpha)
            {
                newColor.a = (byte)(color.a * vt.color.a / 255);
            }

            vt.color = newColor;
            verts[i] = vt;
        }
    }

    public override void ModifyMesh(VertexHelper vh)
    {
        if (!IsActive()) return;

        var verts = new List<UIVertex>();
        vh.GetUIVertexStream(verts);
        vh.Clear();

        var start = 0;
        var count = verts.Count;
        ApplyShadowZeroAlloc(verts, (Color32)effectColor, start, verts.Count, effectDistance.x, effectDistance.y);
        _ = count;

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
            vertex.uv1 = new Vector4(vertex.position.x, vertex.position.y, 0f, 0f);
            vh.SetUIVertex(vertex, i);
        }
    }
}
