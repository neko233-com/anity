namespace UnityEngine.UI;

public interface ICanvasElement
{
  Transform transform { get; }
  void Rebuild(CanvasUpdate executing);
  void LayoutComplete();
  void GraphicUpdateComplete();
  bool IsDestroyed();
}

public enum Transition
{
  None,
  ColorTint,
  SpriteSwap,
  Animation
}

public enum NavigationMode
{
  None,
  Horizontal,
  Vertical,
  Automatic,
  Explicit
}

public enum ImageFillMethod
{
  Horizontal,
  Vertical,
  Radial90,
  Radial180,
  Radial360
}

public enum ToggleTransition
{
  None,
  Fade
}

public enum FontStyles
{
  Normal = 0,
  Bold = 1,
  Italic = 2,
  BoldAndItalic = 3
}

public enum TouchScreenKeyboardType
{
  Default = 0,
  ASCIICapable = 1,
  NumbersAndPunctuation = 2,
  URL = 3,
  NumberPad = 4,
  PhonePad = 5,
  NamePhonePad = 6,
  EmailAddress = 7,
  NintendoNetworkAccount = 8,
  Social = 9,
  Search = 10,
  DecimalPad = 11
}
