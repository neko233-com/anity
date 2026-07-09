using System;

namespace UnityEngine;

/// <summary>
/// Gizmos class for drawing debug visualizations.
/// </summary>
public static class Gizmos
{
    private static Color _color = Color.white;

    public static Color color
    {
        get => _color;
        set => _color = value;
    }

    public static bool matrix { get; set; } = true;

    public static void DrawRay(Ray r) { }
    public static void DrawRay(Vector3 from, Vector3 direction) { }
    public static void DrawLine(Vector3 from, Vector3 to) { }
    public static void DrawLine(Vector3 from, Vector3 to, Color color) { }
    public static void DrawWireSphere(Vector3 center, float radius) { }
    public static void DrawSphere(Vector3 center, float radius) { }
    public static void DrawWireCube(Vector3 center, Vector3 size) { }
    public static void DrawCube(Vector3 center, Vector3 size) { }
    public static void DrawMesh(Mesh mesh) { }
    public static void DrawMesh(Mesh mesh, Vector3 position) { }
    public static void DrawMesh(Mesh mesh, Vector3 position, Quaternion rotation) { }
    public static void DrawMesh(Mesh mesh, Vector3 position, Quaternion rotation, Vector3 scale) { }
    public static void DrawWireMesh(Mesh mesh) { }
    public static void DrawWireMesh(Mesh mesh, Vector3 position) { }
    public static void DrawWireMesh(Mesh mesh, Vector3 position, Quaternion rotation) { }
    public static void DrawWireMesh(Mesh mesh, Vector3 position, Quaternion rotation, Vector3 scale) { }
    public static void DrawIcon(Vector3 center, string name, bool allowScaling) { }
    public static void DrawIcon(Vector3 center, string name) { }
    public static void DrawGUITexture(Rect screenRect, Texture texture) { }
    public static void DrawGUITexture(Rect screenRect, Texture texture, int leftBorder, int rightBorder, int topBorder, int bottomBorder) { }
    public static void DrawGUITexture(Rect screenRect, Texture texture, Material mat) { }
    public static void DrawGUITexture(Rect screenRect, Texture texture, int leftBorder, int rightBorder, int topBorder, int bottomBorder, Material mat) { }
    public static void DrawFrustum(Vector3 center, float fov, float maxRange, float minRange, float aspect) { }
    public static void DrawMesh(Mesh mesh, Matrix4x4 matrix) { }
    public static void DrawMesh(Mesh mesh, Matrix4x4 matrix, Material material) { }
    public static void DrawMesh(Mesh mesh, Matrix4x4 matrix, Material material, int layer) { }
    public static void DrawMesh(Mesh mesh, Matrix4x4 matrix, Material material, int layer, Camera camera) { }
    public static void DrawWireMesh(Mesh mesh, Matrix4x4 matrix) { }
    public static void DrawWireMesh(Mesh mesh, Matrix4x4 matrix, Material material) { }
    public static void DrawWireMesh(Mesh mesh, Matrix4x4 matrix, Material material, int layer) { }
    public static void DrawWireMesh(Mesh mesh, Matrix4x4 matrix, Material material, int layer, Camera camera) { }
    public static void DrawMeshInstanced(Mesh mesh, Matrix4x4[] matrices) { }
    public static void DrawMeshInstanced(Mesh mesh, Matrix4x4[] matrices, Material material) { }
    public static void DrawMeshInstanced(Mesh mesh, Matrix4x4[] matrices, Material material, int layer) { }
    public static void DrawMeshInstanced(Mesh mesh, Matrix4x4[] matrices, Material material, int layer, Camera camera) { }
    public static void DrawMeshInstanced(Mesh mesh, Matrix4x4[] matrices, Material material, int layer, Camera camera, int submeshIndex) { }
    public static void DrawMeshInstanced(Mesh mesh, Matrix4x4[] matrices, Material material, int layer, Camera camera, int submeshIndex, MaterialPropertyBlock properties) { }
    public static void DrawMeshInstanced(Mesh mesh, Matrix4x4[] matrices, Material material, int layer, Camera camera, int submeshIndex, MaterialPropertyBlock properties, int castShadows) { }
    public static void DrawMeshInstanced(Mesh mesh, Matrix4x4[] matrices, Material material, int layer, Camera camera, int submeshIndex, MaterialPropertyBlock properties, int castShadows, int receiveShadows) { }
    public static void DrawMeshInstanced(Mesh mesh, Matrix4x4[] matrices, Material material, int layer, Camera camera, int submeshIndex, MaterialPropertyBlock properties, int castShadows, int receiveShadows, int renderingLayerMask) { }
    public static void DrawMeshInstanced(Mesh mesh, Matrix4x4[] matrices, Material material, int layer, Camera camera, int submeshIndex, MaterialPropertyBlock properties, int castShadows, int receiveShadows, int renderingLayerMask, MaterialPropertyBlock lightProbeProperties) { }

