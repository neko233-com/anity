using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Rendering;

namespace UnityEngine;

public class ComputeShader : Object
{
    private readonly Dictionary<int, object> _properties = new();
    private readonly Dictionary<int, Array> _arrays = new();
    private readonly Dictionary<int, ComputeBuffer> _buffers = new();
    private readonly Dictionary<string, int> _kernelNames = new(StringComparer.Ordinal);
    private readonly HashSet<string> _keywords = new(StringComparer.OrdinalIgnoreCase);
    private int _nextKernelIndex;

    public ComputeShader()
    {
    }

    public int FindKernel(string name)
    {
        if (string.IsNullOrEmpty(name)) return -1;
        if (_kernelNames.TryGetValue(name, out var index)) return index;
        index = _nextKernelIndex++;
        _kernelNames[name] = index;
        return index;
    }

    public bool HasKernel(string name)
    {
        return !string.IsNullOrEmpty(name) && _kernelNames.ContainsKey(name);
    }

    public void Dispatch(int kernelIndex, int threadGroupsX, int threadGroupsY, int threadGroupsZ)
    {
        _ = kernelIndex;
        _ = threadGroupsX;
        _ = threadGroupsY;
        _ = threadGroupsZ;
    }

    public void SetFloat(string name, float value) => SetFloat(Shader.PropertyToID(name), value);
    public void SetFloat(int nameID, float value) => _properties[nameID] = value;
    public float GetFloat(string name) => GetFloat(Shader.PropertyToID(name));
    public float GetFloat(int nameID) => _properties.TryGetValue(nameID, out var v) ? v is float f ? f : v is int i ? i : 0f : 0f;

    public void SetInt(string name, int value) => SetInt(Shader.PropertyToID(name), value);
    public void SetInt(int nameID, int value) => _properties[nameID] = value;
    public int GetInt(string name) => GetInt(Shader.PropertyToID(name));
    public int GetInt(int nameID) => _properties.TryGetValue(nameID, out var v) ? v is int i ? i : v is float f ? (int)f : 0 : 0;

    public void SetBool(string name, bool value) => SetInt(name, value ? 1 : 0);
    public void SetBool(int nameID, bool value) => SetInt(nameID, value ? 1 : 0);
    public bool GetBool(string name) => GetInt(name) != 0;
    public bool GetBool(int nameID) => GetInt(nameID) != 0;

    public void SetVector(string name, Vector4 value) => SetVector(Shader.PropertyToID(name), value);
    public void SetVector(int nameID, Vector4 value) => _properties[nameID] = value;
    public Vector4 GetVector(string name) => GetVector(Shader.PropertyToID(name));
    public Vector4 GetVector(int nameID) => _properties.TryGetValue(nameID, out var v) ? v is Vector4 vec ? vec : v is Color c ? (Vector4)c : Vector4.zero : Vector4.zero;

    public void SetMatrix(string name, Matrix4x4 value) => SetMatrix(Shader.PropertyToID(name), value);
    public void SetMatrix(int nameID, Matrix4x4 value) => _properties[nameID] = value;
    public Matrix4x4 GetMatrix(string name) => GetMatrix(Shader.PropertyToID(name));
    public Matrix4x4 GetMatrix(int nameID) => _properties.TryGetValue(nameID, out var v) && v is Matrix4x4 m ? m : Matrix4x4.identity;

    public void SetTexture(int kernelIndex, string name, Texture? texture) => SetTexture(kernelIndex, Shader.PropertyToID(name), texture);
    public void SetTexture(int kernelIndex, int nameID, Texture? texture) { if (texture != null) _properties[nameID] = texture; else _properties.Remove(nameID); }
    public void SetTexture(int kernelIndex, string name, RenderTexture texture, int mipLevel) => SetTexture(kernelIndex, name, texture);
    public void SetTexture(int kernelIndex, int nameID, RenderTexture texture, int mipLevel) => SetTexture(kernelIndex, nameID, texture);
    public void SetTextureFromGlobal(int kernelIndex, string name, string globalTextureName) { _ = globalTextureName; }
    public void SetTextureFromGlobal(int kernelIndex, int nameID, int globalTextureNameID) { _ = globalTextureNameID; }
    public Texture? GetTexture(int kernelIndex, string name) => GetTexture(kernelIndex, Shader.PropertyToID(name));
    public Texture? GetTexture(int kernelIndex, int nameID) { _ = kernelIndex; return _properties.TryGetValue(nameID, out var v) && v is Texture t ? t : null; }

