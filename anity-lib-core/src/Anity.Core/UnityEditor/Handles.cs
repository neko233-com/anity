using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace UnityEditor;

public static class Handles
{
  private static readonly Stack<Matrix4x4> _matrixStack = new();
  private static readonly Stack<Camera?> _cameraStack = new();
  private static Camera? _sceneCamera;

  public static Color color { get; set; } = Color.white;
  public static Color handleColor => color;
  public static Matrix4x4 matrix { get; set; } = Matrix4x4.identity;
  public static bool lighting { get; set; } = true;
  public static Camera? currentSceneCamera => _sceneCamera;
  public static Camera? camera { get => CurrentCamera; set => CurrentCamera = value; }

  public static CompareFunction zTest { get; set; } = CompareFunction.LessEqual;

  public static void DrawLine(Vector3 p1, Vector3 p2)
  {
    _ = p1;
    _ = p2;
  }

  public static void DrawLine(Vector3 from, Vector3 to, Color? handleColor = null)
  {
    _ = from;
    _ = to;
    _ = handleColor;
  }

  public static void DrawLine(List<Vector3> points, int max = -1)
  {
    _ = points;
    _ = max;
  }

  public static void DrawAAPolyLine(params Vector3[] points)
  {
    _ = points;
  }

  public static void DrawWireSphere(Vector3 center, float radius)
  {
    _ = center;
    _ = radius;
  }

  public static void DrawSphere(Vector3 center, float radius)
  {
    _ = center;
    _ = radius;
  }

  public static void DrawWireDisc(Vector3 center, Vector3 normal, float radius)
  {
    _ = center;
    _ = normal;
    _ = radius;
  }

  public static void DrawDisc(Vector3 center, Vector3 normal, float radius)
  {
    DrawSolidDisc(center, normal, radius);
  }

  public static void DrawSolidDisc(Vector3 center, Vector3 normal, float radius)
  {
    _ = center;
    _ = normal;
    _ = radius;
  }

  public static void DrawWireCube(Vector3 center, Vector3 size)
  {
    _ = center;
    _ = size;
  }

  public static void DrawCube(Vector3 center, Vector3 size)
  {
    _ = center;
    _ = size;
  }

  public static void DrawSolidRectangleWithOutline(Rect rect, Color faceColor, Color outlineColor)
  {
    _ = rect;
    _ = faceColor;
    _ = outlineColor;
  }

  public static void DrawSolidRectangleWithOutline(Vector3[] points, Color faceColor, Color outlineColor)
  {
    _ = points;
    _ = faceColor;
    _ = outlineColor;
  }

  public static void DrawAAConvexPolygon(params Vector3[] points)
  {
    _ = points;
  }

  public static void DrawAAConvexPolygon(Color color, params Vector3[] points)
  {
    _ = color;
    _ = points;
  }

  public static void DrawPolyLine(params Vector3[] points)
  {
    _ = points;
  }

  public static void DrawPolyLine(Color color, params Vector3[] points)
  {
    _ = color;
    _ = points;
  }

  public static void DrawBezier(Vector3 start, Vector3 startTangent, Vector3 end, Vector3 endTangent)
  {
    _ = start;
    _ = startTangent;
    _ = end;
    _ = endTangent;
  }

  public static void DrawBezier(Vector3 start, Vector3 startTangent, Vector3 end, Vector3 endTangent, Color color, float width, Texture2D? texture, float mipLevel)
  {
    _ = start;
    _ = startTangent;
    _ = end;
    _ = endTangent;
    _ = color;
    _ = width;
    _ = texture;
    _ = mipLevel;
  }

  public static void DrawDottedLine(Vector3 from, Vector3 to, float screenSpaceSize)
  {
    _ = from;
    _ = to;
    _ = screenSpaceSize;
  }

  public static void DrawDottedLines(Vector3[] points, float screenSpaceSize)
  {
    _ = points;
    _ = screenSpaceSize;
  }

  public static void DrawWireArc(Vector3 center, Vector3 normal, Vector3 from, float angle, float radius)
  {
    _ = center;
    _ = normal;
    _ = from;
    _ = angle;
    _ = radius;
  }

