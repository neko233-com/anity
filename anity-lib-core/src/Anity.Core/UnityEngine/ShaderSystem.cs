using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Unity.Collections;
using UnityEditor;
using UnityEngine.Rendering;

namespace UnityEngine;

public class Shader : Object
{
    private static readonly Dictionary<string, Shader> _cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<int, string> _propertyNames = new();
    private static readonly Dictionary<string, int> _propertyIDs = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<int, object> _globalProperties = new();
    private static readonly Dictionary<int, Array> _globalArrays = new();
    private static readonly HashSet<string> _globalKeywords = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<int, ComputeBuffer> _globalBuffers = new();
    private static readonly Dictionary<int, (ComputeBuffer buffer, int offset, int size)> _globalConstantBuffersMap = new();
    private static int _nextPropertyID = 1;
    private static readonly Dictionary<string, int> _tagCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<int, string> _tagNames = new();
    private static int _nextTagID = 1;
    private static string _globalRenderPipeline = "UniversalPipeline";
    private static readonly List<Shader> _allShaders = new();
    internal static readonly Dictionary<int, GlobalKeyword> _globalKeywordObjects = new();
    private static readonly List<ConstantBuffer> _globalConstantBuffers = new();

    public string name { get; set; }
    public int renderQueue { get; set; } = (int)RenderQueue.Geometry;
    public int passCount => _passes.Count;
    public int subshaderCount => _subShaders.Count;
    public bool isSupported { get; set; } = true;
    public int maximumLOD { get; set; } = 600;
    public static int globalMaximumLOD { get; set; } = 600;
    public static bool globalKeywordsDirty { get; set; }
    public static bool warmupStarted { get; private set; }
    public static bool isWarmUpSupported { get; } = true;
    public static string globalRenderPipeline { get => _globalRenderPipeline; set => _globalRenderPipeline = value; }
    public string sourceCode { get; private set; } = string.Empty;
    public bool isInstancingSupported { get; private set; }
    public bool isSRPBatcherCompatible { get; private set; } = true;
    public string[] keywordSpace => _keywords.ToArray();
    public IReadOnlyList<ShaderProperty> properties => _properties;
    public IReadOnlyList<ShaderPass> passes => _passes;
    public IReadOnlyList<SubShader> subShaders => _subShaders;
    public IReadOnlyList<ConstantBuffer> constantBuffers => _constantBuffers;
    public string[] dependencyKeywords => _dependencyKeywords.ToArray();

    private readonly List<ShaderProperty> _properties = new();
    private readonly List<ShaderPass> _passes = new();
    private readonly List<SubShader> _subShaders = new();
    private readonly List<ConstantBuffer> _constantBuffers = new();
    private readonly HashSet<string> _keywords = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _dependencyKeywords = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _tags = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Shader> _fallbackShaders = new(StringComparer.OrdinalIgnoreCase);
    private ShaderCompilerPlatform _compilerPlatform = ShaderCompilerPlatform.WebGL;
    private bool _parsed;
    private string _fallbackName = string.Empty;
    private string _customEditor = string.Empty;

    private Shader(string name)
    {
        this.name = name;
        _allShaders.Add(this);
        AddBuiltinProperties();
        AddDefaultPass();
    }

    private void AddBuiltinProperties()
    {
        AddProperty("_MainTex", "Main Texture", ShaderPropertyType.Texture, null, "white");
        AddProperty("_Color", "Main Color", ShaderPropertyType.Color, Color.white);
        AddProperty("_BaseColor", "Base Color", ShaderPropertyType.Color, Color.white);
        AddProperty("_BaseMap", "Base Map", ShaderPropertyType.Texture, null, "white");
        AddProperty("_Glossiness", "Smoothness", ShaderPropertyType.Range, 0.5f, null, 0f, 1f);
        AddProperty("_Metallic", "Metallic", ShaderPropertyType.Range, 0f, null, 0f, 1f);
        AddProperty("_BumpMap", "Normal Map", ShaderPropertyType.Texture, null, "bump");
        AddProperty("_BumpScale", "Normal Scale", ShaderPropertyType.Float, 1f);
        AddProperty("_EmissionColor", "Emission Color", ShaderPropertyType.Color, Color.black, null, 0f, 0f, ShaderPropertyFlags.HDR);
        AddProperty("_EmissionMap", "Emission Map", ShaderPropertyType.Texture, null, "white");
        AddProperty("_Cutoff", "Alpha Cutoff", ShaderPropertyType.Range, 0.5f, null, 0f, 1f);
        AddProperty("_Mode", "Render Mode", ShaderPropertyType.Float, 0f);
        AddProperty("_SrcBlend", "Src Blend", ShaderPropertyType.Float, 1f);
        AddProperty("_DstBlend", "Dst Blend", ShaderPropertyType.Float, 0f);
        AddProperty("_SrcBlendAlpha", "Src Blend Alpha", ShaderPropertyType.Float, 1f);
        AddProperty("_DstBlendAlpha", "Dst Blend Alpha", ShaderPropertyType.Float, 0f);
        AddProperty("_ZWrite", "Z Write", ShaderPropertyType.Float, 1f);
        AddProperty("_ZTest", "Z Test", ShaderPropertyType.Float, 4f);
        AddProperty("_Cull", "Cull Mode", ShaderPropertyType.Float, 2f);
        AddProperty("_BlendOp", "Blend Op", ShaderPropertyType.Float, 0f);
        AddProperty("_BlendOpAlpha", "Blend Op Alpha", ShaderPropertyType.Float, 0f);
        AddProperty("_ColorMask", "Color Mask", ShaderPropertyType.Float, 15f);
        AddProperty("_StencilRef", "Stencil Ref", ShaderPropertyType.Float, 0f);
        AddProperty("_StencilReadMask", "Stencil Read Mask", ShaderPropertyType.Float, 255f);
        AddProperty("_StencilWriteMask", "Stencil Write Mask", ShaderPropertyType.Float, 255f);
        AddProperty("_StencilComp", "Stencil Comp", ShaderPropertyType.Float, 8f);
        AddProperty("_StencilPass", "Stencil Pass", ShaderPropertyType.Float, 0f);
        AddProperty("_StencilFail", "Stencil Fail", ShaderPropertyType.Float, 0f);
        AddProperty("_StencilZFail", "Stencil ZFail", ShaderPropertyType.Float, 0f);
        AddProperty("_OffsetFactor", "Offset Factor", ShaderPropertyType.Float, 0f);
        AddProperty("_OffsetUnits", "Offset Units", ShaderPropertyType.Float, 0f);
        AddProperty("_BaseColorMap_ST", "Base Map ST", ShaderPropertyType.Vector, new Vector4(1, 1, 0, 0));
        AddProperty("_BaseColorMap_TexelSize", "Base Map Texel Size", ShaderPropertyType.Vector, Vector4.zero);
        AddProperty("_unity_MatrixVP", "View Projection Matrix", ShaderPropertyType.Matrix, Matrix4x4.identity, null, 0f, 0f, ShaderPropertyFlags.HideInInspector);
        AddProperty("_unity_MatrixV", "View Matrix", ShaderPropertyType.Matrix, Matrix4x4.identity, null, 0f, 0f, ShaderPropertyFlags.HideInInspector);
        AddProperty("unity_ObjectToWorld", "Object To World", ShaderPropertyType.Matrix, Matrix4x4.identity, null, 0f, 0f, ShaderPropertyFlags.HideInInspector);
        AddProperty("unity_WorldToObject", "World To Object", ShaderPropertyType.Matrix, Matrix4x4.identity, null, 0f, 0f, ShaderPropertyFlags.HideInInspector);
        AddProperty("_Time", "Time", ShaderPropertyType.Vector, Vector4.zero, null, 0f, 0f, ShaderPropertyFlags.HideInInspector);
        AddProperty("_SinTime", "Sin Time", ShaderPropertyType.Vector, Vector4.zero, null, 0f, 0f, ShaderPropertyFlags.HideInInspector);
        AddProperty("_CosTime", "Cos Time", ShaderPropertyType.Vector, Vector4.zero, null, 0f, 0f, ShaderPropertyFlags.HideInInspector);
        AddProperty("_WorldSpaceCameraPos", "Camera Position", ShaderPropertyType.Vector, Vector4.zero, null, 0f, 0f, ShaderPropertyFlags.HideInInspector);
    }

    private void AddProperty(string name, string description, ShaderPropertyType type, object defaultValue, string defaultTextureName = null, float rangeMin = float.NegativeInfinity, float rangeMax = float.PositiveInfinity, ShaderPropertyFlags flags = ShaderPropertyFlags.None)
    {
        var prop = new ShaderProperty
        {
            name = name,
            description = description,
            type = type,
            nameID = PropertyToID(name),
            defaultValue = defaultValue,
            rangeMin = type == ShaderPropertyType.Range ? rangeMin : float.NegativeInfinity,
            rangeMax = type == ShaderPropertyType.Range ? rangeMax : float.PositiveInfinity,
            flags = flags,
            defaultTextureName = defaultTextureName ?? "white"
        };
        _properties.Add(prop);
    }

    private void AddDefaultPass()
    {
        var pass = new ShaderPass
        {
            name = "FORWARD",
            lightMode = "UniversalForward",
            tags = new Dictionary<string, string> { { "LightMode", "UniversalForward" } },
            blendState = Rendering.BlendState.Opaque,
            depthState = Rendering.DepthState.Default,
            rasterState = Rendering.RasterState.Default,
            stencilState = Rendering.StencilState.Default,
            vertexHLSL = DefaultVertexShader(),
            fragmentHLSL = DefaultFragmentShader(),
            supportsInstancing = true,
            useSRPBatcher = true,
            stencilRef = 0,
            stencilReadMask = 255,
            stencilWriteMask = 255,
            colorMask = Rendering.ColorWriteMask.All,
            cullMode = Rendering.CullMode.Back,
            zWrite = true,
            zTest = CompareFunction.LessEqual,
            offsetFactor = 0f,
            offsetUnits = 0f
        };
        _passes.Add(pass);
        _subShaders.Add(new SubShader
        {
            passes = { pass },
            tags = new Dictionary<string, string> { { "RenderType", "Opaque" }, { "Queue", "Geometry" }, { "RenderPipeline", "UniversalPipeline" } },
            lod = 0
        });
        AddDefaultConstantBuffers();
    }

