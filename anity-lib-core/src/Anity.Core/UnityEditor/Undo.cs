using System.Collections.Generic;
using UnityEngine;

namespace UnityEditor;

public static class Undo
{
  private static readonly Stack<UndoRecord> _undo = new();
  private static readonly Stack<UndoRecord> _redo = new();
  private static int _group;
  private static string _groupName = string.Empty;

  public static event System.Action? undoRedoPerformed;

  public static void RecordObject(Object objectToUndo, string name)
  {
    var list = objectToUndo is null ? Array.Empty<Object>() : new[] { objectToUndo };
    RecordObjects(list, name);
  }

  public static void RecordObjects(Object[] objects, string name)
  {
    _undo.Push(new UndoRecord(name, objects, _group, _groupName));
    _redo.Clear();
  }

  public static void RegisterCompleteObjectUndo(Object objectToUndo, string name)
  {
    RecordObject(objectToUndo, name);
  }

  public static void RegisterCreatedObjectUndo(Object objectToUndo, string name)
  {
    RecordObject(objectToUndo, name);
  }

  public static void SetCurrentGroupName(string name)
  {
    _groupName = name ?? string.Empty;
  }

  public static int GetCurrentGroup()
  {
    return _group;
  }

  public static string GetCurrentGroupName()
  {
    return _groupName;
  }

  public static int SetCurrentGroupNameHash(string name)
  {
    _groupName = name ?? string.Empty;
    return GetCurrentGroup();
  }

  public static bool InvertRecording()
  {
    if (_undo.Count == 0) return false;
    var last = _undo.Pop();
    _redo.Push(last);
    undoRedoPerformed?.Invoke();
    return true;
  }

  public static bool UndoAction()
  {
    return InvertRecording();
  }

  public static bool Redo()
  {
    if (_redo.Count == 0)
    {
      return false;
    }

    var last = _redo.Pop();
    _undo.Push(last);
    undoRedoPerformed?.Invoke();
    return true;
  }

  public static void IncrementCurrentGroup()
  {
    _group++;
  }

  public static void ClearAll()
  {
    _undo.Clear();
    _redo.Clear();
  }

  public static void ClearAllInGroup()
  {
    _undo.Clear();
    _redo.Clear();
  }

  public static bool PerformUndo()
  {
    return InvertRecording();
  }

  public static bool PerformRedo()
  {
    return Redo();
  }

  public static void DestroyObjectImmediate(Object obj)
  {
    Object.DestroyImmediate(obj);
  }

  public static void PerformUndoAction()
  {
    _ = InvertRecording();
  }

  public static void RecordObject(Object[] objects, string name)
  {
    RecordObjects(objects, name);
  }

  public static void RecordObject(Object objectToUndo, string name, bool includeChildren)
  {
    _ = includeChildren;
    RecordObject(objectToUndo, name);
  }

  public static void DestroyObjectUndo(Object obj, string name)
  {
    _ = name;
    Object.Destroy(obj);
  }

  public static int GetCurrentGroupNameHash()
  {
    return _groupName.GetHashCode();
  }

  public static void CollapseUndoOperations(int group)
  {
    _group = group;
  }

  public static void RevertAllDownToGroup(int group)
  {
    while (_undo.Count > 0)
    {
      var record = _undo.Peek();
      if (record.Group < group)
        break;
      var popped = _undo.Pop();
      _redo.Push(popped);
    }
    undoRedoPerformed?.Invoke();
  }

  public static void RegisterFullObjectHierarchyUndo(Object objectToUndo, string name)
  {
    RecordObject(objectToUndo, name);
  }

  public static int FlushUndoRecordObjects()
  {
    var count = _undo.Count;
    _undo.Clear();
    return count;
  }

  public static bool GetRecordUndo(Object objectToUndo)
  {
    _ = objectToUndo;
    return true;
  }

  private readonly struct UndoRecord
  {
    public string Name { get; }
    public Object[] Targets { get; }
    public int Group { get; }
    public string GroupName { get; }

    public UndoRecord(string name, Object[] targets, int group, string groupName)
    {
      Name = name;
      Targets = targets;
      Group = group;
      GroupName = groupName;
    }
  }
}
