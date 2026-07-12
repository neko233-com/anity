using System;

namespace UnityEngine;

public class TextAsset : UnityEngine.Object
{
  public string text { get; protected set; }
  public byte[] bytes { get; protected set; }
  public long dataSize => bytes?.Length ?? 0;

  public TextAsset()
  {
    text = string.Empty;
    bytes = Array.Empty<byte>();
  }

  public TextAsset(string text)
  {
    this.text = text ?? string.Empty;
    bytes = System.Text.Encoding.UTF8.GetBytes(this.text);
  }

  public TextAsset(byte[] bytes)
  {
    this.bytes = bytes ?? Array.Empty<byte>();
    text = System.Text.Encoding.UTF8.GetString(this.bytes);
  }

  public override string ToString()
  {
    return text ?? string.Empty;
  }
}
