using System;
using System.Collections.Generic;

namespace UnityEngine;

public enum GizmoCommandType
{
  DrawLine,
  DrawRay,
  DrawWireSphere,
  DrawSphere,
  DrawWireCube,
  DrawCube,
  DrawIcon,
  DrawGUITexture,
  DrawFrustum,
  DrawMesh,
  DrawWireMesh,
  DrawMeshInstanced
}

public struct GizmoCommand
{
  public GizmoCommandType type;
  public Color color;
  public Matrix4x4 matrix;
  public Vector3 from;
  public Vector3 to;
  public Vector3 center;
  public float radius;
  public Vector3 size;
  public Mesh mesh;
  public Material material;
  public int layer;
  public Camera camera;
  public string name;
  public bool allowScaling;
  public Rect screenRect;
  public Texture texture;
  public float fov;
  public float maxRange;
  public float minRange;
  public float aspect;
  public Matrix4x4[] matrices;
  public int submeshIndex;
  public MaterialPropertyBlock properties;
  public int castShadows;
  public int receiveShadows;
  public int renderingLayerMask;
  public MaterialPropertyBlock lightProbeProperties;
  public bool depthTest;
  public float duration;
}

public static class Gizmos
{
  private static Color _color = Color.white;
  private static Matrix4x4 _matrix = Matrix4x4.identity;
  private static float _exposure = 1f;
  private static readonly List<GizmoCommand> _commands = new();

  public static Color color
  {
    get => _color;
    set => _color = value;
  }

  public static Matrix4x4 matrix
  {
    get => _matrix;
    set => _matrix = value;
  }

  public static float exposure
  {
    get => _exposure;
    set => _exposure = value;
  }

  internal static IReadOnlyList<GizmoCommand> commands => _commands.AsReadOnly();

  internal static void ClearCommands()
  {
    _commands.Clear();
  }

  private static void AddCommand(GizmoCommand cmd)
  {
    cmd.color = _color;
    cmd.matrix = _matrix;
    _commands.Add(cmd);
  }

  public static void DrawRay(Ray r)
  {
    DrawRay(r.origin, r.direction);
  }

  public static void DrawRay(Vector3 from, Vector3 direction)
  {
    AddCommand(new GizmoCommand
    {
      type = GizmoCommandType.DrawRay,
      from = from,
      to = from + direction
    });
  }

  public static void DrawLine(Vector3 from, Vector3 to)
  {
    AddCommand(new GizmoCommand
    {
      type = GizmoCommandType.DrawLine,
      from = from,
      to = to
    });
  }

  public static void DrawLine(Vector3 from, Vector3 to, Color color)
  {
    var oldColor = _color;
    _color = color;
    DrawLine(from, to);
    _color = oldColor;
  }

  public static void DrawWireSphere(Vector3 center, float radius)
  {
    AddCommand(new GizmoCommand
    {
      type = GizmoCommandType.DrawWireSphere,
      center = center,
      radius = radius
    });
  }

  public static void DrawSphere(Vector3 center, float radius)
  {
    AddCommand(new GizmoCommand
    {
      type = GizmoCommandType.DrawSphere,
      center = center,
      radius = radius
    });
  }

  public static void DrawWireCube(Vector3 center, Vector3 size)
  {
    AddCommand(new GizmoCommand
    {
      type = GizmoCommandType.DrawWireCube,
      center = center,
      size = size
    });
  }

  public static void DrawCube(Vector3 center, Vector3 size)
  {
    AddCommand(new GizmoCommand
    {
      type = GizmoCommandType.DrawCube,
      center = center,
      size = size
    });
  }

  public static void DrawMesh(Mesh mesh)
  {
    DrawMesh(mesh, Vector3.zero, Quaternion.identity, Vector3.one);
  }

  public static void DrawMesh(Mesh mesh, Vector3 position)
  {
    DrawMesh(mesh, position, Quaternion.identity, Vector3.one);
  }

