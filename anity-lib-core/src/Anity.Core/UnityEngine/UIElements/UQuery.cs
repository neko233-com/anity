using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace UnityEngine.UIElements;

// UQuery support
public struct UQueryEnumerable<T> : IEnumerable<T> where T : VisualElement
{
  private readonly VisualElement _root;
  private readonly string _name;
  private readonly string _className;

  internal UQueryEnumerable(VisualElement root, string name, string className)
  {
    _root = root;
    _name = name;
    _className = className;
  }

  public IEnumerator<T> GetEnumerator()
  {
    foreach (var child in _root.Children())
    {
      if (child is T typed && Matches(typed))
        yield return typed;

      foreach (var descendant in Descendants<T>(child))
      {
        if (Matches(descendant))
          yield return descendant;
      }
    }
  }

  IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

  private bool Matches(VisualElement element)
  {
    if (!string.IsNullOrEmpty(_name) && element.name != _name) return false;
    if (!string.IsNullOrEmpty(_className) && !element.ClassListContains(_className)) return false;
    return true;
  }

  private static IEnumerable<T> Descendants<T>(VisualElement parent) where T : VisualElement
  {
    foreach (var child in parent.Children())
    {
      if (child is T typed)
        yield return typed;

      foreach (var descendant in Descendants<T>(child))
        yield return descendant;
    }
  }
}

public struct UQueryExpression<T> where T : VisualElement
{
  private readonly List<T> _results;

  internal UQueryExpression(List<T> results)
  {
    _results = results;
  }

  public T First()
  {
    return _results.Count > 0 ? _results[0] : default;
  }

  public T Last()
  {
    return _results.Count > 0 ? _results[^1] : default;
  }

  public T AtIndex(int index)
  {
    return index >= 0 && index < _results.Count ? _results[index] : default;
  }

  public List<T> ToList()
  {
    return _results;
  }

  public T[] ToArray()
  {
    return _results.ToArray();
  }

  public T Where(Func<T, bool> predicate)
  {
    return _results.FirstOrDefault(predicate);
  }

  public bool Any(Func<T, bool> predicate = null)
  {
    return predicate is null ? _results.Count > 0 : _results.Any(predicate);
  }

  public int Count()
  {
    return _results.Count;
  }
}

public static class UQueryExtensions
{
  public static UQueryExpression<T> Q<T>(this VisualElement e, string name = null, string className = null) where T : VisualElement
  {
    return new UQueryExpression<T>(e.Query<T>(name, className).ToList());
  }

  public static UQueryExpression<VisualElement> Q(this VisualElement e, string name = null, string className = null)
  {
    return new UQueryExpression<VisualElement>(e.Query<VisualElement>(name, className).ToList());
  }

  public static void ForEach<T>(this IEnumerable<T> source, Action<T> action)
  {
    if (source is null || action is null) return;
    foreach (var item in source)
      action(item);
  }
}
