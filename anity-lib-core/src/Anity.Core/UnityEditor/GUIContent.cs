using System;

namespace UnityEditor;

public sealed class GUIContent
{
  public static readonly GUIContent none = new(string.Empty, null);
  public string text;
  public string? tooltip;
  public object? image;

  public GUIContent(string text = "", string? tooltip = null, object? image = null)
  {
    this.text = text ?? string.Empty;
    this.tooltip = tooltip;
    this.image = image;
  }

  public static GUIContent Temp(string text) => new(text ?? string.Empty);
  public static GUIContent Temp(string text, string tooltip) => new(text ?? string.Empty, tooltip);
  public override string ToString() => $"{text} ({tooltip})";
}