  public static void DrawMesh(Mesh mesh, Vector3 position, Quaternion rotation)
  {
    DrawMesh(mesh, position, rotation, Vector3.one);
  }

  public static void DrawMesh(Mesh mesh, Vector3 position, Quaternion rotation, Vector3 scale)
  {
    AddCommand(new GizmoCommand
    {
      type = GizmoCommandType.DrawMesh,
      mesh = mesh,
      from = position,
      center = rotation.eulerAngles,
      size = scale
    });
  }

  public static void DrawWireMesh(Mesh mesh)
  {
    DrawWireMesh(mesh, Vector3.zero, Quaternion.identity, Vector3.one);
  }

  public static void DrawWireMesh(Mesh mesh, Vector3 position)
  {
    DrawWireMesh(mesh, position, Quaternion.identity, Vector3.one);
  }

  public static void DrawWireMesh(Mesh mesh, Vector3 position, Quaternion rotation)
  {
    DrawWireMesh(mesh, position, rotation, Vector3.one);
  }

  public static void DrawWireMesh(Mesh mesh, Vector3 position, Quaternion rotation, Vector3 scale)
  {
    AddCommand(new GizmoCommand
    {
      type = GizmoCommandType.DrawWireMesh,
      mesh = mesh,
      from = position,
      center = rotation.eulerAngles,
      size = scale
    });
  }

  public static void DrawIcon(Vector3 center, string name, bool allowScaling)
  {
    AddCommand(new GizmoCommand
    {
      type = GizmoCommandType.DrawIcon,
      center = center,
      name = name,
      allowScaling = allowScaling
    });
  }

  public static void DrawIcon(Vector3 center, string name)
  {
    DrawIcon(center, name, true);
  }

  public static void DrawGUITexture(Rect screenRect, Texture texture)
  {
    AddCommand(new GizmoCommand
    {
      type = GizmoCommandType.DrawGUITexture,
      screenRect = screenRect,
      texture = texture
    });
  }

  public static void DrawGUITexture(Rect screenRect, Texture texture, int leftBorder, int rightBorder, int topBorder, int bottomBorder)
  {
    DrawGUITexture(screenRect, texture);
  }

  public static void DrawGUITexture(Rect screenRect, Texture texture, Material mat)
  {
    AddCommand(new GizmoCommand
    {
      type = GizmoCommandType.DrawGUITexture,
      screenRect = screenRect,
      texture = texture,
      material = mat
    });
  }

  public static void DrawGUITexture(Rect screenRect, Texture texture, int leftBorder, int rightBorder, int topBorder, int bottomBorder, Material mat)
  {
    DrawGUITexture(screenRect, texture, mat);
  }

  public static void DrawFrustum(Vector3 center, float fov, float maxRange, float minRange, float aspect)
  {
    AddCommand(new GizmoCommand
    {
      type = GizmoCommandType.DrawFrustum,
      center = center,
      fov = fov,
      maxRange = maxRange,
      minRange = minRange,
      aspect = aspect
    });
  }

  public static void DrawMesh(Mesh mesh, Matrix4x4 matrix)
  {
    AddCommand(new GizmoCommand
    {
      type = GizmoCommandType.DrawMesh,
      mesh = mesh,
      matrix = matrix
    });
  }

  public static void DrawMesh(Mesh mesh, Matrix4x4 matrix, Material material)
  {
    AddCommand(new GizmoCommand
    {
      type = GizmoCommandType.DrawMesh,
      mesh = mesh,
      matrix = matrix,
      material = material
    });
  }

  public static void DrawMesh(Mesh mesh, Matrix4x4 matrix, Material material, int layer)
  {
    AddCommand(new GizmoCommand
    {
      type = GizmoCommandType.DrawMesh,
      mesh = mesh,
      matrix = matrix,
      material = material,
      layer = layer
    });
  }

  public static void DrawMesh(Mesh mesh, Matrix4x4 matrix, Material material, int layer, Camera camera)
  {
    AddCommand(new GizmoCommand
    {
      type = GizmoCommandType.DrawMesh,
      mesh = mesh,
      matrix = matrix,
      material = material,
      layer = layer,
      camera = camera
    });
  }

