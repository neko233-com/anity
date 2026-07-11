using System;
using System.Collections.Generic;

namespace UnityEngine;

public class Material : Object
{
    private readonly Dictionary<int, object?> _properties = new();
    private readonly HashSet<string> _keywords = new(StringComparer.OrdinalIgnoreCase);
    private Vector2 _mainTextureOffset;
    private Vector2 _mainTextureScale = Vector2.one;
    private int _currentPass;

    public Shader? shader { get; set; }
    public int renderQueue { get; set; } = -1;
    public MaterialGlobalIlluminationFlags globalIlluminationFlags { get; set; }

    public Color color
    {
        get => GetColor("_Color");
        set => SetColor("_Color", value);
    }

    public Color mainColor
    {
        get => color;
        set => color = value;
    }

    public Texture? mainTexture
    {
        get => GetTexture("_MainTex");
        set => SetTexture("_MainTex", value);
    }

    public Vector2 mainTextureOffset
    {
        get => _mainTextureOffset;
        set => _mainTextureOffset = value;
    }

    public Vector2 mainTextureScale
    {
        get => _mainTextureScale;
        set => _mainTextureScale = value;
    }

    public string[] shaderKeywords
    {
        get
        {
            var result = new string[_keywords.Count];
            _keywords.CopyTo(result);
            return result;
        }
        set
        {
            _keywords.Clear();
            if (value != null)
            {
                foreach (var keyword in value)
                {
                    _keywords.Add(keyword);
                }
            }
        }
    }

    public Material() : this(null as Shader) { }

    public Material(Shader? shader)
    {
        this.shader = shader;
        name = shader?.name ?? "Unnamed";
    }

    public Material(string shaderName) : this(Shader.Find(shaderName)) { }

    public Material(Material source)
    {
        if (source != null)
        {
            shader = source.shader;
            name = source.name;
            renderQueue = source.renderQueue;
            globalIlluminationFlags = source.globalIlluminationFlags;
            _mainTextureOffset = source._mainTextureOffset;
            _mainTextureScale = source._mainTextureScale;
            foreach (var keyword in source._keywords)
            {
                _keywords.Add(keyword);
            }
            foreach (var kvp in source._properties)
            {
                _properties[kvp.Key] = kvp.Value;
            }
        }
    }

    public float GetFloat(string name) => GetFloat(Shader.PropertyToID(name));
    public float GetFloat(int nameID) => _properties.TryGetValue(nameID, out var v) && v is float f ? f : 0f;
    public void SetFloat(string name, float value) => SetFloat(Shader.PropertyToID(name), value);
    public void SetFloat(int nameID, float value) => _properties[nameID] = value;

    public int GetInt(string name) => GetInt(Shader.PropertyToID(name));
    public int GetInt(int nameID) => _properties.TryGetValue(nameID, out var v) && v is int i ? i : 0;
    public void SetInt(string name, int value) => SetInt(Shader.PropertyToID(name), value);
    public void SetInt(int nameID, int value) => _properties[nameID] = value;

    public Vector4 GetVector(string name) => GetVector(Shader.PropertyToID(name));
    public Vector4 GetVector(int nameID) => _properties.TryGetValue(nameID, out var v) && v is Vector4 vec ? vec : Vector4.zero;
    public void SetVector(string name, Vector4 value) => SetVector(Shader.PropertyToID(name), value);
    public void SetVector(int nameID, Vector4 value) => _properties[nameID] = value;

    public Color GetColor(string name) => GetColor(Shader.PropertyToID(name));
    public Color GetColor(int nameID) => _properties.TryGetValue(nameID, out var v) && v is Color c ? c : Color.white;
    public void SetColor(string name, Color value) => SetColor(Shader.PropertyToID(name), value);
    public void SetColor(int nameID, Color value) => _properties[nameID] = value;