    private void AddDefaultConstantBuffers()
    {
        var perMaterial = new ConstantBuffer
        {
            name = "UnityPerMaterial",
            isPerMaterial = true,
            isPerInstance = false
        };
        perMaterial.properties.AddRange(new[]
        {
            new InstancingProperty { name = "_BaseColor", nameID = PropertyToID("_BaseColor"), type = ShaderParamType.Float, arraySize = 4 },
            new InstancingProperty { name = "_BaseColorMap_ST", nameID = PropertyToID("_BaseColorMap_ST"), type = ShaderParamType.Float, arraySize = 4 },
            new InstancingProperty { name = "_Cutoff", nameID = PropertyToID("_Cutoff"), type = ShaderParamType.Float, arraySize = 1 },
            new InstancingProperty { name = "_Smoothness", nameID = PropertyToID("_Glossiness"), type = ShaderParamType.Float, arraySize = 1 },
            new InstancingProperty { name = "_Metallic", nameID = PropertyToID("_Metallic"), type = ShaderParamType.Float, arraySize = 1 },
        });
        _constantBuffers.Add(perMaterial);

        var perDraw = new ConstantBuffer
        {
            name = "UnityPerDraw",
            isPerMaterial = false,
            isPerInstance = false
        };
        perDraw.properties.AddRange(new[]
        {
            new InstancingProperty { name = "unity_ObjectToWorld", nameID = PropertyToID("unity_ObjectToWorld"), type = ShaderParamType.Float, arraySize = 16 },
            new InstancingProperty { name = "unity_WorldToObject", nameID = PropertyToID("unity_WorldToObject"), type = ShaderParamType.Float, arraySize = 16 },
        });
        _constantBuffers.Add(perDraw);

        var perFrame = new ConstantBuffer
        {
            name = "UnityPerFrame",
            isPerMaterial = false,
            isPerInstance = false
        };
        perFrame.properties.AddRange(new[]
        {
            new InstancingProperty { name = "_Time", nameID = PropertyToID("_Time"), type = ShaderParamType.Float, arraySize = 4 },
            new InstancingProperty { name = "_SinTime", nameID = PropertyToID("_SinTime"), type = ShaderParamType.Float, arraySize = 4 },
            new InstancingProperty { name = "_CosTime", nameID = PropertyToID("_CosTime"), type = ShaderParamType.Float, arraySize = 4 },
            new InstancingProperty { name = "_WorldSpaceCameraPos", nameID = PropertyToID("_WorldSpaceCameraPos"), type = ShaderParamType.Float, arraySize = 3 },
        });
        _constantBuffers.Add(perFrame);
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
        foreach (var s in _allShaders)
        {
            if (s._tags.TryGetValue(tagName, out var v) && v == tagValue)
                return s;
        }
        var builtin = Find("Universal Render Pipeline/Lit");
        builtin?._tags.TryAdd(tagName, tagValue);
        return builtin;
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

    public static string GetPropertyName(int nameID) => _propertyNames.TryGetValue(nameID, out var name) ? name : string.Empty;
    public static object GetPropertyNameDefaultValue(int nameID) => _globalProperties.TryGetValue(nameID, out var v) ? v : null;

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

    public static string IDToTag(int tagID) => _tagNames.TryGetValue(tagID, out var name) ? name : string.Empty;

    #region Global Properties
    public static void SetGlobalFloat(int nameID, float value) => _globalProperties[nameID] = value;
    public static void SetGlobalFloat(string name, float value) => SetGlobalFloat(PropertyToID(name), value);
    public static float GetGlobalFloat(string name) => GetGlobalFloat(PropertyToID(name));
    public static float GetGlobalFloat(int nameID) => _globalProperties.TryGetValue(nameID, out var v) ? TryConvertFloat(v) : 0f;

    public static void SetGlobalInt(int nameID, int value) => _globalProperties[nameID] = value;
    public static void SetGlobalInt(string name, int value) => SetGlobalInt(PropertyToID(name), value);
    public static int GetGlobalInt(string name) => GetGlobalInt(PropertyToID(name));
    public static int GetGlobalInt(int nameID) => _globalProperties.TryGetValue(nameID, out var v) ? TryConvertInt(v) : 0;

    public static void SetGlobalColor(int nameID, Color value) => _globalProperties[nameID] = value;
    public static void SetGlobalColor(string name, Color value) => SetGlobalColor(PropertyToID(name), value);
    public static Color GetGlobalColor(string name) => GetGlobalColor(PropertyToID(name));
    public static Color GetGlobalColor(int nameID) => _globalProperties.TryGetValue(nameID, out var v) && v is Color c ? c : Color.white;

    public static void SetGlobalVector(int nameID, Vector4 value) => _globalProperties[nameID] = value;
    public static void SetGlobalVector(string name, Vector4 value) => SetGlobalVector(PropertyToID(name), value);
    public static Vector4 GetGlobalVector(string name) => GetGlobalVector(PropertyToID(name));
    public static Vector4 GetGlobalVector(int nameID) => _globalProperties.TryGetValue(nameID, out var v) ? TryConvertVector4(v) : Vector4.zero;

    public static void SetGlobalMatrix(int nameID, Matrix4x4 value) => _globalProperties[nameID] = value;
    public static void SetGlobalMatrix(string name, Matrix4x4 value) => SetGlobalMatrix(PropertyToID(name), value);
    public static Matrix4x4 GetGlobalMatrix(string name) => GetGlobalMatrix(PropertyToID(name));
    public static Matrix4x4 GetGlobalMatrix(int nameID) => _globalProperties.TryGetValue(nameID, out var v) && v is Matrix4x4 m ? m : Matrix4x4.identity;

    public static void SetGlobalTexture(int nameID, Texture value) => _globalProperties[nameID] = value;
    public static void SetGlobalTexture(string name, Texture value) => SetGlobalTexture(PropertyToID(name), value);
    public static Texture GetGlobalTexture(string name) => GetGlobalTexture(PropertyToID(name));
    public static Texture GetGlobalTexture(int nameID) => _globalProperties.TryGetValue(nameID, out var v) && v is Texture t ? t : null;

    public static void SetGlobalBuffer(int nameID, ComputeBuffer value) { if (value != null) _globalBuffers[nameID] = value; else _globalBuffers.Remove(nameID); }
    public static void SetGlobalBuffer(string name, ComputeBuffer value) => SetGlobalBuffer(PropertyToID(name), value);
    public static ComputeBuffer GetGlobalBuffer(int nameID) => _globalBuffers.TryGetValue(nameID, out var b) ? b : null;
    public static ComputeBuffer GetGlobalBuffer(string name) => GetGlobalBuffer(PropertyToID(name));

    public static void SetGlobalBuffer(int nameID, GraphicsBuffer value) { if (value != null) _globalProperties[nameID] = value; else _globalProperties.Remove(nameID); }
    public static void SetGlobalBuffer(string name, GraphicsBuffer value) => SetGlobalBuffer(PropertyToID(name), value);

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

    public static void SetGlobalTextureArray(int nameID, RenderTexture[] values) { if (values != null) _globalArrays[nameID] = (RenderTexture[])values.Clone(); else _globalArrays.Remove(nameID); }
    public static void SetGlobalTextureArray(string name, RenderTexture[] values) => SetGlobalTextureArray(PropertyToID(name), values);
    public static void SetGlobalColor(int nameID, List<Color> values) => SetGlobalVectorArray(nameID, values?.Select(c => (Vector4)c).ToList());
    #endregion

    #region Global Keywords
    public static void EnableKeyword(string keyword) { if (!string.IsNullOrEmpty(keyword)) { _globalKeywords.Add(keyword); globalKeywordsDirty = true; } }
    public static void DisableKeyword(string keyword) { if (!string.IsNullOrEmpty(keyword)) { _globalKeywords.Remove(keyword); globalKeywordsDirty = true; } }
    public static bool IsKeywordEnabled(string keyword) => !string.IsNullOrEmpty(keyword) && _globalKeywords.Contains(keyword);
    public static void SetKeyword(string keyword, bool value) { if (value) EnableKeyword(keyword); else DisableKeyword(keyword); }
    public static string[] globalKeywords => _globalKeywords.ToArray();

    public static void EnableKeyword(in GlobalKeyword keyword) => EnableKeyword(keyword.name);
    public static void DisableKeyword(in GlobalKeyword keyword) => DisableKeyword(keyword.name);
    public static bool IsKeywordEnabled(in GlobalKeyword keyword) => IsKeywordEnabled(keyword.name);
    public static void SetKeyword(in GlobalKeyword keyword, bool value) => SetKeyword(keyword.name, value);
    #endregion

    public static void WarmupAllShaders() { warmupStarted = true; }
    public static void WarmupShader(Shader shader, ShaderVariantCollection collection) { warmupStarted = true; shader?.Compile(); }
    public static void WarmupShaderFromCollection(Shader shader, ShaderVariantCollection collection, in ShaderVariant variant) { warmupStarted = true; }
    private static string _surfaceShaderStatus = "unparsed";
    internal static void ParseSurfaceShaders() { _surfaceShaderStatus = "parsed"; }
    public static string FindPassName(int passNameHash) => string.Empty;
    public static void SetGlobalFloat(int nameID, List<float> values) => SetGlobalFloatArray(nameID, values);
    public static IReadOnlyList<Shader> GetAllShaders() => _allShaders;
    public static IReadOnlyList<Shader> allShaders => _allShaders;

    public void SetProperty(string name, object value)
    {
        _globalProperties[PropertyToID(name)] = value;
    }

    public bool HasProperty(string propertyName) => _properties.Any(p => p.name.Equals(propertyName, StringComparison.Ordinal)) || _propertyIDs.ContainsKey(propertyName);
    public bool HasProperty(int nameID) => _properties.Any(p => p.nameID == nameID) || _globalProperties.ContainsKey(nameID);
    public int FindPropertyIndex(string propertyName) { var p = _properties.FindIndex(x => x.name.Equals(propertyName, StringComparison.Ordinal)); return p >= 0 ? p : (_propertyIDs.TryGetValue(propertyName, out var id) ? id : -1); }
    public ShaderPropertyType GetPropertyType(int propertyIndex) => propertyIndex >= 0 && propertyIndex < _properties.Count ? _properties[propertyIndex].type : ShaderPropertyType.Float;
    public ShaderProperty GetProperty(int index) => index >= 0 && index < _properties.Count ? _properties[index] : default;
    public ShaderProperty GetProperty(string name) => _properties.FirstOrDefault(p => p.name.Equals(name, StringComparison.Ordinal));
    public int GetPropertyCount() => _properties.Count;
    public string GetPropertyDescription(int propertyIndex) => propertyIndex >= 0 && propertyIndex < _properties.Count ? _properties[propertyIndex].description : string.Empty;
    public ShaderPropertyFlags GetPropertyFlags(int propertyIndex) => propertyIndex >= 0 && propertyIndex < _properties.Count ? _properties[propertyIndex].flags : ShaderPropertyFlags.None;
    public void SetPropertyFlags(int propertyIndex, ShaderPropertyFlags flags) { if (propertyIndex >= 0 && propertyIndex < _properties.Count) { var p = _properties[propertyIndex]; p.flags = flags; _properties[propertyIndex] = p; } }
    public void GetPropertyRangeLimits(int propertyIndex, out float min, out float max) { min = 0f; max = 1f; if (propertyIndex >= 0 && propertyIndex < _properties.Count) { min = _properties[propertyIndex].rangeMin; max = _properties[propertyIndex].rangeMax; } }
    public Vector2 GetPropertyTextureDimension(int propertyIndex) => new(0, 0);
    public TextureDimension GetTexDim(int propertyIndex)
    {
        if (propertyIndex < 0 || propertyIndex >= _properties.Count) return TextureDimension.None;
        var prop = _properties[propertyIndex];
        if (prop.type != ShaderPropertyType.Texture) return TextureDimension.None;
        var defaultTexName = prop.defaultTextureName?.ToLowerInvariant() ?? string.Empty;
        if (defaultTexName.Contains("cube")) return TextureDimension.Cube;
        if (defaultTexName.Contains("3d")) return TextureDimension.Tex3D;
        if (defaultTexName.Contains("array")) return TextureDimension.Tex2DArray;
        return TextureDimension.Tex2D;
    }
    public object GetPropertyTypeDefaultValue(int propertyIndex) => propertyIndex >= 0 && propertyIndex < _properties.Count ? _properties[propertyIndex].defaultValue : null;
    public bool IsPassEnabled(int passIndex) => passIndex >= 0 && passIndex < _passes.Count && _passes[passIndex].enabled;
    public void SetPassEnabled(int passIndex, bool enabled) { if (passIndex >= 0 && passIndex < _passes.Count) _passes[passIndex].enabled = enabled; }
    public string GetPropertyTextureDefaultName(int propertyIndex) => propertyIndex >= 0 && propertyIndex < _properties.Count ? _properties[propertyIndex].defaultTextureName : "white";
    public string[] GetShaderKeywords() => _keywords.Concat(_dependencyKeywords).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    public int GetStencilRefForPass(int passIndex) => passIndex >= 0 && passIndex < _passes.Count ? _passes[passIndex].stencilRef : 0;
    public void GetBlendFactors(int passIndex, out BlendMode src, out BlendMode dst, out BlendMode srcA, out BlendMode dstA)
    {
        src = BlendMode.One; dst = BlendMode.Zero; srcA = BlendMode.One; dstA = BlendMode.Zero;
        if (passIndex >= 0 && passIndex < _passes.Count)
        {
            var pass = _passes[passIndex];
            src = pass.blendState.sourceBlend;
            dst = pass.blendState.destinationBlend;
            srcA = pass.blendState.sourceBlendAlpha;
            dstA = pass.blendState.destinationBlendAlpha;
        }
    }
    public CompareFunction GetZTest(int passIndex) => passIndex >= 0 && passIndex < _passes.Count ? _passes[passIndex].zTest : CompareFunction.LessEqual;
    public bool GetZWrite(int passIndex) => passIndex >= 0 && passIndex < _passes.Count ? _passes[passIndex].zWrite : true;
    public CullMode GetCullMode(int passIndex) => passIndex >= 0 && passIndex < _passes.Count ? _passes[passIndex].cullMode : CullMode.Back;
    public void GetStencilOp(int passIndex, out StencilOp fail, out StencilOp zfail, out StencilOp zpass)
    {
        fail = StencilOp.Keep; zfail = StencilOp.Keep; zpass = StencilOp.Keep;
        if (passIndex >= 0 && passIndex < _passes.Count)
        {
            var pass = _passes[passIndex];
            fail = pass.stencilState.failOperationFront;
            zfail = pass.stencilState.zFailOperationFront;
            zpass = pass.stencilState.passOperationFront;
        }
    }
    public CompareFunction GetStencilComp(int passIndex) => passIndex >= 0 && passIndex < _passes.Count ? _passes[passIndex].stencilState.compareFunctionFront : CompareFunction.Always;

    public bool ParseShaderSource(string source)
    {
        sourceCode = source ?? string.Empty;
        _parsed = true;
        _passes.Clear();
        _subShaders.Clear();
        _constantBuffers.Clear();
        _keywords.Clear();
        _tags.Clear();
        _properties.Clear();

        AddBuiltinProperties();
        ParseShaderProperties(sourceCode);
        ParseShaderTags(sourceCode);
        ParseSubShaders(sourceCode);
        ParseShaderKeywords(sourceCode);
        ParseConstantBuffers(sourceCode);
        ParseFallback(sourceCode);
        ParseCustomEditor(sourceCode);

        isInstancingSupported = sourceCode.Contains("multi_compile_instancing", StringComparison.OrdinalIgnoreCase)
                             || sourceCode.Contains("UNITY_INSTANCING_BUFFER", StringComparison.OrdinalIgnoreCase)
                             || sourceCode.Contains("UNITY_VERTEX_INPUT_INSTANCE_ID", StringComparison.OrdinalIgnoreCase);
        isSRPBatcherCompatible = sourceCode.Contains("CBUFFER_START(UnityPerMaterial)", StringComparison.OrdinalIgnoreCase);

        if (_passes.Count == 0)
            AddDefaultPass();

        return true;
    }

    private void ParseShaderProperties(string source)
    {
        var propMatch = Regex.Match(source, @"Properties\s*\{(.*?)\}", RegexOptions.Singleline);
        if (!propMatch.Success) return;
        var propBlock = propMatch.Groups[1].Value;

        var propRegex = new Regex(@"(\w+)\s*\(\s*""([^""]*)""\s*,\s*(\w+)\s*\)\s*(?:=\s*([^\r\n{]+))?", RegexOptions.Singleline);
        foreach (Match m in propRegex.Matches(propBlock))
        {
            var name = m.Groups[1].Value;
            var desc = m.Groups[2].Value;
            var typeStr = m.Groups[3].Value;
            var def = m.Groups[4].Success ? m.Groups[4].Value.Trim() : string.Empty;

            var type = ParseShaderPropertyType(typeStr);
            float rangeMin = float.NegativeInfinity, rangeMax = float.PositiveInfinity;
            string defaultTexName = "white";
            ShaderPropertyFlags flags = ShaderPropertyFlags.None;

            if (typeStr.Equals("range", StringComparison.OrdinalIgnoreCase))
            {
                var rangeMatch = Regex.Match(m.Value, @"\(\s*([-+]?[0-9]*\.?[0-9]+)\s*,\s*([-+]?[0-9]*\.?[0-9]+)\s*\)");
                if (rangeMatch.Success)
                {
                    float.TryParse(rangeMatch.Groups[1].Value, out rangeMin);
                    float.TryParse(rangeMatch.Groups[2].Value, out rangeMax);
                }
            }
            if (typeStr.Equals("2d", StringComparison.OrdinalIgnoreCase) || typeStr.Equals("cube", StringComparison.OrdinalIgnoreCase) || typeStr.Equals("3d", StringComparison.OrdinalIgnoreCase))
            {
                var texDefaultMatch = Regex.Match(def, @"""(\w+)""");
                if (texDefaultMatch.Success) defaultTexName = texDefaultMatch.Groups[1].Value;
            }
            if (def.Contains("[HDR]", StringComparison.OrdinalIgnoreCase)) flags |= ShaderPropertyFlags.HDR;
            if (def.Contains("[Gamma]", StringComparison.OrdinalIgnoreCase)) flags |= ShaderPropertyFlags.Gamma;
            if (def.Contains("[Normal]", StringComparison.OrdinalIgnoreCase)) flags |= ShaderPropertyFlags.Normal;
            if (def.Contains("[HideInInspector]", StringComparison.OrdinalIgnoreCase)) flags |= ShaderPropertyFlags.HideInInspector;
            if (def.Contains("[NoScaleOffset]", StringComparison.OrdinalIgnoreCase)) flags |= ShaderPropertyFlags.NoScaleOffset;
            if (def.Contains("[PerRendererData]", StringComparison.OrdinalIgnoreCase)) flags |= ShaderPropertyFlags.PerRendererData;
            if (name.Equals("_MainTex", StringComparison.Ordinal) || name.Equals("_BaseMap", StringComparison.Ordinal)) flags |= ShaderPropertyFlags.MainTexture;
            if (name.Equals("_Color", StringComparison.Ordinal) || name.Equals("_BaseColor", StringComparison.Ordinal)) flags |= ShaderPropertyFlags.MainColor;

            if (!_properties.Any(p => p.name == name))
            {
                _properties.RemoveAll(p => p.name == name);
                AddProperty(name, desc, type, ParseDefaultValue(def, type), defaultTexName, rangeMin, rangeMax, flags);
            }
        }
    }

    private static ShaderPropertyType ParseShaderPropertyType(string typeStr)
    {
        return typeStr.ToLowerInvariant() switch
        {
            "color" => ShaderPropertyType.Color,
            "vector" => ShaderPropertyType.Vector,
            "float" => ShaderPropertyType.Float,
            "range" => ShaderPropertyType.Range,
            "2d" => ShaderPropertyType.Texture,
            "cube" => ShaderPropertyType.Texture,
            "3d" => ShaderPropertyType.Texture,
            "int" => ShaderPropertyType.Int,
            "matrix" => ShaderPropertyType.Matrix,
            _ => ShaderPropertyType.Float
        };
    }

    private object ParseDefaultValue(string def, ShaderPropertyType type)
    {
        if (type == ShaderPropertyType.Color)
        {
            var colorMatch = Regex.Match(def, @"\(\s*([-+]?[0-9]*\.?[0-9]+)\s*,\s*([-+]?[0-9]*\.?[0-9]+)\s*,\s*([-+]?[0-9]*\.?[0-9]+)\s*,\s*([-+]?[0-9]*\.?[0-9]+)\s*\)");
            if (colorMatch.Success)
                return new Color(
                    float.TryParse(colorMatch.Groups[1].Value, out var r) ? r : 1f,
                    float.TryParse(colorMatch.Groups[2].Value, out var g) ? g : 1f,
                    float.TryParse(colorMatch.Groups[3].Value, out var b) ? b : 1f,
                    float.TryParse(colorMatch.Groups[4].Value, out var a) ? a : 1f);
            return Color.white;
        }
        if (type == ShaderPropertyType.Vector)
        {
            var vecMatch = Regex.Match(def, @"\(\s*([-+]?[0-9]*\.?[0-9]+)\s*,\s*([-+]?[0-9]*\.?[0-9]+)\s*,\s*([-+]?[0-9]*\.?[0-9]+)\s*,\s*([-+]?[0-9]*\.?[0-9]+)\s*\)");
            if (vecMatch.Success)
                return new Vector4(
                    float.TryParse(vecMatch.Groups[1].Value, out var x) ? x : 0f,
                    float.TryParse(vecMatch.Groups[2].Value, out var y) ? y : 0f,
                    float.TryParse(vecMatch.Groups[3].Value, out var z) ? z : 0f,
                    float.TryParse(vecMatch.Groups[4].Value, out var w) ? w : 0f);
            return Vector4.zero;
        }
        return type switch
        {
            ShaderPropertyType.Float or ShaderPropertyType.Range => float.TryParse(def.Trim('"', ' '), out var f) ? f : 0f,
            ShaderPropertyType.Int => int.TryParse(def.Trim('"', ' '), out var i) ? i : 0,
            _ => null
        };
    }

    private void ParseShaderTags(string source)
    {
        foreach (Match m in Regex.Matches(source, @"""(\w+)""\s*=\s*""([^""]*)"""))
        {
            _tags[m.Groups[1].Value] = m.Groups[2].Value;
        }

        var queueMatch = Regex.Match(source, @"Tags\s*\{[^}]*""Queue""\s*=\s*""(\w+)([+-]\d+)?""", RegexOptions.Singleline);
        if (queueMatch.Success)
        {
            var queueName = queueMatch.Groups[1].Value;
            var queueOffset = queueMatch.Groups[2].Success && int.TryParse(queueMatch.Groups[2].Value, out var o) ? o : 0;
            renderQueue = queueName.ToLowerInvariant() switch
            {
                "background" => (int)RenderQueue.Background + queueOffset,
                "geometry" => (int)RenderQueue.Geometry + queueOffset,
                "alphatest" => (int)RenderQueue.AlphaTest + queueOffset,
                "transparent" => (int)RenderQueue.Transparent + queueOffset,
                "overlay" => (int)RenderQueue.Overlay + queueOffset,
                _ => (int)RenderQueue.Geometry + queueOffset
            };
        }

        if (_tags.TryGetValue("RenderType", out var renderType))
        {
            renderQueue = renderType.ToLowerInvariant() switch
            {
                "transparent" => (int)RenderQueue.Transparent,
                "transparentcutout" => (int)RenderQueue.AlphaTest,
                "overlay" => (int)RenderQueue.Overlay,
                "background" => (int)RenderQueue.Background,
                _ => renderQueue
            };
        }
    }

    private void ParseSubShaders(string source)
    {
        var subshaderRegex = new Regex(@"SubShader\s*\{(.*?)(?=SubShader\s*\{|UsePass\s*|Fallback\s*|$)", RegexOptions.Singleline);
        foreach (Match subMatch in subshaderRegex.Matches(source))
        {
            var subContent = subMatch.Groups[1].Value;
            var subShader = new SubShader();

            var subTagBlock = Regex.Match(subContent, @"Tags\s*\{(.*?)\}", RegexOptions.Singleline);
            if (subTagBlock.Success)
            {
                foreach (Match tm in Regex.Matches(subTagBlock.Groups[1].Value, @"""(\w+)""\s*=\s*""([^""]*)"""))
                    subShader.tags[tm.Groups[1].Value] = tm.Groups[2].Value;
            }

            var lodMatch = Regex.Match(subContent, @"LOD\s+(\d+)");
            if (lodMatch.Success && int.TryParse(lodMatch.Groups[1].Value, out var lod))
                subShader.lod = lod;

            var passRegex = new Regex(@"(?:Pass|UsePass)\s+(?:""([^""]+)""|\{)", RegexOptions.Singleline);
            var passes = Regex.Matches(subContent, @"Pass\s*\{(.*?)\}(?=\s*(?:Pass|UsePass|$))", RegexOptions.Singleline);
            foreach (Match passMatch in passes)
            {
                var pass = ParsePass(passMatch.Groups[1].Value);
                if (pass != null)
                {
                    _passes.Add(pass);
                    subShader.passes.Add(pass);
                }
            }

            _subShaders.Add(subShader);
        }
    }

    private ShaderPass ParsePass(string passContent)
    {
        var pass = new ShaderPass
        {
            blendState = Rendering.BlendState.Opaque,
            depthState = Rendering.DepthState.Default,
            rasterState = Rendering.RasterState.Default,
            stencilState = Rendering.StencilState.Default,
            cullMode = Rendering.CullMode.Back,
            zWrite = true,
            zTest = CompareFunction.LessEqual,
            colorMask = Rendering.ColorWriteMask.All,
            supportsInstancing = passContent.Contains("multi_compile_instancing", StringComparison.OrdinalIgnoreCase)
        };

        var nameMatch = Regex.Match(passContent, @"Name\s+""([^""]+)""");
        if (nameMatch.Success) pass.name = nameMatch.Groups[1].Value;

        var tagBlock = Regex.Match(passContent, @"Tags\s*\{(.*?)\}", RegexOptions.Singleline);
        if (tagBlock.Success)
        {
            foreach (Match tm in Regex.Matches(tagBlock.Groups[1].Value, @"""(\w+)""\s*=\s*""([^""]*)"""))
            {
                pass.tags[tm.Groups[1].Value] = tm.Groups[2].Value;
                if (tm.Groups[1].Value == "LightMode")
                    pass.lightMode = tm.Groups[2].Value;
            }
        }

        var cullMatch = Regex.Match(passContent, @"Cull\s+(\w+)");
        if (cullMatch.Success)
            pass.cullMode = ParseCullMode(cullMatch.Groups[1].Value);
        pass.rasterState.cullingMode = pass.cullMode;

        var zwriteMatch = Regex.Match(passContent, @"ZWrite\s+(\w+)");
        if (zwriteMatch.Success)
            pass.zWrite = zwriteMatch.Groups[1].Value.Equals("on", StringComparison.OrdinalIgnoreCase);
        pass.depthState.writeEnabled = pass.zWrite;

        var ztestMatch = Regex.Match(passContent, @"ZTest\s+(\w+)");
        if (ztestMatch.Success)
            pass.zTest = ParseCompareFunction(ztestMatch.Groups[1].Value);
        pass.depthState.compareFunction = pass.zTest;

        var blendMatch = Regex.Match(passContent, @"Blend\s+(\w+)\s+(\w+)(?:\s+(\w+)\s+(\w+))?");
        if (blendMatch.Success)
        {
            pass.blendState.enabled = true;
            pass.blendState.sourceBlend = ParseBlendMode(blendMatch.Groups[1].Value);
            pass.blendState.destinationBlend = ParseBlendMode(blendMatch.Groups[2].Value);
            if (blendMatch.Groups[3].Success && blendMatch.Groups[4].Success)
            {
                pass.blendState.sourceBlendAlpha = ParseBlendMode(blendMatch.Groups[3].Value);
                pass.blendState.destinationBlendAlpha = ParseBlendMode(blendMatch.Groups[4].Value);
            }
            else
            {
                pass.blendState.sourceBlendAlpha = pass.blendState.sourceBlend;
                pass.blendState.destinationBlendAlpha = pass.blendState.destinationBlend;
            }
        }

        var blendOpMatch = Regex.Match(passContent, @"BlendOp\s+(\w+)");
        if (blendOpMatch.Success)
            pass.blendState.blendMode = ParseBlendOp(blendOpMatch.Groups[1].Value);

        var colorMaskMatch = Regex.Match(passContent, @"ColorMask\s+(\w+)");
        if (colorMaskMatch.Success)
            pass.colorMask = ParseColorWriteMask(colorMaskMatch.Groups[1].Value);
        pass.blendState.writeMask = pass.colorMask;

        var offsetMatch = Regex.Match(passContent, @"Offset\s+([-+]?[0-9]*\.?[0-9]+)\s*,\s*([-+]?[0-9]*\.?[0-9]+)");
        if (offsetMatch.Success)
        {
            float.TryParse(offsetMatch.Groups[1].Value, out pass.offsetFactor);
            float.TryParse(offsetMatch.Groups[2].Value, out pass.offsetUnits);
        }
        pass.rasterState.depthBias = pass.offsetUnits;
        pass.rasterState.slopeDepthBias = pass.offsetFactor;

        var stencilRefMatch = Regex.Match(passContent, @"Stencil\s*\{[^}]*Ref\s+(\d+)");
        if (stencilRefMatch.Success && int.TryParse(stencilRefMatch.Groups[1].Value, out var sref))
            pass.stencilRef = sref;

        var stencilReadMatch = Regex.Match(passContent, @"Stencil\s*\{[^}]*ReadMask\s+(\d+)");
        if (stencilReadMatch.Success && int.TryParse(stencilReadMatch.Groups[1].Value, out var srm))
            pass.stencilReadMask = (byte)srm;
        var stencilWriteMatch = Regex.Match(passContent, @"Stencil\s*\{[^}]*WriteMask\s+(\d+)");
        if (stencilWriteMatch.Success && int.TryParse(stencilWriteMatch.Groups[1].Value, out var swm))
            pass.stencilWriteMask = (byte)swm;

        var stencilCompMatch = Regex.Match(passContent, @"Stencil\s*\{[^}]*Comp\s+(\w+)");
        if (stencilCompMatch.Success)
            pass.stencilState.compareFunctionFront = pass.stencilState.compareFunctionBack = ParseCompareFunction(stencilCompMatch.Groups[1].Value);
        var stencilPassMatch = Regex.Match(passContent, @"Stencil\s*\{[^}]*Pass\s+(\w+)");
        if (stencilPassMatch.Success)
            pass.stencilState.passOperationFront = pass.stencilState.passOperationBack = ParseStencilOp(stencilPassMatch.Groups[1].Value);
        var stencilFailMatch = Regex.Match(passContent, @"Stencil\s*\{[^}]*Fail\s+(\w+)");
        if (stencilFailMatch.Success)
            pass.stencilState.failOperationFront = pass.stencilState.failOperationBack = ParseStencilOp(stencilFailMatch.Groups[1].Value);
        var stencilZFailMatch = Regex.Match(passContent, @"Stencil\s*\{[^}]*ZFail\s+(\w+)");
        if (stencilZFailMatch.Success)
            pass.stencilState.zFailOperationFront = pass.stencilState.zFailOperationBack = ParseStencilOp(stencilZFailMatch.Groups[1].Value);
        pass.stencilState.readMask = (byte)pass.stencilReadMask;
        pass.stencilState.writeMask = (byte)pass.stencilWriteMask;

        var hlslMatch = Regex.Match(passContent, @"HLSLPROGRAM(.*?)ENDHLSL", RegexOptions.Singleline);
        if (hlslMatch.Success)
        {
            var hlsl = hlslMatch.Groups[1].Value;
            ParsePassHLSL(pass, hlsl);
        }
        else
        {
            var cgMatch = Regex.Match(passContent, @"CGPROGRAM(.*?)ENDCG", RegexOptions.Singleline);
            if (cgMatch.Success)
                ParsePassHLSL(pass, cgMatch.Groups[1].Value);
        }

        return pass;
    }

    private void ParsePassHLSL(ShaderPass pass, string hlsl)
    {
        pass.vertexHLSL = hlsl;
        pass.fragmentHLSL = hlsl;

        var kwRegex = new Regex(@"#pragma\s+(multi_compile|shader_feature|multi_compile_local|shader_feature_local|multi_compile_instancing)[^\n]*");
        foreach (Match m in kwRegex.Matches(hlsl))
        {
            var pragmaType = m.Groups[1].Value;
            if (pragmaType == "multi_compile_instancing")
            {
                pass.supportsInstancing = true;
                isInstancingSupported = true;
                continue;
            }
            var parts = m.Value.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 2; i < parts.Length; i++)
            {
                var kw = parts[i].Trim();
                if (!string.IsNullOrEmpty(kw) && !kw.StartsWith("_") && kw != "_" && !kw.Contains("__"))
                    pass.keywords = pass.keywords.Concat(new[] { kw }).Distinct().ToArray();
                _keywords.Add(kw);
            }
        }

        var vertexMatch = Regex.Match(hlsl, @"#pragma\s+vertex\s+(\w+)");
        if (vertexMatch.Success)
            pass.vertexEntry = vertexMatch.Groups[1].Value;
        var fragmentMatch = Regex.Match(hlsl, @"#pragma\s+fragment\s+(\w+)");
        if (fragmentMatch.Success)
            pass.fragmentEntry = fragmentMatch.Groups[1].Value;

        var hullMatch = Regex.Match(hlsl, @"#pragma\s+hull\s+(\w+)");
        if (hullMatch.Success) { pass.hullEntry = hullMatch.Groups[1].Value; pass.hullHLSL = hlsl; }
        var domainMatch = Regex.Match(hlsl, @"#pragma\s+domain\s+(\w+)");
        if (domainMatch.Success) { pass.domainEntry = domainMatch.Groups[1].Value; pass.domainHLSL = hlsl; }
        var geometryMatch = Regex.Match(hlsl, @"#pragma\s+geometry\s+(\w+)");
        if (geometryMatch.Success) { pass.geometryEntry = geometryMatch.Groups[1].Value; pass.geometryHLSL = hlsl; }
    }