  public static void DrawWireMesh(Mesh mesh, Matrix4x4 matrix)
  {
    AddCommand(new GizmoCommand
    {
      type = GizmoCommandType.DrawWireMesh,
      mesh = mesh,
      matrix = matrix
    });
  }

  public static void DrawWireMesh(Mesh mesh, Matrix4x4 matrix, Material material)
  {
    AddCommand(new GizmoCommand
    {
      type = GizmoCommandType.DrawWireMesh,
      mesh = mesh,
      matrix = matrix,
      material = material
    });
  }

  public static void DrawWireMesh(Mesh mesh, Matrix4x4 matrix, Material material, int layer)
  {
    AddCommand(new GizmoCommand
    {
      type = GizmoCommandType.DrawWireMesh,
      mesh = mesh,
      matrix = matrix,
      material = material,
      layer = layer
    });
  }

  public static void DrawWireMesh(Mesh mesh, Matrix4x4 matrix, Material material, int layer, Camera camera)
  {
    AddCommand(new GizmoCommand
    {
      type = GizmoCommandType.DrawWireMesh,
      mesh = mesh,
      matrix = matrix,
      material = material,
      layer = layer,
      camera = camera
    });
  }

  public static void DrawMeshInstanced(Mesh mesh, Matrix4x4[] matrices)
  {
    AddCommand(new GizmoCommand
    {
      type = GizmoCommandType.DrawMeshInstanced,
      mesh = mesh,
      matrices = matrices
    });
  }

  public static void DrawMeshInstanced(Mesh mesh, Matrix4x4[] matrices, Material material)
  {
    DrawMeshInstanced(mesh, matrices, material, 0, null, 0, null, -1, -1, -1, null);
  }

  public static void DrawMeshInstanced(Mesh mesh, Matrix4x4[] matrices, Material material, int layer)
  {
    DrawMeshInstanced(mesh, matrices, material, layer, null, 0, null, -1, -1, -1, null);
  }

  public static void DrawMeshInstanced(Mesh mesh, Matrix4x4[] matrices, Material material, int layer, Camera camera)
  {
    DrawMeshInstanced(mesh, matrices, material, layer, camera, 0, null, -1, -1, -1, null);
  }

  public static void DrawMeshInstanced(Mesh mesh, Matrix4x4[] matrices, Material material, int layer, Camera camera, int submeshIndex)
  {
    DrawMeshInstanced(mesh, matrices, material, layer, camera, submeshIndex, null, -1, -1, -1, null);
  }

  public static void DrawMeshInstanced(Mesh mesh, Matrix4x4[] matrices, Material material, int layer, Camera camera, int submeshIndex, MaterialPropertyBlock properties)
  {
    DrawMeshInstanced(mesh, matrices, material, layer, camera, submeshIndex, properties, -1, -1, -1, null);
  }

  public static void DrawMeshInstanced(Mesh mesh, Matrix4x4[] matrices, Material material, int layer, Camera camera, int submeshIndex, MaterialPropertyBlock properties, int castShadows)
  {
    DrawMeshInstanced(mesh, matrices, material, layer, camera, submeshIndex, properties, castShadows, -1, -1, null);
  }

  public static void DrawMeshInstanced(Mesh mesh, Matrix4x4[] matrices, Material material, int layer, Camera camera, int submeshIndex, MaterialPropertyBlock properties, int castShadows, int receiveShadows)
  {
    DrawMeshInstanced(mesh, matrices, material, layer, camera, submeshIndex, properties, castShadows, receiveShadows, -1, null);
  }

  public static void DrawMeshInstanced(Mesh mesh, Matrix4x4[] matrices, Material material, int layer, Camera camera, int submeshIndex, MaterialPropertyBlock properties, int castShadows, int receiveShadows, int renderingLayerMask)
  {
    DrawMeshInstanced(mesh, matrices, material, layer, camera, submeshIndex, properties, castShadows, receiveShadows, renderingLayerMask, null);
  }

