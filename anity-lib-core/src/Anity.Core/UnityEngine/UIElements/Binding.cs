using System;
using System.Reflection;

namespace UnityEngine.UIElements;

// Binding system
public abstract class Binding
{
  public string property { get; set; }
  public string path { get; set; }
  public object dataSource { get; set; }
  public Func<object, object> dataSourceGetter { get; set; }
  public Action<object, object> dataSourceSetter { get; set; }
  public BindingStatus status { get; set; }
  public object dataSourcePath { get; set; }

  public void Update()
  {
  }

  public void Update(BindingUpdateTrigger trigger)
  {
    _ = trigger;
  }

  public void MarkDirty()
  {
  }

  public virtual void PreUpdate()
  {
  }

  public virtual void Release()
  {
  }

  public virtual bool IsDirty()
  {
    return false;
  }
}

public enum BindingStatus
{
  Unbound,
  Bound,
  Broken
}

public enum BindingUpdateTrigger
{
  EveryUpdate,
  OnFirstCreate,
  OnHierarchyChange,
  OnInspectorUpdate
}

public static class BindingExtensions
{
  public static IBindingTraits<T> GetOrCreateBindingTraits<T>(this VisualElement element) where T : IBinding, new()
  {
    return new BindingTraits<T>();
  }

  public static void Bind<T>(this VisualElement element, string path, Func<object, object> getter, Action<object, object> setter) where T : class
  {
    _ = element;
    _ = path;
    _ = getter;
    _ = setter;
  }
}

public interface IBindingTraits<T> where T : IBinding
{
  void Update(BindingUpdateTrigger trigger);
}

public class BindingTraits<T> : IBindingTraits<T> where T : IBinding, new()
{
  public void Update(BindingUpdateTrigger trigger)
  {
    _ = trigger;
  }
}

// Typed binding
public abstract class Binding<T> : IBinding
{
  public object dataSource { get; set; }
  public Func<object, T> dataSourceGetter { get; set; }
  public Action<object, T> dataSourceSetter { get; set; }
  public BindingStatus status { get; set; }
  public BindingUpdateTrigger updateTrigger { get; set; }
  public object dataSourcePath { get; set; }

  public string path { get; set; }

  public event Action<T> valueChanged;

  public virtual void PreUpdate()
  {
  }

  public virtual void Update()
  {
    if (dataSource is not null && dataSourceGetter is not null)
    {
      var value = dataSourceGetter(dataSource);
      OnValueChanged(value);
    }
  }

  public virtual void Release()
  {
  }

  public virtual bool IsDirty()
  {
    return false;
  }

  protected virtual void OnValueChanged(T value)
  {
    valueChanged?.Invoke(value);
  }

  protected virtual void OnValueUpdated(T value)
  {
    if (dataSource is not null && dataSourceSetter is not null)
    {
      dataSourceSetter(dataSource, value);
    }
  }
}

// Concrete bindings
public class FloatBinding : Binding<float>
{
}

public class IntBinding : Binding<int>
{
}

public class StringBinding : Binding<string>
{
}

public class BoolBinding : Binding<bool>
{
}

public class ColorBinding : Binding<Color>
{
}

public class ObjectBinding<T> : Binding<T> where T : class
{
}

// Data source
public interface IBindingDataSource
{
  object GetDataSource(string path);
  void SetDataSource(string path, object value);
}

// Binding factory
public static class DataBindingUtility
{
  public static Binding CreateBinding(Type bindingType, string property, object dataSource, string path)
  {
    _ = bindingType;
    _ = property;
    _ = dataSource;
    _ = path;
    return null;
  }

  public static T CreateBinding<T>(string property, object dataSource, string path) where T : Binding, new()
  {
    var binding = new T
    {
      property = property,
      dataSource = dataSource,
      path = path
    };
    return binding;
  }
}
