namespace UnityEditor;

public abstract class Editor
{
  public object? target { get; private set; }
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

  internal void SetTargetObject(object? target)
  {
    this.target = target;
    serializedObject = new SerializedObject(target);
    OnEnable();
  }
}