  public static void DrawMeshInstanced(Mesh mesh, Matrix4x4[] matrices, Material material, int layer, Camera camera, int submeshIndex, MaterialPropertyBlock properties, int castShadows, int receiveShadows, int renderingLayerMask, MaterialPropertyBlock lightProbeProperties)
  {
    AddCommand(new GizmoCommand
    {
      type = GizmoCommandType.DrawMeshInstanced,
      mesh = mesh,
      matrices = matrices,
      material = material,
      layer = layer,
      camera = camera,
      submeshIndex = submeshIndex,
      properties = properties,
      castShadows = castShadows,
      receiveShadows = receiveShadows,
      renderingLayerMask = renderingLayerMask,
      lightProbeProperties = lightProbeProperties
    });
  }

  public static void DrawRay(Ray r, Color color)
  {
    var oldColor = _color;
    _color = color;
    DrawRay(r);
    _color = oldColor;
  }

  public static void DrawRay(Vector3 from, Vector3 direction, Color color)
  {
    var oldColor = _color;
    _color = color;
    DrawRay(from, direction);
    _color = oldColor;
  }

  public static void DrawLine(Vector3 start, Vector3 end, Color color, float duration)
  {
    var oldColor = _color;
    _color = color;
    AddCommand(new GizmoCommand
    {
      type = GizmoCommandType.DrawLine,
      from = start,
      to = end,
      duration = duration
    });
    _color = oldColor;
  }

  public static void DrawLine(Vector3 start, Vector3 end, Color color, float duration, bool depthTest)
  {
    var oldColor = _color;
    _color = color;
    AddCommand(new GizmoCommand
    {
      type = GizmoCommandType.DrawLine,
      from = start,
      to = end,
      duration = duration,
      depthTest = depthTest
    });
    _color = oldColor;
  }

  public static void DrawWireSphere(Vector3 center, float radius, Color color)
  {
    var oldColor = _color;
    _color = color;
    DrawWireSphere(center, radius);
    _color = oldColor;
  }

  public static void DrawSphere(Vector3 center, float radius, Color color)
  {
    var oldColor = _color;
    _color = color;
    DrawSphere(center, radius);
    _color = oldColor;
  }

  public static void DrawWireCube(Vector3 center, Vector3 size, Color color)
  {
    var oldColor = _color;
    _color = color;
    DrawWireCube(center, size);
    _color = oldColor;
  }

  public static void DrawCube(Vector3 center, Vector3 size, Color color)
  {
    var oldColor = _color;
    _color = color;
    DrawCube(center, size);
    _color = oldColor;
  }

  public static void DrawMesh(Mesh mesh, Color color)
  {
    var oldColor = _color;
    _color = color;
    DrawMesh(mesh);
    _color = oldColor;
  }

  public static void DrawMesh(Mesh mesh, Vector3 position, Color color)
  {
    var oldColor = _color;
    _color = color;
    DrawMesh(mesh, position);
    _color = oldColor;
  }

  public static void DrawMesh(Mesh mesh, Vector3 position, Quaternion rotation, Color color)
  {
    var oldColor = _color;
    _color = color;
    DrawMesh(mesh, position, rotation);
    _color = oldColor;
  }

  public static void DrawMesh(Mesh mesh, Vector3 position, Quaternion rotation, Vector3 scale, Color color)
  {
    var oldColor = _color;
    _color = color;
    DrawMesh(mesh, position, rotation, scale);
    _color = oldColor;
  }

  public static void DrawWireMesh(Mesh mesh, Color color)
  {
    var oldColor = _color;
    _color = color;
    DrawWireMesh(mesh);
    _color = oldColor;
  }

  public static void DrawWireMesh(Mesh mesh, Vector3 position, Color color)
  {
    var oldColor = _color;
    _color = color;
    DrawWireMesh(mesh, position);
    _color = oldColor;
  }

