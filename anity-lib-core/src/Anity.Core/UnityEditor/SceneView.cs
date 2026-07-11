using System;
using System.Linq;
using UnityEngine;

namespace UnityEditor;

public enum DrawCameraMode
{
    Normal = 0,
    Textured = 1,
    Wireframe = 2,
    TexturedWire = 3,
    ShadowCascades = 4,
    RenderPaths = 5,
    AlphaChannel = 6,
    Overdraw = 7,
    Mipmaps = 8,
    DeferredDiffuse = 9,
    DeferredSpecular = 10,
    DeferredSmoothness = 11,
    DeferredNormal = 12,
    Charting = 13,
    Systems = 14,
    Albedo = 15,
    Emissive = 16,
    Irradiance = 17,
    Directionality = 18,
    Baked = 19,
    Clustering = 20,
    LitClustering = 21,
    ValidateAlbedo = 22,
    ValidateMetalSpecular = 23,
    ShadowMasks = 24,
    LightOverlap = 25,
    BakedLightmap = 26,
    UpdatedLightmap = 27,
    BakedEmissive = 28,
    TextureStreaming = 29,
    GIContributorsReceivers = 30,
    RenderingModes = 31,
}

public enum SceneViewRotation
{
    Top = 0,
    Bottom = 1,
    Front = 2,
    Back = 3,
    Left = 4,
    Right = 5,
    Perspective = 6,
    Iso = 7,
}

public sealed class SceneViewState
{
    public bool showFog { get; set; } = true;
    public bool showSkybox { get; set; } = true;
    public bool showImageEffects { get; set; } = true;
    public bool showParticleSystems { get; set; } = true;
    public bool showFlares { get; set; } = true;
    public bool showVisualizationGraph { get; set; }
    public bool showMaterialUpdate { get; set; }
    public DrawCameraMode drawCameraMode { get; set; } = DrawCameraMode.Normal;
    public float sceneLighting { get; set; } = 1f;
}

public sealed class SceneView : EditorWindow
{
    private static SceneView? _lastActive;
    private static readonly System.Collections.Generic.List<SceneView> _all = [];

    private Camera _camera = null!;
    private SceneViewState _sceneViewState = new();
    private bool _drawGizmos = true;
    private bool _in2DMode;
    private float _size = 5f;
    private DrawCameraMode _renderMode = DrawCameraMode.Normal;
    private bool _showGrid = true;
    private bool _showSelectionOutline = true;
    private bool _audioPlay;

