using System;
using System.Collections.Generic;

namespace UnityEngine;

public class Shader
{
    private static readonly Dictionary<string, Shader> _cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<int, string> _propertyNames = new();
    private static readonly Dictionary<string, int> _propertyIDs = new();
    private static readonly Dictionary<int, object> _globalProperties = new();
    private static readonly Dictionary<int, Array> _globalArrays = new();
    private static readonly HashSet<string> _globalKeywords = new(StringComparer.OrdinalIgnoreCase);
    private static int _nextPropertyID = 1;
    private static readonly Dictionary<string, int> _tagCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<int, string> _tagNames = new();
    private static int _nextTagID = 1;

    public string name { get; set; }
    public int renderQueue { get; set; } = 2000;
    public int passCount { get; set; } = 1;
    public bool isSupported { get; set; } = true;
    public int maximumLOD { get; set; } = 600;
    public static int globalMaximumLOD { get; set; } = 600;
    public static bool globalKeywordsDirty { get; set; }
    public static bool warmupStarted { get; private set; }
    public static bool isWarmUpSupported { get; } = true;
    private static string _globalRenderPipeline = "UniversalPipeline";
    public static string globalRenderPipeline { get => _globalRenderPipeline; set => _globalRenderPipeline = value; }

    private Shader(string name)
    {
        this.name = name;
    }

    public static Shader Find(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        if (_cache.TryGetValue(name, out var cached))
            return cached;

        cached = new Shader(name);
        _cache[name] = cached;
        return cached;
    }

    public static Shader FindWithTag(string tagName, string tagValue)
    {
        return null;
    }

    public static int PropertyToID(string name)
    {
        if (string.IsNullOrEmpty(name)) return 0;
        if (_propertyIDs.TryGetValue(name, out var id))
            return id;

        id = _nextPropertyID++;
        _propertyIDs[name] = id;
        _propertyNames[id] = name;
        return id;
    }

    public static string GetPropertyName(int nameID)
    {
        return _propertyNames.TryGetValue(nameID, out var name) ? name : string.Empty;
    }

    public static object GetPropertyNameDefaultValue(int nameID)
    {
        return null;
    }

    public static int TagToID(string tagName)
    {
        if (string.IsNullOrEmpty(tagName)) return 0;
        if (_tagCache.TryGetValue(tagName, out var id))
            return id;

        id = _nextTagID++;
        _tagCache[tagName] = id;
        _tagNames[id] = tagName;
        return id;
    }

    public static string IDToTag(int tagID)
    {
        return _tagNames.TryGetValue(tagID, out var name) ? name : string.Empty;
    }

    public static void SetGlobalFloat(int nameID, float value) => _globalProperties[nameID] = value;
    public static void SetGlobalFloat(string name, float value) => SetGlobalFloat(PropertyToID(name), value);
    public static float GetGlobalFloat(string name) => GetGlobalFloat(PropertyToID(name));
    public static float GetGlobalFloat(int nameID) => _globalProperties.TryGetValue(nameID, out var v) && v is float f ? f : 0f;

    public static void SetGlobalInt(int nameID, int value) => _globalProperties[nameID] = value;
    public static void SetGlobalInt(string name, int value) => SetGlobalInt(PropertyToID(name), value);
    public static int GetGlobalInt(string name) => GetGlobalInt(PropertyToID(name));
    public static int GetGlobalInt(int nameID) => _globalProperties.TryGetValue(nameID, out var v) && v is int i ? i : 0;

    public static void SetGlobalColor(int nameID, Color value) => _globalProperties[nameID] = value;
    public static void SetGlobalColor(string name, Color value) => SetGlobalColor(PropertyToID(name), value);
    public static Color GetGlobalColor(string name) => GetGlobalColor(PropertyToID(name));
    public static Color GetGlobalColor(int nameID) => _globalProperties.TryGetValue(nameID, out var v) && v is Color c ? c : Color.white;

    public static void SetGlobalVector(int nameID, Vector4 value) => _globalProperties[nameID] = value;
    public static void SetGlobalVector(string name, Vector4 value) => SetGlobalVector(PropertyToID(name), value);
    public static Vector4 GetGlobalVector(string name) => GetGlobalVector(PropertyToID(name));
    public static Vector4 GetGlobalVector(int nameID) => _globalProperties.TryGetValue(nameID, out var v) && v is Vector4 vec ? vec : Vector4.zero;

    public static void SetGlobalMatrix(int nameID, Matrix4x4 value) => _globalProperties[nameID] = value;
    public static void SetGlobalMatrix(string name, Matrix4x4 value) => SetGlobalMatrix(PropertyToID(name), value);
    public static Matrix4x4 GetGlobalMatrix(string name) => GetGlobalMatrix(PropertyToID(name));
    public static Matrix4x4 GetGlobalMatrix(int nameID) => _globalProperties.TryGetValue(nameID, out var v) && v is Matrix4x4 m ? m : Matrix4x4.identity;

    public static void SetGlobalTexture(int nameID, Texture value) => _globalProperties[nameID] = value;
    public static void SetGlobalTexture(string name, Texture value) => SetGlobalTexture(PropertyToID(name), value);
    public static Texture GetGlobalTexture(string name) => GetGlobalTexture(PropertyToID(name));
    public static Texture GetGlobalTexture(int nameID) => _globalProperties.TryGetValue(nameID, out var v) && v is Texture t ? t : null;

