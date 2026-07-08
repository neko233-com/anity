using System;

namespace UnityEngine;

public class TextAsset : UnityEngine.Object
{
  public string text { get; }
  public byte[] bytes { get; }

  public TextAsset(string text)
  {
    this.text = text;
    this.bytes = text != null ? System.Text.Encoding.UTF8.GetBytes(text) : Array.Empty<byte>();
  }

  public TextAsset(byte[] bytes)
  {
    this.bytes = bytes ?? Array.Empty<byte>();
    this.text = System.Text.Encoding.UTF8.GetString(this.bytes);
  }

  public override string ToString()
  {
    return text ?? string.Empty;
  }
}