    public Matrix4x4 GetMatrix(string name) => GetMatrix(Shader.PropertyToID(name));
    public Matrix4x4 GetMatrix(int nameID) => _properties.TryGetValue(nameID, out var v) && v is Matrix4x4 m ? m : Matrix4x4.identity;
    public void SetMatrix(string name, Matrix4x4 value) => SetMatrix(Shader.PropertyToID(name), value);
    public void SetMatrix(int nameID, Matrix4x4 value) => _properties[nameID] = value;

    public Texture? GetTexture(string name) => GetTexture(Shader.PropertyToID(name));
    public Texture? GetTexture(int nameID) => _properties.TryGetValue(nameID, out var v) && v is Texture t ? t : null;
    public void SetTexture(string name, Texture? value) => SetTexture(Shader.PropertyToID(name), value);
    public void SetTexture(int nameID, Texture? value) => _properties[nameID] = value;

    public void SetBuffer(string name, ComputeBuffer value) => SetBuffer(Shader.PropertyToID(name), value);
    public void SetBuffer(int nameID, ComputeBuffer value) => _properties[nameID] = value;

    public void SetTextureOffset(string name, Vector2 value)
    {
        if (name == "_MainTex")
        {
            _mainTextureOffset = value;
        }
    }

    public Vector2 GetTextureOffset(string name)
    {
        return name == "_MainTex" ? _mainTextureOffset : Vector2.zero;
    }

    public void SetTextureScale(string name, Vector2 value)
    {
        if (name == "_MainTex")
        {
            _mainTextureScale = value;
        }
    }

    public Vector2 GetTextureScale(string name)
    {
        return name == "_MainTex" ? _mainTextureScale : Vector2.one;
    }

    public void EnableKeyword(string keyword) => _keywords.Add(keyword);
    public void DisableKeyword(string keyword) => _keywords.Remove(keyword);
    public bool IsKeywordEnabled(string keyword) => _keywords.Contains(keyword);
    public void SetKeyword(string keyword, bool value)
    {
        if (value) EnableKeyword(keyword);
        else DisableKeyword(keyword);
    }

    public void CopyPropertiesFromMaterial(Material mat)
    {
        if (mat == null) return;
        shader = mat.shader;
        renderQueue = mat.renderQueue;
        globalIlluminationFlags = mat.globalIlluminationFlags;
        _mainTextureOffset = mat._mainTextureOffset;
        _mainTextureScale = mat._mainTextureScale;
        _keywords.Clear();
        foreach (var kw in mat._keywords) _keywords.Add(kw);
        _properties.Clear();
        foreach (var kvp in mat._properties) _properties[kvp.Key] = kvp.Value;
    }

    public bool HasProperty(string name) => HasProperty(Shader.PropertyToID(name));
    public bool HasProperty(int nameID) => _properties.ContainsKey(nameID);
    public bool HasProperty(int nameID, MaterialPropertyType type) => HasProperty(nameID);

    public int FindProperty(string name) => Shader.PropertyToID(name);

    public string GetPassName(int pass) => pass == 0 ? "FORWARD" : string.Empty;
    public int passCount => shader?.passCount ?? 1;
    public void SetPass(int pass) { _currentPass = pass; }

    public string GetTag(string tag, bool searchFallbacks, string defaultValue) => defaultValue;
    public string GetTag(string tag, bool searchFallbacks) => string.Empty;

    public int GetTexturePropertyNameIDs(List<int> outNames) { return 0; }
    public int GetTexturePropertyNames(List<string> outNames) { return 0; }
}

public enum MaterialGlobalIlluminationFlags
{
    None = 0,
    RealtimeEmissive = 1,
    BakedEmissive = 2,
    AnyEmissive = RealtimeEmissive | BakedEmissive,
    EmissiveIsBlack = 4
}

public enum MaterialPropertyType
{
    Float,
    Int,
    Vector,
    Matrix,
    Texture,
    ConstantBuffer,
    ComputeBuffer
}
