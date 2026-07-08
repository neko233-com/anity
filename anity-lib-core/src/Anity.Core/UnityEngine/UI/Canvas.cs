namespace UnityEngine.UI;

public enum RenderMode
{
  ScreenSpaceOverlay = 0,
  ScreenSpaceCamera = 1,
  WorldSpace = 2
}

public enum SortOrder
{
  Normal = 0,
  TopLeft = 1
}

public class Canvas : Behaviour
{
  public RenderMode renderMode { get; set; }
  public Camera? worldCamera { get; set; }
  public int sortingOrder { get; set; }
  public int sortOrder { get; set; }
  public bool overrideSorting { get; set; }
  public int targetDisplay { get; set; }
  public float scaleFactor { get; set; } = 1f;
  public float referencePixelsPerUnit { get; set; } = 100f;
  public bool pixelPerfect { get; set; }
  public bool overridePixelPerfect { get; set; }
  public float planeDistance { get; set; }
  public bool additionalShaderChannelsFlag { get; set; }
  public string sortingLayerName { get; set; } = string.Empty;
  public int sortingLayerID { get; set; }

  public static event System.Action<bool>? preWillRenderCanvases;
  public static event System.Action? willRenderCanvases;

  public void ForceUpdateCanvases()
  {
    willRenderCanvases?.Invoke();
  }
}
