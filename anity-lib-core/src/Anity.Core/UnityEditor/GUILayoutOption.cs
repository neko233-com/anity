namespace UnityEngine;

public sealed class GUILayoutOption
{
  public string? name;
  public object? value;

  internal GUILayoutOption(string name, object value)
  {
    this.name = name;
    this.value = value;
  }
}