    private void ParseShaderKeywords(string source)
    {
        var kwRegex = new Regex(@"#pragma\s+(multi_compile|shader_feature|multi_compile_local|shader_feature_local)[^\n]*");
        foreach (Match m in kwRegex.Matches(source))
        {
            var parts = m.Value.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 2; i < parts.Length; i++)
            {
                var kw = parts[i].Trim();
                if (!string.IsNullOrEmpty(kw) && kw != "_" && !kw.StartsWith("_") || kw.StartsWith("_") && kw.Length > 1)
                    _dependencyKeywords.Add(kw);
            }
        }
    }

    private void ParseConstantBuffers(string source)
    {
        var cbufferRegex = new Regex(@"CBUFFER_START\((\w+)\)(.*?)CBUFFER_END", RegexOptions.Singleline);
        foreach (Match m in cbufferRegex.Matches(source))
        {
            var cbName = m.Groups[1].Value;
            var cbBody = m.Groups[2].Value;

            var cb = new ConstantBuffer
            {
                name = cbName,
                isPerMaterial = cbName.Contains("UnityPerMaterial", StringComparison.OrdinalIgnoreCase),
                isPerInstance = cbName.Contains("UnityPerInstance", StringComparison.OrdinalIgnoreCase) || source.Contains("UNITY_INSTANCING_BUFFER", StringComparison.OrdinalIgnoreCase)
            };

            var propRegex = new Regex(@"(float|float2|float3|float4|half|half2|half3|half4|int|int2|int3|int4|fixed|fixed2|fixed3|fixed4|float4x4|half4x4)\s+(\w+)(?:\s*:\s*\w+)?\s*;");
            foreach (Match pm in propRegex.Matches(cbBody))
            {
                var typeStr = pm.Groups[1].Value;
                var propName = pm.Groups[2].Value;
                var pType = typeStr switch
                {
                    "float" or "half" or "fixed" or "int" => ShaderParamType.Float,
                    "float4x4" or "half4x4" => ShaderParamType.Float,
                    _ => ShaderParamType.Float
                };
                var arraySize = typeStr switch
                {
                    "float" or "half" or "fixed" or "int" => 1,
                    "float2" or "half2" or "fixed2" or "int2" => 2,
                    "float3" or "half3" or "fixed3" or "int3" => 3,
                    "float4" or "half4" or "fixed4" or "int4" => 4,
                    "float4x4" or "half4x4" => 16,
                    _ => 1
                };

                cb.properties.Add(new InstancingProperty
                {
                    name = propName,
                    nameID = PropertyToID(propName),
                    type = pType,
                    arraySize = arraySize
                });
            }

            _constantBuffers.Add(cb);
        }
    }

    private void ParseFallback(string source)
    {
        var fbMatch = Regex.Match(source, @"Fallback\s+""([^""]+)""");
        if (fbMatch.Success)
            _fallbackName = fbMatch.Groups[1].Value;
    }

    private void ParseCustomEditor(string source)
    {
        var ceMatch = Regex.Match(source, @"CustomEditor\s+""([^""]+)""");
        if (ceMatch.Success)
            _customEditor = ceMatch.Groups[1].Value;
    }

    private static CullMode ParseCullMode(string s) => s.ToLowerInvariant() switch
    {
        "off" => CullMode.Off,
        "front" => CullMode.Front,
        "back" => CullMode.Back,
        _ => CullMode.Back
    };
    private static CompareFunction ParseCompareFunction(string s) => s.ToLowerInvariant() switch
    {
        "never" => CompareFunction.Never,
        "less" => CompareFunction.Less,
        "equal" => CompareFunction.Equal,
        "lequal" => CompareFunction.LessEqual,
        "greater" => CompareFunction.Greater,
        "notequal" => CompareFunction.NotEqual,
        "gequal" => CompareFunction.GreaterEqual,
        "always" => CompareFunction.Always,
        "off" => CompareFunction.Disabled,
        _ => CompareFunction.LessEqual
    };
    private static BlendMode ParseBlendMode(string s) => s.ToLowerInvariant() switch
    {
        "zero" => BlendMode.Zero,
        "one" => BlendMode.One,
        "dstcolor" => BlendMode.DstColor,
        "srccolor" => BlendMode.SrcColor,
        "oneminusdstcolor" => BlendMode.OneMinusDstColor,
        "srcalpha" => BlendMode.SrcAlpha,
        "oneminussrcalpha" => BlendMode.OneMinusSrcAlpha,
        "dstalpha" => BlendMode.DstAlpha,
        "oneminusdstalpha" => BlendMode.OneMinusDstAlpha,
        "srcalphasaturate" => BlendMode.SrcAlphaSaturate,
        _ => BlendMode.One
    };
    private static BlendOp ParseBlendOp(string s) => s.ToLowerInvariant() switch
    {
        "add" => BlendOp.Add,
        "sub" => BlendOp.Subtract,
        "subtract" => BlendOp.Subtract,
        "revsub" => BlendOp.ReverseSubtract,
        "reversesubtract" => BlendOp.ReverseSubtract,
        "min" => BlendOp.Min,
        "max" => BlendOp.Max,
        _ => BlendOp.Add
    };
    private static ColorWriteMask ParseColorWriteMask(string s)
    {
        if (s.Equals("0", StringComparison.Ordinal)) return ColorWriteMask.None;
        var mask = ColorWriteMask.None;
        if (s.Contains('R') || s.Contains('r') || s.Equals("all", StringComparison.OrdinalIgnoreCase)) mask |= ColorWriteMask.Red;
        if (s.Contains('G') || s.Contains('g') || s.Equals("all", StringComparison.OrdinalIgnoreCase)) mask |= ColorWriteMask.Green;
        if (s.Contains('B') || s.Contains('b') || s.Equals("all", StringComparison.OrdinalIgnoreCase)) mask |= ColorWriteMask.Blue;
        if (s.Contains('A') || s.Contains('a') || s.Equals("all", StringComparison.OrdinalIgnoreCase) || s.All(char.IsDigit) && int.Parse(s) == 15) mask |= ColorWriteMask.Alpha;
        return mask == ColorWriteMask.None ? ColorWriteMask.All : mask;
    }
    private static StencilOp ParseStencilOp(string s) => s.ToLowerInvariant() switch
    {
        "keep" => StencilOp.Keep,
        "zero" => StencilOp.Zero,
        "replace" => StencilOp.Replace,
        "incrsat" => StencilOp.IncrementSaturate,
        "decrsat" => StencilOp.DecrementSaturate,
        "invert" => StencilOp.Invert,
        "incrwrap" => StencilOp.IncrementWrap,
        "decrwrap" => StencilOp.DecrementWrap,
        _ => StencilOp.Keep
    };

    private static float TryConvertFloat(object v) => v switch { float f => f, int i => i, double d => (float)d, _ => 0f };
    private static int TryConvertInt(object v) => v switch { int i => i, float f => (int)f, double d => (int)d, _ => 0 };
    private static Vector4 TryConvertVector4(object v) => v switch { Vector4 vec => vec, Color c => (Vector4)c, Vector3 v3 => new Vector4(v3.x, v3.y, v3.z, 0), Vector2 v2 => new Vector4(v2.x, v2.y, 0, 0), _ => Vector4.zero };

    public CompiledShader Compile(ShaderCompilerPlatform platform = ShaderCompilerPlatform.WebGL)
    {
        _compilerPlatform = platform;
        return new CompiledShader
        {
            shader = this,
            platform = platform,
            variants = CompileAllVariants(),
            isSupported = true
        };
    }

    private List<CompiledShaderVariant> CompileAllVariants()
    {
        var variants = new List<CompiledShaderVariant>();
        foreach (var pass in _passes)
        {
            var keywordCombinations = GenerateKeywordCombinations(pass.keywords);
            if (keywordCombinations.Count == 0)
                keywordCombinations.Add(Array.Empty<string>());

            foreach (var kwCombo in keywordCombinations)
            {
                variants.Add(new CompiledShaderVariant
                {
                    pass = pass,
                    keywords = kwCombo,
                    vertexShader = pass.vertexHLSL,
                    fragmentShader = pass.fragmentHLSL,
                    hullShader = pass.hullHLSL,
                    domainShader = pass.domainHLSL,
                    geometryShader = pass.geometryHLSL,
                    stage = ShaderStage.Vertex | ShaderStage.Fragment |
                            (string.IsNullOrEmpty(pass.hullHLSL) ? 0 : ShaderStage.Hull) |
                            (string.IsNullOrEmpty(pass.domainHLSL) ? 0 : ShaderStage.Domain) |
                            (string.IsNullOrEmpty(pass.geometryHLSL) ? 0 : ShaderStage.Geometry),
                    instancingEnabled = pass.supportsInstancing
                });
            }
        }
        return variants;
    }

    private List<string[]> GenerateKeywordCombinations(string[] keywords)
    {
        var result = new List<string[]>();
        if (keywords == null || keywords.Length == 0) return result;

        var groups = new List<List<string>>();
        var currentGroup = new List<string>();
        foreach (var kw in keywords)
        {
            if (kw == "_")
            {
                if (currentGroup.Count > 0) groups.Add(currentGroup);
                currentGroup = new List<string>();
            }
            else
            {
                currentGroup.Add(kw);
            }
        }
        if (currentGroup.Count > 0) groups.Add(currentGroup);

        if (groups.Count == 0) return result;

        GenerateCombinationsRecursive(groups, 0, new List<string>(), result);
        return result;
    }

    private void GenerateCombinationsRecursive(List<List<string>> groups, int index, List<string> current, List<string[]> result)
    {
        if (index == groups.Count)
        {
            result.Add(current.ToArray());
            return;
        }
        foreach (var kw in groups[index])
        {
            current.Add(kw);
            GenerateCombinationsRecursive(groups, index + 1, current, result);
            current.RemoveAt(current.Count - 1);
        }
    }

    public string GetTag(string tagName, bool searchFallbacks, string defaultValue) => _tags.TryGetValue(tagName, out var v) ? v : defaultValue;
    public string GetTag(string tagName, bool searchFallbacks) => _tags.TryGetValue(tagName, out var v) ? v : string.Empty;
    public void SetTag(string tagName, string tagValue) => _tags[tagName] = tagValue;
    public bool HasTag(string tagName) => _tags.ContainsKey(tagName);
    public string fallbackName => _fallbackName;
    public string customEditor => _customEditor;

    public Shader GetFallback() => string.IsNullOrEmpty(_fallbackName) ? null : Find(_fallbackName);

    public int FindPass(string passName)
    {
        for (int i = 0; i < _passes.Count; i++)
        {
            if (_passes[i].name.Equals(passName, StringComparison.OrdinalIgnoreCase) ||
                _passes[i].lightMode.Equals(passName, StringComparison.OrdinalIgnoreCase))
                return i;
        }
        return -1;
    }

    public string GetPassName(int pass) => pass >= 0 && pass < _passes.Count ? _passes[pass].name : string.Empty;
    public ShaderPass GetPass(int pass) => pass >= 0 && pass < _passes.Count ? _passes[pass] : null;

    public void SetPassLightMode(int passIndex, string lightMode)
    {
        if (passIndex >= 0 && passIndex < _passes.Count)
        {
            _passes[passIndex].lightMode = lightMode;
            _passes[passIndex].tags["LightMode"] = lightMode;
        }
    }

    public static void SetGlobalMatrix(int nameID, Matrix4x4[] values) => SetGlobalMatrixArray(nameID, values);
    public static void SetGlobalMatrix(string name, Matrix4x4[] values) => SetGlobalMatrixArray(name, values);

    private static string DefaultVertexShader() => @"
