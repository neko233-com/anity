using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace UnityEditor;

public enum SerializedPropertyType
{
  Generic = 0,
  Integer = 1,
  Boolean = 2,
  Float = 3,
  String = 4,
  Color = 5,
  ObjectReference = 6,
  Vector2 = 7,
  Vector3 = 8,
  Vector4 = 9,
  Rect = 10,
  Array = 11,
  Enum = 12,
  Vector2Int = 20,
  Vector3Int = 21,
  RectInt = 22,
  Quaternion = 13,
  AnimationCurve = 14,
  Bounds = 15,
  Gradient = 16,
  Char = 17,
  BoundsInt = 23,
  GradientSize = 18,
  LayerMask = 19
}

public sealed class SerializedProperty
{
  private readonly object? _container;
  private readonly string _propertyPath;
  private readonly SerializedObject? _serializedObject;
  private string _displayName;
  private string _tooltip;
  private bool _displayNameResolved;
  private bool _tooltipResolved;

  public SerializedObject? serializedObject => _serializedObject;
  public string propertyPath { get; }
  public string name { get; }
  public string type => rawValue?.GetType().Name ?? string.Empty;
  public string displayName
  {
    get
    {
      if (!_displayNameResolved)
      {
        ResolveDisplayInfo();
      }
      return _displayName;
    }
    set
    {
      _displayName = value;
      _displayNameResolved = true;
    }
  }

  public bool displayNameHasRichText { get; set; }

  public string tooltip
  {
    get
    {
      if (!_tooltipResolved)
      {
        ResolveDisplayInfo();
      }
      return _tooltip;
    }
    set
    {
      _tooltip = value;
      _tooltipResolved = true;
    }
  }

  public int depth => _propertyPath.Count(c => c == '.');
  public int childrenCount => GetChildrenCount();
  public object? rawValue => ReadValue();

  public SerializedPropertyType propertyType => ResolvePropertyType();
  public bool hasVisibleChildren => hasChildren;
  public bool hasMultipleDifferentValues => false;
  public bool isArray => rawValue is Array || rawValue is IList;
  public bool hasChildren => propertyType == SerializedPropertyType.Generic;
  public bool isExpanded { get; set; }
  public bool isFixedBufferSize => false;
  public int fixedBufferSize => 0;
  public bool editable { get; set; } = true;
  public bool isAnimated { get; set; }
  public bool isInstantiatedPrefab { get; set; }
  public bool prefabOverride { get; set; }
  public string arrayElementType
  {
    get
    {
      var value = rawValue;
      if (value is Array arr && arr.Length > 0) return arr.GetType().GetElementType()?.Name ?? "object";
      if (value is IList list && list.Count > 0)
      {
        var item = list[0];
        return item?.GetType().Name ?? "object";
      }
      return "object";
    }
  }

  public bool boxerOnEnumValueWarning { get; set; }

  public WeakReference? objectReferenceValueWeak { get; set; }

  public int enumValueIndex
  {
    get
    {
      if (rawValue is Enum enumValue)
      {
        return Convert.ToInt32(enumValue);
      }
      return rawValue is int intVal ? intVal : 0;
    }
    set => SetValue(value);
  }

  public int enumValueFlag
  {
    get => enumValueIndex;
    set => enumValueIndex = value;
  }

  public string[] enumNames
  {
    get
    {
      var value = rawValue;
      if (value is Enum enumVal)
      {
        return Enum.GetNames(enumVal.GetType());
      }
      return Array.Empty<string>();
    }
  }

  public string[] enumDisplayNames => enumNames;

  public object? managedReferenceValue
  {
    get => rawValue;
    set => SetValue(value);
  }
  public string managedReferenceFieldTypename => rawValue?.GetType().FullName ?? string.Empty;

  public char charValue
  {
    get
    {
      if (rawValue is char c) return c;
      var str = stringValue;
      return !string.IsNullOrEmpty(str) ? str[0] : '\0';
    }
    set => SetValue(value);
  }

