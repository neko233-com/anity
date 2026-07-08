using System;
using System.Linq;
using UnityEngine;

namespace UnityEditor;

public sealed class SceneView : EditorWindow
{
  private static SceneView? _lastActive;
  private static readonly System.Collections.Generic.List<SceneView> _all = [];

  public static event Action<SceneView>? duringSceneGui;
  public static event Action<SceneView>? beforeSceneGui;
  public static event Action<SceneView, SceneView>? lastActiveSceneViewChanged;

  public static SceneView lastActiveSceneView
  {
    get
    {
      if (_lastActive is not null)
      {
        return _lastActive;
      }

      if (_all.Count == 0)
      {
        _all.Add(new SceneView());
      }

      return _all[0];
    }
  }

  public static SceneView[] sceneViews
  {
    get
    {
      if (_all.Count == 0)
      {
        _all.Add(new SceneView());
      }

      return _all.ToArray();
    }
  }

  public float orthographicSize { get; set; } = 5f;
  public Vector3 pivot { get; set; }
  public Quaternion rotation { get; set; } = Quaternion.identity;
  public EventType lastEventType { get; private set; }
  public static bool autoRepaintOnSceneChange { get; set; } = false;
  public static int count => _all.Count;

  public bool orthographic { get; set; }
  public bool in2DMode { get; set; }
  public bool isRotationLocked { get; set; }
  public float nearClipPlane { get; set; } = 0.1f;
  public float farClipPlane { get; set; } = 1000f;
  public float fieldOfView { get; set; } = 60f;

  public SceneView()
  {
    _lastActive = this;
    if (!_all.Contains(this))
    {
      _all.Add(this);
    }
  }

  public static void RepaintAll()
  {
  }

  public void LookAt(Vector3 point)
  {
    pivot = point;
  }

  public void LookAt(Vector3 point, Quaternion rotation)
  {
    pivot = point;
    this.rotation = rotation;
  }

  public void LookAt(Vector3 point, Quaternion rotation, float size)
  {
    pivot = point;
    this.rotation = rotation;
    orthographicSize = size;
  }

  public void LookAt(Vector3 point, float size)
  {
    pivot = point;
    orthographicSize = size;
  }

  public void LookAt(Transform target)
  {
    if (target != null)
    {
      pivot = target.position;
    }
  }

  public void LookAt(Transform target, Vector3 worldOffset)
  {
    if (target != null)
    {
      pivot = target.position + worldOffset;
    }
  }

  public void LookAtSelected()
  {
  }

  public void LookAtSelected(float size)
  {
    orthographicSize = size;
  }

  public void ResetCameraOrientation()
  {
    rotation = Quaternion.identity;
  }

  public Vector3 ScreenToWorldPoint(Vector3 position)
  {
    return position;
  }

  public void RepaintImmediate()
  {
  }

  public void Focus()
  {
    _lastActive = this;
  }

  public static bool showGrid
  {
    get => true;
    set => _ = value;
  }

  public static SceneView DrawCreate(Rect position, string title)
  {
    var sceneView = new SceneView();
    sceneView.titleContent = new GUIContent(title);
    return sceneView;
  }

  public static Matrix4x4 GetAllSceneCamerasProjection()
  {
    return Matrix4x4.identity;
  }
}
