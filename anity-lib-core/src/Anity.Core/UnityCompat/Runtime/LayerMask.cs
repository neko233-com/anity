using System.Collections.Generic;

namespace UnityEngine;

public struct LayerMask
{
  public int value { get; set; }

  public LayerMask(int value)
  {
    this.value = value;
  }

  public static implicit operator int(LayerMask mask) => mask.value;
  public static implicit operator LayerMask(int value) => new LayerMask(value);
  public override string ToString() => $"LayerMask({value})";

  private static readonly Dictionary<string, int> s_LayerNameToIndex = new Dictionary<string, int>
  {
    ["Default"] = 0,
    ["TransparentFX"] = 1,
    ["Ignore Raycast"] = 2,
    ["Water"] = 4,
    ["UI"] = 5,
  };

  private static readonly Dictionary<int, string> s_LayerIndexToName = new Dictionary<int, string>
  {
    [0] = "Default",
    [1] = "TransparentFX",
    [2] = "Ignore Raycast",
    [4] = "Water",
    [5] = "UI",
  };

  public static int NameToLayer(string layerName)
  {
    return s_LayerNameToIndex.TryGetValue(layerName, out int index) ? index : -1;
  }

  public static string LayerToName(int layer)
  {
    return s_LayerIndexToName.TryGetValue(layer, out string name) ? name : string.Empty;
  }

  public static int GetMask(params string[] layerNames)
  {
    int mask = 0;
    foreach (string layerName in layerNames)
    {
      int index = NameToLayer(layerName);
      if (index >= 0)
      {
        mask |= 1 << index;
      }
    }
    return mask;
  }

  public static void RegisterLayer(string name, int index)
  {
    s_LayerNameToIndex[name] = index;
    s_LayerIndexToName[index] = name;
  }
}