HLSLPROGRAM
#pragma vertex vert
#pragma fragment frag
#pragma multi_compile_fog
#pragma multi_compile_instancing
#include ""Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl""
struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
struct Varyings { float4 positionHCS : SV_POSITION; float2 uv : TEXCOORD0; float fogFactor : TEXCOORD1; UNITY_VERTEX_INPUT_INSTANCE_ID };
TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex); CBUFFER_START(UnityPerMaterial) float4 _MainTex_ST; float4 _Color; CBUFFER_END
Varyings vert(Attributes input) { Varyings output; UNITY_SETUP_INSTANCE_ID(input); UNITY_TRANSFER_INSTANCE_ID(input, output); output.positionHCS = TransformObjectToHClip(input.positionOS.xyz); output.uv = TRANSFORM_TEX(input.uv, _MainTex); output.fogFactor = ComputeFogFactor(output.positionHCS.z); return output; }
ENDHLSL";

    private static string DefaultFragmentShader() => @"
half4 frag(Varyings input) : SV_Target { UNITY_SETUP_INSTANCE_ID(input); half4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * _Color; col.rgb = MixFog(col.rgb, input.fogFactor); return col; }";

    public static Shader Create(string name, string source)
    {
        var shader = Find(name) ?? new Shader(name);
        shader.ParseShaderSource(source);
        _cache[name] = shader;
        return shader;
    }

    public void SetBuffer(int nameID, GraphicsBuffer buffer)
    {
        if (buffer == null)
        {
            _properties.RemoveAll(p => p.nameID == nameID);
            return;
        }
        for (int i = 0; i < _properties.Count; i++)
        {
            if (_properties[i].nameID == nameID)
            {
                var prop = _properties[i];
                prop.defaultValue = buffer;
                _properties[i] = prop;
                return;
            }
        }
        _properties.Add(new ShaderProperty
        {
            name = GetPropertyName(nameID),
            nameID = nameID,
            defaultValue = buffer,
            type = ShaderPropertyType.Int
        });
    }
    public void SetBuffer(string name, GraphicsBuffer buffer) => SetBuffer(PropertyToID(name), buffer);

    public static void SetGlobalConstantBuffer(ComputeBuffer buffer, int nameID, int offset, int size)
    {
        if (buffer != null)
            _globalConstantBuffersMap[nameID] = (buffer, offset, size);
        else
            _globalConstantBuffersMap.Remove(nameID);
    }
    public static void SetGlobalConstantBuffer(ComputeBuffer buffer, string name, int offset, int size)
        => SetGlobalConstantBuffer(buffer, PropertyToID(name), offset, size);

    internal static void AddGlobalConstantBuffer(ConstantBuffer cb)
    {
        _globalConstantBuffers.RemoveAll(b => b.name == cb.name);
        _globalConstantBuffers.Add(cb);
    }

    internal static void ClearCache()
    {
        _cache.Clear();
        _propertyNames.Clear();
        _propertyIDs.Clear();
        _globalProperties.Clear();
        _globalArrays.Clear();
        _globalBuffers.Clear();
        _globalConstantBuffersMap.Clear();
        _tagCache.Clear();
        _tagNames.Clear();
        _globalKeywords.Clear();
        _globalKeywordObjects.Clear();
        _globalConstantBuffers.Clear();
        _allShaders.Clear();
        _nextPropertyID = 1;
        _nextTagID = 1;
    }

    public static TextureDimension GetGlobalTextureDimension(int nameID)
    {
        var tex = GetGlobalTexture(nameID);
        return tex?.dimension ?? TextureDimension.None;
    }

    public static byte CalculateFogStencil(bool fogEnabled)
    {
        return fogEnabled ? (byte)1 : (byte)0;
    }
}

