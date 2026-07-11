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

public enum FontStyles
{
  Normal = 0,
  Bold = 1,
  Italic = 2,
  BoldAndItalic = 3
}
