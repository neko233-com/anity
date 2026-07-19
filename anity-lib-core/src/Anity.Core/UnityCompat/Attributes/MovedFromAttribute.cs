using System;
using System.Runtime.InteropServices;

namespace UnityEngine.Scripting.APIUpdating;

[AttributeUsage((AttributeTargets)5148)]
public class MovedFromAttribute : Attribute
{
  private readonly string _sourceAssembly;

  public MovedFromAttribute(string sourceNamespace)
    : this(true, sourceNamespace, null, null)
  {
  }

  public MovedFromAttribute(
    bool autoUpdateAPI,
    [Optional, DefaultParameterValue(null)] string sourceNamespace,
    [Optional, DefaultParameterValue(null)] string sourceAssembly,
    [Optional, DefaultParameterValue(null)] string sourceClassName)
  {
    _ = autoUpdateAPI;
    _ = sourceNamespace;
    _sourceAssembly = sourceAssembly;
    _ = sourceClassName;
  }

  public bool IsInDifferentAssembly => !string.IsNullOrEmpty(_sourceAssembly);
}
