using System;
using System.Collections.Generic;

namespace UnityEngine;

/// <summary>
/// Unity Renderer base class for all renderable components.
/// </summary>
[RequireComponent(typeof(Transform))]
public class Renderer : Component
{
    private bool _enabled = true;
    private Material? _sharedMaterial;
    private Material[] _sharedMaterials = Array.Empty<Material>();
    private Material? _material;
    private Material[] _materials = Array.Empty<Material>();
    private int _sortingLayerID;
    private string _sortingLayerName = string.Empty;
    private int _sortingOrder;
    private bool _motionVectors = true;
    private int _lightmapIndex = -1;
    private Vector4 _lightmapScaleOffset = new Vector4(1, 1, 0, 0);

    public bool enabled
    {
        get => _enabled;
        set => _enabled = value;
    }

    public bool isVisible { get; private set; } = true;

    public Bounds bounds { get; private set; } = new Bounds(Vector3.zero, Vector3.one);

    public Bounds localBounds { get; set; } = new Bounds(Vector3.zero, Vector3.one);

    public Material? material
    {
        get => _material;
        set => _material = value;
    }

    public Material[] materials
    {
        get => _materials;
        set => _materials = value ?? Array.Empty<Material>();
    }

    public Material? sharedMaterial
    {
        get => _sharedMaterial;
        set => _sharedMaterial = value;
    }

    public Material[] sharedMaterials
    {
        get => _sharedMaterials;
        set => _sharedMaterials = value ?? Array.Empty<Material>();
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

    public bool motionVectors
    {
        get => _motionVectors;
        set => _motionVectors = value;
    }

    public int lightmapIndex
    {
        get => _lightmapIndex;
        set => _lightmapIndex = value;
    }

    public Vector4 lightmapScaleOffset
    {
        get => _lightmapScaleOffset;
        set => _lightmapScaleOffset = value;
    }

    public bool isPartOfStaticBatch { get; }
    public bool hasLightProbeProxyVolume { get; }
    public Matrix4x4 worldToLocalMatrix => gameObject?.transform?.worldToLocalMatrix ?? Matrix4x4.identity;
    public Matrix4x4 localToWorldMatrix => gameObject?.transform?.localToWorldMatrix ?? Matrix4x4.identity;

    public void SetPropertyBlock(MaterialPropertyBlock properties) { }
    public void SetPropertyBlock(MaterialPropertyBlock properties, int materialIndex) { }
    public void GetPropertyBlock(MaterialPropertyBlock properties) { }
    public void GetPropertyBlock(MaterialPropertyBlock properties, int materialIndex) { }

    public Material[] GetSharedMaterials(List<Material> m)
    {
        m?.Clear();
        m?.AddRange(_sharedMaterials);
        return _sharedMaterials;
    }

    public void SetMaterials(List<Material> materials) { }

    public Material[] GetMaterials(List<Material> m)
    {
        m?.Clear();
        m?.AddRange(_materials);
        return _materials;
    }

    public void Render() { }
    public void Render(int materialPass) { }
}

/// <summary>
/// MaterialPropertyBlock for per-renderer material properties.
/// </summary>
public class MaterialPropertyBlock
{
    private readonly Dictionary<string, object> _properties = new();

    public bool isEmpty => _properties.Count == 0;

    public void SetFloat(string name, float value) => _properties[name] = value;
    public void SetFloat(int nameID, float value) => _properties[nameID.ToString()] = value;
    public float GetFloat(string name) => _properties.TryGetValue(name, out var v) && v is float f ? f : 0f;
    public float GetFloat(int nameID) => GetFloat(nameID.ToString());

    public void SetVector(string name, Vector4 value) => _properties[name] = value;
    public void SetVector(int nameID, Vector4 value) => _properties[nameID.ToString()] = value;
    public Vector4 GetVector(string name) => _properties.TryGetValue(name, out var v) && v is Vector4 vec ? vec : Vector4.zero;
    public Vector4 GetVector(int nameID) => GetVector(nameID.ToString());

