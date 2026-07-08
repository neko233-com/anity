namespace UnityEditor;

public readonly struct MenuCommand
{
  public object? context { get; }
  public int userData { get; }

  public MenuCommand(object? context = null, int userData = 0)
  {
    this.context = context;
    this.userData = userData;
  }
}

