using System;
using System.Collections.Generic;

namespace UnityEngine.UIElements;

public interface IUxmlFactory
{
  string uxmlName { get; }
  Type substituteForType { get; }
  Type type { get; }
  IEnumerable<UxmlAttributeDescription> uxmlAttributes { get; }
  IEnumerable<UxmlChildElementDescription> uxmlChildElements { get; }
  bool CanHaveAttributes { get; }
  bool CanHaveChildren { get; }

  VisualElement Create(IUxmlAttributes bag, CreationContext cc);
  void AcceptAttributes(VisualElement ve, IUxmlAttributes bag, CreationContext cc);
}

public interface IUxmlFactory<TELEMENT, TBag> : IUxmlFactory where TELEMENT : VisualElement, new() where TBag : IUxmlAttributes, new()
{
  new TELEMENT Create(IUxmlAttributes bag, CreationContext cc);
  void AcceptAttributes(TELEMENT ve, TBag bag, CreationContext cc);
}

public interface IUxmlAttributes
{
  bool HasAttribute(string attributeName);
  string GetAttributeValue(string attributeName);
  bool TryGetAttributeValue(string attributeName, out string value);
}

public struct CreationContext
{
  public VisualElement target { get; set; }
  public VisualTreeAsset sourceAsset { get; set; }
  public Dictionary<string, List<VisualElementAsset>> assetOverrides { get; set; }
  public List<VisualElementAsset> overrides { get; set; }
  public StyleSheet styleSheet { get; set; }
  public List<string> errors { get; set; }
}

public class UxmlAttributeDescription
{
  public string name { get; set; }
  public string obsoleteNames { get; set; }
  public string typeFromPreviousAttribute { get; set; }
  public Type type { get; set; }
  public Type defaultValueType { get; set; }
  public object defaultValue { get; set; }
  public string restrictions { get; set; }
  public bool useTypeRestriction { get; set; }
}

public class UxmlChildElementDescription
{
  public Type elementType { get; set; }
  public List<string> allowedNames { get; set; }
}

public class UxmlAttributeOverride
{
  public string elementName { get; set; }
  public string attributeName { get; set; }
  public string attributeValue { get; set; }
}
