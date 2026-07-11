using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Rendering;

namespace UnityEngine;

public class Material : Object
{
    private Shader? _shader;
    private readonly Dictionary<int, object> _properties = new();
    private readonly Dictionary<int, Vector2> _textureOffsets = new();
    private readonly Dictionary<int, Vector2> _textureScales = new();
    private readonly Dictionary<int, Array> _arrays = new();
    private readonly Dictionary<int, ComputeBuffer> _buffers = new();
    private readonly HashSet<string> _keywords = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _tags = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, float> _floats = new();
    private readonly Dictionary<string, Color> _colors = new();
    private int _currentPass;
    private Color _color = Color.white;
    private Texture? _mainTexture;
    private Vector2 _mainTextureOffset;
    private Vector2 _mainTextureScale = Vector2.one;
    private bool _enableInstancing;
    private bool _doubleSidedGI;
    private MaterialGlobalIlluminationFlags _globalIlluminationFlags;
    private MaterialPropertyBlock? _propertyBlock;
    private HideFlags _hideFlags;

    public Shader? shader
    {
        get => _shader;
        set
        {
            _shader = value;
            if (value != null && renderQueue == -1)
                renderQueue = value.renderQueue;
            ApplyShaderDefaults();
        }
    }

    public int renderQueue { get; set; } = -1;
    public MaterialGlobalIlluminationFlags globalIlluminationFlags
    {
        get => _globalIlluminationFlags;
        set => _globalIlluminationFlags = value;
    }
    public bool enableInstancing { get => _enableInstancing || shader?.isInstancingSupported == true; set => _enableInstancing = value; }
    public bool doubleSidedGI { get => _doubleSidedGI; set => _doubleSidedGI = value; }
    public HideFlags hideFlags { get => _hideFlags; set => _hideFlags = value; }
    public string[] shaderKeywords
    {
        get => _keywords.ToArray();
        set
        {
            _keywords.Clear();
            if (value != null)
                foreach (var kw in value)
                    if (!string.IsNullOrEmpty(kw)) _keywords.Add(kw);
        }
    }
    public Color color
    {
        get => GetColor("_Color");
        set => SetColor("_Color", value);
    }
    public Color mainColor
    {
        get => GetColor("_BaseColor");
        set => SetColor("_BaseColor", value);
    }
    public Texture? mainTexture
    {
        get => GetTexture("_MainTex");
        set => SetTexture("_MainTex", value);
    }
    public Vector2 mainTextureOffset
    {
        get => GetTextureOffset("_MainTex");
        set => SetTextureOffset("_MainTex", value);
    }
    public Vector2 mainTextureScale
    {
        get => GetTextureScale("_MainTex");
        set => SetTextureScale("_MainTex", value);
    }
    public int passCount => shader?.passCount ?? 1;
    public int subshaderCount => shader?.subshaderCount ?? 1;
    public string name => shader?.name ?? "Material";

    public Material() : this((Shader?)null) { }

    public Material(Shader? shader)
    {
        this.shader = shader ?? Shader.Find("Universal Render Pipeline/Lit");
        renderQueue = this.shader?.renderQueue ?? 2000;
    }

    public Material(string shaderName) : this(Shader.Find(shaderName)) { }

    public Material(Material source)
    {
        if (source == null) { shader = Shader.Find("Universal Render Pipeline/Lit"); return; }
        shader = source.shader;
        renderQueue = source.renderQueue;
        _globalIlluminationFlags = source._globalIlluminationFlags;
        _enableInstancing = source._enableInstancing;
        _doubleSidedGI = source._doubleSidedGI;
        foreach (var kv in source._properties) _properties[kv.Key] = kv.Value;
        foreach (var kv in source._textureOffsets) _textureOffsets[kv.Key] = kv.Value;
        foreach (var kv in source._textureScales) _textureScales[kv.Key] = kv.Value;
        foreach (var kv in source._arrays) _arrays[kv.Key] = (Array)kv.Value.Clone();
        foreach (var kw in source._keywords) _keywords.Add(kw);
        foreach (var kv in source._tags) _tags[kv.Key] = kv.Value;
    }

