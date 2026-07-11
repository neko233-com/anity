using System;
using System.Collections.Generic;

namespace UnityEngine;

[RequireComponent(typeof(Transform))]
public class Renderer : Component
{
    private bool _enabled = true;
    private Material? _sharedMaterial;
    private Material[] _sharedMaterials = Array.Empty<Material>();
    private Material? _material;
    private Material[] _materials = Array.Empty<Material>();
    private MaterialPropertyBlock? _propertyBlock;
    private int _sortingLayerID;
    private int _renderCount;
    private string _sortingLayerName = string.Empty;
    private int _sortingOrder;
    private bool _receiveShadows = true;
    private ShadowCastingMode _shadowCastingMode = ShadowCastingMode.On;
    private MotionVectorGenerationMode _motionVectorGenerationMode = MotionVectorGenerationMode.Object;
    private LightProbeUsage _lightProbeUsage = LightProbeUsage.BlendProbes;
    private ReflectionProbeUsage _reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;
    private GameObject? _lightProbeProxyVolumeOverride;
    private Transform? _probeAnchor;
    private uint _renderingLayerMask = 1;
    private bool _allowOcclusionWhenDynamic = true;
    private Bounds _localBounds = new Bounds(Vector3.zero, Vector3.one);
    private float _lodBound;

    public bool enabled
    {
        get => _enabled;
        set => _enabled = value;
    }

    public bool isVisible { get; set; } = true;

    public Bounds bounds
    {
        get
        {
            var meshFilter = gameObject?.GetComponent<MeshFilter>();
            var mesh = meshFilter?.sharedMesh;
            if (mesh != null)
            {
                var mf = gameObject?.GetComponent<MeshFilter>();
                if (mf?.sharedMesh != null)
                {
                    return mf.sharedMesh.bounds;
                }
            }
            return _localBounds;
        }
    }

    public Bounds localBounds
    {
        get => _localBounds;
        set => _localBounds = value;
    }

    public float lodBound
    {
        get => _lodBound;
        set => _lodBound = value;
    }

    public Material? material
    {
        get
        {
            if (_material == null && _sharedMaterial != null)
            {
                _material = new Material(_sharedMaterial);
            }
            return _material;
        }
        set
        {
            _material = value;
            if (value != null)
            {
                _sharedMaterial = value;
            }
        }
    }

    public Material[] materials
    {
        get
        {
            if (_materials.Length == 0 && _sharedMaterials.Length > 0)
            {
                _materials = new Material[_sharedMaterials.Length];
                for (int i = 0; i < _sharedMaterials.Length; i++)
                {
                    _materials[i] = new Material(_sharedMaterials[i]);
                }
            }
            return _materials;
        }
        set
        {
            _materials = value ?? Array.Empty<Material>();
            if (value != null && value.Length > 0)
            {
                _sharedMaterials = value;
            }
        }
    }

    public Material? sharedMaterial
    {
        get => _sharedMaterial;
        set
        {
            _sharedMaterial = value;
            _material = null;
        }
    }

    public Material[] sharedMaterials
    {
        get => _sharedMaterials;
        set
        {
            _sharedMaterials = value ?? Array.Empty<Material>();
            _materials = Array.Empty<Material>();
        }
    }

    public int sortingLayerID
    {
        get => _sortingLayerID;
        set => _sortingLayerID = value;
    }

    public string sortingLayerName
    {
        get => _sortingLayerName;
        set => _sortingLayerName = value ?? string.Empty;
    }

    public int sortingOrder
    {
        get => _sortingOrder;
        set => _sortingOrder = value;
    }

    public uint renderingLayerMask
    {
        get => _renderingLayerMask;
        set => _renderingLayerMask = value;
    }

    public MotionVectorGenerationMode motionVectorGenerationMode
    {
        get => _motionVectorGenerationMode;
        set => _motionVectorGenerationMode = value;
    }

    public bool receiveShadows
    {
        get => _receiveShadows;
        set => _receiveShadows = value;
    }

    public ShadowCastingMode shadowCastingMode
    {
        get => _shadowCastingMode;
        set => _shadowCastingMode = value;
    }

    public LightProbeUsage lightProbeUsage
    {
        get => _lightProbeUsage;
        set => _lightProbeUsage = value;
    }

    public ReflectionProbeUsage reflectionProbeUsage
    {
        get => _reflectionProbeUsage;
        set => _reflectionProbeUsage = value;
    }

