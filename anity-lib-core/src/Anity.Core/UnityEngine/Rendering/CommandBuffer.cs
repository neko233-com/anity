using System;

namespace UnityEngine.Rendering;

public class CommandBuffer : IDisposable
{
    private string _name = string.Empty;
    private bool _disposed;

    public CommandBuffer() { }

    public CommandBuffer(string name)
    {
        _name = name ?? string.Empty;
    }

    public string name
    {
        get => _name;
        set => _name = value ?? string.Empty;
    }

    public int sizeInBytes { get; }

    public void BeginSample(string name) { }
    public void EndSample(string name) { }
    public void Clear() { }
    public void Release() { }
    public void Dispose() { _disposed = true; }

    public void DrawRenderer(Renderer renderer, Material material) { }
    public void DrawRenderer(Renderer renderer, Material material, int submeshIndex) { }
    public void DrawRenderer(Renderer renderer, Material material, int submeshIndex, int shaderPass) { }

    public void DrawMesh(Mesh mesh, Matrix4x4 matrix, Material material) { }
    public void DrawMesh(Mesh mesh, Matrix4x4 matrix, Material material, int submeshIndex) { }
    public void DrawMesh(Mesh mesh, Matrix4x4 matrix, Material material, int submeshIndex, int shaderPass) { }
    public void DrawMesh(Mesh mesh, Matrix4x4 matrix, Material material, int submeshIndex, int shaderPass, MaterialPropertyBlock properties) { }

    public void DrawProcedural(Matrix4x4 matrix, Material material, ShaderPassName shaderPassName, MeshTopology topology, int vertexCount) { }
    public void DrawProcedural(Matrix4x4 matrix, Material material, ShaderPassName shaderPassName, MeshTopology topology, int vertexCount, int instanceCount) { }

    public void DrawMeshInstanced(Mesh mesh, int submeshIndex, Material material, Matrix4x4[] matrices) { }
    public void DrawMeshInstanced(Mesh mesh, int submeshIndex, Material material, Matrix4x4[] matrices, int count) { }
    public void DrawMeshInstanced(Mesh mesh, int submeshIndex, Material material, Matrix4x4[] matrices, int count, MaterialPropertyBlock properties) { }

    public void SetGlobalFloat(string name, float value) { }
    public void SetGlobalFloat(int nameID, float value) { }
    public void SetGlobalVector(string name, Vector4 value) { }
    public void SetGlobalVector(int nameID, Vector4 value) { }
    public void SetGlobalColor(string name, Color value) { }
    public void SetGlobalColor(int nameID, Color value) { }
    public void SetGlobalMatrix(string name, Matrix4x4 value) { }
    public void SetGlobalMatrix(int nameID, Matrix4x4 value) { }
    public void SetGlobalTexture(string name, RenderTargetIdentifier value) { }
    public void SetGlobalTexture(int nameID, RenderTargetIdentifier value) { }
    public void SetGlobalInt(string name, int value) { }
    public void SetGlobalInt(int nameID, int value) { }

    public void ClearRenderTarget(bool clearDepth, bool clearColor, Color backgroundColor) { }
    public void ClearRenderTarget(bool clearDepth, bool clearColor, Color backgroundColor, float depth) { }

    public void SetRenderTarget(RenderTargetIdentifier rt) { }
    public void SetRenderTarget(RenderTargetIdentifier color, RenderTargetIdentifier depth) { }
}