    private void ApplyShaderDefaults()
    {
        if (_shader == null) return;
        foreach (var prop in _shader.properties)
        {
            if (!_properties.ContainsKey(prop.nameID) && prop.defaultValue != null)
                _properties[prop.nameID] = prop.defaultValue;
        }
    }

    public float GetFloat(string name) => GetFloat(Shader.PropertyToID(name));
    public float GetFloat(int nameID) => _properties.TryGetValue(nameID, out var v) ? v is float f ? f : v is int i ? i : 0f : 0f;
    public void SetFloat(string name, float value) => SetFloat(Shader.PropertyToID(name), value);
    public void SetFloat(int nameID, float value)
    {
        _properties[nameID] = value;
        if (nameID == Shader.PropertyToID("_Mode")) SetRenderingMode(value);
    }

    public int GetInt(string name) => GetInt(Shader.PropertyToID(name));
    public int GetInt(int nameID) => _properties.TryGetValue(nameID, out var v) ? v is int i ? i : v is float f ? (int)f : 0 : 0;
    public void SetInt(string name, int value) => SetInt(Shader.PropertyToID(name), value);
    public void SetInt(int nameID, int value) => _properties[nameID] = value;

    public bool GetBool(string name) => GetInt(name) != 0;
    public bool GetBool(int nameID) => GetInt(nameID) != 0;
    public void SetBool(string name, bool value) => SetInt(name, value ? 1 : 0);
    public void SetBool(int nameID, bool value) => SetInt(nameID, value ? 1 : 0);

    public Vector4 GetVector(string name) => GetVector(Shader.PropertyToID(name));
    public Vector4 GetVector(int nameID) => _properties.TryGetValue(nameID, out var v) ? v is Vector4 vec ? vec : v is Color c ? (Vector4)c : Vector4.zero : Vector4.zero;
    public void SetVector(string name, Vector4 value) => SetVector(Shader.PropertyToID(name), value);
    public void SetVector(int nameID, Vector4 value) => _properties[nameID] = value;

    public Color GetColor(string name) => GetColor(Shader.PropertyToID(name));
    public Color GetColor(int nameID)
    {
        if (_properties.TryGetValue(nameID, out var v))
        {
            if (v is Color c) return c;
            if (v is Vector4 vec) return new Color(vec.x, vec.y, vec.z, vec.w);
        }
        return nameID == Shader.PropertyToID("_BaseColor") || nameID == Shader.PropertyToID("_Color") ? Color.white : Color.clear;
    }
    public void SetColor(string name, Color value) => SetColor(Shader.PropertyToID(name), value);
    public void SetColor(int nameID, Color value) => _properties[nameID] = value;

    public Matrix4x4 GetMatrix(string name) => GetMatrix(Shader.PropertyToID(name));
    public Matrix4x4 GetMatrix(int nameID) => _properties.TryGetValue(nameID, out var v) && v is Matrix4x4 m ? m : Matrix4x4.identity;
    public void SetMatrix(string name, Matrix4x4 value) => SetMatrix(Shader.PropertyToID(name), value);
    public void SetMatrix(int nameID, Matrix4x4 value) => _properties[nameID] = value;

    public Texture? GetTexture(string name) => GetTexture(Shader.PropertyToID(name));
    public Texture? GetTexture(int nameID) => _properties.TryGetValue(nameID, out var v) && v is Texture t ? t : null;
    public void SetTexture(string name, Texture? value) => SetTexture(Shader.PropertyToID(name), value);
    public void SetTexture(int nameID, Texture? value) { if (value != null) _properties[nameID] = value; else _properties.Remove(nameID); }

    public void SetTexture(string name, RenderTexture value, Rendering.RenderTextureSubElement element) => SetTexture(name, value);

