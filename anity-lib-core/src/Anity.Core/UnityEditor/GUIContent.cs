using System;

namespace UnityEngine;

public sealed class GUIContent
{
  public static readonly GUIContent none = new(string.Empty);
  public string text;
  public string? tooltip;
  public Texture? image;

  public GUIContent()
  {
    text = string.Empty;
  }

  public GUIContent(string text)
  {
    this.text = text ?? string.Empty;
  }

  public GUIContent(string text, string? tooltip)
  {
    this.text = text ?? string.Empty;
    this.tooltip = tooltip;
  }

  public GUIContent(string text, Texture? image)
  {
    this.text = text ?? string.Empty;
    this.image = image;
  }

  public GUIContent(string text, Texture? image, string? tooltip)
  {
    this.text = text ?? string.Empty;
    this.image = image;
    this.tooltip = tooltip;
  }

  public GUIContent(Texture? image)
  {
    this.text = string.Empty;
    this.image = image;
  }

  public GUIContent(GUIContent other)
  {
    text = other.text;
    tooltip = other.tooltip;
    image = other.image;
  }

  public static GUIContent Temp(string text) => new(text ?? string.Empty);
  public static GUIContent Temp(string text, string tooltip) => new(text ?? string.Empty, tooltip);
  public static GUIContent Temp(string text, Texture image) => new(text ?? string.Empty, image);
  public static GUIContent Temp(Texture image) => new(image);

  public override string ToString() => tooltip != null ? $"{text} ({tooltip})" : text;
}