  public static void DrawArc(Vector3 center, Vector3 normal, Vector3 from, float angle, float radius)
  {
    DrawSolidArc(center, normal, from, angle, radius);
  }

  public static void DrawSolidArc(Vector3 center, Vector3 normal, Vector3 from, float angle, float radius)
  {
    _ = center;
    _ = normal;
    _ = from;
    _ = angle;
    _ = radius;
  }

  public static Quaternion LookRotation(Quaternion rotation, Vector3 position)
  {
    _ = position;
    return rotation;
  }

  public static void Camera(Vector3 position, Quaternion rotation, float size, float disc)
  {
    _ = position;
    _ = rotation;
    _ = size;
    _ = disc;
  }

  public static void DrawCamera(Camera camera)
  {
    _ = camera;
  }

  public static void DrawCamera(Rect position, Camera camera)
  {
    _ = position;
    _ = camera;
  }

  public static void Label(Vector3 position, string text)
  {
    _ = position;
    _ = text;
  }

  public static void Label(Vector3 position, GUIContent content)
  {
    _ = position;
    _ = content?.text;
  }

  public static void Label(Vector3 position, string text, GUIStyle style)
  {
    _ = position;
    _ = text;
    _ = style;
  }

  public static void Label(Vector3 position, GUIContent content, GUIStyle style)
  {
    _ = position;
    _ = content;
    _ = style;
  }

  public static bool Button(Vector3 position, Quaternion rotation, float size, float pickSize, CapFunction capFunction)
  {
    _ = position;
    _ = rotation;
    _ = size;
    _ = pickSize;
    _ = capFunction;
    return false;
  }

  public static Vector3 Slider(Vector3 position, Vector3 direction)
  {
    _ = direction;
    return position;
  }

  public static Vector3 Slider(Vector3 position, Vector3 direction, float size)
  {
    _ = direction;
    _ = size;
    return position;
  }

  public static Vector3 Slider(Vector3 position, Vector3 direction, float? size, float snap)
  {
    _ = direction;
    _ = size;
    _ = snap;
    return position;
  }

  public static Vector3 Slider(Vector3 position, Vector3 direction, float? size = null)
  {
    _ = direction;
    _ = size;
    return position;
  }

  public static float Slider(float value, float min, float max, GUIStyle? slider, GUIStyle? thumb, float snap)
  {
    _ = min;
    _ = max;
    _ = slider;
    _ = thumb;
    _ = snap;
    return value;
  }

  public static float Slider(float value, float min, float max, float size, CapFunction? slider, float snap)
  {
    _ = min;
    _ = max;
    _ = size;
    _ = slider;
    _ = snap;
    return value;
  }

  public static Vector2 Slider2D(int id, Vector3 handlePos, Vector3 offset, Vector3 handleDir, Vector3 slideDir1, Vector3 slideDir2, float handleSize, CapFunction capFunction, Vector2 snap)
  {
    _ = id;
    _ = handlePos;
    _ = offset;
    _ = handleDir;
    _ = slideDir1;
    _ = slideDir2;
    _ = handleSize;
    _ = capFunction;
    _ = snap;
    return Vector2.zero;
  }

  public static Vector2 Slider2D(int id, Vector3 handlePos, Vector3 offset, Vector3 handleDir, Vector3 slideDir1, Vector3 slideDir2, float handleSize, CapFunction capFunction)
  {
    return Slider2D(id, handlePos, offset, handleDir, slideDir1, slideDir2, handleSize, capFunction, Vector2.zero);
  }

  public static Vector3 PositionHandle(Vector3 position, Quaternion rotation)
  {
    _ = rotation;
    return position;
  }

  public static Quaternion RotationHandle(Quaternion rotation, Vector3 position)
  {
    _ = position;
    return rotation;
  }

  public static Vector3 ScaleHandle(Vector3 scale, Vector3 position, Quaternion rotation, float size)
  {
    _ = rotation;
    _ = position;
    _ = size;
    return scale;
  }

  public static Vector3 FreeMoveHandle(Vector3 position, Quaternion rotation, float size, Vector3 snap, CapFunction? func)
  {
    _ = rotation;
    _ = size;
    _ = snap;
    _ = func;
    return position;
  }

