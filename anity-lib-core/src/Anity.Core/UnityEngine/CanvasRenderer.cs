using System;
using System.Collections.Generic;
using Anity.Core.Runtime.Native;
using UnityEngine.UI;

namespace UnityEngine;

[Bindings.NativeHeader("Modules/UI/CanvasRenderer.h")]
[NativeClass("UI::CanvasRenderer")]
public sealed class CanvasRenderer : Component
{
    public delegate void OnRequestRebuild();
    public static event OnRequestRebuild? onRequestRebuild;

    private Material? _material;
    private Material[] _materials = Array.Empty<Material>();
    private Material[] _popMaterials = Array.Empty<Material>();
    private Color _color = Color.white;
    private float _alpha = 1f;
    private Texture? _mainTexture;
    private Texture? _alphaTexture;
    private bool _cull;
    private bool _hasMoved = true;
    private bool _hasPopInstruction;
    private bool _cullTransparentMesh = true;
    private bool _hasRectClipping;
    private bool _isMask;
    private Rect _clippingRect;
    private Vector2 _clippingSoftness;
    private int _absoluteDepth = -1;
    private int _relativeDepth = -1;
    private bool _inPopulateMesh;
    private readonly List<Vector3> _vertices = new();
    private readonly List<Vector2> _uv0 = new();
    private readonly List<Vector2> _uv1 = new();
    private readonly List<int> _indices = new();
    private readonly List<Color32> _colors32 = new();
    private Mesh? _mesh;
    private int _materialCount;
    private int _popMaterialCount;
    private AnityNative.UIVisibility _nativeVisibility;

    [Bindings.NativeProperty("ShouldCull", false, Bindings.TargetType.Function)]
    public bool cull
    {
        get => _cull;
        set => _cull = value;
    }

    public bool hasMoved => _hasMoved;

    public int absoluteDepth => _absoluteDepth;
    [Bindings.NativeProperty("Depth", false, Bindings.TargetType.Function)]
    public int relativeDepth => _relativeDepth;
    internal bool inPopulateMesh => _inPopulateMesh;
    public int materialCount
    {
        get => _materialCount;
        set
        {
            _materialCount = Math.Max(0, value);
            Array.Resize(ref _materials, _materialCount);
            _material = _materialCount > 0 ? _materials[0] : null;
        }
    }
    internal int populateMaterialCount => _materialCount;
    internal int meshCount => _mesh != null ? 1 : 0;
    internal AnityNative.UIVisibility nativeVisibility => _nativeVisibility;
    internal Texture? nativeAlphaTexture => _alphaTexture;
    internal Rect nativeClippingRect => _clippingRect;
    internal bool nativeIsMask => _isMask;

    public int popMaterialCount
    {
        get => _popMaterialCount;
        set
        {
            _popMaterialCount = Math.Max(0, value);
            Array.Resize(ref _popMaterials, _popMaterialCount);
        }
    }

    public bool hasPopInstruction
    {
        get => _hasPopInstruction;
        set => _hasPopInstruction = value;
    }

    public bool cullTransparentMesh
    {
        get => _cullTransparentMesh;
        set
        {
            _cullTransparentMesh = value;
            RefreshNativeVisibility();
        }
    }

    [Bindings.NativeProperty("RectClipping", false, Bindings.TargetType.Function)]
    public bool hasRectClipping => _hasRectClipping;

    [Obsolete("isMask is no longer supported.See EnableClipping for vertex clipping configuration", false)]
    public bool isMask
    {
        get => _isMask;
        set => _isMask = value;
    }

    public Vector2 clippingSoftness
    {
        get => _clippingSoftness;
        set
        {
            _clippingSoftness = value;
            RefreshNativeVisibility();
        }
    }

    internal event Action? OnPopulateMesh;

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

    public Material? GetMaterial() => GetMaterial(0);