    public void SetBuffer(string name, ComputeBuffer value) => SetBuffer(Shader.PropertyToID(name), value);
    public void SetBuffer(int nameID, ComputeBuffer value) { if (value != null) _buffers[nameID] = value; else _buffers.Remove(nameID); }
    public ComputeBuffer GetBuffer(string name) => GetBuffer(Shader.PropertyToID(name));
    public ComputeBuffer GetBuffer(int nameID) => _buffers.TryGetValue(nameID, out var b) ? b : null;

    public void SetFloatArray(string name, float[] values) => SetFloatArray(Shader.PropertyToID(name), values);
    public void SetFloatArray(int nameID, float[] values) { if (values != null) _arrays[nameID] = (float[])values.Clone(); else _arrays.Remove(nameID); }
    public void SetFloatArray(string name, List<float> values) => SetFloatArray(Shader.PropertyToID(name), values);
    public void SetFloatArray(int nameID, List<float> values) { if (values != null) _arrays[nameID] = values.ToArray(); else _arrays.Remove(nameID); }
    public float[] GetFloatArray(string name) => GetFloatArray(Shader.PropertyToID(name));
    public float[] GetFloatArray(int nameID) => _arrays.TryGetValue(nameID, out var arr) && arr is float[] f ? (float[])f.Clone() : Array.Empty<float>();
    public void GetFloatArray(int nameID, List<float> values) { values?.Clear(); var arr = GetFloatArray(nameID); values?.AddRange(arr); }

    public void SetVectorArray(string name, Vector4[] values) => SetVectorArray(Shader.PropertyToID(name), values);
    public void SetVectorArray(int nameID, Vector4[] values) { if (values != null) _arrays[nameID] = (Vector4[])values.Clone(); else _arrays.Remove(nameID); }
    public void SetVectorArray(string name, List<Vector4> values) => SetVectorArray(Shader.PropertyToID(name), values);
    public void SetVectorArray(int nameID, List<Vector4> values) { if (values != null) _arrays[nameID] = values.ToArray(); else _arrays.Remove(nameID); }
    public Vector4[] GetVectorArray(string name) => GetVectorArray(Shader.PropertyToID(name));
    public Vector4[] GetVectorArray(int nameID) => _arrays.TryGetValue(nameID, out var arr) && arr is Vector4[] v ? (Vector4[])v.Clone() : Array.Empty<Vector4>();
    public void GetVectorArray(int nameID, List<Vector4> values) { values?.Clear(); var arr = GetVectorArray(nameID); values?.AddRange(arr); }

    public void SetMatrixArray(string name, Matrix4x4[] values) => SetMatrixArray(Shader.PropertyToID(name), values);
    public void SetMatrixArray(int nameID, Matrix4x4[] values) { if (values != null) _arrays[nameID] = (Matrix4x4[])values.Clone(); else _arrays.Remove(nameID); }
    public void SetMatrixArray(string name, List<Matrix4x4> values) => SetMatrixArray(Shader.PropertyToID(name), values);
    public void SetMatrixArray(int nameID, List<Matrix4x4> values) { if (values != null) _arrays[nameID] = values.ToArray(); else _arrays.Remove(nameID); }
    public Matrix4x4[] GetMatrixArray(string name) => GetMatrixArray(Shader.PropertyToID(name));
    public Matrix4x4[] GetMatrixArray(int nameID) => _arrays.TryGetValue(nameID, out var arr) && arr is Matrix4x4[] m ? (Matrix4x4[])m.Clone() : Array.Empty<Matrix4x4>();
    public void GetMatrixArray(int nameID, List<Matrix4x4> values) { values?.Clear(); var arr = GetMatrixArray(nameID); values?.AddRange(arr); }

    public void SetTextureOffset(string name, Vector2 value) => _textureOffsets[Shader.PropertyToID(name)] = value;
    public Vector2 GetTextureOffset(string name) => _textureOffsets.TryGetValue(Shader.PropertyToID(name), out var v) ? v : Vector2.zero;
    public void SetTextureScale(string name, Vector2 value) => _textureScales[Shader.PropertyToID(name)] = value;
    public Vector2 GetTextureScale(string name) => _textureScales.TryGetValue(Shader.PropertyToID(name), out var v) ? v : Vector2.one;