  public Vector2Int vector2IntValue
  {
    get => rawValue is Vector2Int v ? v : default;
    set => SetValue(value);
  }

  public Vector3Int vector3IntValue
  {
    get => rawValue is Vector3Int v ? v : default;
    set => SetValue(value);
  }

  public RectInt rectIntValue
  {
    get => rawValue is RectInt r ? r : default;
    set => SetValue(value);
  }

  public int arraySize
  {
    get
    {
      return rawValue switch
      {
        Array arr => arr.Length,
        IList list => list.Count,
        _ => 0
      };
    }
    set
    {
      var target = rawValue;
      if (target is Array arr)
      {
        var elementType = arr.GetType().GetElementType() ?? typeof(object);
        var normalized = Math.Max(0, value);
        var resized = Array.CreateInstance(elementType, normalized);
        Array.Copy(arr, resized, Math.Min(arr.Length, resized.Length));
        SetValue(resized);
        return;
      }

      if (target is IList list)
      {
        var targetCount = list.Count;
        var targetSize = Math.Max(0, value);
        if (targetSize < targetCount)
        {
          for (var i = targetCount - 1; i >= targetSize; --i)
          {
            list.RemoveAt(i);
          }
          return;
        }

        var elementType = GetListElementType(list) ?? typeof(object);
        for (var i = targetCount; i < targetSize; ++i)
        {
          list.Add(CreateDefault(elementType));
        }
      }
    }
  }

  public SerializedProperty(object? container, string propertyPath) : this(null, container, propertyPath) { }

  public SerializedProperty(SerializedObject? serializedObject, object? container, string propertyPath)
  {
    _serializedObject = serializedObject;
    _container = container;
    this.propertyPath = propertyPath;
    _propertyPath = propertyPath;
    name = GetLeafName(propertyPath);
    _displayName = ObjectNames.NicifyVariableName(name);
    _tooltip = string.Empty;
  }

  public int intValue
  {
    get => Convert.ToInt32(rawValue ?? 0, CultureInfo.InvariantCulture);
    set => SetValue(value);
  }

  public float floatValue
  {
    get => Convert.ToSingle(rawValue ?? 0f, CultureInfo.InvariantCulture);
    set => SetValue(value);
  }

  public double doubleValue
  {
    get => Convert.ToDouble(rawValue ?? 0d, CultureInfo.InvariantCulture);
    set => SetValue(value);
  }

  public long longValue
  {
    get => Convert.ToInt64(rawValue ?? 0L, CultureInfo.InvariantCulture);
    set => SetValue(value);
  }

  public bool boolValue
  {
    get => Convert.ToBoolean(rawValue ?? false, CultureInfo.InvariantCulture);
    set => SetValue(value);
  }

  public string? stringValue
  {
    get => rawValue?.ToString();
    set => SetValue(value);
  }

  public UnityEngine.Object? objectReferenceValue
  {
    get => rawValue as UnityEngine.Object;
    set
    {
      SetValue(value);
      objectReferenceValueWeak = value is not null ? new WeakReference(value) : null;
    }
  }

  public Color colorValue
  {
    get => rawValue is Color color ? color : default;
    set => SetValue(value);
  }

  public Vector2 vector2Value
  {
    get => rawValue is Vector2 value ? value : default;
    set => SetValue(value);
  }

  public Vector3 vector3Value
  {
    get => rawValue is Vector3 value ? value : default;
    set => SetValue(value);
  }

  public Vector4 vector4Value
  {
    get => rawValue is Vector4 value ? value : default;
    set => SetValue(value);
  }

  public Quaternion quaternionValue
  {
    get => rawValue is Quaternion value ? value : default;
    set => SetValue(value);
  }

  public Rect rectValue
  {
    get => rawValue is Rect value ? value : default;
    set => SetValue(value);
  }

  public Bounds boundsValue
  {
    get => rawValue is Bounds value ? value : default;
    set => SetValue(value);
  }

