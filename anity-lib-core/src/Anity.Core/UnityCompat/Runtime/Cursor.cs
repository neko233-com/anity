namespace UnityEngine;

public enum CursorLockMode
{
  None,
  Locked,
  Confined
}

public enum CursorMode
{
  Auto,
  ForceSoftware
}

public static class Cursor
{
  public static bool visible { get; set; } = true;
  public static CursorLockMode lockState { get; set; } = CursorLockMode.None;

  private static Texture2D _cursorTexture;
  private static Vector2 _hotspot;
  private static CursorMode _cursorMode;

  public static void SetCursor(Texture2D texture, Vector2 hotspot, CursorMode cursorMode)
  {
    _cursorTexture = texture;
    _hotspot = hotspot;
    _cursorMode = cursorMode;
  }
}
