using System;
using UnityEngine;

namespace UnityEditor
{
  public static class EditorGUIUtility
  {
    private static readonly System.Collections.Generic.Dictionary<string, Texture2D> _icons = new();
    private static Texture2D? _whiteTexture;
    private static Texture2D? _scriptIcon;
    private static Texture2D? _standardScriptIcon;
    private static int _controlID;
    private static float _pixelsPerPoint = 1f;
    private static Vector2 _mousePosition;
    private static GUIContent _tempContent = new GUIContent();
    private static string _systemCopyBuffer = string.Empty;
    private static bool _editingTextField;

    public static float pixelsPerPoint
    {
      get => _pixelsPerPoint;
      set => _pixelsPerPoint = value;
    }

    public static float singleLineHeight => 20f;
    public static float standardVerticalSpacing => 6f;
    public static float inspectorWidth => 330f;
    public static float currentViewWidth { get; set; }
    public static float CurrentViewWidth { get => currentViewWidth; set => currentViewWidth = value; }
    public static Vector2 currentMousePosition => _mousePosition;
    public static bool isProSkin { get; set; }
    public static bool editingTextField { get => _editingTextField; set => _editingTextField = value; }
    public static string systemCopyBuffer { get => _systemCopyBuffer; set => _systemCopyBuffer = value ?? string.Empty; }
    public static string SystemCopyBuffer { get => systemCopyBuffer; set => systemCopyBuffer = value; }
    public static Color mainBackgroundColor => isProSkin ? new Color(0.22f, 0.22f, 0.22f, 1f) : new Color(0.76f, 0.76f, 0.76f, 1f);

    public static Texture2D whiteTexture
    {
      get
      {
        if (_whiteTexture == null)
        {
          _whiteTexture = new Texture2D(1, 1);
          _whiteTexture.SetPixel(0, 0, Color.white);
          _whiteTexture.Apply();
        }
        return _whiteTexture;
      }
    }

    public static Texture2D scriptIcon
    {
      get
      {
        if (_scriptIcon == null)
          _scriptIcon = FindTexture("cs Script Icon");
        return _scriptIcon;
      }
    }

    public static Texture2D standardScriptIcon
    {
      get
      {
        if (_standardScriptIcon == null)
          _standardScriptIcon = FindTexture("dll Script Icon");
        return _standardScriptIcon;
      }
    }

    public static Texture2D FindTexture(string name)
    {
      if (_icons.TryGetValue(name, out var tex))
        return tex;

      var texture = new Texture2D(16, 16);
      _icons[name] = texture;
      return texture;
    }

    public static Texture2D FindTexture(string name, System.Type type)
    {
      _ = type;
      return FindTexture(name);
    }

    public static UnityEngine.Object? Load(string path)
    {
      if (string.IsNullOrEmpty(path))
        return null;

      if (path.EndsWith(".png") || path.EndsWith(".jpg") || path.EndsWith(".gif") || path.EndsWith(".bmp"))
        return FindTexture(System.IO.Path.GetFileNameWithoutExtension(path));

      return FindTexture(path);
    }

    public static T Load<T>(string path) where T : UnityEngine.Object
    {
      var obj = Load(path);
      return obj as T;
    }

    public static Texture2D IconContent(string name)
    {
      return FindTexture(name);
    }

    public static GUIContent IconContent(string name, string text)
    {
      _tempContent.text = text;
      _tempContent.image = FindTexture(name);
      return _tempContent;
    }

    public static GUIContent TrTextContent(string text, string tooltip = null)
    {
      _tempContent.text = text;
      _tempContent.tooltip = tooltip ?? string.Empty;
      return _tempContent;
    }

    public static int GetControlID(int hint)
    {
      return ++_controlID;
    }

    public static int GetControlID(string name, FocusType focusType)
    {
      _ = name;
      _ = focusType;
      return ++_controlID;
    }

    public static Rect GetAspectRect(Rect rect, float aspectRatio)
    {
      if (aspectRatio <= 0f) return rect;
      float height = rect.width / aspectRatio;
      if (height <= rect.height)
        return new Rect(rect.x, rect.y + (rect.height - height) * 0.5f, rect.width, height);
      float width = rect.height * aspectRatio;
      return new Rect(rect.x + (rect.width - width) * 0.5f, rect.y, width, rect.height);
    }

    public static string GetSaveFolderPanel(string title, string folder, string defaultName)
    {
      _ = title;
      _ = folder;
      _ = defaultName;
      return string.Empty;
    }

    public static string SaveFilePanel(string title, string directory, string defaultName, string extension)
    {
      _ = title;
      _ = directory;
      _ = defaultName;
      _ = extension;
      return string.Empty;
    }

    public static string OpenFilePanel(string title, string directory, string extension)
    {
      _ = title;
      _ = directory;
      _ = extension;
      return string.Empty;
    }

    public static string[] OpenFilePanelWithFilters(string title, string directory, string[] filters)
    {
      _ = title;
      _ = directory;
      _ = filters;
      return Array.Empty<string>();
    }

    public static void PingObject(int instanceID)
    {
      _ = instanceID;
    }

    public static void PingObject(Object obj)
    {
      _ = obj;
    }

    public static int GetInstanceIDFromGUID(string guid)
    {
      _ = guid;
      return 0;
    }

    public static string GetGUIDFromInstanceID(int instanceID)
    {
      _ = instanceID;
      return string.Empty;
    }

    public static Object InstanceIDToObject(int instanceID)
    {
      _ = instanceID;
      return null;
    }

    public static Object[] InstanceIDsToObjects(int[] instanceIDs)
    {
      _ = instanceIDs;
      return Array.Empty<Object>();
    }

    public static bool TryGetString(string name, out string? value)
    {
      value = null;
      return false;
    }

    public static bool HasObjectThumbnail(Type type)
    {
      _ = type;
      return true;
    }

    public static Color ConvertToGammaSpace(Color color)
    {
      return new Color(
        Mathf.GammaToLinearSpace(color.r),
        Mathf.GammaToLinearSpace(color.g),
        Mathf.GammaToLinearSpace(color.b),
        color.a
      );
    }

    public static Color ConvertToLinearSpace(Color color)
    {
      return new Color(
        Mathf.LinearToGammaSpace(color.r),
        Mathf.LinearToGammaSpace(color.g),
        Mathf.LinearToGammaSpace(color.b),
        color.a
      );
    }

    public static float GetHeightOfObjectSelector()
    {
      return 256f;
    }

    public static float wideModeMinWidth => 600f;
    public static float contextWidth => 280f;

    public static void SetShowModeForAllInspectors(int mode)
    {
      _ = mode;
    }

    public static void AddRectToCurrentDirtyArea(Rect rect)
    {
      _ = rect;
    }
  }
}
