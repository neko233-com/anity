using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityEditor;

public static class Handles
{
  public static Color color { get; set; } = Color.white;
  public static Color handleColor => color;
  public static Matrix4x4 matrix { get; set; } = Matrix4x4.identity;

  public static float zTest
  {
    get => 0f;
    set => _ = value;
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

  public static void DrawWireDisc(Vector3 center, Vector3 normal, float radius)
  {
    _ = center;
    _ = normal;
    _ = radius;
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

  public static void DrawSolidRectangleWithOutline(Rect rect, Color faceColor, Color outlineColor)
  {
    _ = rect;
    _ = faceColor;
    _ = outlineColor;
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

  public static void DrawSphere(Vector3 center, float radius)
  {
    _ = center;
    _ = radius;
  }

  public static void DrawCube(Vector3 center, Vector3 size)
  {
    _ = center;
    _ = size;
  }

  public static Vector3[] HandlesToWorldPoint(Vector3[] points)
  {
    return points;
  }

  public static Vector3 WorldToGUIPoint(Vector3 position)
  {
    return position;
  }

  public static void BeginGUI() {}
  public static void EndGUI() {}

  public static void BeginChangeCheck() => EditorGUI.BeginChangeCheck();
  public static bool EndChangeCheck() => EditorGUI.EndChangeCheck();

  public static Vector3 Slider(Vector3 position, Vector3 direction, float? size = null)
  {
    _ = direction;
    _ = size;
    return position;
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

  public static Vector3 FreeMoveHandle(Vector3 position, Quaternion rotation, float size, Vector3 snap, HandlesCapFunction? func)
  {
    _ = rotation;
    _ = size;
    _ = snap;
    _ = func;
    return position;
  }

  public static Vector3 ScaleHandle(Vector3 scale, Vector3 position, Quaternion rotation, float size)
  {
    _ = rotation;
    _ = position;
    _ = size;
    return scale;
  }

  // Drawing primitives

  public static void DrawArc(Vector3 center, Vector3 normal, Vector3 from, float angle, float radius)
  {
    _ = center;
    _ = normal;
    _ = from;
    _ = angle;
    _ = radius;
  }

  public static void DrawWireArc(Vector3 center, Vector3 normal, Vector3 from, float angle, float radius)
  {
    _ = center;
    _ = normal;
    _ = from;
    _ = angle;
    _ = radius;
  }

  public static void DrawSolidArc(Vector3 center, Vector3 normal, Vector3 from, float angle, float radius)
  {
    _ = center;
    _ = normal;
    _ = from;
    _ = angle;
    _ = radius;
  }

  public static void DrawCone(Vector3 position, Vector3 direction, float angle, float length)
  {
    _ = position;
    _ = direction;
    _ = angle;
    _ = length;
  }

  public static void DrawDottedLine(Vector3 from, Vector3 to, float screenSpaceSize)
  {
    _ = from;
    _ = to;
    _ = screenSpaceSize;
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

  public static void DrawPolyLine(params Vector3[] points)
  {
    _ = points;
  }

  public static void DrawPolyLine(Color color, params Vector3[] points)
  {
    _ = color;
    _ = points;
  }

  // Interactive handles

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

  // Utility methods

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

  // Types

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
}

public delegate void HandlesCapFunction(int controlID, Vector3 position, Quaternion rotation, float size, EventType eventType);

public enum EventType
{
  MouseDown,
  MouseUp,
  MouseDrag,
  Repaint
}

public enum HandlesDrawMode
{
  Solid,
  Wire,
  Center
}