    public void SetTextureOffset(int nameID, Vector2 value) => _textureOffsets[nameID] = value;
    public Vector2 GetTextureOffset(int nameID) => _textureOffsets.TryGetValue(nameID, out var v) ? v : Vector2.zero;
    public void SetTextureScale(int nameID, Vector2 value) => _textureScales[nameID] = value;
    public Vector2 GetTextureScale(int nameID) => _textureScales.TryGetValue(nameID, out var v) ? v : Vector2.one;

    public void EnableKeyword(string keyword) { _keywords.Add(keyword); Shader.EnableKeyword(keyword); }
    public void DisableKeyword(string keyword) { _keywords.Remove(keyword); Shader.DisableKeyword(keyword); }
    public bool IsKeywordEnabled(string keyword) => _keywords.Contains(keyword) || Shader.IsKeywordEnabled(keyword);
    public void SetKeyword(string keyword, bool value) { if (value) EnableKeyword(keyword); else DisableKeyword(keyword); }
    public void SetKeyword(in GlobalKeyword keyword, bool value) => SetKeyword(keyword.name, value);
    public void EnableKeyword(in GlobalKeyword keyword) => EnableKeyword(keyword.name);
    public void DisableKeyword(in GlobalKeyword keyword) => DisableKeyword(keyword.name);
    public bool IsKeywordEnabled(in GlobalKeyword keyword) => IsKeywordEnabled(keyword.name);

    public void CopyPropertiesFromMaterial(Material mat)
    {
        if (mat == null) return;
        shader = mat.shader;
        renderQueue = mat.renderQueue;
        _properties.Clear();
        foreach (var kv in mat._properties) _properties[kv.Key] = kv.Value;
        _textureOffsets.Clear();
        foreach (var kv in mat._textureOffsets) _textureOffsets[kv.Key] = kv.Value;
        _textureScales.Clear();
        foreach (var kv in mat._textureScales) _textureScales[kv.Key] = kv.Value;
        _arrays.Clear();
        foreach (var kv in mat._arrays) _arrays[kv.Key] = (Array)kv.Value.Clone();
        _keywords.Clear();
        foreach (var kw in mat._keywords) _keywords.Add(kw);
    }

    public void CopyPropertiesFromMaterial(Material mat, Type type) => CopyPropertiesFromMaterial(mat);

    public bool HasProperty(string name) => HasProperty(Shader.PropertyToID(name));
    public bool HasProperty(int nameID)
    {
        if (_properties.ContainsKey(nameID)) return true;
        if (_textureOffsets.ContainsKey(nameID) || _textureScales.ContainsKey(nameID)) return true;
        return shader?.HasProperty(nameID) ?? false;
    }
    public bool HasProperty(int nameID, MaterialPropertyType type) => HasProperty(nameID);

    public int FindProperty(string name) => Shader.PropertyToID(name);
    public string GetPassName(int pass) => pass == 0 ? "FORWARD" : $"PASS_{pass}";
    public int FindPass(string passName) => string.Equals(passName, "FORWARD", StringComparison.OrdinalIgnoreCase) || string.Equals(passName, "UniversalForward", StringComparison.OrdinalIgnoreCase) ? 0 : -1;
    public void SetPass(int pass) { _currentPass = pass; }
    public void SetOverrideTag(string tag, string val) { _tags[tag] = val; }
    public string GetTag(string tag, bool searchFallbacks, string defaultValue) => _tags.TryGetValue(tag, out var v) ? v : shader?.GetTag(tag, searchFallbacks, defaultValue) ?? defaultValue;
    public string GetTag(string tag, bool searchFallbacks) => GetTag(tag, searchFallbacks, string.Empty);

    public int GetTexturePropertyNameIDs(List<int> outNames)
    {
        outNames?.Clear();
        int count = 0;
        if (shader != null)
        {
            foreach (var p in shader.properties)
                if (p.type == ShaderPropertyType.Texture)
                { outNames?.Add(p.nameID); count++; }
        }
        return count;
    }

