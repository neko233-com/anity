using System.Collections.Generic;

namespace UnityEngine;

public class Font : Object
{
  private string[] _fontNames = Array.Empty<string>();
  private Material? _material;
  private int _fontSize;
  private bool _dynamic;
  private readonly Dictionary<char, CharacterInfo> _characterInfo = new();

  public Font()
  {
    _fontNames = new[] { "Arial" };
  }

  public Font(string name)
  {
    _fontNames = new[] { name ?? "Arial" };
    this.name = name ?? "Arial";
  }

  public int fontSize
  {
    get => _fontSize;
    set => _fontSize = value;
  }

  public Material? material
  {
    get => _material;
    set => _material = value;
  }

  public string[] fontNames
  {
    get => _fontNames;
    set => _fontNames = value ?? Array.Empty<string>();
  }

  public bool dynamic
  {
    get => _dynamic;
    set => _dynamic = value;
  }

  public CharacterInfo[] characterInfo
  {
    get => new List<CharacterInfo>(_characterInfo.Values).ToArray();
    set
    {
      _characterInfo.Clear();
      if (value != null)
      {
        foreach (var info in value)
        {
          _characterInfo[(char)info.index] = info;
        }
      }
    }
  }

  public bool GetCharacterInfo(char ch, out CharacterInfo info, int size)
  {
    return _characterInfo.TryGetValue(ch, out info);
  }

  public bool GetCharacterInfo(char ch, out CharacterInfo info)
  {
    return _characterInfo.TryGetValue(ch, out info);
  }

  public bool GetCharacterInfo(char ch, out CharacterInfo info, int size, FontStyle style)
  {
    _ = style;
    return _characterInfo.TryGetValue(ch, out info);
  }

  public void RequestCharactersInTexture(string characters, int size = 0, FontStyle style = FontStyle.Normal)
  {
    _ = characters; _ = size; _ = style;
  }

  public static Font CreateDynamicFontFromOSFont(string fontname, int size)
  {
    var font = new Font(fontname);
    font.fontSize = size;
    font.dynamic = true;
    return font;
  }

  public static Font CreateDynamicFontFromOSFont(string[] fontnames, int size)
  {
    var font = new Font(fontnames?.Length > 0 ? fontnames[0] : "Arial");
    font.fontNames = fontnames ?? Array.Empty<string>();
    font.fontSize = size;
    font.dynamic = true;
    return font;
  }

  public static event Action<Font>? textureRebuilt;

  internal static void OnTextureRebuilt(Font font)
  {
    textureRebuilt?.Invoke(font);
  }

  public int lineHeight => Mathf.Max(1, _fontSize > 0 ? _fontSize : 16);
  public int ascent => lineHeight;
}

public struct CharacterInfo
{
  public int index;
  public float advance;
  public int size;
  public Rect glyph;
  public Rect uv;
  public bool flipped;
}