    public void SetBuffer(int kernelIndex, string name, ComputeBuffer? buffer) => SetBuffer(kernelIndex, Shader.PropertyToID(name), buffer);
    public void SetBuffer(int kernelIndex, int nameID, ComputeBuffer? buffer) { _ = kernelIndex; if (buffer != null) _buffers[nameID] = buffer; else _buffers.Remove(nameID); }
    public void SetBuffer(int kernelIndex, string name, GraphicsBuffer? buffer) { _ = kernelIndex; _ = buffer; }
    public void SetBuffer(int kernelIndex, int nameID, GraphicsBuffer? buffer) { _ = kernelIndex; _ = buffer; }
    public ComputeBuffer? GetBuffer(int kernelIndex, string name) => GetBuffer(kernelIndex, Shader.PropertyToID(name));
    public ComputeBuffer? GetBuffer(int kernelIndex, int nameID) { _ = kernelIndex; return _buffers.TryGetValue(nameID, out var b) ? b : null; }

    public void SetFloats(string name, params float[] values) => SetFloats(Shader.PropertyToID(name), values);
    public void SetFloats(int nameID, params float[] values) { if (values != null) _arrays[nameID] = (float[])values.Clone(); else _arrays.Remove(nameID); }
    public void SetInts(string name, params int[] values) => SetInts(Shader.PropertyToID(name), values);
    public void SetInts(int nameID, params int[] values) { if (values != null) _arrays[nameID] = Array.ConvertAll(values, v => (float)v); else _arrays.Remove(nameID); }
    public void SetVectors(string name, params Vector4[] values) => SetVectors(Shader.PropertyToID(name), values);
    public void SetVectors(int nameID, params Vector4[] values) { if (values != null) _arrays[nameID] = (Vector4[])values.Clone(); else _arrays.Remove(nameID); }
    public void SetMatrices(string name, params Matrix4x4[] values) => SetMatrices(Shader.PropertyToID(name), values);
    public void SetMatrices(int nameID, params Matrix4x4[] values) { if (values != null) _arrays[nameID] = (Matrix4x4[])values.Clone(); else _arrays.Remove(nameID); }

    public void SetFloatArray(string name, float[] values) => SetFloats(name, values);
    public void SetFloatArray(int nameID, float[] values) => SetFloats(nameID, values);
    public void SetIntArray(string name, int[] values) => SetInts(name, values);
    public void SetIntArray(int nameID, int[] values) => SetInts(nameID, values);
    public void SetVectorArray(string name, Vector4[] values) => SetVectors(name, values);
    public void SetVectorArray(int nameID, Vector4[] values) => SetVectors(nameID, values);
    public void SetMatrixArray(string name, Matrix4x4[] values) => SetMatrices(name, values);
    public void SetMatrixArray(int nameID, Matrix4x4[] values) => SetMatrices(nameID, values);

    public void EnableKeyword(string keyword) { if (!string.IsNullOrEmpty(keyword)) _keywords.Add(keyword); }
    public void DisableKeyword(string keyword) { if (!string.IsNullOrEmpty(keyword)) _keywords.Remove(keyword); }
    public bool IsKeywordEnabled(string keyword) => !string.IsNullOrEmpty(keyword) && _keywords.Contains(keyword);
    public void SetKeyword(string keyword, bool value) { if (value) EnableKeyword(keyword); else DisableKeyword(keyword); }

    public string[] keywordSpace => _keywords.ToArray();

    public void SetConstantBuffer(int nameID, ComputeBuffer buffer, int offset, int size) { _ = nameID; _ = buffer; _ = offset; _ = size; }
    public void SetConstantBuffer(string name, ComputeBuffer buffer, int offset, int size) => SetConstantBuffer(Shader.PropertyToID(name), buffer, offset, size);
    public void SetConstantBuffer(int nameID, GraphicsBuffer buffer, int offset, int size) { _ = nameID; _ = buffer; _ = offset; _ = size; }
    public void SetConstantBuffer(string name, GraphicsBuffer buffer, int offset, int size) => SetConstantBuffer(Shader.PropertyToID(name), buffer, offset, size);

    public void DispatchIndirect(int kernelIndex, ComputeBuffer argsBuffer, uint argsOffset = 0) { _ = kernelIndex; _ = argsBuffer; _ = argsOffset; }
    public void DispatchIndirect(int kernelIndex, GraphicsBuffer argsBuffer, uint argsOffset = 0) { _ = kernelIndex; _ = argsBuffer; _ = argsOffset; }
}
