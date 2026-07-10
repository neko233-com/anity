namespace UnityEditor;

public abstract class Editor
{
  public object? target { get; private set; }
  public object[]? targets { get; private set; }
  public SerializedObject? serializedObject { get; private set; }

  public bool useGUILayout { get; set; } = true;
  public bool isInspectorExpanded { get; set; } = true;

  public virtual bool HasPreviewGUI() => false;
  public virtual GUIContent GetPreviewTitle() => GUIContent.Temp("Preview");
  public virtual bool ShouldHideOpenButton() => false;
  public virtual bool RequiresConstantRepaint() => false;

  public virtual void OnInspectorGUI() {}
  protected virtual void OnHeaderGUI() {}
  protected virtual void OnEnable() {}
  protected virtual void OnDisable() {}

  public virtual bool UseDefaultMargins() => true;
  public virtual bool DrawDefaultInspector() => true;
  public virtual bool CanEditMultipleObjects() => false;

  public static Editor CreateEditor(UnityEngine.Object targetObject)
  {
    return CreateEditor(new[] { targetObject });
  }

  public static Editor CreateEditor(UnityEngine.Object targetObject, System.Type editorType)
  {
    _ = editorType;
    return CreateEditor(new[] { targetObject });
  }

  public static Editor CreateEditor(UnityEngine.Object[] targetObjects)
  {
    var editor = new GenericEditor();
    editor.SetTargetObjects(targetObjects);
    return editor;
  }

  public static Editor CreateEditor(UnityEngine.Object[] targetObjects, System.Type editorType)
  {
    _ = editorType;
    return CreateEditor(targetObjects);
  }

  public static bool HasPreviewGUI(Editor editor)
  {
    return editor?.HasPreviewGUI() ?? false;
  }

  public void DrawHeader()
  {
    OnHeaderGUI();
  }

  public void Repaint()
  {
  }

  internal void SetTargetObject(object? target)
  {
    this.target = target;
    this.targets = target != null ? new[] { target } : null;
    serializedObject = new SerializedObject(target);
    OnEnable();
  }

  internal void SetTargetObjects(object[] targetObjects)
  {
    this.targets = targetObjects;
    this.target = targetObjects != null && targetObjects.Length > 0 ? targetObjects[0] : null;
    serializedObject = new SerializedObject(target);
    OnEnable();
  }
}

internal sealed class GenericEditor : Editor
{
  public override bool DrawDefaultInspector() => true;
}

