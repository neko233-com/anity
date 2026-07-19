using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace UnityEditor;

[Serializable]
public enum DataMode
{
    Disabled = 0,
    Authoring = 1,
    Mixed = 2,
    Runtime = 3
}

[Serializable]
[UnityEngine.Bindings.NativeHeader("Editor/Src/Utility/ActiveEditorTracker.bindings.h")]
[UnityEngine.Scripting.RequiredByNativeCode]
public sealed class ActiveEditorTracker
{
    private static readonly object SharedLock = new();
    private static ActiveEditorTracker? _sharedTracker;
    private static bool _delayFlushDirtyRebuild;
    private readonly List<Editor> _activeEditors = new();
    private readonly List<UnityEngine.Object> _lockedObjects = new();
    private readonly Dictionary<Editor, int> _visibilityByEditor = new();
    private readonly Dictionary<Editor, bool> _unsavedChanges = new();
    private bool _destroyed;
    private bool _dirty = true;
    private bool _isLocked;
    private InspectorMode _inspectorMode;
    private DataMode _dataMode = DataMode.Authoring;

    internal static event Action? editorTrackerRebuilt;

    public ActiveEditorTracker()
    {
        Selection.selectionChanged += MarkDirty;
    }

    ~ActiveEditorTracker() => Destroy();

    public Editor[] activeEditors
    {
        get
        {
            RebuildIfNecessary();
            return _activeEditors.ToArray();
        }
    }

    public bool isDirty => _dirty;

    public bool isLocked
    {
        get => _isLocked;
        set
        {
            if (_isLocked == value) return;
            _isLocked = value;
            if (_isLocked && _lockedObjects.Count == 0) _lockedObjects.AddRange(Selection.objects);
            MarkDirty();
        }
    }

    public bool hasUnsavedChanges => _unsavedChanges.Values.Any(value => value);

    internal static bool delayFlushDirtyRebuild
    {
        get => _delayFlushDirtyRebuild;
        set => _delayFlushDirtyRebuild = value;
    }

    public InspectorMode inspectorMode
    {
        get => _inspectorMode;
        set
        {
            if (_inspectorMode == value) return;
            _inspectorMode = value;
            MarkDirty();
        }
    }

    public bool hasComponentsWhichCannotBeMultiEdited
        => _activeEditors.Any(editor => (editor.targets?.Length ?? 0) > 1 && !editor.CanEditMultipleObjects());

    internal DataMode dataMode
    {
        get => _dataMode;
        set
        {
            if (_dataMode == value) return;
            _dataMode = value;
            MarkDirty();
        }
    }

    public static ActiveEditorTracker sharedTracker
    {
        get
        {
            lock (SharedLock)
                return _sharedTracker ??= new ActiveEditorTracker();
        }
    }

    [Obsolete("Use Editor.CreateEditor instead")]
    public static Editor MakeCustomEditor(UnityEngine.Object obj) => Editor.CreateEditor(obj);

    public static bool HasCustomEditor(UnityEngine.Object obj) => obj != null;

    public override bool Equals(object? o) => ReferenceEquals(this, o);

    public override int GetHashCode() => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this);

    public void Destroy()
    {
        if (_destroyed) return;
        _destroyed = true;
        Selection.selectionChanged -= MarkDirty;
        _activeEditors.Clear();
        _visibilityByEditor.Clear();
        _unsavedChanges.Clear();
    }

    internal void GetObjectsLockedByThisTracker(List<UnityEngine.Object> lockedObjects)
    {
        if (lockedObjects == null) throw new ArgumentNullException(nameof(lockedObjects));
        lockedObjects.AddRange(_lockedObjects);
    }

    internal void SetObjectsLockedByThisTracker(List<UnityEngine.Object> toBeLocked)
    {
        if (toBeLocked == null) throw new ArgumentNullException(nameof(toBeLocked));
        _lockedObjects.Clear();
        _lockedObjects.AddRange(toBeLocked.Where(item => item != null).Distinct());
        MarkDirty();
    }

    public int GetVisible(int index)
    {
        RebuildIfNecessary();
        if (index < 0 || index >= _activeEditors.Count) return 0;
        return _visibilityByEditor.TryGetValue(_activeEditors[index], out var visible) ? visible : 1;
    }

    public void SetVisible(int index, int visible)
    {
        RebuildIfNecessary();
        if (index < 0 || index >= _activeEditors.Count) return;
        _visibilityByEditor[_activeEditors[index]] = visible;
    }

    public void ClearDirty() => _dirty = false;

    internal void UnsavedChangesStateChanged(Editor editor, bool value)
    {
        if (editor == null) throw new ArgumentNullException(nameof(editor));
        _unsavedChanges[editor] = value;
    }

    public void RebuildIfNecessary()
    {
        if (_destroyed || !_dirty || _delayFlushDirtyRebuild) return;
        Rebuild();
    }

    internal void RebuildAllIfNecessary()
    {
        RebuildIfNecessary();
        sharedTracker.RebuildIfNecessary();
    }

    public void ForceRebuild()
    {
        if (_destroyed) return;
        Rebuild();
    }

    public void VerifyModifiedMonoBehaviours() => RebuildIfNecessary();

    private void MarkDirty() => _dirty = true;

    private void Rebuild()
    {
        _activeEditors.Clear();
        IEnumerable<UnityEngine.Object> source = _isLocked ? _lockedObjects : Selection.objects;
        var targets = source.Where(item => item != null).ToArray();
        if (targets.Length > 0)
        {
            var editor = Editor.CreateEditor(targets);
            _activeEditors.Add(editor);
            _visibilityByEditor.TryAdd(editor, 1);
        }
        _visibilityByEditor.Keys.Where(editor => !_activeEditors.Contains(editor)).ToArray().ToList().ForEach(editor => _visibilityByEditor.Remove(editor));
        _unsavedChanges.Keys.Where(editor => !_activeEditors.Contains(editor)).ToArray().ToList().ForEach(editor => _unsavedChanges.Remove(editor));
        _dirty = false;
        editorTrackerRebuilt?.Invoke();
    }
}
