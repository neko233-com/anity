using System;
using System.Collections.Generic;

namespace UnityEngine;

public class Shader
{
    private static readonly Dictionary<string, Shader> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, object> _properties = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _globalKeywords = new(StringComparer.OrdinalIgnoreCase);

    public string name { get; set; }
    public int renderQueue { get; set; } = 2000;
    public int passCount { get; set; } = 1;
    public bool isSupported { get; set; } = true;
    public int maximumLOD { get; set; } = 600;
    public static int globalMaximumLOD { get; set; } = 600;

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

    public object GetProperty(string name)
    {
        _properties.TryGetValue(name, out var val);
        return val;
    }

    public void SetProperty(string name, object value)
    {
        _properties[name] = value;
    }

    public bool HasProperty(string propertyName)
    {
        return _properties.ContainsKey(propertyName);
    }

    public int FindPropertyIndex(string propertyName)
    {
        int i = 0;
        foreach (var kvp in _properties)
        {
            if (string.Equals(kvp.Key, propertyName, StringComparison.OrdinalIgnoreCase))
                return i;
            i++;
        }
        return -1;
    }

    public string GetPropertyName(int propertyIndex)
    {
        int i = 0;
        foreach (var kvp in _properties)
        {
            if (i == propertyIndex)
                return kvp.Key;
            i++;
        }
        return string.Empty;
    }

    public ShaderPropertyType GetPropertyType(int propertyIndex)
    {
        return ShaderPropertyType.Color;
    }

    public static void SetGlobalFloat(int nameID, float value) { }
    public static void SetGlobalFloat(string name, float value) { }
    public static float GetGlobalFloat(string name) => 0f;
    public static float GetGlobalFloat(int nameID) => 0f;

    public static void SetGlobalInt(int nameID, int value) { }
    public static void SetGlobalInt(string name, int value) { }
    public static int GetGlobalInt(string name) => 0;
    public static int GetGlobalInt(int nameID) => 0;

    public static void SetGlobalColor(int nameID, Color value) { }
    public static void SetGlobalColor(string name, Color value) { }
    public static Color GetGlobalColor(string name) => Color.white;
    public static Color GetGlobalColor(int nameID) => Color.white;

    public static void SetGlobalVector(int nameID, Vector4 value) { }
    public static void SetGlobalVector(string name, Vector4 value) { }
    public static Vector4 GetGlobalVector(string name) => Vector4.zero;
    public static Vector4 GetGlobalVector(int nameID) => Vector4.zero;

    public static void SetGlobalMatrix(int nameID, Matrix4x4 value) { }
    public static void SetGlobalMatrix(string name, Matrix4x4 value) { }
    public static Matrix4x4 GetGlobalMatrix(string name) => Matrix4x4.identity;
    public static Matrix4x4 GetGlobalMatrix(int nameID) => Matrix4x4.identity;

    public static void SetGlobalTexture(int nameID, Texture value) { }
    public static void SetGlobalTexture(string name, Texture value) { }
    public static Texture GetGlobalTexture(string name) => null;
    public static Texture GetGlobalTexture(int nameID) => null;

    public static void SetGlobalFloatArray(int nameID, float[] values) { }
    public static void SetGlobalFloatArray(string name, float[] values) { }
    public static void SetGlobalFloatArray(int nameID, List<float> values) { }
    public static void SetGlobalFloatArray(string name, List<float> values) { }

    public static void SetGlobalVectorArray(int nameID, Vector4[] values) { }
    public static void SetGlobalVectorArray(string name, Vector4[] values) { }
    public static void SetGlobalVectorArray(int nameID, List<Vector4> values) { }
    public static void SetGlobalVectorArray(string name, List<Vector4> values) { }

    public static void SetGlobalMatrixArray(int nameID, Matrix4x4[] values) { }
    public static void SetGlobalMatrixArray(string name, Matrix4x4[] values) { }
    public static void SetGlobalMatrixArray(int nameID, List<Matrix4x4> values) { }
    public static void SetGlobalMatrixArray(string name, List<Matrix4x4> values) { }