public static class ShaderUtil
{
    private static ShaderVariantCollection _shaderVariantCollection;

    public static Shader FindShader(string name) => Shader.Find(name);
    public static int GetPropertyCount(Shader shader) => shader?.GetPropertyCount() ?? 0;
    public static ShaderPropertyType GetPropertyType(Shader shader, int propertyIndex) => shader?.GetPropertyType(propertyIndex) ?? ShaderPropertyType.Float;
    public static string GetPropertyName(Shader shader, int propertyIndex) => shader?.GetPropertyDescription(propertyIndex) ?? string.Empty;
    public static string GetPropertyDescription(Shader shader, int propertyIndex) => shader?.GetPropertyDescription(propertyIndex) ?? string.Empty;
    public static void GetRangeLimits(Shader shader, int propertyIndex, out float min, out float max) { min = 0f; max = 1f; shader?.GetPropertyRangeLimits(propertyIndex, out min, out max); }
    public static TextureDimension GetTexDim(Shader shader, int propertyIndex) => shader?.GetTexDim(propertyIndex) ?? TextureDimension.None;
    public static object GetPropertyTypeDefaultValue(Shader shader, int propertyIndex) => shader?.GetPropertyTypeDefaultValue(propertyIndex);
    public static bool IsPassEnabled(Shader shader, int passIndex) => shader?.IsPassEnabled(passIndex) ?? false;
    public static void SetPassEnabled(Shader shader, int passIndex, bool enabled) => shader?.SetPassEnabled(passIndex, enabled);
    public static MaterialProperty[] GetMaterialProperties(Material[] mats)
    {
        if (mats == null || mats.Length == 0 || mats[0] == null || mats[0].shader == null)
            return Array.Empty<MaterialProperty>();
        var shader = mats[0].shader;
        var props = new List<MaterialProperty>();
        foreach (var sp in shader.properties)
        {
            var mp = new MaterialProperty
            {
                name = sp.name,
                nameID = sp.nameID,
                type = (MaterialPropertyType)sp.type,
                flags = (MaterialPropertyFlags)sp.flags,
                rangeLimits = new Vector2(sp.rangeMin, sp.rangeMax),
                textureDimension = shader.GetTexDim(shader.properties.ToList().IndexOf(sp))
            };
            var firstMat = mats[0];
            switch (sp.type)
            {
                case ShaderPropertyType.Float:
                case ShaderPropertyType.Range:
                case ShaderPropertyType.Int:
                    mp.floatValue = firstMat.GetFloat(sp.nameID);
                    break;
                case ShaderPropertyType.Color:
                    mp.colorValue = firstMat.GetColor(sp.nameID);
                    break;
                case ShaderPropertyType.Vector:
                    mp.vectorValue = firstMat.GetVector(sp.nameID);
                    break;
                case ShaderPropertyType.Texture:
                    mp.textureValue = firstMat.GetTexture(sp.nameID);
                    break;
            }
            props.Add(mp);
        }
        return props.ToArray();
    }
    public static MaterialProperty GetMaterialProperty(Shader shader, int propertyIndex)
    {
        if (shader == null || propertyIndex < 0 || propertyIndex >= shader.properties.Count)
            return default;
        var sp = shader.properties[propertyIndex];
        return new MaterialProperty
        {
            name = sp.name,
            nameID = sp.nameID,
            type = (MaterialPropertyType)sp.type,
            flags = (MaterialPropertyFlags)sp.flags,
            rangeLimits = new Vector2(sp.rangeMin, sp.rangeMax),
            textureDimension = shader.GetTexDim(propertyIndex)
        };
    }
    public static void ApplyMaterialPropertyBlock(MaterialPropertyBlock props, Material mat)
    {
        props?.ApplyToMaterial(mat);
    }
    public static object ExtractCachedProperty(Material mat, int nameID)
    {
        return mat?.ExtractProperty(nameID);
    }
    public static byte CalculateFogStencil(bool fogEnabled) => Shader.CalculateFogStencil(fogEnabled);
    public static int GetStencilRefForPass(Shader shader, int pass) => shader?.GetStencilRefForPass(pass) ?? 0;
    public static void GetBlendFactors(Shader shader, int pass, out BlendMode src, out BlendMode dst, out BlendMode srcA, out BlendMode dstA) { src = BlendMode.One; dst = BlendMode.Zero; srcA = BlendMode.One; dstA = BlendMode.Zero; shader?.GetBlendFactors(pass, out src, out dst, out srcA, out dstA); }
    public static CompareFunction GetZTest(Shader shader, int pass) => shader?.GetZTest(pass) ?? CompareFunction.LessEqual;
    public static bool GetZWrite(Shader shader, int pass) => shader?.GetZWrite(pass) ?? true;
    public static CullMode GetCullMode(Shader shader, int pass) => shader?.GetCullMode(pass) ?? CullMode.Back;
    public static void GetStencilOp(Shader shader, int pass, out StencilOp fail, out StencilOp zfail, out StencilOp zpass) { fail = StencilOp.Keep; zfail = StencilOp.Keep; zpass = StencilOp.Keep; shader?.GetStencilOp(pass, out fail, out zfail, out zpass); }
    public static CompareFunction GetStencilComp(Shader shader, int pass) => shader?.GetStencilComp(pass) ?? CompareFunction.Always;
    public static string[] GetShaderKeywords(Shader shader) => shader?.GetShaderKeywords() ?? Array.Empty<string>();
    public static TextureDimension GetGlobalTextureDimension(int nameID) => Shader.GetGlobalTextureDimension(nameID);
    public static bool IsShaderPropertyHidden(Shader shader, int propertyIndex) => shader != null && shader.GetPropertyFlags(propertyIndex).HasFlag(ShaderPropertyFlags.HideInInspector);
    public static void SetShaderVariantCollection(ShaderVariantCollection collection) { _shaderVariantCollection = collection; }
    public static void ClearShaderCache() { _shaderVariantCollection = null; Shader.ClearCache(); }
    public static CompiledShader CompileShader(Shader shader, ShaderCompilerPlatform platform) => shader?.Compile(platform);
    public static int GetComboCount(Shader shader) => shader != null ? shader.passes.Sum(p => p.keywords.Length) : 0;
    public static string GetDependency(Shader shader, string dependencyName) => string.Empty;
    public static string[] GetDependencyNames(Shader shader) => shader?.dependencyKeywords ?? Array.Empty<string>();
    public static int GetTextureBindingIndex(Shader shader, int propertyIndex) => 0;
    public static int GetBufferBindingIndex(Shader shader, int propertyIndex) => 0;
    public static Material CreateShaderMaterial(Shader shader) => shader != null ? new Material(shader) : null;
    public static bool HasInstancing(Shader shader) => shader?.isInstancingSupported ?? false;
    public static bool DoesShaderHaveMipStreamingForProperty(Shader shader, int propertyIndex) => false;
    public static int GetRenderQueue(Shader shader) => shader?.renderQueue ?? 2000;
    public static void SetRenderQueue(Shader shader, int queue) { if (shader != null) shader.renderQueue = queue; }
    public static string GetCustomEditor(Shader shader) => shader?.customEditor ?? string.Empty;
    public static string GetShaderErrorMessages() => string.Empty;
    public static bool IsShaderCompiled(Shader shader) => shader != null && shader.passCount > 0;
    public static void WarmupShaderFromCollection(Shader shader, ShaderVariantCollection collection, in ShaderVariant variant) => Shader.WarmupShaderFromCollection(shader, collection, variant);
}