    public static event Action<SceneView>? duringSceneGui;
    public static event Action<SceneView>? beforeSceneGui;
    public static event Action<SceneView, SceneView>? lastActiveSceneViewChanged;
    public event Action? onSceneGUI;

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
        set
        {
            if (_lastActive != value)
            {
                var old = _lastActive;
                _lastActive = value;
                lastActiveSceneViewChanged?.Invoke(old!, value);
            }
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

    public Camera camera
    {
        get
        {
            if (_camera == null)
            {
                _camera = new Camera();
                _camera.cameraType = CameraType.SceneView;
                UpdateCameraFromView();
            }
            return _camera;
        }
    }

    public SceneViewState sceneViewState
    {
        get => _sceneViewState;
        set => _sceneViewState = value ?? new SceneViewState();
    }

    public object? rotationGizmo { get; set; }

    public float orthographicSize { get; set; } = 5f;
    public Vector3 pivot { get; set; }
    public Quaternion rotation { get; set; } = Quaternion.identity;
    public EventType lastEventType { get; private set; }
    public static bool autoRepaintOnSceneChange { get; set; } = false;
    public static int count => _all.Count;

    public bool orthographic { get; set; }

    public bool in2DMode
    {
        get => _in2DMode;
        set
        {
            if (_in2DMode != value)
            {
                _in2DMode = value;
                if (value)
                {
                    rotation = Quaternion.Euler(90f, 0f, 0f);
                    orthographic = true;
                }
            }
        }
    }

    public bool isRotationLocked { get; set; }
    public float nearClipPlane { get; set; } = 0.1f;
    public float farClipPlane { get; set; } = 1000f;
    public float fieldOfView { get; set; } = 60f;

    public float size
    {
        get => _size;
        set => _size = Mathf.Max(0.001f, value);
    }

    public bool drawGizmos
    {
        get => _drawGizmos;
        set => _drawGizmos = value;
    }

    public bool showFog
    {
        get => _sceneViewState.showFog;
        set => _sceneViewState.showFog = value;
    }

    public bool showSkybox
    {
        get => _sceneViewState.showSkybox;
        set => _sceneViewState.showSkybox = value;
    }

    public bool showImageEffects
    {
        get => _sceneViewState.showImageEffects;
        set => _sceneViewState.showImageEffects = value;
    }

    public bool showParticleSystems
    {
        get => _sceneViewState.showParticleSystems;
        set => _sceneViewState.showParticleSystems = value;
    }

    public bool showFlares
    {
        get => _sceneViewState.showFlares;
        set => _sceneViewState.showFlares = value;
    }

    public DrawCameraMode renderMode
    {
        get => _renderMode;
        set => _renderMode = value;
    }

    public bool showSelectionOutline
    {
        get => _showSelectionOutline;
        set => _showSelectionOutline = value;
    }

    public bool audioPlay
    {
        get => _audioPlay;
        set => _audioPlay = value;
    }

    public SceneView()
    {
        _lastActive = this;
        if (!_all.Contains(this))
        {
            _all.Add(this);
        }
        titleContent = new GUIContent("Scene");
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        wantsMouseMove = true;
    }

    protected override void OnGUI()
    {
        var original = current;
        current = this;
        _lastActive = this;

        beforeSceneGui?.Invoke(this);

        DrawToolbar();

        CallDuringSceneGui();

        onSceneGUI?.Invoke();

        DrawOrientationGizmo();

        current = original;
    }

    private void DrawToolbar()
    {
        GUILayout.BeginHorizontal(EditorStyles.toolbar);

        DrawRenderModePopup();

        GUILayout.Space(4);

        Draw2D3DToggle();

        DrawLightToggle();

        DrawAudioToggle();

        DrawGizmosToggle();

        GUILayout.FlexibleSpace();

        DrawViewQuickButtons();

        GUILayout.Space(4);

        DrawZoomControl();

        GUILayout.EndHorizontal();
    }

    private static readonly string[] DrawModeOptions = new[]
    {
        "Shaded",
        "Wireframe",
        "Shaded Wireframe",
        "Shadow Cascades",
        "Render Paths",
        "Alpha Channel",
        "Overdraw",
        "Mipmaps",
        "Albedo",
        "Emissive",
        "Normals",
        "Texture Streaming",
    };

    private void DrawRenderModePopup()
    {
        int selected = (int)_renderMode;
        if (selected >= DrawModeOptions.Length) selected = 0;
        int newSelected = GUILayout.Toolbar(selected, DrawModeOptions, GUILayout.Width(80));
        if (newSelected != selected)
        {
            _renderMode = (DrawCameraMode)newSelected;
        }
    }

    private void Draw2D3DToggle()
    {
        bool new2D = GUILayout.Toggle(_in2DMode, "2D", EditorStyles.toolbarButton, GUILayout.Width(30));
        if (new2D != _in2DMode)
        {
            in2DMode = new2D;
        }
    }

    private void DrawLightToggle()
    {
        bool newLight = GUILayout.Toggle(_sceneViewState.sceneLighting > 0.5f, "Lit", EditorStyles.toolbarButton, GUILayout.Width(35));
        _sceneViewState.sceneLighting = newLight ? 1f : 0f;
    }

    private void DrawAudioToggle()
    {
        _audioPlay = GUILayout.Toggle(_audioPlay, "Audio", EditorStyles.toolbarButton, GUILayout.Width(45));
    }

    private void DrawGizmosToggle()
    {
        _drawGizmos = GUILayout.Toggle(_drawGizmos, "Gizmos", EditorStyles.toolbarButton, GUILayout.Width(50));
    }

    private void DrawViewQuickButtons()
    {
        GUILayout.BeginHorizontal();

        if (GUILayout.Button("Top", EditorStyles.toolbarButton, GUILayout.Width(35)))
            SetupViewRotation(SceneViewRotation.Top);
        if (GUILayout.Button("Bottom", EditorStyles.toolbarButton, GUILayout.Width(45)))
            SetupViewRotation(SceneViewRotation.Bottom);
        if (GUILayout.Button("Front", EditorStyles.toolbarButton, GUILayout.Width(40)))
            SetupViewRotation(SceneViewRotation.Front);
        if (GUILayout.Button("Back", EditorStyles.toolbarButton, GUILayout.Width(38)))
            SetupViewRotation(SceneViewRotation.Back);
        if (GUILayout.Button("Left", EditorStyles.toolbarButton, GUILayout.Width(35)))
            SetupViewRotation(SceneViewRotation.Left);
        if (GUILayout.Button("Right", EditorStyles.toolbarButton, GUILayout.Width(38)))
            SetupViewRotation(SceneViewRotation.Right);
        if (GUILayout.Button(orthographic ? "Iso" : "Persp", EditorStyles.toolbarButton, GUILayout.Width(42)))
            TogglePerspectiveOrIso();

        GUILayout.EndHorizontal();
    }

    private void TogglePerspectiveOrIso()
    {
        orthographic = !orthographic;
        if (orthographic)
        {
            SetupViewRotation(SceneViewRotation.Iso);
        }
        else
        {
            SetupViewRotation(SceneViewRotation.Perspective);
        }
    }

    private void DrawZoomControl()
    {
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("-", EditorStyles.toolbarButton, GUILayout.Width(20)))
        {
            size = _size * 1.25f;
            orthographicSize = _size;
        }
        GUILayout.Label(_size.ToString("F1"), EditorStyles.miniLabel, GUILayout.Width(40));
        if (GUILayout.Button("+", EditorStyles.toolbarButton, GUILayout.Width(20)))
        {
            size = _size * 0.8f;
            orthographicSize = _size;
        }
        GUILayout.EndHorizontal();
    }

