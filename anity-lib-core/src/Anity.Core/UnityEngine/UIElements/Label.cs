namespace UnityEngine.UIElements;

public class Label : VisualElement
{
  public string text { get; set; } = string.Empty;

  public Label()
  {
  }

  public Label(string text)
  {
    this.text = text;
  }
}
