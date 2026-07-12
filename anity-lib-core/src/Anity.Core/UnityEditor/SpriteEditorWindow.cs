using UnityEngine;

namespace UnityEditor;

public enum SpriteEditorMode
{
    SpriteEditor,
    SpritePolygon,
    SpritePhysicsShape,
    SpriteBone
}

public sealed class SpriteEditorWindow : EditorWindow
{
    private SpriteEditorMode _mode = SpriteEditorMode.SpriteEditor;
    private Sprite? _selectedSprite;
    private Texture2D? _spriteTexture;
    private Vector2 _scrollPosition;
    private Rect _textureViewRect;
    private float _zoom = 1f;
    private Vector2 _panOffset;
    private bool _applyRevertEnabled;
    private Vector4 _border;
    private SpriteMeshType _meshType = SpriteMeshType.Tight;
    private float _pivotX = 0.5f;
    private float _pivotY = 0.5f;
    private int _pixelsPerUnit = 100;
    private Vector2 _previewSize;

    public static SpriteEditorWindow? instance { get; private set; }

    public Sprite? selectedSprite
    {
        get => _selectedSprite;
        set
        {
            _selectedSprite = value;
            if (value != null)
            {
                _spriteTexture = value.texture;
                _border = value.border;
                _pixelsPerUnit = (int)value.pixelsPerUnit;
                _previewSize = new Vector2(value.rect.width, value.rect.height);
                _applyRevertEnabled = false;
            }
        }
    }

    public SpriteEditorWindow()
    {
        titleContent = new GUIContent("Sprite Editor");
        minSize = new Vector2(500f, 400f);
        instance = this;
    }

    protected override void OnSelectionChange()
    {
        base.OnSelectionChange();
        var selected = Selection.activeObject;
        if (selected is Sprite sprite)
        {
            selectedSprite = sprite;
        }
        else if (selected is Texture2D tex)
        {
            _spriteTexture = tex;
        }
        Repaint();
    }

    protected override void OnGUI()
    {
        DrawToolbar();

        GUILayout.BeginHorizontal();
        DrawSpriteView();
        DrawInspectorPanel();
        GUILayout.EndHorizontal();

        DrawApplyRevertBar();
    }

    private void DrawApplyRevertBar()
    {
        if (_applyRevertEnabled)
        {
            GUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Apply", EditorStyles.toolbarButton, GUILayout.Width(60f))) Apply();
            GUILayout.Space(4f);
            if (GUILayout.Button("Revert", EditorStyles.toolbarButton, GUILayout.Width(60f))) Revert();
            GUILayout.Space(4f);
            GUILayout.EndHorizontal();
        }
    }

    private void DrawToolbar()
    {
        GUILayout.BeginHorizontal(EditorStyles.toolbar);

        GUILayout.Label("Sprite Editor", EditorStyles.boldLabel);

        GUILayout.Space(10f);
        string[] modeNames = { "Sprite Editor", "Sprite Polygon", "Physics Shape", "Bones" };
        int modeIndex = (int)_mode;
        int newMode = GUILayout.Toolbar(modeIndex, modeNames, EditorStyles.toolbarButton, GUILayout.Width(350f));
        if (newMode != modeIndex)
        {
            _mode = (SpriteEditorMode)newMode;
        }

        GUILayout.FlexibleSpace();

        if (GUILayout.Button("▼", EditorStyles.toolbarPopup, GUILayout.Width(24f)))
        {
            var menu = new GenericMenu();
            menu.AddItem("Apply", false, (Action)Apply);
            menu.AddItem("Revert", false, (Action)Revert);
            menu.ShowAsContext();
        }

        GUILayout.EndHorizontal();
    }