public enum ShaderPropertyType
{
    Color,
    Vector,
    Float,
    Range,
    Texture,
    Int,
    Matrix
}

[Flags]
public enum ShaderPropertyFlags
{
    None = 0,
    HDR = 1 << 0,
    Gamma = 1 << 1,
    Normal = 1 << 2,
    HideInInspector = 1 << 3,
    NoScaleOffset = 1 << 4,
    PerRendererData = 1 << 5,
    MainTexture = 1 << 6,
    MainColor = 1 << 7,
}

public struct ShaderProperty
{
    public string name;
    public string description;
    public ShaderPropertyType type;
    public int nameID;
    public object defaultValue;
    public float rangeMin;
    public float rangeMax;
    public ShaderPropertyFlags flags;
    public string defaultTextureName;
}

public class SubShader
{
    public List<ShaderPass> passes = new();
    public Dictionary<string, string> tags = new(StringComparer.OrdinalIgnoreCase);
    public int lod;
    public string[] shaderRequirements = Array.Empty<string>();
    public IReadOnlyList<ShaderPass> Passes => passes;
    public string GetTag(string name, string defaultValue) => tags.TryGetValue(name, out var v) ? v : defaultValue;
}

public class ShaderPass
{
    public string name = string.Empty;
    public string lightMode = string.Empty;
    public Dictionary<string, string> tags = new(StringComparer.OrdinalIgnoreCase);
    public Rendering.BlendState blendState = Rendering.BlendState.Opaque;
    public Rendering.DepthState depthState = Rendering.DepthState.Default;
    public Rendering.RasterState rasterState = Rendering.RasterState.Default;
    public Rendering.StencilState stencilState = Rendering.StencilState.Default;
    public string vertexHLSL = string.Empty;
    public string fragmentHLSL = string.Empty;
    public string hullHLSL = string.Empty;
    public string domainHLSL = string.Empty;
    public string geometryHLSL = string.Empty;
    public string vertexEntry = "vert";
    public string fragmentEntry = "frag";
    public string hullEntry = string.Empty;
    public string domainEntry = string.Empty;
    public string geometryEntry = string.Empty;
    public string[] keywords = Array.Empty<string>();
    public bool supportsInstancing;
    public bool useSRPBatcher = true;
    public bool enabled = true;
    public int stencilRef;
    public int stencilReadMask = 255;
    public int stencilWriteMask = 255;
    public Rendering.ColorWriteMask colorMask = Rendering.ColorWriteMask.All;
    public Rendering.CullMode cullMode = Rendering.CullMode.Back;
    public bool zWrite = true;
    public CompareFunction zTest = CompareFunction.LessEqual;
    public float offsetFactor;
    public float offsetUnits;
    public string GetTag(string tagName, string defaultValue) => tags.TryGetValue(tagName, out var v) ? v : defaultValue;
}