    public GameObject? lightProbeProxyVolumeOverride
    {
        get => _lightProbeProxyVolumeOverride;
        set => _lightProbeProxyVolumeOverride = value;
    }

    public Transform? probeAnchor
    {
        get => _probeAnchor;
        set => _probeAnchor = value;
    }

    public bool allowOcclusionWhenDynamic
    {
        get => _allowOcclusionWhenDynamic;
        set => _allowOcclusionWhenDynamic = value;
    }

    public Matrix4x4 worldToLocalMatrix => gameObject?.transform?.worldToLocalMatrix ?? Matrix4x4.identity;
    public Matrix4x4 localToWorldMatrix => gameObject?.transform?.localToWorldMatrix ?? Matrix4x4.identity;
    public int lightmapIndex { get; set; } = -1;
    public Vector4 lightmapScaleOffset { get; set; } = new Vector4(1, 1, 0, 0);
    public bool isPartOfStaticBatch { get; }
    public bool hasLightProbeProxyVolume { get; }

    public void SetPropertyBlock(MaterialPropertyBlock properties)
    {
        _propertyBlock = properties;
    }

    public void SetPropertyBlock(MaterialPropertyBlock properties, int materialIndex)
    {
        _propertyBlock = properties;
    }

    public void GetPropertyBlock(MaterialPropertyBlock properties)
    {
        if (_propertyBlock != null && properties != null)
        {
            properties.CopyFrom(_propertyBlock);
        }
    }

    public void GetPropertyBlock(MaterialPropertyBlock properties, int materialIndex)
    {
        GetPropertyBlock(properties);
    }

    public bool HasPropertyBlock() => _propertyBlock != null;

    public Material[] GetSharedMaterials(List<Material> m)
    {
        m?.Clear();
        m?.AddRange(_sharedMaterials);
        return _sharedMaterials;
    }

    public void SetMaterials(List<Material> materials)
    {
        if (materials != null)
        {
            this.materials = materials.ToArray();
        }
    }

    public Material[] GetMaterials(List<Material> m)
    {
        var mats = materials;
        m?.Clear();
        m?.AddRange(mats);
        return mats;
    }

    public void Render() { _renderCount++; }
    public void Render(int materialPass) { _renderCount++; }
}

public class MaterialPropertyBlock
{
    private readonly Dictionary<int, object> _properties = new();
    private float[] _shCoefficients = Array.Empty<float>();
    private float[] _occlusionProbes = Array.Empty<float>();

    public bool isEmpty => _properties.Count == 0;

    public void SetFloat(string name, float value) => _properties[Shader.PropertyToID(name)] = value;
    public void SetFloat(int nameID, float value) => _properties[nameID] = value;
    public float GetFloat(string name) => GetFloat(Shader.PropertyToID(name));
    public float GetFloat(int nameID) => _properties.TryGetValue(nameID, out var v) && v is float f ? f : 0f;

    public void SetInt(string name, int value) => _properties[Shader.PropertyToID(name)] = value;
    public void SetInt(int nameID, int value) => _properties[nameID] = value;
    public int GetInt(string name) => GetInt(Shader.PropertyToID(name));
    public int GetInt(int nameID) => _properties.TryGetValue(nameID, out var v) && v is int i ? i : 0;

    public void SetVector(string name, Vector4 value) => _properties[Shader.PropertyToID(name)] = value;
    public void SetVector(int nameID, Vector4 value) => _properties[nameID] = value;
    public Vector4 GetVector(string name) => GetVector(Shader.PropertyToID(name));
    public Vector4 GetVector(int nameID) => _properties.TryGetValue(nameID, out var v) && v is Vector4 vec ? vec : Vector4.zero;

    public void SetColor(string name, Color value) => _properties[Shader.PropertyToID(name)] = value;
    public void SetColor(int nameID, Color value) => _properties[nameID] = value;
    public Color GetColor(string name) => GetColor(Shader.PropertyToID(name));
    public Color GetColor(int nameID) => _properties.TryGetValue(nameID, out var v) && v is Color c ? c : Color.clear;