  public AnimationCurve animationCurveValue
  {
    get => rawValue as AnimationCurve ?? new AnimationCurve();
    set => SetValue(value);
  }

  public Gradient gradientValue
  {
    get => rawValue as Gradient ?? new Gradient();
    set => SetValue(value);
  }

  public SerializedPropertyType GuessType(object? value)
  {
    return value switch
    {
      null => SerializedPropertyType.Generic,
      int => SerializedPropertyType.Integer,
      long => SerializedPropertyType.Integer,
      short => SerializedPropertyType.Integer,
      byte => SerializedPropertyType.Integer,
      uint => SerializedPropertyType.Integer,
      ulong => SerializedPropertyType.Integer,
      ushort => SerializedPropertyType.Integer,
      sbyte => SerializedPropertyType.Integer,
      float => SerializedPropertyType.Float,
      double => SerializedPropertyType.Float,
      bool => SerializedPropertyType.Boolean,
      string => SerializedPropertyType.String,
      char => SerializedPropertyType.Char,
      Vector2 => SerializedPropertyType.Vector2,
      Vector3 => SerializedPropertyType.Vector3,
      Vector4 => SerializedPropertyType.Vector4,
      Quaternion => SerializedPropertyType.Quaternion,
      Rect => SerializedPropertyType.Rect,
      Vector2Int => SerializedPropertyType.Vector2Int,
      Vector3Int => SerializedPropertyType.Vector3Int,
      RectInt => SerializedPropertyType.RectInt,
      BoundsInt => SerializedPropertyType.BoundsInt,
      Color => SerializedPropertyType.Color,
      UnityEngine.Object => SerializedPropertyType.ObjectReference,
      Enum => SerializedPropertyType.Enum,
      Bounds => SerializedPropertyType.Bounds,
      AnimationCurve => SerializedPropertyType.AnimationCurve,
      Gradient => SerializedPropertyType.Gradient,
      IList => SerializedPropertyType.Generic,
      _ => SerializedPropertyType.Generic
    };
  }

  public SerializedProperty? FindPropertyRelative(string relativePropertyPath)
  {
    if (_container is null || string.IsNullOrWhiteSpace(relativePropertyPath))
    {
      return null;
    }

    var current = rawValue ?? _container;
    var combined = CombinePath(_propertyPath, relativePropertyPath);
    return new SerializedProperty(_serializedObject, current is not null ? current : _container, combined);
  }

  public SerializedProperty GetArrayElementAtIndex(int index)
  {
    return new SerializedProperty(_serializedObject, _container, $"{_propertyPath}[{Math.Max(0, index)}]");
  }

  public SerializedProperty Copy()
  {
    return new SerializedProperty(_serializedObject, _container, _propertyPath)
    {
      isExpanded = isExpanded,
      editable = editable,
      displayNameHasRichText = displayNameHasRichText
    };
  }

  public SerializedProperty GetEndProperty()
  {
    return GetEndProperty(true);
  }

  public SerializedProperty GetEndProperty(bool includeInvisible)
  {
    _ = includeInvisible;
    return Copy();
  }

  public bool Next(bool enterChildren)
  {
    if (_container is null)
    {
      return false;
    }

    if (enterChildren && hasChildren)
    {
      return true;
    }

    return false;
  }

  public bool NextVisible(bool enterChildren)
  {
    return Next(enterChildren);
  }

  public void Reset()
  {
  }

  public int CountInProperty()
  {
    var value = rawValue;
    if (value is Array arr) return arr.Length;
    if (value is IList list) return list.Count;
    return 0;
  }

  public void ClearArray()
  {
    var node = Resolve(_container, _propertyPath);
    if (node.Value is Array arr)
    {
      var elementType = arr.GetType().GetElementType() ?? typeof(object);
      var empty = Array.CreateInstance(elementType, 0);
      SetValue(empty);
      return;
    }

    if (node.Value is IList list)
    {
      list.Clear();
    }
  }