    public void SetPopMaterial(Material? material, int index)
    {
        if (index < 0) return;
        if (_popMaterials.Length <= index)
            Array.Resize(ref _popMaterials, index + 1);
        _popMaterials[index] = material!;
        if (_popMaterialCount <= index)
            _popMaterialCount = index + 1;
    }

    public Material? GetPopMaterial(int index)
    {
        return index >= 0 && index < _popMaterials.Length ? _popMaterials[index] : null;
    }

    public void SetMesh(Mesh? mesh)
    {
        _mesh = mesh;
        RefreshNativeVisibility();
    }

    public Mesh? GetMesh() => _mesh;

    public void SetColor(Color color)
    {
        _color = color;
        _alpha = color.a;
        RefreshNativeVisibility();
    }

    public Color GetColor() => _color;

    public void SetAlpha(float alpha)
    {
        _alpha = alpha;
        _color.a = alpha;
        RefreshNativeVisibility();
    }

    public float GetAlpha() => _alpha;

    public float GetInheritedAlpha()
    {
        float inherited = 1f;
        Transform? current = transform;
        while (current is not null)
        {
            CanvasGroup? group = current.GetComponent<CanvasGroup>();
            if (group is not null)
            {
                inherited *= group.alpha;
                if (group.ignoreParentGroups)
                    break;
            }
            current = current.parent;
        }
        return inherited;
    }

    public static void SplitUIVertexStreams(
        List<UIVertex> verts, List<Vector3> positions, List<Color32> colors,
        List<Vector4> uv0S, List<Vector4> uv1S, List<Vector3> normals,
        List<Vector4> tangents, List<int> indices)
    {
        SplitUIVertexStreams(verts, positions, colors, uv0S, uv1S,
            new List<Vector4>(), new List<Vector4>(), normals, tangents, indices);
    }

    public static void SplitUIVertexStreams(
        List<UIVertex> verts, List<Vector3> positions, List<Color32> colors,
        List<Vector4> uv0S, List<Vector4> uv1S, List<Vector4> uv2S,
        List<Vector4> uv3S, List<Vector3> normals, List<Vector4> tangents,
        List<int> indices)
    {
        positions.Clear();
        colors.Clear();
        uv0S.Clear();
        uv1S.Clear();
        uv2S.Clear();
        uv3S.Clear();
        normals.Clear();
        tangents.Clear();
        indices.Clear();
        AddUIVertexStream(verts, positions, colors, uv0S, uv1S, uv2S, uv3S, normals, tangents);
        for (int i = 0; i < verts.Count; i++)
            indices.Add(i);
    }

    public static void CreateUIVertexStream(
        List<UIVertex> verts, List<Vector3> positions, List<Color32> colors,
        List<Vector4> uv0S, List<Vector4> uv1S, List<Vector3> normals,
        List<Vector4> tangents, List<int> indices)
    {
        CreateUIVertexStream(verts, positions, colors, uv0S, uv1S,
            new List<Vector4>(), new List<Vector4>(), normals, tangents, indices);
    }

    public static void CreateUIVertexStream(
        List<UIVertex> verts, List<Vector3> positions, List<Color32> colors,
        List<Vector4> uv0S, List<Vector4> uv1S, List<Vector4> uv2S,
        List<Vector4> uv3S, List<Vector3> normals, List<Vector4> tangents,
        List<int> indices)
    {
        verts.Clear();
        for (int i = 0; i < indices.Count; i++)
        {
            int index = indices[i];
            verts.Add(BuildVertex(index, positions, colors, uv0S, uv1S, uv2S, uv3S, normals, tangents));
        }
    }

    public static void AddUIVertexStream(
        List<UIVertex> verts, List<Vector3> positions, List<Color32> colors,
        List<Vector4> uv0S, List<Vector4> uv1S, List<Vector3> normals,
        List<Vector4> tangents)
    {
        AddUIVertexStream(verts, positions, colors, uv0S, uv1S,
            new List<Vector4>(), new List<Vector4>(), normals, tangents);
    }