  public static void DrawWireMesh(Mesh mesh, Vector3 position, Quaternion rotation, Color color)
  {
    var oldColor = _color;
    _color = color;
    DrawWireMesh(mesh, position, rotation);
    _color = oldColor;
  }

  public static void DrawWireMesh(Mesh mesh, Vector3 position, Quaternion rotation, Vector3 scale, Color color)
  {
    var oldColor = _color;
    _color = color;
    DrawWireMesh(mesh, position, rotation, scale);
    _color = oldColor;
  }

  public static void DrawIcon(Vector3 center, string name, bool allowScaling, Color color)
  {
    var oldColor = _color;
    _color = color;
    DrawIcon(center, name, allowScaling);
    _color = oldColor;
  }

  public static void DrawGUITexture(Rect screenRect, Texture texture, Color color)
  {
    var oldColor = _color;
    _color = color;
    DrawGUITexture(screenRect, texture);
    _color = oldColor;
  }

  public static void DrawGUITexture(Rect screenRect, Texture texture, int leftBorder, int rightBorder, int topBorder, int bottomBorder, Color color)
  {
    var oldColor = _color;
    _color = color;
    DrawGUITexture(screenRect, texture, leftBorder, rightBorder, topBorder, bottomBorder);
    _color = oldColor;
  }

  public static void DrawGUITexture(Rect screenRect, Texture texture, Material mat, Color color)
  {
    var oldColor = _color;
    _color = color;
    DrawGUITexture(screenRect, texture, mat);
    _color = oldColor;
  }

  public static void DrawGUITexture(Rect screenRect, Texture texture, int leftBorder, int rightBorder, int topBorder, int bottomBorder, Material mat, Color color)
  {
    var oldColor = _color;
    _color = color;
    DrawGUITexture(screenRect, texture, leftBorder, rightBorder, topBorder, bottomBorder, mat);
    _color = oldColor;
  }

  public static void DrawFrustum(Vector3 center, float fov, float maxRange, float minRange, float aspect, Color color)
  {
    var oldColor = _color;
    _color = color;
    DrawFrustum(center, fov, maxRange, minRange, aspect);
    _color = oldColor;
  }

  public static void DrawMesh(Mesh mesh, Matrix4x4 matrix, Color color)
  {
    var oldColor = _color;
    _color = color;
    DrawMesh(mesh, matrix);
    _color = oldColor;
  }

  public static void DrawMesh(Mesh mesh, Matrix4x4 matrix, Material material, Color color)
  {
    var oldColor = _color;
    _color = color;
    DrawMesh(mesh, matrix, material);
    _color = oldColor;
  }

  public static void DrawMesh(Mesh mesh, Matrix4x4 matrix, Material material, int layer, Color color)
  {
    var oldColor = _color;
    _color = color;
    DrawMesh(mesh, matrix, material, layer);
    _color = oldColor;
  }

  public static void DrawMesh(Mesh mesh, Matrix4x4 matrix, Material material, int layer, Camera camera, Color color)
  {
    var oldColor = _color;
    _color = color;
    DrawMesh(mesh, matrix, material, layer, camera);
    _color = oldColor;
  }

  public static void DrawWireMesh(Mesh mesh, Matrix4x4 matrix, Color color)
  {
    var oldColor = _color;
    _color = color;
    DrawWireMesh(mesh, matrix);
    _color = oldColor;
  }

  public static void DrawWireMesh(Mesh mesh, Matrix4x4 matrix, Material material, Color color)
  {
    var oldColor = _color;
    _color = color;
    DrawWireMesh(mesh, matrix, material);
    _color = oldColor;
  }

  public static void DrawWireMesh(Mesh mesh, Matrix4x4 matrix, Material material, int layer, Color color)
  {
    var oldColor = _color;
    _color = color;
    DrawWireMesh(mesh, matrix, material, layer);
    _color = oldColor;
  }

  public static void DrawWireMesh(Mesh mesh, Matrix4x4 matrix, Material material, int layer, Camera camera, Color color)
  {
    var oldColor = _color;
    _color = color;
    DrawWireMesh(mesh, matrix, material, layer, camera);
    _color = oldColor;
  }

