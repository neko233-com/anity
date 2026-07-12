using UnityEngine;

namespace UnityEditor;

public sealed class MenuCommand
{
  public Object context { get; }
  public int userData { get; }

  public MenuCommand(Object context)
  {
    this.context = context;
    this.userData = 0;
  }

  public MenuCommand(Object context, int userData)
  {
    this.context = context;
    this.userData = userData;
  }
}
