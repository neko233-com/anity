using System;

namespace UnityEngine.Bindings
{
  internal enum TargetType
  {
    Function = 0,
    Field = 1
  }

  [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true)]
  internal sealed class NativeHeaderAttribute : Attribute
  {
    public NativeHeaderAttribute(string header) => Header = header;
    public string Header { get; }
  }

  [AttributeUsage(AttributeTargets.Property)]
  internal sealed class NativePropertyAttribute : Attribute
  {
    public NativePropertyAttribute()
    {
    }

    public NativePropertyAttribute(string name) => Name = name;

    public NativePropertyAttribute(string name, bool isFree, TargetType targetType)
    {
      Name = name;
      IsFree = isFree;
      TargetType = targetType;
    }

    public string? Name { get; }
    public bool IsFree { get; }
    public TargetType TargetType { get; }
  }

  [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property)]
  internal sealed class NativeThrowsAttribute : Attribute
  {
  }

  [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
  internal sealed class PreventExecutionInStateAttribute : Attribute
  {
    public PreventExecutionInStateAttribute(int state, int severity, string message)
    {
      State = state;
      Severity = severity;
      Message = message;
    }

    public int State { get; }
    public int Severity { get; }
    public string Message { get; }
  }

  internal enum StaticAccessorType
  {
    Dot = 0,
    Arrow = 1,
    DoubleColon = 2,
    ArrowWithDefaultReturnIfNull = 3
  }

  [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Property)]
  internal sealed class StaticAccessorAttribute : Attribute
  {
    public StaticAccessorAttribute(string name)
      : this(name, StaticAccessorType.Dot)
    {
    }

    public StaticAccessorAttribute(string name, StaticAccessorType type)
    {
      Name = name;
      Type = type;
    }

    public string Name { get; }
    public StaticAccessorType Type { get; }
  }

  [AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor)]
  internal sealed class NativeMethodAttribute : Attribute
  {
    public NativeMethodAttribute()
    {
    }

    public NativeMethodAttribute(string name) => Name = name;

    public string? Name { get; set; }
    public bool IsFreeFunction { get; set; }
    public bool ThrowsException { get; set; }
  }

  [AttributeUsage(AttributeTargets.Parameter)]
  internal sealed class NotNullAttribute : Attribute
  {
    public NotNullAttribute(string exception) => Exception = exception;
    public string Exception { get; }
  }

  [AttributeUsage(AttributeTargets.Method)]
  internal sealed class FreeFunctionAttribute : Attribute
  {
    public FreeFunctionAttribute()
    {
    }

    public FreeFunctionAttribute(string name) => Name = name;

    public string? Name { get; set; }
    public bool HasExplicitThis { get; set; }
    public bool ThrowsException { get; set; }
    public bool IsThreadSafe { get; set; }
  }

  [AttributeUsage(AttributeTargets.Field | AttributeTargets.Method | AttributeTargets.Property)]
  internal sealed class NativeNameAttribute : Attribute
  {
    public NativeNameAttribute(string name) => Name = name;
    public string Name { get; }
  }

  [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
  internal sealed class NativeTypeAttribute : Attribute
  {
    public NativeTypeAttribute()
    {
    }

    public NativeTypeAttribute(int targetType, string name)
    {
      TargetType = targetType;
      Name = name;
    }

    public NativeTypeAttribute(string header) => Header = header;

    public int TargetType { get; }
    public string? Name { get; }
    public string? Header { get; set; }
  }

  [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
  internal sealed class NativeAsStructAttribute : Attribute
  {
  }

  [AttributeUsage(AttributeTargets.Method)]
  internal sealed class ThreadSafeAttribute : Attribute
  {
  }

  [AttributeUsage(AttributeTargets.Parameter)]
  internal sealed class UnmarshalledAttribute : Attribute
  {
  }
}

namespace UnityEngine
{
  [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
  internal sealed class NativeClassAttribute : Attribute
  {
    public NativeClassAttribute(string? qualifiedNativeName) => QualifiedNativeName = qualifiedNativeName;
    public string? QualifiedNativeName { get; }
  }

  [AttributeUsage(AttributeTargets.Class)]
  internal sealed class ExtensionOfNativeClassAttribute : Attribute
  {
  }

  [AttributeUsage(AttributeTargets.Class, Inherited = false)]
  [Scripting.UsedByNativeCode]
  public class ExcludeFromPresetAttribute : Attribute
  {
  }
}

namespace UnityEngine.Internal
{
  [Serializable]
  public class ExcludeFromDocsAttribute : Attribute
  {
  }

  [Serializable]
  [AttributeUsage((AttributeTargets)18432)]
  public class DefaultValueAttribute : Attribute
  {
    public DefaultValueAttribute(string value) => Value = value;

    public object Value { get; }

    public override bool Equals(object? obj)
      => obj is DefaultValueAttribute other && Equals(Value, other.Value);

    public override int GetHashCode() => Value.GetHashCode();
  }
}

namespace UnityEngineInternal
{
  internal enum TypeInferenceRules
  {
    TypeReferencedByFirstArgument = 0,
    TypeReferencedBySecondArgument = 1,
    ArrayOfTypeReferencedByFirstArgument = 2,
    TypeOfFirstArgument = 3
  }

  [AttributeUsage(AttributeTargets.Method)]
  internal sealed class TypeInferenceRuleAttribute : Attribute
  {
    public TypeInferenceRuleAttribute(TypeInferenceRules rule) => Rule = rule;
    public TypeInferenceRules Rule { get; }
  }
}

namespace Unity.IL2CPP.CompilerServices
{
  [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
  internal sealed class Il2CppEagerStaticClassConstructionAttribute : Attribute
  {
  }
}