    public static void EnableKeyword(string keyword) { }
    public static void DisableKeyword(string keyword) { }
    public static bool IsKeywordEnabled(string keyword) => false;

    public static void SetKeyword(string keyword, bool value)
    {
        if (value)
            EnableKeyword(keyword);
        else
            DisableKeyword(keyword);
    }

    public static bool globalKeywordsDirty { get; set; }
    public static bool warmupStarted { get; }

    public static int PropertyToID(string name)
    {
        return Shader.PropertyToID(name);
    }

    public static int PropertyToID(string name)
    {
        return name.GetHashCode();
    }

    public static string IdToProperty(int id)
    {
        return id.ToString();
    }

    public static void ParseSurfaceShaders() { }

    public static string FindPassName(int passNameHash)
    {
        return string.Empty;
    }

    public static bool isWarmUpSupported { get; } = true;
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

public class ComputeShader : Object
{
    public string name { get; set; }
    public int kernelCount { get; }

    public int FindKernel(string name)
    {
        return 0;
    }

    public void SetFloat(string name, float val) { }
    public void SetFloat(int nameID, float val) { }
    public void SetInt(string name, int val) { }
    public void SetInt(int nameID, int val) { }
    public void SetVector(string name, Vector4 val) { }
    public void SetVector(int nameID, Vector4 val) { }
    public void SetMatrix(string name, Matrix4x4 val) { }
    public void SetMatrix(int nameID, Matrix4x4 val) { }
    public void SetTexture(int kernelIndex, string name, Texture texture) { }
    public void SetTexture(int kernelIndex, int nameID, Texture texture) { }
    public void SetBuffer(int kernelIndex, string name, ComputeBuffer buffer) { }
    public void SetBuffer(int kernelIndex, int nameID, ComputeBuffer buffer) { }
    public void SetFloatArray(string name, float[] values) { }
    public void SetFloatArray(int nameID, float[] values) { }
    public void SetVectorArray(string name, Vector4[] values) { }
    public void SetVectorArray(int nameID, Vector4[] values) { }
    public void SetMatrixArray(string name, Matrix4x4[] values) { }
    public void SetMatrixArray(int nameID, Matrix4x4[] values) { }

    public void Dispatch(int kernelIndex, int threadGroupsX, int threadGroupsY, int threadGroupsZ) { }

    public void GetKernelThreadGroupSizes(int kernelIndex, out uint x, out uint y, out uint z)
    {
        x = 1; y = 1; z = 1;
    }

    public bool HasKernel(string name) => true;
}

public class ComputeBuffer : IDisposable
{
    public int count { get; }
    public int stride { get; }
    public ComputeBufferType type { get; }
    public string name { get; set; }
    public bool enableRandomWrite { get; set; } = true;

    public ComputeBuffer(int count, int stride)
    {
        this.count = count;
        this.stride = stride;
        this.type = ComputeBufferType.Default;
    }

    public ComputeBuffer(int count, int stride, ComputeBufferType type)
    {
        this.count = count;
        this.stride = stride;
        this.type = type;
    }

    public void SetData(Array data) { }
    public void SetData<T>(T[] data) where T : struct { }
    public void SetData<T>(List<T> data) where T : struct { }
    public void SetData(Array data, int managedBufferStartIndex, int computeBufferStartIndex, int count) { }

    public void GetData(Array data) { }
    public void GetData<T>(T[] data) where T : struct { }
    public void GetData(Array data, int managedBufferStartIndex, int computeBufferStartIndex, int count) { }

    public IntPtr GetNativeBufferPtr() => IntPtr.Zero;

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing) { }

    ~ComputeBuffer() => Dispose(false);

    public static void CopyCount(ComputeBuffer src, ComputeBuffer dst, int dstOffsetBytes) { }
}

public enum ComputeBufferType
{
    Default = 0,
    Raw = 1,
    Append = 2,
    Counter = 4,
    Constant = 8,
    Structured = 16,
    DrawIndirect = 256,
    GPUMemory = 512
}

public enum ComputeBufferMode
{
    Immutable,
    Dynamic,
    Ring,
    StreamOut
}