    public static void AddUIVertexStream(
        List<UIVertex> verts, List<Vector3> positions, List<Color32> colors,
        List<Vector4> uv0S, List<Vector4> uv1S, List<Vector4> uv2S,
        List<Vector4> uv3S, List<Vector3> normals, List<Vector4> tangents)
    {
        positions.Clear();
        colors.Clear();
        uv0S.Clear();
        uv1S.Clear();
        uv2S.Clear();
        uv3S.Clear();
        normals.Clear();
        tangents.Clear();
        for (int i = 0; i < verts.Count; i++)
        {
            UIVertex vertex = verts[i];
            positions.Add(vertex.position);
            colors.Add(vertex.color);
            uv0S.Add(vertex.uv0);
            uv1S.Add(vertex.uv1);
            uv2S.Add(vertex.uv2);
            uv3S.Add(vertex.uv3);
            normals.Add(vertex.normal);
            tangents.Add(vertex.tangent);
        }
    }

    private static UIVertex BuildVertex(
        int index, List<Vector3> positions, List<Color32> colors,
        List<Vector4> uv0S, List<Vector4> uv1S, List<Vector4> uv2S,
        List<Vector4> uv3S, List<Vector3> normals, List<Vector4> tangents)
    {
        UIVertex vertex = UIVertex.simpleVert;
        vertex.position = positions[index];
        if (index < colors.Count) vertex.color = colors[index];
        if (index < uv0S.Count) vertex.uv0 = uv0S[index];
        if (index < uv1S.Count) vertex.uv1 = uv1S[index];
        if (index < uv2S.Count) vertex.uv2 = uv2S[index];
        if (index < uv3S.Count) vertex.uv3 = uv3S[index];
        if (index < normals.Count) vertex.normal = normals[index];
        if (index < tangents.Count) vertex.tangent = tangents[index];
        return vertex;
    }

    [Obsolete("UI System now uses meshes.Generate a mesh and use 'SetMesh' instead", false)]
    public void SetVertices(List<UIVertex> vertices)
    {
        SetVertices(vertices.ToArray(), vertices.Count);
    }

    [Obsolete("UI System now uses meshes.Generate a mesh and use 'SetMesh' instead", false)]
    public void SetVertices(UIVertex[] vertices, int size)
    {
        if (size >= 0 && size <= vertices.Length && size % 4 == 0 &&
            TryBuildLegacyMeshNative(vertices, size, out Mesh? nativeMesh))
        {
            SetMesh(nativeMesh);
            return;
        }

        SetMesh(BuildLegacyMeshManaged(vertices, size));
    }

    private static bool TryBuildLegacyMeshNative(UIVertex[] vertices, int size, out Mesh? mesh)
    {
        mesh = null;
        var nativeVertices = new AnityNative.UIVertexNative[size];
        for (int index = 0; index < size; index++)
        {
            UIVertex vertex = vertices[index];
            nativeVertices[index] = new AnityNative.UIVertexNative
            {
                position = ToNative(vertex.position),
                normal = ToNative(vertex.normal),
                tangent = ToNative(vertex.tangent),
                color = new AnityNative.UIColor32(vertex.color.r, vertex.color.g, vertex.color.b, vertex.color.a),
                uv0 = ToNative(vertex.uv0),
                uv1 = ToNative(vertex.uv1),
                uv2 = ToNative(vertex.uv2),
                uv3 = ToNative(vertex.uv3)
            };
        }

        if (!AnityNative.TryPackUIVertices(nativeVertices, size, out AnityNative.UIPackedVertex[] packed,
                out AnityNative.UIBounds nativeBounds) ||
            !AnityNative.TryBuildUIQuadIndices(size, out int[] nativeIndices))
            return false;

        var positions = new List<Vector3>(size);
        var colors = new List<Color32>(size);
        var uv0 = new List<Vector2>(size);
        var uv1 = new List<Vector2>(size);
        var normals = new List<Vector3>(size);
        var tangents = new List<Vector4>(size);
        for (int index = 0; index < packed.Length; index++)
        {
            AnityNative.UIPackedVertex vertex = packed[index];
            positions.Add(FromNative(vertex.position));
            colors.Add(new Color32(vertex.color.r, vertex.color.g, vertex.color.b, vertex.color.a));
            uv0.Add(new Vector2(vertex.uv0.x, vertex.uv0.y));
            uv1.Add(new Vector2(vertex.uv1.x, vertex.uv1.y));
            normals.Add(FromNative(vertex.normal));
            tangents.Add(FromNative(vertex.tangent));
        }

        mesh = BuildLegacyMesh(positions, colors, uv0, uv1, normals, tangents, nativeIndices);
        if (size > 0)
        {
            Vector3 minimum = FromNative(nativeBounds.min);
            Vector3 maximum = FromNative(nativeBounds.max);
            mesh.bounds = new Bounds((minimum + maximum) * .5f, maximum - minimum);
        }
        return true;
    }