  public static Quaternion FreeRotateHandle(Quaternion rotation, Vector3 position, float size)
  {
    _ = position;
    _ = size;
    return rotation;
  }

  public static float RadiusHandle(Quaternion rotation, Vector3 position, float radius)
  {
    _ = rotation;
    _ = position;
    return radius;
  }

  public static float RadiusHandle(Quaternion rotation, Vector3 position, float radius, HandlesDrawMode caps)
  {
    _ = rotation;
    _ = position;
    _ = caps;
    return radius;
  }

  public static void ArrowHandleCap(int controlID, Vector3 position, Quaternion rotation, float size, EventType eventType)
  {
    _ = controlID;
    _ = position;
    _ = rotation;
    _ = size;
    _ = eventType;
  }

  public static void DotHandleCap(int controlID, Vector3 position, Quaternion rotation, float size, EventType eventType)
  {
    _ = controlID;
    _ = position;
    _ = rotation;
    _ = size;
    _ = eventType;
  }

  public static void SphereHandleCap(int controlID, Vector3 position, Quaternion rotation, float size, EventType eventType)
  {
    _ = controlID;
    _ = position;
    _ = rotation;
    _ = size;
    _ = eventType;
  }

  public static void CircleHandleCap(int controlID, Vector3 position, Quaternion rotation, float size, EventType eventType)
  {
    _ = controlID;
    _ = position;
    _ = rotation;
    _ = size;
    _ = eventType;
  }

  public static void ConeHandleCap(int controlID, Vector3 position, Quaternion rotation, float size, EventType eventType)
  {
    _ = controlID;
    _ = position;
    _ = rotation;
    _ = size;
    _ = eventType;
  }

  public static void CubeHandleCap(int controlID, Vector3 position, Quaternion rotation, float size, EventType eventType)
  {
    _ = controlID;
    _ = position;
    _ = rotation;
    _ = size;
    _ = eventType;
  }

  public static void RectangleHandleCap(int controlID, Vector3 position, Quaternion rotation, float size, EventType eventType)
  {
    _ = controlID;
    _ = position;
    _ = rotation;
    _ = size;
    _ = eventType;
  }

  public static void CylinderHandleCap(int controlID, Vector3 position, Quaternion rotation, float size, EventType eventType)
  {
    _ = controlID;
    _ = position;
    _ = rotation;
    _ = size;
    _ = eventType;
  }

  public static Vector3[] HandlesToWorldPoint(Vector3[] points)
  {
    return points;
  }

  public static Vector3 WorldToGUIPoint(Vector3 position)
  {
    return position;
  }

  public static void BeginGUI()
  {
    _matrixStack.Push(matrix);
    _cameraStack.Push(CurrentCamera);
    matrix = Matrix4x4.identity;
  }

  public static void EndGUI()
  {
    if (_matrixStack.Count > 0)
      matrix = _matrixStack.Pop();
    if (_cameraStack.Count > 0)
      CurrentCamera = _cameraStack.Pop();
  }

  public static void BeginChangeCheck() => EditorGUI.BeginChangeCheck();
  public static bool EndChangeCheck() => EditorGUI.EndChangeCheck();

  public static void DrawTexture(Rect position, Texture image)
  {
    _ = position;
    _ = image;
  }

  public static float WireDisc(Quaternion rotation, Vector3 position, Vector3 axis, float radius)
  {
    _ = rotation;
    _ = axis;
    DrawWireDisc(position, axis, radius);
    return radius;
  }

  public static float SolidDisc(Quaternion rotation, Vector3 position, Vector3 axis, float radius)
  {
    _ = rotation;
    _ = axis;
    DrawSolidDisc(position, axis, radius);
    return radius;
  }

  public static Quaternion Disc(Quaternion rotation, Vector3 position, Vector3 axis, bool drawPlane, float snapDegrees)
  {
    _ = position;
    _ = axis;
    _ = drawPlane;
    _ = snapDegrees;
    return rotation;
  }

  public static Quaternion Disc(Quaternion rotation, Vector3 position, Vector3 axis, bool drawPlane, float snapDegrees, float radius)
  {
    _ = position;
    _ = axis;
    _ = drawPlane;
    _ = snapDegrees;
    _ = radius;
    return rotation;
  }