    public void SetColor(string name, Color value) => _properties[name] = value;
    public void SetColor(int nameID, Color value) => _properties[nameID.ToString()] = value;
    public Color GetColor(string name) => _properties.TryGetValue(name, out var v) && v is Color c ? c : Color.clear;
    public Color GetColor(int nameID) => GetColor(nameID.ToString());

    public void SetMatrix(string name, Matrix4x4 value) => _properties[name] = value;
    public void SetMatrix(int nameID, Matrix4x4 value) => _properties[nameID.ToString()] = value;
    public Matrix4x4 GetMatrix(string name) => _properties.TryGetValue(name, out var v) && v is Matrix4x4 m ? m : Matrix4x4.identity;
    public Matrix4x4 GetMatrix(int nameID) => GetMatrix(nameID.ToString());

    public void SetTexture(string name, Texture value) => _properties[name] = value;
    public void SetTexture(int nameID, Texture value) => _properties[nameID.ToString()] = value;
    public Texture? GetTexture(string name) => _properties.TryGetValue(name, out var v) && v is Texture t ? t : null;
    public Texture? GetTexture(int nameID) => GetTexture(nameID.ToString());

    public void SetInt(string name, int value) => _properties[name] = value;
    public void SetInt(int nameID, int value) => _properties[nameID.ToString()] = value;
    public int GetInt(string name) => _properties.TryGetValue(name, out var v) && v is int i ? i : 0;
    public int GetInt(int nameID) => GetInt(nameID.ToString());

    public bool HasProperty(string name) => _properties.ContainsKey(name);
    public bool HasProperty(int nameID) => _properties.ContainsKey(nameID.ToString());

    public void Clear() => _properties.Clear();
}

/// <summary>
/// MeshFilter component for attaching meshes to GameObjects.
/// </summary>
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

/// <summary>
/// MeshRenderer component for rendering meshes.
/// </summary>
public class MeshRenderer : Renderer { }

/// <summary>
/// SkinnedMeshRenderer component for animated meshes.
/// </summary>
public class SkinnedMeshRenderer : Renderer
{
    private Mesh? _sharedMesh;
    private Transform[] _bones = Array.Empty<Transform>();
    private Transform? _rootBone;
    private Bounds _localBounds;
    private bool _updateWhenOffscreen;
    private int _quality = 2;
    private bool _skinnedMotionVectors = true;
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

    public Bounds localBounds
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

    public void BakeMesh(Mesh mesh) { }
    public void BakeMesh(Mesh mesh, bool useScale) { }
}

/// <summary>
/// CanvasRenderer component for UI rendering.
/// </summary>
public class CanvasRenderer : Component
{
    private Color _color = Color.white;
    private float _alpha = 1.0f;
    private bool _cull;
    private Material? _material;
    private Material? _popMaterial;
    private int _absoluteDepth;
    private bool _hasPopInstruction;
    public int materialCount { get; set; } = 1;
    public int popMaterialCount { get; set; }

    public Color GetColor() => _color;
    public void SetColor(Color color) => _color = color;

    public float GetAlpha() => _alpha;
    public void SetAlpha(float alpha) => _alpha = alpha;

    public bool cull
    {
        get => _cull;
        set => _cull = value;
    }

    public bool hasPopInstruction
    {
        get => _hasPopInstruction;
        set => _hasPopInstruction = value;
    }

    public Material? GetMaterial() => _material;
    public Material? GetMaterial(int index) => _material;
    public void SetMaterial(Material? material, int index = 0) => _material = material;

    public Material? GetPopMaterial() => _popMaterial;
    public Material? GetPopMaterial(int index) => _popMaterial;
    public void SetPopMaterial(Material? material, int index = 0) => _popMaterial = material;

    public int absoluteDepth => _absoluteDepth;
    public bool hasMoved { get; }

    public void SetAlphaTexture(Texture? texture) { }
    public void Clear() { }
    public void Layout() { }
    public void SetMaterial(Material? material, Texture? texture) { }
}