    private static Mesh BuildLegacyMeshManaged(UIVertex[] vertices, int size)
    {
        var positions = new List<Vector3>(size);
        var colors = new List<Color32>(size);
        var uv0 = new List<Vector2>(size);
        var uv1 = new List<Vector2>(size);
        var normals = new List<Vector3>(size);
        var tangents = new List<Vector4>(size);
        var indices = new List<int>(size / 4 * 6);
        for (int start = 0; start < size; start += 4)
        {
            for (int i = 0; i < 4; i++)
            {
                UIVertex vertex = vertices[start + i];
                positions.Add(vertex.position);
                colors.Add(vertex.color);
                uv0.Add(new Vector2(vertex.uv0.x, vertex.uv0.y));
                uv1.Add(new Vector2(vertex.uv1.x, vertex.uv1.y));
                normals.Add(vertex.normal);
                tangents.Add(vertex.tangent);
            }
            indices.Add(start);
            indices.Add(start + 1);
            indices.Add(start + 2);
            indices.Add(start + 2);
            indices.Add(start + 3);
            indices.Add(start);
        }

        return BuildLegacyMesh(positions, colors, uv0, uv1, normals, tangents, indices);
    }

    private static Mesh BuildLegacyMesh(
        List<Vector3> positions, List<Color32> colors,
        List<Vector2> uv0, List<Vector2> uv1,
        List<Vector3> normals, List<Vector4> tangents,
        IEnumerable<int> indices)
    {
        var mesh = new Mesh();
        mesh.SetVertices(positions);
        mesh.SetColors(colors);
        mesh.SetUVs(0, uv0);
        mesh.SetUVs(1, uv1);
        mesh.SetNormals(normals);
        mesh.SetTangents(tangents);
        mesh.SetIndices(indices is List<int> list ? list : new List<int>(indices), MeshTopology.Triangles, 0);
        return mesh;
    }

    private static AnityNative.UIVector3 ToNative(Vector3 value)
        => new(value.x, value.y, value.z);

    private static AnityNative.UIVector4 ToNative(Vector4 value)
        => new(value.x, value.y, value.z, value.w);

    private static Vector3 FromNative(AnityNative.UIVector3 value)
        => new(value.x, value.y, value.z);

    private static Vector4 FromNative(AnityNative.UIVector4 value)
        => new(value.x, value.y, value.z, value.w);

    public void SetTexture(Texture? texture)
    {
        _mainTexture = texture;
    }

    internal Texture? GetTexture() => _mainTexture;

    public void SetAlphaTexture(Texture? texture)
    {
        _alphaTexture = texture;
    }

    public void SetMaterial(Material? material, Texture? texture)
    {
        materialCount = Math.Max(1, materialCount);
        SetMaterial(material, 0);
        SetTexture(texture);
    }