    public static void SetGlobalFloatArray(int nameID, float[] values) { if (values != null) _globalArrays[nameID] = (float[])values.Clone(); else _globalArrays.Remove(nameID); }
    public static void SetGlobalFloatArray(string name, float[] values) => SetGlobalFloatArray(PropertyToID(name), values);
    public static void SetGlobalFloatArray(int nameID, List<float> values) { if (values != null) _globalArrays[nameID] = values.ToArray(); else _globalArrays.Remove(nameID); }
    public static void SetGlobalFloatArray(string name, List<float> values) => SetGlobalFloatArray(PropertyToID(name), values);
    public static float[] GetGlobalFloatArray(int nameID) => _globalArrays.TryGetValue(nameID, out var arr) && arr is float[] f ? (float[])f.Clone() : Array.Empty<float>();
    public static float[] GetGlobalFloatArray(string name) => GetGlobalFloatArray(PropertyToID(name));
    public static void GetGlobalFloatArray(int nameID, List<float> values) { values?.Clear(); var arr = GetGlobalFloatArray(nameID); values?.AddRange(arr); }
    public static void GetGlobalFloatArray(string name, List<float> values) => GetGlobalFloatArray(PropertyToID(name), values);

    public static void SetGlobalVectorArray(int nameID, Vector4[] values) { if (values != null) _globalArrays[nameID] = (Vector4[])values.Clone(); else _globalArrays.Remove(nameID); }
    public static void SetGlobalVectorArray(string name, Vector4[] values) => SetGlobalVectorArray(PropertyToID(name), values);
    public static void SetGlobalVectorArray(int nameID, List<Vector4> values) { if (values != null) _globalArrays[nameID] = values.ToArray(); else _globalArrays.Remove(nameID); }
    public static void SetGlobalVectorArray(string name, List<Vector4> values) => SetGlobalVectorArray(PropertyToID(name), values);
    public static Vector4[] GetGlobalVectorArray(int nameID) => _globalArrays.TryGetValue(nameID, out var arr) && arr is Vector4[] v ? (Vector4[])v.Clone() : Array.Empty<Vector4>();
    public static Vector4[] GetGlobalVectorArray(string name) => GetGlobalVectorArray(PropertyToID(name));
    public static void GetGlobalVectorArray(int nameID, List<Vector4> values) { values?.Clear(); var arr = GetGlobalVectorArray(nameID); values?.AddRange(arr); }
    public static void GetGlobalVectorArray(string name, List<Vector4> values) => GetGlobalVectorArray(PropertyToID(name), values);

    public static void SetGlobalMatrixArray(int nameID, Matrix4x4[] values) { if (values != null) _globalArrays[nameID] = (Matrix4x4[])values.Clone(); else _globalArrays.Remove(nameID); }
    public static void SetGlobalMatrixArray(string name, Matrix4x4[] values) => SetGlobalMatrixArray(PropertyToID(name), values);
    public static void SetGlobalMatrixArray(int nameID, List<Matrix4x4> values) { if (values != null) _globalArrays[nameID] = values.ToArray(); else _globalArrays.Remove(nameID); }
    public static void SetGlobalMatrixArray(string name, List<Matrix4x4> values) => SetGlobalMatrixArray(PropertyToID(name), values);
    public static Matrix4x4[] GetGlobalMatrixArray(int nameID) => _globalArrays.TryGetValue(nameID, out var arr) && arr is Matrix4x4[] m ? (Matrix4x4[])m.Clone() : Array.Empty<Matrix4x4>();
    public static Matrix4x4[] GetGlobalMatrixArray(string name) => GetGlobalMatrixArray(PropertyToID(name));
    public static void GetGlobalMatrixArray(int nameID, List<Matrix4x4> values) { values?.Clear(); var arr = GetGlobalMatrixArray(nameID); values?.AddRange(arr); }
    public static void GetGlobalMatrixArray(string name, List<Matrix4x4> values) => GetGlobalMatrixArray(PropertyToID(name), values);

    public static void EnableKeyword(string keyword) => _globalKeywords.Add(keyword);
    public static void DisableKeyword(string keyword) => _globalKeywords.Remove(keyword);
    public static bool IsKeywordEnabled(string keyword) => _globalKeywords.Contains(keyword);
    public static void SetKeyword(string keyword, bool value)
    {
        if (value) EnableKeyword(keyword);
        else DisableKeyword(keyword);
    }

    public static void WarmupAllShaders() { warmupStarted = true; }
    private static string _surfaceShaderStatus = "unparsed";
    internal static void ParseSurfaceShaders() { _surfaceShaderStatus = "parsed"; }
    public static string FindPassName(int passNameHash) => string.Empty;

    public object GetProperty(string name)
    {
        _globalProperties.TryGetValue(PropertyToID(name), out var val);
        return val;
    }

    public void SetProperty(string name, object value)
    {
        _globalProperties[PropertyToID(name)] = value;
    }

    public bool HasProperty(string propertyName)
    {
        return _propertyIDs.ContainsKey(propertyName);
    }

    public int FindPropertyIndex(string propertyName)
    {
        return _propertyIDs.TryGetValue(propertyName, out var id) ? id : -1;
    }

    public ShaderPropertyType GetPropertyType(int propertyIndex)
    {
        return ShaderPropertyType.Color;
    }
}

public enum ShaderPropertyType
{
    Color,
    Vector,
    Float,
    Range,
    Texture,
    Int
}

public enum RenderQueue
{
    Background = 1000,
    Geometry = 2000,
    AlphaTest = 2450,
    Transparent = 3000,
    Overlay = 4000
}