  public SerializedProperty GetFixedBufferElementAtIndex(int index)
  {
    return new SerializedProperty(_serializedObject, _container, $"{_propertyPath}[{Math.Max(0, index)}]");
  }

  public void InsertArrayElementAtIndex(int index)
  {
    var node = Resolve(_container, _propertyPath);
    if (node.Value is null) return;
    if (node.Value is Array arr)
    {
      var elementType = arr.GetType().GetElementType() ?? typeof(object);
      var normalized = Math.Clamp(index, 0, arr.Length);
      var resized = Array.CreateInstance(elementType, arr.Length + 1);
      Array.Copy(arr, 0, resized, 0, normalized);
      Array.Copy(arr, normalized, resized, normalized + 1, arr.Length - normalized);
      resized.SetValue(CreateDefault(elementType), normalized);
      SetValue(resized);
      return;
    }

    if (node.Value is IList list)
    {
      var normalized = Math.Clamp(index, 0, list.Count);
      var defaultValue = CreateDefault(GetListElementType(list));
      list.Insert(normalized, defaultValue);
    }
  }

  public void DeleteArrayElementAtIndex(int index)
  {
    var node = Resolve(_container, _propertyPath);
    if (node.Value is Array arr)
    {
      if (index < 0 || index >= arr.Length)
      {
        return;
      }
      var elementType = arr.GetType().GetElementType() ?? typeof(object);
      var resized = Array.CreateInstance(elementType, Math.Max(0, arr.Length - 1));
      Array.Copy(arr, 0, resized, 0, index);
      if (index + 1 < arr.Length)
      {
        Array.Copy(arr, index + 1, resized, index, arr.Length - index - 1);
      }
      SetValue(resized);
      return;
    }

    if (node.Value is IList list && index >= 0 && index < list.Count)
    {
      list.RemoveAt(index);
    }
  }

  public void MoveArrayElement(int srcIndex, int dstIndex)
  {
    var node = Resolve(_container, _propertyPath);
    if (node.Value is Array arr)
    {
      if (srcIndex < 0 || srcIndex >= arr.Length || dstIndex < 0 || dstIndex >= arr.Length)
      {
        return;
      }
      var elementType = arr.GetType().GetElementType() ?? typeof(object);
      var resized = Array.CreateInstance(elementType, arr.Length);
      var element = arr.GetValue(srcIndex);
      if (srcIndex < dstIndex)
      {
        Array.Copy(arr, 0, resized, 0, srcIndex);
        Array.Copy(arr, srcIndex + 1, resized, srcIndex, dstIndex - srcIndex);
        Array.Copy(arr, dstIndex + 1, resized, dstIndex + 1, arr.Length - dstIndex - 1);
      }
      else
      {
        Array.Copy(arr, 0, resized, 0, dstIndex);
        Array.Copy(arr, dstIndex, resized, dstIndex + 1, srcIndex - dstIndex);
        Array.Copy(arr, srcIndex + 1, resized, srcIndex + 1, arr.Length - srcIndex - 1);
      }
      resized.SetValue(element, dstIndex);
      SetValue(resized);
      return;
    }

    if (node.Value is IList list && srcIndex >= 0 && srcIndex < list.Count && dstIndex >= 0 && dstIndex < list.Count)
    {
      var item = list[srcIndex];
      list.RemoveAt(srcIndex);
      list.Insert(dstIndex, item);
    }
  }

  public int GetArrayElementIndex()
  {
    var idx = ReadPathArrayIndex(_propertyPath);
    return idx ?? -1;
  }

  private int GetChildrenCount()
  {
    var value = rawValue;
    if (value is null) return 0;
    var type = value.GetType();
    var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
    return fields.Count(f => !f.IsStatic && f.IsPublic || f.GetCustomAttribute<SerializeField>() is not null);
  }

