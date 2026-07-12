namespace UnityEngine.UIElements;

public class HelpBox : VisualElement
{
  public string text { get; set; } = string.Empty;
  public HelpBoxMessageType messageType { get; set; }

  public HelpBox()
  {
  }

  public HelpBox(string text, HelpBoxMessageType messageType)
  {
    this.text = text;
    this.messageType = messageType;
  }
}

public enum HelpBoxMessageType
{
  None = 0,
  Info = 1,
  Warning = 2,
  Error = 3
}