  public static void DrawMeshInstanced(Mesh mesh, Matrix4x4[] matrices, Color color)
  {
    var oldColor = _color;
    _color = color;
    DrawMeshInstanced(mesh, matrices);
    _color = oldColor;
  }

  public static void DrawMeshInstanced(Mesh mesh, Matrix4x4[] matrices, Material material, Color color)
  {
    var oldColor = _color;
    _color = color;
    DrawMeshInstanced(mesh, matrices, material);
    _color = oldColor;
  }

  public static void DrawMeshInstanced(Mesh mesh, Matrix4x4[] matrices, Material material, int layer, Color color)
  {
    var oldColor = _color;
    _color = color;
    DrawMeshInstanced(mesh, matrices, material, layer);
    _color = oldColor;
  }

  public static void DrawMeshInstanced(Mesh mesh, Matrix4x4[] matrices, Material material, int layer, Camera camera, Color color)
  {
    var oldColor = _color;
    _color = color;
    DrawMeshInstanced(mesh, matrices, material, layer, camera);
    _color = oldColor;
  }

  public static void DrawMeshInstanced(Mesh mesh, Matrix4x4[] matrices, Material material, int layer, Camera camera, int submeshIndex, Color color)
  {
    var oldColor = _color;
    _color = color;
    DrawMeshInstanced(mesh, matrices, material, layer, camera, submeshIndex);
    _color = oldColor;
  }

  public static void DrawMeshInstanced(Mesh mesh, Matrix4x4[] matrices, Material material, int layer, Camera camera, int submeshIndex, MaterialPropertyBlock properties, Color color)
  {
    var oldColor = _color;
    _color = color;
    DrawMeshInstanced(mesh, matrices, material, layer, camera, submeshIndex, properties);
    _color = oldColor;
  }

  public static void DrawMeshInstanced(Mesh mesh, Matrix4x4[] matrices, Material material, int layer, Camera camera, int submeshIndex, MaterialPropertyBlock properties, int castShadows, Color color)
  {
    var oldColor = _color;
    _color = color;
    DrawMeshInstanced(mesh, matrices, material, layer, camera, submeshIndex, properties, castShadows);
    _color = oldColor;
  }

  public static void DrawMeshInstanced(Mesh mesh, Matrix4x4[] matrices, Material material, int layer, Camera camera, int submeshIndex, MaterialPropertyBlock properties, int castShadows, int receiveShadows, Color color)
  {
    var oldColor = _color;
    _color = color;
    DrawMeshInstanced(mesh, matrices, material, layer, camera, submeshIndex, properties, castShadows, receiveShadows);
    _color = oldColor;
  }

  public static void DrawMeshInstanced(Mesh mesh, Matrix4x4[] matrices, Material material, int layer, Camera camera, int subMeshIndex, MaterialPropertyBlock properties, int castShadows, int receiveShadows, int renderingLayerMask, Color color)
  {
    var oldColor = _color;
    _color = color;
    DrawMeshInstanced(mesh, matrices, material, layer, camera, subMeshIndex, properties, castShadows, receiveShadows, renderingLayerMask);
    _color = oldColor;
  }

  public static void DrawMeshInstanced(Mesh mesh, Matrix4x4[] matrices, Material material, int layer, Camera camera, int subMeshIndex, MaterialPropertyBlock properties, int castShadows, int receiveShadows, int renderingLayerMask, MaterialPropertyBlock lightProbeProperties, Color color)
  {
    var oldColor = _color;
    _color = color;
    DrawMeshInstanced(mesh, matrices, material, layer, camera, subMeshIndex, properties, castShadows, receiveShadows, renderingLayerMask, lightProbeProperties);
    _color = oldColor;
  }
}

[AttributeUsage(AttributeTargets.Class)]
public class GizmoIconAttribute : Attribute
{
  public string iconPath { get; }

  public GizmoIconAttribute(string iconPath)
  {
    this.iconPath = iconPath;
  }
}
