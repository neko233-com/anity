using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace UnityEditor;

/// <summary>
/// Game View — renders Camera.main (and multi-display) via Camera.Render / SRP context.
/// Unity 2022 layout: Display / Aspect / Scale / VSync / Maximize On Play.
/// </summary>
public sealed class GameView : EditorWindow
{
    private static readonly string[] AspectLabels =
    {
        "Free Aspect", "16:9", "16:10", "4:3", "5:4", "21:9", "iPhone X", "iPad"
    };

    private static readonly float[] AspectRatios =
    {
        0f, 16f / 9f, 16f / 10f, 4f / 3f, 5f / 4f, 21f / 9f, 19.5f / 9f, 4f / 3f
    };

    private int _targetDisplay;
    private int _aspectIndex;
    private float _scale = 1f;
    private bool _vSyncEnabled = true;
    private bool _maximizeOnPlay;
    private bool _muteAudio;
    private bool _stats;
    private Color _playModeTint = new Color(0.8f, 0.8f, 1f, 0.15f);
    private Vector2 _scrollPosition;
    private RenderTexture? _gameTexture;
    private int _lastWidth = 960;
    private int _lastHeight = 540;
    private string _statusLine = "No camera";

    public static GameView? instance { get; private set; }

    public int targetDisplay
    {
        get => _targetDisplay;
        set => _targetDisplay = value;
    }

    public bool vSyncEnabled
    {
        get => _vSyncEnabled;
        set => _vSyncEnabled = value;
    }

    public Color playModeTint
    {
        get => _playModeTint;
        set => _playModeTint = value;
    }

    public bool maximizeOnPlay
    {
        get => _maximizeOnPlay;
        set => _maximizeOnPlay = value;
    }

    public float scale
    {
        get => _scale;
        set => _scale = Mathf.Clamp(value, 0.1f, 5f);
    }

    public GameView()
    {
        titleContent = new GUIContent("Game");
        minSize = new Vector2(320f, 240f);
        instance = this;
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        instance = this;
        EnsureGameTexture(_lastWidth, _lastHeight);
    }

    protected override void OnDisable()
    {
        ReleaseGameTexture();
        if (instance == this) instance = null;
        base.OnDisable();
    }

    protected override void OnGUI()
    {
        DrawToolbar();
        DrawGameArea();
        if (_stats)
            DrawStats();
    }

    private void DrawToolbar()
    {
        GUILayout.BeginHorizontal(EditorStyles.toolbar);

        GUILayout.Label("Display", EditorStyles.miniLabel, GUILayout.Width(48f));
        _targetDisplay = EditorGUILayout.IntField(_targetDisplay, GUILayout.Width(28f));

        GUILayout.Space(6f);
        GUILayout.Label("Aspect", EditorStyles.miniLabel, GUILayout.Width(42f));
        _aspectIndex = EditorGUILayout.Popup(_aspectIndex, AspectLabels, GUILayout.Width(90f));

        GUILayout.Space(6f);
        GUILayout.Label("Scale", EditorStyles.miniLabel, GUILayout.Width(36f));
        _scale = GUILayout.HorizontalSlider(_scale, 0.25f, 2f, GUILayout.Width(80f));

        GUILayout.Space(6f);
        _vSyncEnabled = GUILayout.Toggle(_vSyncEnabled, "VSync", EditorStyles.toolbarButton, GUILayout.Width(48f));
        _maximizeOnPlay = GUILayout.Toggle(_maximizeOnPlay, "Max", EditorStyles.toolbarButton, GUILayout.Width(36f));
        _muteAudio = GUILayout.Toggle(_muteAudio, "Mute", EditorStyles.toolbarButton, GUILayout.Width(40f));
        _stats = GUILayout.Toggle(_stats, "Stats", EditorStyles.toolbarButton, GUILayout.Width(42f));

        GUILayout.FlexibleSpace();

        if (GUILayout.Button("Render", EditorStyles.toolbarButton, GUILayout.Width(52f)))
            RenderFrame();

        GUILayout.EndHorizontal();
    }