    private void DrawSpriteView()
    {
        GUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true));

        var viewRect = GUILayoutUtility.GetRect(400f, 300f, GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true));
        _textureViewRect = viewRect;

        EditorGUI.DrawRect(viewRect, new Color(0.18f, 0.18f, 0.18f, 1f));

        if (_spriteTexture != null)
        {
            float aspect = (float)_spriteTexture.width / Mathf.Max(1, _spriteTexture.height);
            float drawWidth = Mathf.Min(viewRect.width - 40f, viewRect.height * aspect);
            float drawHeight = drawWidth / Mathf.Max(0.01f, aspect);
            float drawX = viewRect.x + (viewRect.width - drawWidth) * 0.5f + _panOffset.x;
            float drawY = viewRect.y + (viewRect.height - drawHeight) * 0.5f + _panOffset.y;
            var drawRect = new Rect(drawX, drawY, drawWidth * _zoom, drawHeight * _zoom);

            if (_selectedSprite != null)
            {
                var spriteRect = _selectedSprite.rect;
                float texW = _spriteTexture.width;
                float texH = _spriteTexture.height;
                float spriteNormX = spriteRect.x / Mathf.Max(1, texW);
                float spriteNormY = spriteRect.y / Mathf.Max(1, texH);
                float spriteNormW = spriteRect.width / Mathf.Max(1, texW);
                float spriteNormH = spriteRect.height / Mathf.Max(1, texH);
                var actualDrawRect = new Rect(
                    drawRect.x + spriteNormX * drawRect.width,
                    drawRect.y + (1f - spriteNormY - spriteNormH) * drawRect.height,
                    spriteNormW * drawRect.width,
                    spriteNormH * drawRect.height);

                if (_mode == SpriteEditorMode.SpriteEditor)
                {
                    DrawSpriteBorder(actualDrawRect);
                }
            }
        }
        else
        {
            var labelRect = new Rect(viewRect.x, viewRect.y + viewRect.height * 0.4f, viewRect.width, 40f);
            EditorGUI.LabelField(labelRect, "No sprite selected.\nSelect a sprite or texture to edit.", EditorStyles.centeredGreyMiniLabel);
        }

        DrawZoomControls();

        GUILayout.EndVertical();
    }

    private void DrawSpriteBorder(Rect spriteRect)
    {
        float bw = _border.x / Mathf.Max(1f, _spriteTexture?.width ?? 1) * spriteRect.width;
        float br = _border.z / Mathf.Max(1f, _spriteTexture?.width ?? 1) * spriteRect.width;
        float bt = _border.w / Mathf.Max(1f, _spriteTexture?.height ?? 1) * spriteRect.height;
        float bb = _border.y / Mathf.Max(1f, _spriteTexture?.height ?? 1) * spriteRect.height;

        var borderColor = new Color(0.2f, 0.6f, 1f, 0.6f);
        EditorGUI.DrawRect(new Rect(spriteRect.x + bw, spriteRect.y, 1f, spriteRect.height), borderColor);
        EditorGUI.DrawRect(new Rect(spriteRect.xMax - br, spriteRect.y, 1f, spriteRect.height), borderColor);
        EditorGUI.DrawRect(new Rect(spriteRect.x, spriteRect.y + bt, spriteRect.width, 1f), borderColor);
        EditorGUI.DrawRect(new Rect(spriteRect.x, spriteRect.yMax - bb, spriteRect.width, 1f), borderColor);
    }

    private void DrawZoomControls()
    {
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("-", EditorStyles.miniButton, GUILayout.Width(24f))) _zoom = Mathf.Max(0.1f, _zoom * 0.8f);
        GUILayout.Label($"{_zoom * 100f:F0}%", EditorStyles.miniLabel, GUILayout.Width(40f));
        if (GUILayout.Button("+", EditorStyles.miniButton, GUILayout.Width(24f))) _zoom = Mathf.Min(10f, _zoom * 1.25f);
        if (GUILayout.Button("Fit", EditorStyles.miniButton, GUILayout.Width(36f))) { _zoom = 1f; _panOffset = Vector2.zero; }
        GUILayout.Space(8f);
        GUILayout.EndHorizontal();
    }

    private void DrawInspectorPanel()
    {
        GUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(220f));

        _scrollPosition = GUILayout.BeginScrollView(_scrollPosition);

        GUILayout.Label("Sprite", EditorStyles.boldLabel);

        if (_selectedSprite != null)
        {
            GUILayout.Label($"Name: {_selectedSprite.name}", EditorStyles.miniLabel);
            if (_spriteTexture != null)
            {
                GUILayout.Label($"Texture: {_spriteTexture.width}x{_spriteTexture.height}", EditorStyles.miniLabel);
            }
            GUILayout.Label($"Rect: {_selectedSprite.rect.width:F0}x{_selectedSprite.rect.height:F0}", EditorStyles.miniLabel);

            GUILayout.Space(8f);
            GUILayout.Label("Mesh Type", EditorStyles.miniBoldLabel);
            _meshType = (SpriteMeshType)EditorGUILayout.Popup((int)_meshType, new[] { "Full Rect", "Tight" });

            GUILayout.Space(4f);
            GUILayout.Label("Pivot", EditorStyles.miniBoldLabel);
            string[] pivotOptions = { "Center", "Top Left", "Top", "Top Right", "Left", "Right", "Bottom Left", "Bottom", "Bottom Right", "Custom" };
            int pivotIndex = GetPivotPresetIndex();
            int newPivot = EditorGUILayout.Popup(pivotIndex, pivotOptions);
            if (newPivot != pivotIndex) SetPivotFromPreset(newPivot);

            _pivotX = EditorGUILayout.Slider("Pivot X", _pivotX, 0f, 1f);
            _pivotY = EditorGUILayout.Slider("Pivot Y", _pivotY, 0f, 1f);

            GUILayout.Space(4f);
            _pixelsPerUnit = EditorGUILayout.IntField("Pixels Per Unit", _pixelsPerUnit);

            GUILayout.Space(8f);
            GUILayout.Label("Border", EditorStyles.miniBoldLabel);
            EditorGUI.BeginChangeCheck();
            float borderL = EditorGUILayout.FloatField("Left", _border.x);
            float borderB = EditorGUILayout.FloatField("Bottom", _border.y);
            float borderR = EditorGUILayout.FloatField("Right", _border.z);
            float borderT = EditorGUILayout.FloatField("Top", _border.w);
            if (EditorGUI.EndChangeCheck())
            {
                _border = new Vector4(borderL, borderB, borderR, borderT);
                _applyRevertEnabled = true;
            }

            if (GUILayout.Button("Apply", GUILayout.Height(24f))) Apply();
            if (GUILayout.Button("Revert", GUILayout.Height(24f))) Revert();
        }
        else
        {
            GUILayout.Label("Select a sprite to edit its properties.", EditorStyles.wordWrappedMiniLabel);
        }

        GUILayout.EndScrollView();
        GUILayout.EndVertical();
    }

    private int GetPivotPresetIndex()
    {
        bool isCenter = Mathf.Approximately(_pivotX, 0.5f) && Mathf.Approximately(_pivotY, 0.5f);
        bool isTopLeft = Mathf.Approximately(_pivotX, 0f) && Mathf.Approximately(_pivotY, 1f);
        bool isTop = Mathf.Approximately(_pivotX, 0.5f) && Mathf.Approximately(_pivotY, 1f);
        bool isTopRight = Mathf.Approximately(_pivotX, 1f) && Mathf.Approximately(_pivotY, 1f);
        bool isLeft = Mathf.Approximately(_pivotX, 0f) && Mathf.Approximately(_pivotY, 0.5f);
        bool isRight = Mathf.Approximately(_pivotX, 1f) && Mathf.Approximately(_pivotY, 0.5f);
        bool isBottomLeft = Mathf.Approximately(_pivotX, 0f) && Mathf.Approximately(_pivotY, 0f);
        bool isBottom = Mathf.Approximately(_pivotX, 0.5f) && Mathf.Approximately(_pivotY, 0f);
        bool isBottomRight = Mathf.Approximately(_pivotX, 1f) && Mathf.Approximately(_pivotY, 0f);

        if (isCenter) return 0;
        if (isTopLeft) return 1;
        if (isTop) return 2;
        if (isTopRight) return 3;
        if (isLeft) return 4;
        if (isRight) return 5;
        if (isBottomLeft) return 6;
        if (isBottom) return 7;
        if (isBottomRight) return 8;
        return 9;
    }

    private void SetPivotFromPreset(int preset)
    {
        _pivotX = preset switch
        {
            0 => 0.5f,
            1 or 4 or 6 => 0f,
            2 or 7 => 0.5f,
            3 or 5 or 8 => 1f,
            _ => _pivotX
        };
        _pivotY = preset switch
        {
            0 => 0.5f,
            1 or 2 or 3 => 1f,
            4 or 5 => 0.5f,
            6 or 7 or 8 => 0f,
            _ => _pivotY
        };
        _applyRevertEnabled = true;
    }

    public void ApplyRevert()
    {
        if (_applyRevertEnabled)
        {
            Apply();
        }
    }

    public void Apply()
    {
        if (_selectedSprite != null)
        {
            _selectedSprite.border = _border;
            _selectedSprite.pixelsPerUnit = _pixelsPerUnit;
            _selectedSprite.pivot = new Vector2(_pivotX * _previewSize.x, _pivotY * _previewSize.y);
        }
        _applyRevertEnabled = false;
        EditorUtility.SetDirty(_spriteTexture);
    }

    public void Revert()
    {
        if (_selectedSprite != null)
        {
            _border = _selectedSprite.border;
            _pixelsPerUnit = (int)_selectedSprite.pixelsPerUnit;
            _previewSize = new Vector2(_selectedSprite.rect.width, _selectedSprite.rect.height);
            float w = Mathf.Max(1f, _previewSize.x);
            float h = Mathf.Max(1f, _previewSize.y);
            _pivotX = _selectedSprite.pivot.x / w;
            _pivotY = _selectedSprite.pivot.y / h;
        }
        _applyRevertEnabled = false;
    }

    public static SpriteEditorWindow GetWindow()
    {
        return EditorWindow.GetWindow<SpriteEditorWindow>("Sprite Editor");
    }

    [MenuItem("Window/2D/Sprite Editor")]
    public static SpriteEditorWindow OpenWindow()
    {
        return GetWindow();
    }
}
