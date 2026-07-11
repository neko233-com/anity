using System;
using System.Collections.Generic;

namespace UnityEngine;

public struct LayerMask
{
  public int value;

  private static readonly Dictionary<string, int> _layerNameToIndex = new()
  {
    { "Default", 0 },
    { "TransparentFX", 1 },
    { "Ignore Raycast", 2 },
    { "Layer3", 3 },
    { "Water", 4 },
    { "UI", 5 },
    { "Layer6", 6 },
    { "Layer7", 7 },
    { "Layer8", 8 },
    { "Layer9", 9 },
    { "Layer10", 10 },
    { "Layer11", 11 },
    { "Layer12", 12 },
    { "Layer13", 13 },
    { "Layer14", 14 },
    { "Layer15", 15 },
    { "Layer16", 16 },
    { "Layer17", 17 },
    { "Layer18", 18 },
    { "Layer19", 19 },
    { "Layer20", 20 },
    { "Layer21", 21 },
    { "Layer22", 22 },
    { "Layer23", 23 },
    { "Layer24", 24 },
    { "Layer25", 25 },
    { "Layer26", 26 },
    { "Layer27", 27 },
    { "Layer28", 28 },
    { "Layer29", 29 },
    { "Layer30", 30 },
    { "Layer31", 31 }
  };

  private static readonly Dictionary<int, string> _layerIndexToName = new()
  {
    { 0, "Default" },
    { 1, "TransparentFX" },
    { 2, "Ignore Raycast" },
    { 3, "Layer3" },
    { 4, "Water" },
    { 5, "UI" }
  };

  public static int NameToLayer(string layerName)
  {
    if (_layerNameToIndex.TryGetValue(layerName, out int layer))
    {
      return layer;
    }
    return -1;
  }

  public static string LayerToName(int layer)
  {
    if (_layerIndexToName.TryGetValue(layer, out string? name))
    {
      return name;
    }
    return $"Layer{layer}";
  }

  public static int GetMask(params string[] layerNames)
  {
    int mask = 0;
    foreach (string name in layerNames)
    {
      int layer = NameToLayer(name);
      if (layer >= 0 && layer < 32)
      {
        mask |= 1 << layer;
      }
    }
    return mask;
  }

  public static implicit operator int(LayerMask mask) => mask.value;
  public static implicit operator LayerMask(int intVal) => new LayerMask { value = intVal };

  public override string ToString()
  {
    return value.ToString();
  }

  public override bool Equals(object? obj)
  {
    if (obj is LayerMask other) return value == other.value;
    if (obj is int i) return value == i;
    return false;
  }

  public override int GetHashCode() => value.GetHashCode();

  public static bool operator ==(LayerMask a, LayerMask b) => a.value == b.value;
  public static bool operator !=(LayerMask a, LayerMask b) => a.value != b.value;

  public static void RegisterLayer(int index, string name)
  {
    if (index < 0 || index >= 32) return;
    _layerNameToIndex[name] = index;
    _layerIndexToName[index] = name;
  }
}