  private void ResolveDisplayInfo()
  {
    _displayName = ObjectNames.NicifyVariableName(name);
    _tooltip = string.Empty;

    var value = rawValue;
    if (value is not null || _container is not null)
    {
      var target = value ?? _container;
      var member = FindMember(target, name);
      if (member is not null)
      {
        var tooltipAttr = member.GetCustomAttribute<TooltipAttribute>();
        if (tooltipAttr is not null)
        {
          _tooltip = tooltipAttr.tooltip;
        }

        var headerAttr = member.GetCustomAttribute<HeaderAttribute>();
        if (headerAttr is not null)
        {
          _displayName = headerAttr.header;
        }
      }
    }

    _displayNameResolved = true;
    _tooltipResolved = true;
  }

  private static MemberInfo? FindMember(object? target, string memberName)
  {
    if (target is null) return null;
    var type = target.GetType();
    const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    var field = type.GetField(memberName, flags);
    if (field is not null) return field;
    return type.GetProperty(memberName, flags);
  }

  private SerializedPropertyType ResolvePropertyType() => GuessType(rawValue);

  private object? ReadValue()
  {
    if (_container is null || string.IsNullOrWhiteSpace(_propertyPath))
    {
      return null;
    }
    return Resolve(_container, _propertyPath).Value;
  }

  private void SetValue(object? value)
  {
    if (_container is null || string.IsNullOrWhiteSpace(_propertyPath))
    {
      return;
    }
    var node = Resolve(_container, _propertyPath);
    if (!node.TrySet(_container, value))
    {
      if (node.Value == null)
      {
        return;
      }
    }
    if (_serializedObject is not null)
    {
      _serializedObject.hasModifiedProperties = true;
    }
  }

  private static object? CreateDefault(Type? type)
  {
    if (type is null)
    {
      return null;
    }
    return type.IsValueType ? Activator.CreateInstance(type) : null;
  }

  private static Type? GetListElementType(IList list)
  {
    var type = list.GetType();
    if (!type.IsGenericType)
    {
      return null;
    }

    var args = type.GetGenericArguments();
    return args.Length > 0 ? args[0] : null;
  }

  private static string CombinePath(string left, string right)
  {
    if (string.IsNullOrWhiteSpace(left))
    {
      return right;
    }
    if (string.IsNullOrWhiteSpace(right))
    {
      return left;
    }
    return $"{left}.{right}";
  }

  private static string GetLeafName(string path)
  {
    if (string.IsNullOrWhiteSpace(path))
    {
      return string.Empty;
    }
    var normalized = path.Trim();
    var lastDot = normalized.LastIndexOf('.');
    var lastSegment = lastDot < 0 ? normalized : normalized[(lastDot + 1)..];
    var bracket = lastSegment.IndexOf('[');
    return bracket < 0 ? lastSegment : lastSegment[..bracket];
  }

  private static int? ReadPathArrayIndex(string path)
  {
    var start = path.IndexOf('[');
    var end = path.IndexOf(']', start + 1);
    if (start >= 0 && end > start + 1 && int.TryParse(path[(start + 1)..end], out var index))
    {
      return index;
    }
    return null;
  }

  private static ResolvedPathNode Resolve(object? root, string path)
  {
    if (root is null || string.IsNullOrWhiteSpace(path))
    {
      return ResolvedPathNode.Invalid;
    }

    var current = root;
    var tokens = path.Split('.', StringSplitOptions.RemoveEmptyEntries)
      .Select(t => t.Trim())
      .ToArray();
    if (tokens.Length == 0)
    {
      return ResolvedPathNode.Invalid;
    }

    object? owner = null;
    string? memberName = null;
    int? arrayIndex = null;
    object? value = null;

    for (var i = 0; i < tokens.Length; i++)
    {
      var token = tokens[i];
      if (!TryParseToken(token, out var name, out var index))
      {
        return ResolvedPathNode.Invalid;
      }

      var member = ReadMember(current, name);
      if (member is null)
      {
        return ResolvedPathNode.Invalid;
      }

      owner = current;
      memberName = name;
      arrayIndex = null;

      if (index is null)
      {
        if (i == tokens.Length - 1)
        {
          value = member;
          return new ResolvedPathNode(owner, memberName, null, value);
        }
        current = member;
        continue;
      }

      if (member is not Array arr && member is not IList list)
      {
        return ResolvedPathNode.Invalid;
      }

      var valueAtIndex = ReadIndexed(member, index.Value);
      if (i == tokens.Length - 1)
      {
        return new ResolvedPathNode(
          owner: owner,
          memberName: memberName,
          arrayIndex: index,
          value: valueAtIndex,
          collection: member,
          hasCollection: true);
      }

      if (valueAtIndex is null)
      {
        return ResolvedPathNode.Invalid;
      }

      current = valueAtIndex;
      value = member;
      arrayIndex = index;
    }

    if (memberName is null)
    {
      return ResolvedPathNode.Invalid;
    }

    return new ResolvedPathNode(owner, memberName, arrayIndex, value, null, false);
  }