[Flags]
public enum ShaderStage
{
    None = 0,
    Vertex = 1 << 0,
    Fragment = 1 << 1,
    Hull = 1 << 2,
    Domain = 1 << 3,
    Geometry = 1 << 4,
    All = Vertex | Fragment | Hull | Domain | Geometry
}

public struct GlobalKeyword
{
    public string name;
    public int index;
    public bool isDynamic;
    public bool isValid => !string.IsNullOrEmpty(name);
    public static GlobalKeyword Create(string name)
    {
        var id = Shader.PropertyToID(name);
        Shader._globalKeywordObjects[id] = new GlobalKeyword { name = name, index = id, isDynamic = false };
        return Shader._globalKeywordObjects[id];
    }
    public override string ToString() => name ?? string.Empty;
}

public class CompiledShader
{
    public Shader shader;
    public ShaderCompilerPlatform platform;
    public List<CompiledShaderVariant> variants = new();
    public bool isSupported;
    public List<ShaderCompilerMessage> messages = new();
}

public class CompiledShaderVariant
{
    public ShaderPass pass;
    public string[] keywords;
    public string vertexShader;
    public string fragmentShader;
    public string hullShader;
    public string domainShader;
    public string geometryShader;
    public ShaderStage stage;
    public bool instancingEnabled;
    public byte[] bytecode;
}

public class ShaderCompilerMessage
{
    public string message;
    public string file;
    public int line;
    public ShaderCompilerMessageSeverity severity;
    public ShaderCompilerPlatform platform;
}

public enum ShaderCompilerMessageSeverity { Error, Warning, Info }

public struct ShaderVariant
{
    public Shader shader;
    public PassType passType;
    public string[] keywords;
    public ShaderVariant(Shader shader, PassType passType, string[] keywords)
    {
        this.shader = shader;
        this.passType = passType;
        this.keywords = keywords ?? Array.Empty<string>();
    }
}

public enum PassType
{
    Normal = 0,
    Vertex = 1,
    VertexLM = 2,
    VertexLMRGBM = 3,
    ForwardBase = 4,
    ForwardAdd = 5,
    LightPrePassBase = 6,
    LightPrePassFinal = 7,
    ShadowCaster = 8,
    Deferred = 10,
    Meta = 11,
    MotionVectors = 12,
    ScriptableRenderPipeline = 13,
    GrabPass = 16,
    GBuffer = 14,
}

public class ShaderVariantCollection : Object
{
    private readonly List<ShaderVariantEntry> _variants = new();
    private bool _warmedUp;
    public int variantCount => _variants.Count;
    public bool isWarmedUp => _warmedUp;
    public void Add(Shader shader, PassType passType, params string[] keywords) => _variants.Add(new ShaderVariantEntry { shader = shader, passType = passType, keywords = keywords?.ToArray() ?? Array.Empty<string>() });
    public void Add(in ShaderVariant variant) => Add(variant.shader, variant.passType, variant.keywords);
    public bool Remove(Shader shader, PassType passType, params string[] keywords) => _variants.RemoveAll(v => v.shader == shader && v.passType == passType) > 0;
    public bool Contains(Shader shader, PassType passType, params string[] keywords) => _variants.Exists(v => v.shader == shader && v.passType == passType);
    public void Clear() => _variants.Clear();
    public void WarmUp() { _warmedUp = true; Shader.WarmupAllShaders(); foreach (var v in _variants) v.shader?.Compile(); }
    public ShaderVariantEntry[] GetAllVariants() => _variants.ToArray();
}

