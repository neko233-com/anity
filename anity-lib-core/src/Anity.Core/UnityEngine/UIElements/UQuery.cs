using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace UnityEngine.UIElements;

// UQuery support
public struct UQueryEnumerable<T> : IEnumerable<T>, IEnumerable where T : VisualElement
{
  private readonly VisualElement _root;
  private readonly string _name;
  private readonly string _className;
  private readonly string _tagName;
  private readonly List<Predicate<T>> _predicates;

  internal UQueryEnumerable(VisualElement root, string name = null, string className = null, string tagName = null)
  {
    _root = root;
    _name = name;
    _className = className;
    _tagName = tagName;
    _predicates = new List<Predicate<T>>();
  }

  internal UQueryEnumerable(VisualElement root, string name, string className, string tagName, List<Predicate<T>> predicates)
  {
    _root = root;
    _name = name;
    _className = className;
    _tagName = tagName;
    _predicates = predicates;
  }

  public IEnumerator<T> GetEnumerator()
  {
    foreach (var child in _root.Children())
    {
      if (child is T typed && Matches(typed))
        yield return typed;

      foreach (var descendant in Descendants(child))
      {
        if (Matches(descendant))
          yield return descendant;
      }
    }
  }

  IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

  public UQueryEnumerable<T> Where(Predicate<T> predicate)
  {
    var newPredicates = new List<Predicate<T>>(_predicates) { predicate };
    return new UQueryEnumerable<T>(_root, _name, _className, _tagName, newPredicates);
  }

  public UQueryExpression<T> First()
  {
    return new UQueryExpression<T>(this);
  }

  public UQueryExpression<T> Last()
  {
    var list = this.ToList();
    return new UQueryExpression<T>(list.Count > 0 ? new List<T> { list[^1] } : new List<T>());
  }

  public UQueryExpression<T> AtIndex(int index)
  {
    var list = this.ToList();
    return new UQueryExpression<T>(index >= 0 && index < list.Count ? new List<T> { list[index] } : new List<T>());
  }

  public List<T> ToList()
  {
    var result = new List<T>();
    foreach (var item in this)
      result.Add(item);
    return result;
  }

  public T[] ToArray()
  {
    return ToList().ToArray();
  }

  public bool Any()
  {
    return this.GetEnumerator().MoveNext();
  }

  public bool Any(Func<T, bool> predicate)
  {
    return this.Any(t => predicate(t));
  }

  public bool All(Func<T, bool> predicate)
  {
    return !this.Any(t => !predicate(t));
  }

  public int Count()
  {
    return ToList().Count;
  }

  public T Single(Func<T, bool> predicate)
  {
    return this.SingleOrDefault(predicate);
  }

  public T SingleOrDefault(Func<T, bool> predicate)
  {
    T found = default;
    bool hasFound = false;
    foreach (var item in this)
    {
      if (predicate(item))
      {
        if (hasFound)
          throw new InvalidOperationException("Sequence contains more than one matching element");
        found = item;
        hasFound = true;
      }
    }
    return found;
  }

  public T First(Func<T, bool> predicate)
  {
    return this.FirstOrDefault(predicate);
  }

  public T FirstOrDefault(Func<T, bool> predicate)
  {
    foreach (var item in this)
    {
      if (predicate(item))
        return item;
    }
    return default;
  }

  public T Last(Func<T, bool> predicate)
  {
    return this.LastOrDefault(predicate);
  }

  public T LastOrDefault(Func<T, bool> predicate)
  {
    T found = default;
    foreach (var item in this)
    {
      if (predicate(item))
        found = item;
    }
    return found;
  }

  public T ElementAt(int index)
  {
    int i = 0;
    foreach (var item in this)
    {
      if (i == index)
        return item;
      i++;
    }
    throw new ArgumentOutOfRangeException(nameof(index));
  }

  public T ElementAtOrDefault(int index)
  {
    int i = 0;
    foreach (var item in this)
    {
      if (i == index)
        return item;
      i++;
    }
    return default;
  }

  public UQueryExpression<T> Take(int count)
  {
    var list = new List<T>();
    int i = 0;
    foreach (var item in this)
    {
      if (i >= count) break;
      list.Add(item);
      i++;
    }
    return new UQueryExpression<T>(list);
  }

  public UQueryExpression<T> Skip(int count)
  {
    var list = new List<T>();
    int i = 0;
    foreach (var item in this)
    {
      if (i >= count)
        list.Add(item);
      i++;
    }
    return new UQueryExpression<T>(list);
  }

  public void ForEach(Action<T> action)
  {
    foreach (var item in this)
      action(item);
  }

  public bool Matches(VisualElement element)
  {
    if (element is not T typed) return false;
    if (!string.IsNullOrEmpty(_name) && element.name != _name) return false;
    if (!string.IsNullOrEmpty(_className) && !element.ClassListContains(_className)) return false;
    foreach (var predicate in _predicates)
    {
      if (!predicate(typed)) return false;
    }
    return true;
  }

  private static IEnumerable<T> Descendants(VisualElement parent)
  {
    foreach (var child in parent.Children())
    {
      if (child is T typed)
        yield return typed;

      foreach (var descendant in Descendants(child))
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

  internal UQueryExpression(UQueryEnumerable<T> enumerable)
  {
    _results = enumerable.ToList();
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

  public static UQueryExpression<T> Q<T>(this VisualElement e, string name, string className, int index) where T : VisualElement
  {
    var list = e.Query<T>(name, className).ToList();
    return new UQueryExpression<T>(index >= 0 && index < list.Count ? new List<T> { list[index] } : new List<T>());
  }

  public static UQueryExpression<VisualElement> Q(this VisualElement e, string name, string className, int index)
  {
    return e.Q<VisualElement>(name, className, index);
  }

  public static UQueryExpression<T> Q<T>(this VisualElement e, string name, string className, string nthChild) where T : VisualElement
  {
    _ = nthChild;
    return new UQueryExpression<T>(e.Query<T>(name, className).ToList());
  }

  public static void ForEach<T>(this IEnumerable<T> source, Action<T> action)
  {
    if (source is null || action is null) return;
    foreach (var item in source)
      action(item);
  }
}
