using System;
using System.Collections.Generic;

namespace UnityEngine;

public class CanvasRenderer : Component
{
    private Material? _material;
    private Material[] _materials = Array.Empty<Material>();
    private Color _color = Color.white;
    private float _alpha = 1f;
    private Texture? _mainTexture;
    private bool _cull;
    private bool _hasMoved;
    private int _absoluteDepth;
    private int _relativeDepth;
    private bool _inPopulateMesh;
    private readonly List<Vector3> _vertices = new();
    private readonly List<Vector2> _uv0 = new();
    private readonly List<Vector2> _uv1 = new();
    private readonly List<int> _indices = new();
    private readonly List<Color32> _colors32 = new();
    private Mesh? _mesh;
    private int _materialCount = 1;
    private int _populateMaterialCount = 1;

    public bool cull
    {
        get => _cull;
        set => _cull = value;
    }

    public bool hasMoved
    {
        get => _hasMoved;
        set => _hasMoved = value;
    }

    public int absoluteDepth => _absoluteDepth;
    public int relativeDepth => _relativeDepth;
    public bool inPopulateMesh => _inPopulateMesh;
    public int materialCount => _materialCount;
    public int populateMaterialCount => _populateMaterialCount;
    public int meshCount => _mesh != null ? 1 : 0;

    public event Action? OnPopulateMesh;

    public void SetMaterial(Material? material, int index)
    {
        if (index == 0)
            _material = material;
        if (_materials.Length <= index)
            Array.Resize(ref _materials, index + 1);
        _materials[index] = material!;
    }

    public Material? GetMaterial(int index)
    {
        if (index == 0) return _material;
        if (index >= 0 && index < _materials.Length) return _materials[index];
        return null;
    }

    public void SetMesh(Mesh? mesh)
    {
        _mesh = mesh;
    }

    public Mesh? GetMesh() => _mesh;

    public void SetColor(Color color)
    {
        _color = color;
    }

    public Color GetColor() => _color;

    public void SetAlpha(float alpha)
    {
        _alpha = alpha;
    }

    public float GetAlpha() => _alpha;

    public void SetTexture(Texture? texture)
    {
        _mainTexture = texture;
    }

    public Texture? GetTexture() => _mainTexture;

    public void SetVertices(List<Vector3> vertices)
    {
        _vertices.Clear();
        if (vertices != null)
            _vertices.AddRange(vertices);
    }

    public void SetVertices(Vector3[] vertices)
    {
        _vertices.Clear();
        if (vertices != null)
            _vertices.AddRange(vertices);
    }

    public void SetUv0(List<Vector2> uvs)
    {
        _uv0.Clear();
        if (uvs != null)
            _uv0.AddRange(uvs);
    }

    public void SetUv0(Vector2[] uvs)
    {
        _uv0.Clear();
        if (uvs != null)
            _uv0.AddRange(uvs);
    }

    public void SetUv1(List<Vector2> uvs)
    {
        _uv1.Clear();
        if (uvs != null)
            _uv1.AddRange(uvs);
    }

    public void SetUv1(Vector2[] uvs)
    {
        _uv1.Clear();
        if (uvs != null)
            _uv1.AddRange(uvs);
    }

    public void SetIndices(List<int> indices)
    {
        _indices.Clear();
        if (indices != null)
            _indices.AddRange(indices);
    }

    public void SetIndices(int[] indices)
    {
        _indices.Clear();
        if (indices != null)
            _indices.AddRange(indices);
    }

    public void SetTriangleCount(int count)
    {
        int triCount = count * 3;
        while (_indices.Count < triCount)
            _indices.Add(0);
        if (_indices.Count > triCount)
            _indices.RemoveRange(triCount, _indices.Count - triCount);
    }

    public void SetColors(List<Color32> colors)
    {
        _colors32.Clear();
        if (colors != null)
            _colors32.AddRange(colors);
    }

    public void SetColors(Color32[] colors)
    {
        _colors32.Clear();
        if (colors != null)
            _colors32.AddRange(colors);
    }

    public void Clear()
    {
        _vertices.Clear();
        _uv0.Clear();
        _uv1.Clear();
        _indices.Clear();
        _colors32.Clear();
        _mesh = null;
        _material = null;
        _mainTexture = null;
    }

    public void SetMaterialCount(int count, int popuplateCount = -1)
    {
        _materialCount = Math.Max(1, count);
        _populateMaterialCount = popuplateCount >= 0 ? popuplateCount : _materialCount;
    }

    public void SetPopulateMaterialCount(int count)
    {
        _populateMaterialCount = Math.Max(1, count);
    }

    internal void InvokePopulateMesh()
    {
        _inPopulateMesh = true;
        try
        {
            OnPopulateMesh?.Invoke();
        }
        finally
        {
            _inPopulateMesh = false;
        }
    }

    public void SetDepth(int absoluteDepth, int relativeDepth)
    {
        _absoluteDepth = absoluteDepth;
        _relativeDepth = relativeDepth;
    }
}