    private void DrawGameArea()
    {
        var area = GUILayoutUtility.GetRect(10f, 10f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
        int w = Math.Max(64, (int)(area.width / Math.Max(0.01f, _scale)));
        int h = Math.Max(64, (int)(area.height / Math.Max(0.01f, _scale)));

        float aspect = AspectRatios[Math.Clamp(_aspectIndex, 0, AspectRatios.Length - 1)];
        if (aspect > 0.01f)
        {
            // fit letterbox to aspect
            float targetH = w / aspect;
            if (targetH > h)
            {
                w = (int)(h * aspect);
            }
            else
            {
                h = (int)targetH;
            }
        }

        if (w != _lastWidth || h != _lastHeight)
        {
            _lastWidth = w;
            _lastHeight = h;
            EnsureGameTexture(w, h);
            if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
                RenderFrame();
        }

        // Play mode tint background
        if (EditorApplication.isPlaying)
        {
            var tint = _playModeTint;
            tint.a = 0.12f;
            EditorGUI.DrawRect(area, tint);
        }
        else
        {
            EditorGUI.DrawRect(area, new Color(0.15f, 0.15f, 0.15f, 1f));
        }

        // Center the game rect
        float drawW = w * _scale;
        float drawH = h * _scale;
        var drawRect = new Rect(
            area.x + (area.width - drawW) * 0.5f,
            area.y + (area.height - drawH) * 0.5f,
            drawW,
            drawH);

        if (_gameTexture != null)
        {
            // Placeholder: texture content is logical; draw border + label overlay
            EditorGUI.DrawRect(drawRect, new Color(0.08f, 0.08f, 0.1f, 1f));
            GUI.Box(drawRect, GUIContent.none);
        }

        var cam = ResolveCamera();
        string label = cam != null
            ? $"{cam.name}  {_lastWidth}x{_lastHeight}  display={_targetDisplay}"
            : "No Camera (tag MainCamera)";
        var labelRect = new Rect(drawRect.x + 8f, drawRect.y + 8f, drawRect.width - 16f, 20f);
        GUI.Label(labelRect, label, EditorStyles.whiteMiniLabel);

        if (!EditorApplication.isPlaying)
        {
            var hint = new Rect(drawRect.x + 8f, drawRect.yMax - 28f, drawRect.width - 16f, 20f);
            GUI.Label(hint, "Enter Play Mode to stream game cameras · Camera.Render / SRP", EditorStyles.centeredGreyMiniLabel);
        }

        _statusLine = cam != null
            ? $"Camera={cam.name} FOV={cam.fieldOfView:F0} depth={cam.depth} pipeline={(RenderPipelineManager.currentPipeline != null ? "SRP" : "Builtin")}"
            : "Waiting for Camera.main";
    }

    private void DrawStats()
    {
        GUILayout.BeginHorizontal(EditorStyles.helpBox);
        GUILayout.Label(_statusLine, EditorStyles.miniLabel);
        GUILayout.FlexibleSpace();
        GUILayout.Label($"fps~{(1f / Math.Max(0.001f, Time.deltaTime)):F0}  dt={Time.deltaTime * 1000f:F1}ms", EditorStyles.miniLabel);
        GUILayout.EndHorizontal();
    }

    /// <summary>
    /// Render active game camera(s) into the Game View target via Camera.Render → SRP.
    /// </summary>
    public void RenderFrame()
    {
        EnsureGameTexture(_lastWidth, _lastHeight);
        var cam = ResolveCamera();
        if (cam == null)
        {
            _statusLine = "No camera to render";
            return;
        }

        var prev = cam.targetTexture;
        try
        {
            cam.targetTexture = _gameTexture;
            cam.pixelWidth = _lastWidth;
            cam.pixelHeight = _lastHeight;

            // Light probe sample at camera for ambient continuity with SceneViewCamera
            if (cam.transform != null)
                LightProbes.GetInterpolatedProbe(cam.transform.position, null, out var sh);

            cam.Render();

            // Soft HDR post grade when HDR + Linear (preview path)
            if (cam.allowHDR && QualitySettings.activeColorSpace == UnityEngine.ColorSpace.Linear)
            {
                UnityEngine.Rendering.Universal.PostProcessRuntime.LastGrade.tonemapMode =
                    HDROutputSettings.main.automaticHDRTonemapping ? 2 : 0;
            }

            _statusLine = $"Rendered {cam.name} {_lastWidth}x{_lastHeight} HDR={cam.allowHDR} @ {Time.frameCount}";
        }
        finally
        {
            cam.targetTexture = prev;
        }
    }

    private Camera? ResolveCamera()
    {
        // Prefer MainCamera; fall back to first enabled camera matching display
        var main = Camera.main;
        if (main != null && main.enabled) return main;

        foreach (var c in Camera.allCameras)
        {
            if (c != null && c.enabled && c.cameraType == CameraType.Game)
                return c;
        }

        foreach (var c in Camera.allCameras)
        {
            if (c != null && c.enabled) return c;
        }

        return null;
    }

    private void EnsureGameTexture(int width, int height)
    {
        width = Math.Max(16, width);
        height = Math.Max(16, height);
        if (_gameTexture != null && _gameTexture.width == width && _gameTexture.height == height)
            return;

        ReleaseGameTexture();
        _gameTexture = new RenderTexture(width, height, 24)
        {
            name = "GameViewRT",
            antiAliasing = QualitySettings.antiAliasing > 0 ? QualitySettings.antiAliasing : 1
        };
    }

    private void ReleaseGameTexture()
    {
        if (_gameTexture != null)
        {
            _gameTexture.Release();
            _gameTexture = null;
        }
    }

    [MenuItem("Window/General/Game")]
    public static GameView ShowWindow()
    {
        return GetWindow<GameView>("Game");
    }
}