    public void SetMatrix(string name, Matrix4x4 value) => _properties[Shader.PropertyToID(name)] = value;
    public void SetMatrix(int nameID, Matrix4x4 value) => _properties[nameID] = value;
    public Matrix4x4 GetMatrix(string name) => GetMatrix(Shader.PropertyToID(name));
    public Matrix4x4 GetMatrix(int nameID) => _properties.TryGetValue(nameID, out var v) && v is Matrix4x4 m ? m : Matrix4x4.identity;

    public void SetTexture(string name, Texture value) => _properties[Shader.PropertyToID(name)] = value;
    public void SetTexture(int nameID, Texture value) => _properties[nameID] = value;
    public Texture? GetTexture(string name) => GetTexture(Shader.PropertyToID(name));
    public Texture? GetTexture(int nameID) => _properties.TryGetValue(nameID, out var v) && v is Texture t ? t : null;

    public void SetBuffer(string name, ComputeBuffer value) => _properties[Shader.PropertyToID(name)] = value;
    public void SetBuffer(int nameID, ComputeBuffer value) => _properties[nameID] = value;

    public bool HasProperty(string name) => HasProperty(Shader.PropertyToID(name));
    public bool HasProperty(int nameID) => _properties.ContainsKey(nameID);

    public void Clear() => _properties.Clear();

    public void CopySHCoefficientArraysFrom(float[]? coefficients) { if (coefficients != null) _shCoefficients = coefficients; }
    public void CopySHCoefficientArraysFrom(List<float>? coefficients) { if (coefficients != null) _shCoefficients = coefficients.ToArray(); }
    public void CopyProbeOcclusionArrayFrom(float[]? occlusionProbes) { if (occlusionProbes != null) _occlusionProbes = occlusionProbes; }
    public void CopyProbeOcclusionArrayFrom(List<float>? occlusionProbes) { if (occlusionProbes != null) _occlusionProbes = occlusionProbes.ToArray(); }

    internal void CopyFrom(MaterialPropertyBlock other)
    {
        _properties.Clear();
        foreach (var kvp in other._properties)
        {
            _properties[kvp.Key] = kvp.Value;
        }
    }
}

public class MeshFilter : Component
{
    private Mesh? _mesh;
    private Mesh? _sharedMesh;

    public Mesh? mesh
    {
        get => _mesh;
        set => _mesh = value;
    }

    public Mesh? sharedMesh
    {
        get => _sharedMesh;
        set => _sharedMesh = value;
    }
}

public class MeshRenderer : Renderer { }

public class SkinnedMeshRenderer : Renderer
{
    private Mesh? _sharedMesh;
    private Transform[] _bones = Array.Empty<Transform>();
    private Transform? _rootBone;
    private Bounds _localBounds;
    private bool _updateWhenOffscreen;
    private int _quality = 2;
    private bool _skinnedMotionVectors = true;
    private Vector3[] _vertices = Array.Empty<Vector3>();
    private Vector2[] _uvs = Array.Empty<Vector2>();
    private Vector3[] _normals = Array.Empty<Vector3>();
    private Renderer[] _mipMapFade = Array.Empty<Renderer>();

    public Mesh? sharedMesh
    {
        get => _sharedMesh;
        set => _sharedMesh = value;
    }

    public Transform[] bones
    {
        get => _bones;
        set => _bones = value ?? Array.Empty<Transform>();
    }

    public Transform? rootBone
    {
        get => _rootBone;
        set => _rootBone = value;
    }

    public new Bounds localBounds
    {
        get => _localBounds;
        set => _localBounds = value;
    }

    public bool updateWhenOffscreen
    {
        get => _updateWhenOffscreen;
        set => _updateWhenOffscreen = value;
    }

    public int quality
    {
        get => _quality;
        set => _quality = value;
    }

    public bool skinnedMotionVectors
    {
        get => _skinnedMotionVectors;
        set => _skinnedMotionVectors = value;
    }

    public Vector3[] vertices
    {
        get => _vertices;
        set => _vertices = value ?? Array.Empty<Vector3>();
    }

    public Vector2[] uvs
    {
        get => _uvs;
        set => _uvs = value ?? Array.Empty<Vector2>();
    }

    public Vector3[] normals
    {
        get => _normals;
        set => _normals = value ?? Array.Empty<Vector3>();
    }

    public void BakeMesh(Mesh mesh) { BakeMesh(mesh, true); }
    public void BakeMesh(Mesh mesh, bool useScale)
    {
        if (mesh != null)
        {
            mesh.Clear();
        }
    }
}