    public static void DrawRay(Ray r, Color color) { }
    public static void DrawRay(Vector3 from, Vector3 direction, Color color) { }
    public static void DrawLine(Vector3 start, Vector3 end, Color color, float duration) { }
    public static void DrawLine(Vector3 start, Vector3 end, Color color, float duration, bool depthTest) { }
    public static void DrawWireSphere(Vector3 center, float radius, Color color) { }
    public static void DrawSphere(Vector3 center, float radius, Color color) { }
    public static void DrawWireCube(Vector3 center, Vector3 size, Color color) { }
    public static void DrawCube(Vector3 center, Vector3 size, Color color) { }
    public static void DrawMesh(Mesh mesh, Color color) { }
    public static void DrawMesh(Mesh mesh, Vector3 position, Color color) { }
    public static void DrawMesh(Mesh mesh, Vector3 position, Quaternion rotation, Color color) { }
    public static void DrawMesh(Mesh mesh, Vector3 position, Quaternion rotation, Vector3 scale, Color color) { }
    public static void DrawWireMesh(Mesh mesh, Color color) { }
    public static void DrawWireMesh(Mesh mesh, Vector3 position, Color color) { }
    public static void DrawWireMesh(Mesh mesh, Vector3 position, Quaternion rotation, Color color) { }
    public static void DrawWireMesh(Mesh mesh, Vector3 position, Quaternion rotation, Vector3 scale, Color color) { }
    public static void DrawIcon(Vector3 center, string name, bool allowScaling, Color color) { }
    public static void DrawGUITexture(Rect screenRect, Texture texture, Color color) { }
    public static void DrawGUITexture(Rect screenRect, Texture texture, int leftBorder, int rightBorder, int topBorder, int bottomBorder, Color color) { }
    public static void DrawGUITexture(Rect screenRect, Texture texture, Material mat, Color color) { }
    public static void DrawGUITexture(Rect screenRect, Texture texture, int leftBorder, int rightBorder, int topBorder, int bottomBorder, Material mat, Color color) { }
    public static void DrawFrustum(Vector3 center, float fov, float maxRange, float minRange, float aspect, Color color) { }
    public static void DrawMesh(Mesh mesh, Matrix4x4 matrix, Color color) { }
    public static void DrawMesh(Mesh mesh, Matrix4x4 matrix, Material material, Color color) { }
    public static void DrawMesh(Mesh mesh, Matrix4x4 matrix, Material material, int layer, Color color) { }
    public static void DrawMesh(Mesh mesh, Matrix4x4 matrix, Material material, int layer, Camera camera, Color color) { }
    public static void DrawWireMesh(Mesh mesh, Matrix4x4 matrix, Color color) { }
    public static void DrawWireMesh(Mesh mesh, Matrix4x4 matrix, Material material, Color color) { }
    public static void DrawWireMesh(Mesh mesh, Matrix4x4 matrix, Material material, int layer, Color color) { }
    public static void DrawWireMesh(Mesh mesh, Matrix4x4 matrix, Material material, int layer, Camera camera, Color color) { }
    public static void DrawMeshInstanced(Mesh mesh, Matrix4x4[] matrices, Color color) { }
    public static void DrawMeshInstanced(Mesh mesh, Matrix4x4[] matrices, Material material, Color color) { }
    public static void DrawMeshInstanced(Mesh mesh, Matrix4x4[] matrices, Material material, int layer, Color color) { }
    public static void DrawMeshInstanced(Mesh mesh, Matrix4x4[] matrices, Material material, int layer, Camera camera, Color color) { }
    public static void DrawMeshInstanced(Mesh mesh, Matrix4x4[] matrices, Material material, int layer, Camera camera, int submeshIndex, Color color) { }
    public static void DrawMeshInstanced(Mesh mesh, Matrix4x4[] matrices, Material material, int layer, Camera camera, int submeshIndex, MaterialPropertyBlock properties, Color color) { }
    public static void DrawMeshInstanced(Mesh mesh, Matrix4x4[] matrices, Material material, int layer, Camera camera, int submeshIndex, MaterialPropertyBlock properties, int castShadows, Color color) { }
    public static void DrawMeshInstanced(Mesh mesh, Matrix4x4[] matrices, Material material, int layer, Camera camera, int submeshIndex, MaterialPropertyBlock properties, int castShadows, int receiveShadows, Color color) { }
    public static void DrawMeshInstanced(Mesh mesh, Matrix4x4[] matrices, Material material, int layer, Camera camera, int subMeshIndex, MaterialPropertyBlock properties, int castShadows, int receiveShadows, int renderingLayerMask, Color color) { }
    public static void DrawMeshInstanced(Mesh mesh, Matrix4x4[] matrices, Material material, int layer, Camera camera, int subMeshIndex, MaterialPropertyBlock properties, int castShadows, int receiveShadows, int renderingLayerMask, MaterialPropertyBlock lightProbeProperties, Color color) { }
}

/// <summary>
/// Gizmos.DrawIcon attribute for custom gizmo icons.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class GizmoIconAttribute : Attribute
{
    public string iconPath { get; }

    public GizmoIconAttribute(string iconPath)
    {
        this.iconPath = iconPath;
    }
}