    public int GetTexturePropertyNames(List<string> outNames)
    {
        outNames?.Clear();
        int count = 0;
        if (shader != null)
        {
            foreach (var p in shader.properties)
                if (p.type == ShaderPropertyType.Texture)
                { outNames?.Add(p.name); count++; }
        }
        return count;
    }

    public Shader GetShader() => shader;
    public void SetShader(Shader s) => shader = s;
    public int GetPropertyCount() => shader?.GetPropertyCount() ?? 0;
    public string GetPropertyName(int propertyIndex) { if (shader != null && propertyIndex >= 0 && propertyIndex < shader.properties.Count) return shader.properties[propertyIndex].name; return string.Empty; }
    public ShaderPropertyType GetPropertyType(int propertyIndex) => shader?.GetPropertyType(propertyIndex) ?? ShaderPropertyType.Float;
    public ShaderPropertyFlags GetPropertyFlags(int propertyIndex) => shader?.GetPropertyFlags(propertyIndex) ?? ShaderPropertyFlags.None;
    public void SetPropertyFlags(int propertyIndex, ShaderPropertyFlags flags) => shader?.SetPropertyFlags(propertyIndex, flags);
    public void GetPropertyRangeLimits(int propertyIndex, out float min, out float max) { min = 0f; max = 1f; shader?.GetPropertyRangeLimits(propertyIndex, out min, out max); }
    public Vector2 GetPropertyTextureDimension(int propertyIndex) => shader?.GetPropertyTextureDimension(propertyIndex) ?? Vector2.zero;
    public string GetPropertyTextureDefaultName(int propertyIndex) => shader?.GetPropertyTextureDefaultName(propertyIndex) ?? "white";
    public string GetPropertyDescription(int propertyIndex) => shader?.GetPropertyDescription(propertyIndex) ?? string.Empty;

    public void SetFloat(int nameID, List<float> values) => SetFloatArray(nameID, values);
    public void SetVector(int nameID, List<Vector4> values) => SetVectorArray(nameID, values);
    public void SetColor(int nameID, List<Color> values) => SetVectorArray(nameID, values?.Select(c => (Vector4)c).ToList());
    public void SetMatrix(int nameID, List<Matrix4x4> values) => SetMatrixArray(nameID, values);

    public void SetInt(int nameID, List<int> values) { if (values != null) _arrays[nameID] = values.Select(v => (float)v).ToArray(); else _arrays.Remove(nameID); }

