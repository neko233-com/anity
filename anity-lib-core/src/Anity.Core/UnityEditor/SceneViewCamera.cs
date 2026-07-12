using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace UnityEditor;

/// <summary>
/// Dedicated camera used by Scene View (CameraType.SceneView).
/// Bridges editor navigation to Camera.Render / SRP context.
/// </summary>
public sealed class SceneViewCamera
{
    private readonly Camera _camera;
    private readonly SceneView? _owner;
    private bool _drawGizmos = true;
    private bool _drawGrid = true;
    private DrawCameraMode _drawMode = DrawCameraMode.Normal;

    public Camera camera => _camera;
    public SceneView? sceneView => _owner;
    public bool drawGizmos { get => _drawGizmos; set => _drawGizmos = value; }
    public bool drawGrid { get => _drawGrid; set => _drawGrid = value; }
    public DrawCameraMode drawMode { get => _drawMode; set => _drawMode = value; }

    public Vector3 position
    {
        get => _camera.transform != null ? _camera.transform.position : Vector3.zero;
        set { if (_camera.transform != null) _camera.transform.position = value; }
    }

    public Quaternion rotation
    {
        get => _camera.transform != null ? _camera.transform.rotation : Quaternion.identity;
        set { if (_camera.transform != null) _camera.transform.rotation = value; }
    }

    public float fieldOfView
    {
        get => _camera.fieldOfView;
        set => _camera.fieldOfView = value;
    }

    public bool orthographic
    {
        get => _camera.orthographic;
        set => _camera.orthographic = value;
    }

    public float orthographicSize
    {
        get => _camera.orthographicSize;
        set => _camera.orthographicSize = value;
    }

    public float nearClipPlane
    {
        get => _camera.nearClipPlane;
        set => _camera.nearClipPlane = value;
    }

    public float farClipPlane
    {
        get => _camera.farClipPlane;
        set => _camera.farClipPlane = value;
    }

    public SceneViewCamera(SceneView? owner = null)
    {
        _owner = owner;
        var go = new GameObject("SceneViewCamera") { hideFlags = UnityEngine.HideFlags.HideAndDontSave };
        _camera = go.AddComponent<Camera>();
        _camera.cameraType = CameraType.SceneView;
        _camera.clearFlags = CameraClearFlags.Skybox;
        _camera.backgroundColor = new Color(0.22f, 0.22f, 0.22f, 1f);
        _camera.fieldOfView = 60f;
        _camera.nearClipPlane = 0.03f;
        _camera.farClipPlane = 10000f;
        _camera.depth = 100f;
        _camera.allowHDR = true;
        _camera.allowMSAA = true;
    }

    public SceneViewCamera(Camera existing)
    {
        _camera = existing ?? throw new ArgumentNullException(nameof(existing));
        _camera.cameraType = CameraType.SceneView;
    }

    public void SyncFromSceneView(SceneView view)
    {
        if (view == null) return;
        orthographic = view.orthographic || view.in2DMode;
        orthographicSize = view.orthographicSize;
        fieldOfView = view.fieldOfView;
        nearClipPlane = view.nearClipPlane;
        farClipPlane = view.farClipPlane;
        _drawMode = view.renderMode;
        _drawGizmos = view.drawGizmos;

        // place camera looking at pivot
        Vector3 pivot = view.pivot;
        Quaternion rot = view.rotation;
        float dist = view.size / Mathf.Max(0.001f, Mathf.Tan(fieldOfView * 0.5f * Mathf.Deg2Rad));
        if (orthographic) dist = view.size * 2f;
        position = pivot - rot * Vector3.forward * dist;
        rotation = rot;
    }

    /// <summary>Render scene view into optional target with SRP + light probes + gizmo pass.</summary>
    public void Render(RenderTexture? target = null)
    {
        var prev = _camera.targetTexture;
        if (target != null) _camera.targetTexture = target;

        try
        {
            // Sample light probes at camera for ambient
            LightProbes.GetInterpolatedProbe(position, null, out var sh);
            RenderSettings.ambientProbe = sh;

            _camera.Render();

            if (_drawGizmos)
                DrawGizmoPass();
            if (_drawGrid)
                DrawGridPass();
        }
        finally
        {
            _camera.targetTexture = prev;
        }
    }

    public void RenderToTexture(RenderTexture target) => Render(target);

    public Ray ScreenPointToRay(Vector3 screenPoint) =>
        _camera.ScreenPointToRay(screenPoint);

    public Vector3 WorldToScreenPoint(Vector3 worldPoint) =>
        _camera.WorldToScreenPoint(worldPoint);

    public Vector3 ScreenToWorldPoint(Vector3 screenPoint) =>
        _camera.ScreenToWorldPoint(screenPoint);

    private void DrawGizmoPass()
    {
        // Gizmos draw is invoked via Gizmos / Handles in SceneView.OnGUI
        Handles.CurrentCamera = _camera;
    }

    private void DrawGridPass()
    {
        // Grid is drawn by SceneView / Handles
    }

    public void Dispose()
    {
        if (_camera != null && _camera.gameObject != null)
            UnityEngine.Object.DestroyImmediate(_camera.gameObject);
    }
}
