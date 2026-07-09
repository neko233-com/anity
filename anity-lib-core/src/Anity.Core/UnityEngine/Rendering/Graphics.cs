using System;

namespace UnityEngine.Rendering;

/// <summary>
/// Unity Graphics class for rendering operations.
/// </summary>
public static class Graphics
{
    public static void DrawMesh(Mesh mesh, Vector3 position, Quaternion rotation) { }
    public static void DrawMesh(Mesh mesh, Vector3 position, Quaternion rotation, Material material) { }
    public static void DrawMesh(Mesh mesh, Vector3 position, Quaternion rotation, Material material, int layer) { }
    public static void DrawMesh(Mesh mesh, Vector3 position, Quaternion rotation, Material material, int layer, Camera camera) { }

    public static void DrawMesh(Mesh mesh, Matrix4x4 matrix, Material material) { }
    public static void DrawMesh(Mesh mesh, Matrix4x4 matrix, Material material, int layer) { }
    public static void DrawMesh(Mesh mesh, Matrix4x4 matrix, Material material, int layer, Camera camera) { }

    public static void DrawMesh(Mesh mesh, Matrix4x4 matrix, Material material, int layer, Camera camera, int submeshIndex) { }
    public static void DrawMesh(Mesh mesh, Matrix4x4 matrix, Material material, int layer, Camera camera, int submeshIndex, MaterialPropertyBlock properties) { }

    public static void DrawMeshInstanced(Mesh mesh, int submeshIndex, Material material, Matrix4x4[] matrices) { }
    public static void DrawMeshInstanced(Mesh mesh, int submeshIndex, Material material, Matrix4x4[] matrices, int count) { }
    public static void DrawMeshInstanced(Mesh mesh, int submeshIndex, Material material, Matrix4x4[] matrices, int count, MaterialPropertyBlock properties) { }

    public static void DrawProcedural(Matrix4x4 matrix, Material material, ShaderPassName shaderPassName, MeshTopology topology, int vertexCount) { }
    public static void DrawProcedural(Matrix4x4 matrix, Material material, ShaderPassName shaderPassName, MeshTopology topology, int vertexCount, int instanceCount) { }

    public static void Blit(Texture source, RenderTexture dest) { }
    public static void Blit(Texture source, RenderTexture dest, Material mat) { }
    public static void Blit(Texture source, Material mat) { }

    public static void SetRenderTarget(RenderTargetIdentifier rt) { }
    public static void SetRenderTarget(RenderTargetIdentifier color, RenderTargetIdentifier depth) { }

    public static void ClearRenderTarget(bool clearDepth, bool clearColor, Color backgroundColor) { }
    public static void ClearRenderTarget(bool clearDepth, bool clearColor, Color backgroundColor, float depth) { }

    public static void ExecuteCommandBuffer(CommandBuffer commandBuffer) { }
    public static void ExecuteCommandBufferAsync(CommandBuffer commandBuffer, ComputeQueueType queueType) { }

    public static void CopyTexture(Texture src, Texture dst) { }
    public static void CopyTexture(Texture src, int srcElement, Texture dst, int dstElement) { }
    public static void CopyTexture(Texture src, int srcElement, int srcMip, Texture dst, int dstElement, int dstMip) { }
}

/// <summary>
/// Compute queue type.
/// </summary>
public enum ComputeQueueType
{
    Default = 0,
    Background = 1,
    Normal = 2,
    High = 3
}