public class ShaderVariantEntry
{
    public Shader shader;
    public PassType passType;
    public string[] keywords;
}

public struct ShaderKeywordSet
{
    private HashSet<string> _keywords;
    public bool IsEnabled(ShaderKeyword kw) => _keywords?.Contains(kw.name) ?? false;
    public void Enable(ShaderKeyword kw) { _keywords ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase); _keywords.Add(kw.name); }
    public void Disable(ShaderKeyword kw) { _keywords?.Remove(kw.name); }
    public ShaderKeyword[] GetShaderKeywords() => _keywords?.Select(k => new ShaderKeyword(k)).ToArray() ?? Array.Empty<ShaderKeyword>();
}

public struct ShaderKeyword
{
    public string name;
    public ShaderKeywordType type;
    public bool isValid => !string.IsNullOrEmpty(name);
    public ShaderKeyword(string name) { this.name = name; this.type = ShaderKeywordType.BuiltinDefault; }
    public ShaderKeyword(Shader shader, string name) { this.name = name; this.type = ShaderKeywordType.BuiltinDefault; }
    public override string ToString() => name ?? string.Empty;
}

public enum ShaderKeywordType { None = 0, BuiltinDefault = 1, BuiltinExtra = 2, UserDefined = 4 }

public enum RenderQueue
{
    Background = 1000,
    Geometry = 2000,
    AlphaTest = 2450,
    GeometryLast = 2500,
    Transparent = 3000,
    Overlay = 4000
}

public static class ShaderID
{
    public static readonly int _MainTex = Shader.PropertyToID("_MainTex");
    public static readonly int _Color = Shader.PropertyToID("_Color");
    public static readonly int _BaseColor = Shader.PropertyToID("_BaseColor");
    public static readonly int _BaseMap = Shader.PropertyToID("_BaseMap");
    public static readonly int _Cutoff = Shader.PropertyToID("_Cutoff");
    public static readonly int _Glossiness = Shader.PropertyToID("_Glossiness");
    public static readonly int _Metallic = Shader.PropertyToID("_Metallic");
    public static readonly int _BumpMap = Shader.PropertyToID("_BumpMap");
    public static readonly int _BumpScale = Shader.PropertyToID("_BumpScale");
    public static readonly int _EmissionColor = Shader.PropertyToID("_EmissionColor");
    public static readonly int _EmissionMap = Shader.PropertyToID("_EmissionMap");
    public static readonly int _SrcBlend = Shader.PropertyToID("_SrcBlend");
    public static readonly int _DstBlend = Shader.PropertyToID("_DstBlend");
    public static readonly int _ZWrite = Shader.PropertyToID("_ZWrite");
    public static readonly int _ZTest = Shader.PropertyToID("_ZTest");
    public static readonly int _Cull = Shader.PropertyToID("_Cull");
    public static readonly int _WorldSpaceCameraPos = Shader.PropertyToID("_WorldSpaceCameraPos");
    public static readonly int _Time = Shader.PropertyToID("_Time");
    public static readonly int _SinTime = Shader.PropertyToID("_SinTime");
    public static readonly int _CosTime = Shader.PropertyToID("_CosTime");
    public static readonly int unity_WorldTransformParams = Shader.PropertyToID("unity_WorldTransformParams");
    public static readonly int unity_ObjectToWorld = Shader.PropertyToID("unity_ObjectToWorld");
    public static readonly int unity_WorldToObject = Shader.PropertyToID("unity_WorldToObject");
    public static readonly int unity_MatrixVP = Shader.PropertyToID("unity_MatrixVP");
    public static readonly int unity_MatrixV = Shader.PropertyToID("unity_MatrixV");
    public static readonly int unity_MatrixInvV = Shader.PropertyToID("unity_MatrixInvV");
    public static readonly int unity_MatrixP = Shader.PropertyToID("unity_MatrixP");
}

public enum ShaderHardwareTier { Tier1 = 0, Tier2 = 1, Tier3 = 2 }
public enum ShaderParamType { Float = 0, Int = 1, Bool = 2, Half = 3, Short = 4, UInt = 5 }

public class InstancingProperty
{
    public int nameID;
    public string name;
    public ShaderParamType type;
    public int arraySize = 1;
    public object defaultValue;
}

public class ConstantBuffer
{
    public string name;
    public List<InstancingProperty> properties = new();
    public int size;
    public bool isPerMaterial;
    public bool isPerInstance;
}

public class GraphicsBuffer : IDisposable
{
    private int _count;
    private int _stride;
    private GraphicsBuffer.Target _target;
    private byte[] _data;
    private bool _released;
    private uint _counterValue;

    public int count { get => _count; set => _count = value; }
    public int stride => _stride;
    public GraphicsBuffer.Target target => _target;

    public GraphicsBuffer(GraphicsBuffer.Target target, int count, int stride)
    {
        if (count <= 0) throw new ArgumentException("Count must be > 0", nameof(count));
        if (stride <= 0) throw new ArgumentException("Stride must be > 0", nameof(stride));
        _target = target;
        _count = count;
        _stride = stride;
        _data = new byte[count * stride];
    }

    public void SetData(Array data)
    {
        if (_released) throw new InvalidOperationException("GraphicsBuffer released");
        if (data == null) throw new ArgumentNullException(nameof(data));
        int bytes = System.Buffer.ByteLength(data);
        System.Buffer.BlockCopy(data, 0, _data, 0, Math.Min(bytes, _data.Length));
    }

    public void SetData(Array data, int managedBufferStartIndex, int graphicsBufferStartIndex, int count)
    {
        if (_released) throw new InvalidOperationException("GraphicsBuffer released");
        if (data == null) throw new ArgumentNullException(nameof(data));
        int elementSize = System.Buffer.ByteLength(data) / Math.Max(1, data.Length);
        int srcOffset = managedBufferStartIndex * elementSize;
        int dstOffset = graphicsBufferStartIndex * _stride;
        int byteCount = count * Math.Min(elementSize, _stride);
        if (srcOffset + byteCount <= System.Buffer.ByteLength(data) && dstOffset + byteCount <= _data.Length)
            System.Buffer.BlockCopy(data, srcOffset, _data, dstOffset, byteCount);
    }

    public void GetData(Array data)
    {
        if (_released) throw new InvalidOperationException("GraphicsBuffer released");
        if (data == null) throw new ArgumentNullException(nameof(data));
        int bytes = System.Buffer.ByteLength(data);
        System.Buffer.BlockCopy(_data, 0, data, 0, Math.Min(_data.Length, bytes));
    }

    public void GetData(Array data, int managedBufferStartIndex, int graphicsBufferStartIndex, int count)
    {
        if (_released) throw new InvalidOperationException("GraphicsBuffer released");
        if (data == null) throw new ArgumentNullException(nameof(data));
        int elementSize = System.Buffer.ByteLength(data) / Math.Max(1, data.Length);
        int srcOffset = graphicsBufferStartIndex * _stride;
        int dstOffset = managedBufferStartIndex * elementSize;
        int byteCount = count * Math.Min(elementSize, _stride);
        if (srcOffset + byteCount <= _data.Length && dstOffset + byteCount <= System.Buffer.ByteLength(data))
            System.Buffer.BlockCopy(_data, srcOffset, data, dstOffset, byteCount);
    }

    public void SetCounterValue(uint counterValue) { _counterValue = counterValue; }
    public void SetData<T>(List<T> data) where T : struct { if (data != null) SetData(data.ToArray()); }
    public void GetData<T>(List<T> data) where T : struct { _ = data; }

    public IntPtr GetNativeBufferPtr() => IntPtr.Zero;
    public bool IsValid() => !_released && _data != null;

    internal bool TryGetReadbackData(int size, int offset, out byte[] data)
    {
        data = Array.Empty<byte>();
        if (_released || _data == null || size < 0 || offset < 0 || offset > _data.Length || size > _data.Length - offset)
            return false;
        data = new byte[size];
        Buffer.BlockCopy(_data, offset, data, 0, size);
        return true;
    }

    public void Release()
    {
        if (!_released)
        {
            _data = null;
            _count = 0;
            _released = true;
        }
    }

    public void Dispose() => Release();

    [Flags]
    public enum Target
    {
        Index = 1,
        Vertex = 2,
        CopySource = 4,
        CopyDestination = 8,
        Structured = 16,
        Raw = 32,
        Append = 64,
        Counter = 128,
        IndirectArguments = 256,
        Constant = 512
    }
}

public struct MaterialProperty
{
    public string name;
    public int nameID;
    public MaterialPropertyType type;
    public float floatValue;
    public Color colorValue;
    public Vector4 vectorValue;
    public Texture textureValue;
    public MaterialPropertyFlags flags;
    public Vector2 rangeLimits;
    public TextureDimension textureDimension;

    public Vector2 textureScaleOffset { get; set; }

    public MaterialProperty(Shader shader, int propertyIndex)
    {
        name = string.Empty;
        nameID = 0;
        type = MaterialPropertyType.Float;
        floatValue = 0f;
        colorValue = Color.white;
        vectorValue = Vector4.zero;
        textureValue = null;
        flags = MaterialPropertyFlags.None;
        rangeLimits = Vector2.zero;
        textureDimension = TextureDimension.Tex2D;
        textureScaleOffset = Vector2.one;

        if (shader == null || propertyIndex < 0 || propertyIndex >= shader.properties.Count)
            return;

        var sp = shader.properties[propertyIndex];
        name = sp.name;
        nameID = sp.nameID;
        type = (MaterialPropertyType)sp.type;
        flags = (MaterialPropertyFlags)sp.flags;
        rangeLimits = new Vector2(sp.rangeMin, sp.rangeMax);
        textureDimension = shader.GetTexDim(propertyIndex);
        textureScaleOffset = Vector2.one;

        if (sp.defaultValue is float f) floatValue = f;
        else if (sp.defaultValue is int i) floatValue = i;
        else if (sp.defaultValue is Color c) colorValue = c;
        else if (sp.defaultValue is Vector4 v) vectorValue = v;
        else if (sp.defaultValue is Texture t) textureValue = t;
    }
}

[Flags]
public enum MaterialPropertyFlags
{
    None = 0,
    HDR = 1 << 0,
    Gamma = 1 << 1,
    Normal = 1 << 2,
    HideInInspector = 1 << 3,
    NoScaleOffset = 1 << 4,
    PerRendererData = 1 << 5,
    MainTexture = 1 << 6,
    MainColor = 1 << 7,
}
