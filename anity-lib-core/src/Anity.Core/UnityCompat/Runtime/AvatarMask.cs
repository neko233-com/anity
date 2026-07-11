using System.Collections.Generic;

namespace UnityEngine;

public struct TransformMaskElement
{
  public string path;
  public bool active;

  public TransformMaskElement(string path, bool active)
  {
    this.path = path ?? string.Empty;
    this.active = active;
  }
}

public class AvatarMask : Object
{
  public string name { get; set; } = string.Empty;
  public int humanMachineCount { get; set; }
  public int skeletonCount { get; set; }
  private readonly Dictionary<HumanBodyBones, bool> _humanoidParts = new();
  private readonly List<TransformMaskElement> _transformElements = new();

  public int transformCount => _transformElements.Count;

  public TransformMaskElement[] GetTransformMaskElements()
  {
    return _transformElements.ToArray();
  }

  public bool GetHumanoidBodyPartActive(HumanBodyBones humanBodyPart)
  {
    return _humanoidParts.TryGetValue(humanBodyPart, out var active) && active;
  }

  public void SetHumanoidBodyPartActive(HumanBodyBones humanBodyPart, bool value)
  {
    _humanoidParts[humanBodyPart] = value;
  }

  public bool GetHumanoidBodyPartMask(HumanBodyBones humanBodyPart)
  {
    return GetHumanoidBodyPartActive(humanBodyPart);
  }

  public void SetHumanoidBodyPartMask(HumanBodyBones humanBodyPart, bool value)
  {
    SetHumanoidBodyPartActive(humanBodyPart, value);
  }

  public bool GetTransformMask(Transform transform)
  {
    if (transform == null) return false;
    string path = GetTransformPathFromTransform(transform);
    for (int i = 0; i < _transformElements.Count; i++)
    {
      if (_transformElements[i].path == path)
        return _transformElements[i].active;
    }
    return false;
  }

  public void SetTransformMask(Transform transform, bool value)
  {
    if (transform == null) return;
    string path = GetTransformPathFromTransform(transform);
    for (int i = 0; i < _transformElements.Count; i++)
    {
      if (_transformElements[i].path == path)
      {
        _transformElements[i] = new TransformMaskElement(path, value);
        return;
      }
    }
    _transformElements.Add(new TransformMaskElement(path, value));
  }

  public bool GetTransformActive(int index)
  {
    if (index >= 0 && index < _transformElements.Count)
      return _transformElements[index].active;
    return false;
  }

  public void SetTransformActive(int index, bool value)
  {
    if (index < 0) return;
    while (_transformElements.Count <= index)
      _transformElements.Add(new TransformMaskElement(string.Empty, false));
    var elem = _transformElements[index];
    elem.active = value;
    _transformElements[index] = elem;
  }

  public string GetTransformPath(int index)
  {
    if (index >= 0 && index < _transformElements.Count)
      return _transformElements[index].path;
    return string.Empty;
  }

  public void SetTransformPath(int index, string path)
  {
    if (index < 0) return;
    while (_transformElements.Count <= index)
      _transformElements.Add(new TransformMaskElement(string.Empty, false));
    var elem = _transformElements[index];
    elem.path = path ?? string.Empty;
    _transformElements[index] = elem;
  }

  public void AddTransformPath(string path)
  {
    _transformElements.Add(new TransformMaskElement(path ?? string.Empty, true));
  }

  public void AddTransformPath(string path, bool active)
  {
    _transformElements.Add(new TransformMaskElement(path ?? string.Empty, active));
  }

  public void RemoveTransformPath(int index)
  {
    if (index >= 0 && index < _transformElements.Count)
      _transformElements.RemoveAt(index);
  }

  private string GetTransformPathFromTransform(Transform transform)
  {
    var names = new List<string>();
    var current = transform;
    while (current != null && current.parent != null)
    {
      names.Add(current.name);
      current = current.parent;
    }
    names.Reverse();
    return string.Join("/", names);
  }
}