public class SpriteRenderer : Renderer
{
    private Sprite? _sprite;
    private Color _color = Color.white;
    private Vector2 _size;
    private bool _flipX;
    private bool _flipY;
    private SpriteDrawMode _drawMode;
    private SpriteTileMode _tileMode;

    public Sprite? sprite
    {
        get => _sprite;
        set => _sprite = value;
    }

    public Color color
    {
        get => _color;
        set => _color = value;
    }

    public Vector2 size
    {
        get => _size;
        set => _size = value;
    }

    public bool flipX
    {
        get => _flipX;
        set => _flipX = value;
    }

    public bool flipY
    {
        get => _flipY;
        set => _flipY = value;
    }

    public SpriteDrawMode drawMode
    {
        get => _drawMode;
        set => _drawMode = value;
    }

    public SpriteTileMode tileMode
    {
        get => _tileMode;
        set => _tileMode = value;
    }
}

public enum SpriteDrawMode
{
    Simple,
    Sliced,
    Tiled
}

public enum SpriteTileMode
{
    Continuous,
    Adaptive
}

public enum SpriteMaskInteraction
{
    None,
    VisibleInsideMask,
    VisibleOutsideMask
}

public partial class TrailRenderer : Renderer
{
    public float time { get; set; } = 5f;
    public float minVertexDistance { get; set; } = 0.1f;
    public bool autodestruct { get; set; }
    public AnimationCurve widthCurve { get; set; } = AnimationCurve.Linear(0f, 1f, 1f, 1f);
    public float widthMultiplier { get; set; } = 1f;
    public Gradient colorGradient { get; set; } = new Gradient();
    public int numCapVertices { get; set; }
    public int numCornerVertices { get; set; }
    public void Clear() { time = 0f; widthMultiplier = 1f; }
    public void AddPosition(Vector3 position) { _ = position; }
    public void Embed(Vector3 point) { AddPosition(point); }
    public Vector3 GetPosition(int index) => Vector3.zero;
    public float GetPosition(int index, out Vector3 position) { position = GetPosition(index); return 0f; }
    public int GetPositions(Vector3[] positions) => 0;
    public void SetPosition(int index, Vector3 position) { _ = index; _ = position; }
    public void SetPositions(Vector3[] positions, int count) { _ = positions; _ = count; }
    public void BakeMesh(Mesh mesh, bool useTransform = false) { mesh?.Clear(); }
}

public class LineRenderer : Renderer
{
    public int positionCount { get; set; }
    public bool useWorldSpace { get; set; } = true;
    public AnimationCurve widthCurve { get; set; } = AnimationCurve.Linear(0f, 1f, 1f, 1f);
    public float widthMultiplier { get; set; } = 1f;
    public bool loop { get; set; }
    public int numCapVertices { get; set; }
    public int numCornerVertices { get; set; }
    private readonly List<Vector3> _points = new();

    public void SetPosition(int index, Vector3 position)
    {
        while (_points.Count <= index) _points.Add(Vector3.zero);
        _points[index] = position;
        positionCount = _points.Count;
    }
    public void SetPositions(Vector3[] positions)
    {
        _points.Clear();
        if (positions != null) { _points.AddRange(positions); positionCount = positions.Length; }
    }
    public Vector3 GetPosition(int index) => index >= 0 && index < _points.Count ? _points[index] : Vector3.zero;
    public int GetPositions(Vector3[] positions)
    {
        int n = Mathf.Min(positions?.Length ?? 0, _points.Count);
        for (int i = 0; i < n; i++) positions[i] = _points[i];
        return n;
    }
    public void AddPosition(Vector3 position) { _points.Add(position); positionCount = _points.Count; }
    public void AddPositions(Vector3[] positions) { if (positions != null) { _points.AddRange(positions); positionCount = _points.Count; } }
    public void BakeMesh(Mesh mesh, bool useTransform = false) { mesh?.Clear(); }
}

public enum ShadowCastingMode
{
    Off,
    On,
    TwoSided,
    ShadowsOnly
}

public enum MotionVectorGenerationMode
{
    Camera,
    Object,
    ForceNoMotion
}

public enum LightProbeUsage
{
    Off,
    BlendProbes,
    UseProxyVolume,
    CustomProvided
}

public enum ReflectionProbeUsage
{
    Off,
    BlendProbes,
    BlendProbesAndSkybox,
    Simple
}