  public static Quaternion FreeDisc(Vector3 position, Quaternion rotation)
  {
    _ = position;
    return rotation;
  }

  public static float DistanceHandle(Quaternion rotation, Vector3 position)
  {
    _ = rotation;
    _ = position;
    return 0f;
  }

  public static Vector3 ScaleSlider(Vector3 scale, Vector3 position, Vector3 direction, Quaternion rotation, float size, float snap)
  {
    _ = position;
    _ = direction;
    _ = rotation;
    _ = size;
    _ = snap;
    return scale;
  }

  public static Vector3 ScaleSlider(Vector3 scale, Vector3 position, Vector3 direction, Quaternion rotation, float size, float snap, Vector3 minLimit, Vector3 maxLimit)
  {
    _ = position;
    _ = direction;
    _ = rotation;
    _ = size;
    _ = snap;
    _ = minLimit;
    _ = maxLimit;
    return scale;
  }

  public static void AddDefaultControl(int controlId, float distance)
  {
    _ = controlId;
    _ = distance;
  }

  public static void SetBoneRotation(Transform bone, Quaternion rotation)
  {
    if (bone != null) bone.rotation = rotation;
  }

  public static void DrawDottedDisc(Vector3 center, Vector3 normal, float radius, float screenSpaceSize)
  {
    _ = center;
    _ = normal;
    _ = radius;
    _ = screenSpaceSize;
  }

  public static void DrawIcon(Vector3 position, string icon, bool allowScaling)
  {
    _ = position;
    _ = icon;
    _ = allowScaling;
  }

  public static void DrawAAPolyLine(float width, params Vector3[] points)
  {
    _ = width;
    _ = points;
  }

  public static void DrawAAPolyLine(Color color, float width, params Vector3[] points)
  {
    _ = color;
    _ = width;
    _ = points;
  }

  public static void DrawCylinder(Vector3 position, float radius, Quaternion rotation)
  {
    _ = position;
    _ = radius;
    _ = rotation;
  }

  public static void DrawCapsule(Vector3 start, Vector3 end, float radius)
  {
    _ = start;
    _ = end;
    _ = radius;
  }

  public static void DrawCone(Vector3 position, Vector3 direction, float angle, float length)
  {
    _ = position;
    _ = direction;
    _ = angle;
    _ = length;
  }

  public static Matrix4x4 InverseMatrix()
  {
    return matrix.inverse;
  }

  public static Camera? CurrentCamera { get; set; }

  public static float GetHandleSize(Vector3 position)
  {
    _ = position;
    return 1f;
  }

  public static Vector3[] MakeBezierPoints(Vector3 start, Vector3 startTangent, Vector3 end, Vector3 endTangent, int segments)
  {
    _ = start;
    _ = startTangent;
    _ = end;
    _ = endTangent;
    _ = segments;
    return Array.Empty<Vector3>();
  }

  public static void ClickDefaultControl(int controlID, Event evt)
  {
    _ = controlID;
    _ = evt;
  }

  public struct ControlId
  {
    public int id;

    public ControlId(int id)
    {
      this.id = id;
    }
  }

  public static void AddControl(ControlId control)
  {
    _ = control;
  }

  public static Vector3 PositionHandle(Vector3 position, Quaternion rotation, bool ignoreNodeEditor)
  {
    _ = ignoreNodeEditor;
    return PositionHandle(position, rotation);
  }

  public static Quaternion RotationHandle(Quaternion rotation, Vector3 position, bool ignoreNodeEditor)
  {
    _ = ignoreNodeEditor;
    return RotationHandle(rotation, position);
  }

  public static Vector3 ScaleHandle(Vector3 scale, Vector3 position, Quaternion rotation, float size, bool ignoreNodeEditor)
  {
    _ = ignoreNodeEditor;
    return ScaleHandle(scale, position, rotation, size);
  }
}

public delegate void CapFunction(int controlID, Vector3 position, Quaternion rotation, float size, EventType eventType);
public delegate void HandlesCapFunction(int controlID, Vector3 position, Quaternion rotation, float size, EventType eventType);

public enum HandlesDrawMode
{
  Solid,
  Wire,
  Center
}
