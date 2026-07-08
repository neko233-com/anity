using System;
using System.Collections.Generic;

namespace UnityEngine.UIElements;

[Serializable]
public class StyleSheet : ScriptableObject
{
  [SerializeField]
  private List<StyleRule> _rules = new();

  public IEnumerable<StyleRule> rules => _rules;

  public int ruleCount => _rules.Count;

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
    rule = null;
    StyleRule bestMatch = null;
    StyleComplexSelector.StyleSpecificity bestSpecificity = default;

    foreach (var r in _rules)
    {
      if (r?.selectors != null)
      {
        foreach (var selectorObj in r.selectors)
        {
          if (selectorObj?.ToString() == selector)
          {
            if (bestMatch is null || selectorObj.specificity > bestSpecificity)
            {
              bestMatch = r;
              bestSpecificity = selectorObj.specificity;
            }
          }
        }
      }
    }

    rule = bestMatch;
    return rule is not null;
  }

  public bool TryGetStyleRuleWithSpecificity(string selector, out StyleRule rule, out StyleComplexSelector.StyleSpecificity specificity)
  {
    rule = null;
    specificity = default;

    foreach (var r in _rules)
    {
      if (r?.selectors != null)
      {
        foreach (var selectorObj in r.selectors)
        {
          if (selectorObj?.ToString() == selector)
          {
            rule = r;
            specificity = selectorObj.specificity;
            return true;
          }
        }
      }
    }
    return false;
  }

  public void Rebuild()
  {
  }

  public void AddRule(StyleRule rule)
  {
    if (rule is not null)
    {
      _rules.Add(rule);
    }
  }

  public void RemoveRule(StyleRule rule)
  {
    _rules.Remove(rule);
  }

  public void ClearRules()
  {
    _rules.Clear();
  }

  public StyleRule? FindRule(string selector)
  {
    foreach (var rule in _rules)
    {
      if (rule?.selectors != null)
      {
        foreach (var selectorObj in rule.selectors)
        {
          if (selectorObj?.ToString() == selector)
          {
            return rule;
          }
        }
      }
    }
    return null;
  }

  public List<StyleRule> FindAllRules(string selector)
  {
    var result = new List<StyleRule>();
    foreach (var rule in _rules)
    {
      if (rule?.selectors != null)
      {
        foreach (var selectorObj in rule.selectors)
        {
          if (selectorObj?.ToString() == selector)
          {
            result.Add(rule);
            break;
          }
        }
      }
    }
    return result;
  }

  public static StyleSheet FromUss(string ussContent)
  {
    _ = ussContent;
    return new StyleSheet();
  }

  public string ToUss()
  {
    return string.Empty;
  }
}

[Serializable]
public class StyleRule
{
  public string name;
  public List<StyleSelector> selectors = new();
  public List<StyleProperty> properties = new();

  public StyleProperty? FindProperty(string propertyName)
  {
    return properties.Find(p => p.name == propertyName);
  }

  public string GetPropertyValue(string propertyName, string defaultValue = "")
  {
    var prop = FindProperty(propertyName);
    return prop?.value ?? defaultValue;
  }

  public void SetProperty(string name, string value)
  {
    var existing = FindProperty(name);
    if (existing is not null)
    {
      existing.value = value;
    }
    else
    {
      properties.Add(new StyleProperty { name = name, value = value });
    }
  }

  public void AddSelector(string selector)
  {
    selectors.Add(new StyleSelector { rawSelector = selector });
  }
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
  public StyleSelectorRelationship previousRelationship;

  public override string ToString()
  {
    return rawSelector ?? string.Empty;
  }
}

public enum StyleSelectorRelationship
{
  None,
  Child,
  Descendant
}

public class StyleComplexSelector
{
  public struct StyleSpecificity : IComparable<StyleSpecificity>
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

    public int CompareTo(StyleSpecificity other)
    {
      if (this > other) return 1;
      if (this < other) return -1;
      return 0;
    }
  }

  public List<StyleSelector> selectors = new();
}

// USS parsing support
public static class StyleSheetParser
{
  public static StyleSheet Parse(string ussContent)
  {
    _ = ussContent;
    return new StyleSheet();
  }

  public static StyleSheet ParseFromFile(string filePath)
  {
    _ = filePath;
    return new StyleSheet();
  }
}