  private static object? ReadMember(object target, string memberName)
  {
    if (target is null)
    {
      return null;
    }
    var type = target.GetType();
    const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    var field = type.GetField(memberName, flags);
    if (field is not null)
    {
      return field.GetValue(target);
    }

    var property = type.GetProperty(memberName, flags);
    return property?.GetValue(target);
  }

  private static void WriteMember(object target, string memberName, object? value)
  {
    if (target is null)
    {
      return;
    }
    var type = target.GetType();
    const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    var field = type.GetField(memberName, flags);
    if (field is not null)
    {
      field.SetValue(target, value);
      return;
    }

    var property = type.GetProperty(memberName, flags);
    if (property is not null && property.CanWrite)
    {
      property.SetValue(target, value);
    }
  }

  private static object? ReadIndexed(object collection, int index)
  {
    if (collection is Array arr)
    {
      return index >= 0 && index < arr.Length ? arr.GetValue(index) : null;
    }
    if (collection is IList list)
    {
      return index >= 0 && index < list.Count ? list[index] : null;
    }
    return null;
  }

  private static void WriteIndexed(object collection, int index, object? value)
  {
    if (collection is Array arr)
    {
      if (index >= 0 && index < arr.Length)
      {
        arr.SetValue(value, index);
      }
      return;
    }

    if (collection is IList list)
    {
      if (index >= 0 && index < list.Count)
      {
        list[index] = value;
      }
    }
  }

  private static bool TryParseToken(string token, out string memberName, out int? index)
  {
    memberName = token;
    index = null;

    var bracketStart = token.IndexOf('[');
    if (bracketStart < 0)
    {
      return true;
    }

    if (bracketStart == 0 || !token.EndsWith(']'))
    {
      return false;
    }

    memberName = token[..bracketStart];
    var inside = token[(bracketStart + 1)..^1];
    if (string.IsNullOrWhiteSpace(inside) || !int.TryParse(inside, out var parsed))
    {
      return false;
    }
    index = parsed;
    return true;
  }

  private sealed class ResolvedPathNode
  {
    public ResolvedPathNode(
      object? owner,
      string? memberName,
      int? arrayIndex,
      object? value,
      object? collection = null,
      bool hasCollection = false)
    {
      Owner = owner;
      MemberName = memberName;
      ArrayIndex = arrayIndex;
      Value = value;
      Collection = collection;
      HasCollection = hasCollection;
    }

    public object? Owner { get; }
    public string? MemberName { get; }
    public int? ArrayIndex { get; }
    public object? Value { get; }
    public object? Collection { get; }
    public bool HasCollection { get; }

    public static ResolvedPathNode Invalid => new ResolvedPathNode(null, null, null, null, null, false);

    public bool TrySet(object root, object? value)
    {
      if (Owner is null || MemberName is null)
      {
        return false;
      }

      if (HasCollection && ArrayIndex is not null)
      {
        WriteIndexed(Collection!, ArrayIndex.Value, value);
        return true;
      }

      WriteMember(Owner, MemberName, value);
      return true;
    }
  }
}