    public void EnableRectClipping(Rect rect)
    {
        _clippingRect = rect;
        _hasRectClipping = true;
        RefreshNativeVisibility();
    }

    public void DisableRectClipping()
    {
        _hasRectClipping = false;
        RefreshNativeVisibility();
    }

    internal void SetVertices(List<Vector3> vertices)
    {
        _vertices.Clear();
        if (vertices != null)
            _vertices.AddRange(vertices);
    }

    internal void SetVertices(Vector3[] vertices)
    {
        _vertices.Clear();
        if (vertices != null)
            _vertices.AddRange(vertices);
    }

    internal void SetUv0(List<Vector2> uvs)
    {
        _uv0.Clear();
        if (uvs != null)
            _uv0.AddRange(uvs);
    }

    internal void SetUv0(Vector2[] uvs)
    {
        _uv0.Clear();
        if (uvs != null)
            _uv0.AddRange(uvs);
    }

    internal void SetUv1(List<Vector2> uvs)
    {
        _uv1.Clear();
        if (uvs != null)
            _uv1.AddRange(uvs);
    }

    internal void SetUv1(Vector2[] uvs)
    {
        _uv1.Clear();
        if (uvs != null)
            _uv1.AddRange(uvs);
    }

    internal void SetIndices(List<int> indices)
    {
        _indices.Clear();
        if (indices != null)
            _indices.AddRange(indices);
    }

    internal void SetIndices(int[] indices)
    {
        _indices.Clear();
        if (indices != null)
            _indices.AddRange(indices);
    }

    internal void SetTriangleCount(int count)
    {
        int triCount = count * 3;
        while (_indices.Count < triCount)
            _indices.Add(0);
        if (_indices.Count > triCount)
            _indices.RemoveRange(triCount, _indices.Count - triCount);
    }

    internal void SetColors(List<Color32> colors)
    {
        _colors32.Clear();
        if (colors != null)
            _colors32.AddRange(colors);
    }

    internal void SetColors(Color32[] colors)
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
        _materials = Array.Empty<Material>();
        _popMaterials = Array.Empty<Material>();
        _materialCount = 0;
        _popMaterialCount = 0;
        _mainTexture = null;
        _alphaTexture = null;
        RefreshNativeVisibility();
    }

    private void RefreshNativeVisibility()
    {
        Bounds bounds = _mesh?.bounds ?? default;
        var state = new AnityNative.UIRenderState
        {
            bounds = new AnityNative.UIBounds
            {
                min = ToNative(bounds.min),
                max = ToNative(bounds.max)
            },
            clipXMin = _clippingRect.xMin,
            clipYMin = _clippingRect.yMin,
            clipXMax = _clippingRect.xMax,
            clipYMax = _clippingRect.yMax,
            softnessX = _clippingSoftness.x,
            softnessY = _clippingSoftness.y,
            colorAlpha = _color.a,
            inheritedAlpha = GetInheritedAlpha(),
            hasGeometry = _mesh is { vertexCount: > 0 } ? 1 : 0,
            rectClipping = _hasRectClipping ? 1 : 0,
            cullTransparentMesh = _cullTransparentMesh ? 1 : 0
        };
        if (AnityNative.TryEvaluateUIVisibility(state, out AnityNative.UIVisibility visibility))
            _nativeVisibility = visibility;
        else
            _nativeVisibility = default;
    }

    internal void SetMaterialCount(int count, int popuplateCount = -1)
    {
        _materialCount = Math.Max(1, count);
        materialCount = count;
        if (popuplateCount >= 0)
            popMaterialCount = popuplateCount;
    }

    internal void SetPopulateMaterialCount(int count)
    {
        materialCount = count;
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

    internal void SetDepth(int absoluteDepth, int relativeDepth)
    {
        _absoluteDepth = absoluteDepth;
        _relativeDepth = relativeDepth;
    }
}
