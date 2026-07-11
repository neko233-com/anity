using System.Collections.Generic;

namespace UnityEngine;

public class AvatarMask : Object
{
  public string name { get; set; } = string.Empty;
  public int humanMachineCount { get; set; }
  public int skeletonCount { get; set; }
  private readonly Dictionary<HumanBodyBones, bool> _humanoidParts = new();
  private readonly List<string> _transformPaths = new();

  public int transformCount => _transformPaths.Count;

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
    _ = transform;
    return false;
  }

  public void SetTransformMask(Transform transform, bool value)
  {
    _ = transform;
    _ = value;
  }

  public string GetTransformPath(int index)
  {
    if (index >= 0 && index < _transformPaths.Count)
      return _transformPaths[index];
    return string.Empty;
  }

  public void SetTransformPath(int index, string path)
  {
    while (_transformPaths.Count <= index)
      _transformPaths.Add(string.Empty);
    _transformPaths[index] = path ?? string.Empty;
  }

  public void AddTransformPath(string path)
  {
    _transformPaths.Add(path ?? string.Empty);
  }

  public void RemoveTransformPath(int index)
  {
    if (index >= 0 && index < _transformPaths.Count)
      _transformPaths.RemoveAt(index);
  }
}