    public void SetRenderingMode(float mode)
    {
        switch ((int)mode)
        {
            case 0: SetInt("_SrcBlend", (int)BlendMode.One); SetInt("_DstBlend", (int)BlendMode.Zero); SetInt("_ZWrite", 1); renderQueue = (int)RenderQueue.Geometry; break;
            case 1: SetInt("_SrcBlend", (int)BlendMode.SrcAlpha); SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha); SetInt("_ZWrite", 0); renderQueue = (int)RenderQueue.Transparent; break;
            case 2: SetInt("_SrcBlend", (int)BlendMode.One); SetInt("_DstBlend", (int)BlendMode.Zero); SetInt("_ZWrite", 1); renderQueue = (int)RenderQueue.AlphaTest; break;
            case 3: SetInt("_SrcBlend", (int)BlendMode.One); SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha); SetInt("_ZWrite", 0); renderQueue = (int)RenderQueue.Transparent; break;
        }
    }

    public void Lerp(Material start, Material end, float t)
    {
        if (start == null || end == null) return;
        foreach (var prop in shader?.properties ?? Array.Empty<ShaderProperty>())
        {
            switch (prop.type)
            {
                case ShaderPropertyType.Float:
                case ShaderPropertyType.Range:
                    SetFloat(prop.nameID, Mathf.Lerp(start.GetFloat(prop.nameID), end.GetFloat(prop.nameID), t));
                    break;
                case ShaderPropertyType.Color:
                    SetColor(prop.nameID, Color.Lerp(start.GetColor(prop.nameID), end.GetColor(prop.nameID), t));
                    break;
                case ShaderPropertyType.Vector:
                    var sv = start.GetVector(prop.nameID); var ev = end.GetVector(prop.nameID);
                    SetVector(prop.nameID, Vector4.Lerp(sv, ev, t));
                    break;
            }
        }
    }

    public void SetBuffer(string name, GraphicsBuffer value) => SetBuffer(Shader.PropertyToID(name), value);
    public void SetBuffer(int nameID, GraphicsBuffer value) { if (value != null) _buffers[nameID] = null; }
    public GraphicsBuffer GetGraphicsBuffer(int nameID) => null;
    public GraphicsBuffer GetGraphicsBuffer(string name) => GetGraphicsBuffer(Shader.PropertyToID(name));

    public void SetInstancingBuffer(ComputeBuffer buffer) { }
    public bool HasInstancingVariant => enableInstancing;

    public int GetPass(int pass) => pass >= 0 && pass < passCount ? pass : -1;
    public void SetShaderPassNames(string[] names) { }

    public void SetTexture(int nameID, RenderTexture value, Rendering.RenderTextureSubElement element) => SetTexture(nameID, value);
    public void SetTexture(string name, Texture value, Rendering.RenderTextureSubElement element) => SetTexture(name, value);

    public void SetColor(string name, List<Color> values) => SetColor(Shader.PropertyToID(name), values);
    public void SetColorArray(int nameID, Color[] values) => SetVectorArray(nameID, values?.Select(c => (Vector4)c).ToArray());
    public void SetColorArray(string name, Color[] values) => SetColorArray(Shader.PropertyToID(name), values);
    public void SetColorArray(int nameID, List<Color> values) => SetVectorArray(nameID, values?.Select(c => (Vector4)c).ToList());
    public void SetColorArray(string name, List<Color> values) => SetColorArray(Shader.PropertyToID(name), values);
    public Color[] GetColorArray(int nameID) => GetVectorArray(nameID).Select(v => (Color)v).ToArray();
    public Color[] GetColorArray(string name) => GetColorArray(Shader.PropertyToID(name));
    public void GetColorArray(int nameID, List<Color> values) { values?.Clear(); foreach (var v in GetVectorArray(nameID)) values?.Add((Color)v); }
    public void GetColorArray(string name, List<Color> values) => GetColorArray(Shader.PropertyToID(name), values);

    public void SetIntArray(int nameID, int[] values) { if (values != null) _arrays[nameID] = values.Select(v => (float)v).ToArray(); else _arrays.Remove(nameID); }
    public void SetIntArray(string name, int[] values) => SetIntArray(Shader.PropertyToID(name), values);
    public void SetIntArray(int nameID, List<int> values) { if (values != null) _arrays[nameID] = values.Select(v => (float)v).ToArray(); else _arrays.Remove(nameID); }
    public void SetIntArray(string name, List<int> values) => SetIntArray(Shader.PropertyToID(name), values);

    public int GetInt(int nameID, int defaultValue) => _properties.TryGetValue(nameID, out var v) ? v is int i ? i : v is float f ? (int)f : defaultValue : defaultValue;
    public float GetFloat(int nameID, float defaultValue) => _properties.TryGetValue(nameID, out var v) ? v is float f ? f : v is int i ? i : defaultValue : defaultValue;

    public void SetRenderQueue(RenderQueue queue) => renderQueue = (int)queue;
    public RenderQueue GetRenderQueue() => (RenderQueue)renderQueue;

    public IReadOnlyDictionary<string, string> TagMap => _tags;
    public int currentPass => _currentPass;
}

public enum MaterialGlobalIlluminationFlags
{
    None = 0,
    RealtimeEmissive = 1,
    BakedEmissive = 2,
    EmissiveIsBlack = 4,
    AnyEmissive = RealtimeEmissive | BakedEmissive
}

public enum MaterialPropertyType
{
    Float = 0,
    Int = 1,
    Vector = 2,
    Matrix = 3,
    Texture = 4,
    Color = -1,
    ConstantBuffer = 5,
    ComputeBuffer = 6
}
