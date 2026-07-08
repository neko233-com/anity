using System;
using System.Collections.Generic;

namespace UnityEngine.UIElements;

[Serializable]
public class StyleSheet : ScriptableObject
{
  [SerializeField]
  private List<StyleRule> _rules = new();

  public IEnumerable<StyleRule> rules => _rules;

  public bool TryGetStyleRule(string selector, out StyleRule rule)
  {
    rule = null;
    foreach (var r in _rules)
    {
      if (r?.selectors != null)
      {
        foreach (var selectorObj in r.selectors)
        {
          if (selectorObj?.ToString() == selector)
          {
            rule = r;
            return true;
          }
        }
      }
    }
    return false;
  }

  public bool TryGetStyleRule(string selector, StyleComplexSelector.StyleSpecificity specificity, out StyleRule rule)
  {
    return TryGetStyleRule(selector, out rule);
  }

  public void Rebuild()
  {
    // Stub
  }
}

[Serializable]
public class StyleRule
{
  public string name;
  public List<StyleSelector> selectors;
  public List<StyleProperty> properties;
}

[Serializable]
public class StyleProperty
{
  public string name;
  public string value;
}

[Serializable]
public class StyleSelector
{
  public string rawSelector;
  public StyleComplexSelector.StyleSpecificity specificity;

  public override string ToString()
  {
    return rawSelector ?? string.Empty;
  }
}

public class StyleComplexSelector
{
  public struct StyleSpecificity
  {
    public uint a;
    public uint b;
    public uint c;

    public static bool operator >(StyleSpecificity left, StyleSpecificity right)
    {
      if (left.a != right.a) return left.a > right.a;
      if (left.b != right.b) return left.b > right.b;
      return left.c > right.c;
    }

    public static bool operator <(StyleSpecificity left, StyleSpecificity right)
    {
      if (left.a != right.a) return left.a < right.a;
      if (left.b != right.b) return left.b < right.b;
      return left.c < right.c;
    }
  }
}