    private void DrawOrientationGizmo()
    {
    }

    private void CallDuringSceneGui()
    {
        duringSceneGui?.Invoke(this);
        Handles.CurrentCamera = camera;
    }

    private void UpdateCameraFromView()
    {
        var cam = camera;
        cam.fieldOfView = fieldOfView;
        cam.nearClipPlane = nearClipPlane;
        cam.farClipPlane = farClipPlane;
        cam.orthographic = orthographic || _in2DMode;
        cam.orthographicSize = orthographicSize > 0 ? orthographicSize : _size;
        cam.pixelWidth = (int)position.width;
        cam.pixelHeight = (int)Math.Max(1f, position.height - 20f);
    }

    private float CalculateCameraDistance()
    {
        if (orthographic || _in2DMode)
        {
            return _size * 2f;
        }
        return _size / Mathf.Tan(fieldOfView * 0.5f * Mathf.Deg2Rad);
    }

    public static void RepaintAll()
    {
        foreach (var sv in _all)
        {
            sv.Repaint();
        }
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
        this.size = size;
        orthographicSize = size;
    }

    public void LookAt(Vector3 point, float size)
    {
        pivot = point;
        this.size = size;
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
        FrameSelected();
    }

    public void LookAtSelected(float size)
    {
        FrameSelected(size);
    }

    public void ResetCameraOrientation()
    {
        rotation = Quaternion.identity;
        orthographic = false;
        _in2DMode = false;
    }

    public void FrameSelected()
    {
        FrameSelected(false);
    }

    public void FrameSelected(bool frame)
    {
        var selected = Selection.GetFiltered<GameObject>(SelectionMode.Unfiltered);
        if (selected.Length > 0)
        {
            Frame(selected.Select(go => go.transform).ToArray(), frame);
        }
        else
        {
            LookAt(Vector3.zero, 10f);
        }
    }

    public void FrameSelected(float size)
    {
        var selected = Selection.GetFiltered<GameObject>(SelectionMode.Unfiltered);
        if (selected.Length > 0)
        {
            Frame(selected.Select(go => go.transform).ToArray(), 0f, false);
            this.size = size;
            orthographicSize = size;
        }
        else
        {
            LookAt(Vector3.zero, size);
        }
    }

    public void Frame(Transform[] transforms, bool instant)
    {
        Frame(transforms, 2f, instant);
    }

