using System;

namespace UnityEngine.Scripting
{
  [AttributeUsage(AttributeTargets.All, Inherited = false)]
  internal sealed class RequiredByNativeCodeAttribute : Attribute
  {
    public bool GenerateProxy { get; set; }
    public bool Optional { get; set; }
  }

  [AttributeUsage(AttributeTargets.All, Inherited = false)]
  internal sealed class UsedByNativeCodeAttribute : Attribute
  {
  }

  /// <summary>
  /// Prevents the linked IL2CPP code generator from stripping the attributed type or member.
  /// Unity-compatible preserve attribute for IL2CPP/AOT builds.
  /// </summary>
  [AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Enum | AttributeTargets.Constructor | AttributeTargets.Method | AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Event, Inherited = false)]
  public sealed class PreserveAttribute : Attribute
  {
  }

  /// <summary>
  /// Marks a method as always being included in the build, even if it's not directly referenced.
  /// </summary>
  [AttributeUsage(AttributeTargets.Method)]
  public sealed class AlwaysLinkAssemblyAttribute : Attribute
  {
  }

  /// <summary>
  /// Marks a type as always being preserved during code stripping.
  /// </summary>
  [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface)]
  public sealed class AlwaysLinkTypeAttribute : Attribute
  {
  }
}

namespace UnityEngine
{
  /// <summary>
  /// IL2CPP-specific options for controlling code generation behavior.
  /// </summary>
  [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = true)]
  public sealed class Il2CppSetOptionAttribute : Attribute
  {
    public Option Option { get; }
    public object Value { get; }

    public Il2CppSetOptionAttribute(Option option, object value)
    {
      Option = option;
      Value = value;
    }

    public Il2CppSetOptionAttribute(Option option)
    {
      Option = option;
      Value = true;
    }
  }

  public enum Option
  {
    NullCheck = 0,
    ArrayBoundsCheck = 1,
    DivideByZeroCheck = 2,
    SubArrayBoundsCheck = 3,
    Rpc = 4,
    MayResetExistingConnection = 5
  }
}