    public void Frame(Transform[] transforms, float frameTarget, bool instant)
    {
        _ = instant;
        if (transforms == null || transforms.Length == 0)
            return;

        Bounds bounds = new Bounds(transforms[0].position, Vector3.one * 0.1f);
        foreach (var t in transforms)
        {
            if (t != null)
            {
                var renderer = t.GetComponent<Renderer>();
                if (renderer != null)
                {
                    bounds.Encapsulate(renderer.bounds);
                }
                else
                {
                    bounds.Encapsulate(t.position);
                }
            }
        }

        pivot = bounds.center;
        float maxExtent = Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z);
        size = maxExtent * frameTarget;
        orthographicSize = size;
    }

    public void Frame(Bounds bounds, bool instant)
    {
        _ = instant;
        pivot = bounds.center;
        float maxExtent = Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z);
        size = maxExtent * 2f;
        orthographicSize = size;
    }

    public void SetupViewRotation(SceneViewRotation viewRotation)
    {
        switch (viewRotation)
        {
            case SceneViewRotation.Top:
                rotation = Quaternion.Euler(90f, 0f, 0f);
                break;
            case SceneViewRotation.Bottom:
                rotation = Quaternion.Euler(-90f, 0f, 0f);
                break;
            case SceneViewRotation.Front:
                rotation = Quaternion.Euler(0f, 0f, 0f);
                break;
            case SceneViewRotation.Back:
                rotation = Quaternion.Euler(0f, 180f, 0f);
                break;
            case SceneViewRotation.Left:
                rotation = Quaternion.Euler(0f, -90f, 0f);
                break;
            case SceneViewRotation.Right:
                rotation = Quaternion.Euler(0f, 90f, 0f);
                break;
            case SceneViewRotation.Perspective:
                rotation = Quaternion.Euler(30f, 45f, 0f);
                orthographic = false;
                _in2DMode = false;
                break;
            case SceneViewRotation.Iso:
                rotation = Quaternion.Euler(30f, 45f, 0f);
                orthographic = true;
                break;
        }
    }

    public void AlignViewToObject(Transform t)
    {
        if (t == null) return;
        rotation = t.rotation;
        pivot = t.position;
    }

    public void AlignWithView()
    {
        var activeTransform = Selection.activeTransform;
        if (activeTransform != null)
        {
            AlignViewToObject(activeTransform);
        }
    }

    public void MoveToView()
    {
        var activeTransform = Selection.activeTransform;
        if (activeTransform != null)
        {
            float dist = DistanceToCamera(activeTransform.position);
            Vector3 cameraOffset = rotation * Vector3.back * dist;
            activeTransform.position = pivot + cameraOffset;
            activeTransform.rotation = rotation;
        }
    }

    public float DistanceToCamera(Vector3 worldPos)
    {
        Vector3 viewDir = rotation * Vector3.forward;
        Vector3 offset = worldPos - pivot;
        float dist = Vector3.Dot(offset, -viewDir);
        return Mathf.Max(0.01f, dist);
    }

    public Vector3 ViewToScreenPoint(Vector3 viewCoords)
    {
        float pixelWidth = Mathf.Max(1, (int)position.width);
        float pixelHeight = Mathf.Max(1, (int)(position.height - 20f));
        return new Vector3(
            viewCoords.x * pixelWidth,
            viewCoords.y * pixelHeight,
            viewCoords.z
        );
    }

    public Vector3 ScreenToViewPoint(Vector3 screenCoords)
    {
        float pixelWidth = Mathf.Max(1, (int)position.width);
        float pixelHeight = Mathf.Max(1, (int)(position.height - 20f));
        return new Vector3(
            screenCoords.x / pixelWidth,
            screenCoords.y / pixelHeight,
            screenCoords.z
        );
    }

    public Vector3 WorldToScreenPoint(Vector3 worldPosition)
    {
        UpdateCameraFromView();
        return camera.WorldToScreenPoint(worldPosition);
    }

    public Vector3 ScreenToWorldPoint(Vector3 position)
    {
        UpdateCameraFromView();
        return camera.ScreenToWorldPoint(position);
    }

    public void RepaintImmediate()
    {
        Repaint();
    }

    public new void Focus()
    {
        base.Focus();
        _lastActive = this;
    }

    public static bool showGrid
  {
    get
    {
      var sv = lastActiveSceneView;
      return sv != null && sv._showGrid;
    }
    set
    {
      foreach (var sv in _all)
      {
        sv._showGrid = value;
      }
    }
  }

  [MenuItem("Window/General/Scene")]
  public static SceneView ShowWindow()
  {
    return GetWindow<SceneView>("Scene");
  }

    public static SceneView DrawCreate(Rect position, string title)
    {
        var sceneView = new SceneView();
        sceneView.titleContent = new GUIContent(title);
        sceneView.position = position;
        return sceneView;
    }

    public static Matrix4x4 GetAllSceneCamerasProjection()
    {
        return lastActiveSceneView?.camera.projectionMatrix ?? Matrix4x4.identity;
    }

    public void SetSceneViewShaderReplace(Shader shader, string replaceTag)
    {
        _ = shader;
        _ = replaceTag;
    }

    public void AddDefaultItemsToMenu(GenericMenu menu, ScriptableObject view)
    {
        _ = menu;
        _ = view;
    }

    public bool IsOpenForEdit<T>(T target) where T : class
    {
        _ = target;
        return true;
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        if (_all.Contains(this))
        {
            _all.Remove(this);
        }
        if (_lastActive == this)
        {
            _lastActive = _all.Count > 0 ? _all[0] : null;
        }
    }
}
