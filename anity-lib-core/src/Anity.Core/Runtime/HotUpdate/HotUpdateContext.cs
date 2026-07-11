using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;

namespace Anity.Core.Runtime.HotUpdate;

internal sealed class DefaultAssemblyLoadContext : AssemblyLoadContext
{
  public DefaultAssemblyLoadContext(string name, bool isCollectible)
  {
    _name = name;
    _isCollectible = isCollectible;
  }
  private readonly string _name;
  private readonly bool _isCollectible;
  public override string ToString() => _name;
  protected override Assembly Load(AssemblyName assemblyName) => null;
  public void Unload() { }
}

public sealed class HotUpdateContext : IDisposable
{
  private readonly DefaultAssemblyLoadContext _loadContext;
  private readonly Dictionary<string, Assembly> _loadedAssemblies = new(StringComparer.OrdinalIgnoreCase);
  private readonly Dictionary<string, byte[]> _assemblyBytes = new(StringComparer.OrdinalIgnoreCase);
  private readonly object _lock = new();
  private bool _disposed;
  private bool _hotUpdateInProgress;

  public IReadOnlyList<Assembly> Assemblies
  {
    get
    {
      lock (_lock)
      {
        return _loadedAssemblies.Values.ToList().AsReadOnly();
      }
    }
  }

  public bool hotUpdateInProgress
  {
    get => _hotUpdateInProgress;
    private set => _hotUpdateInProgress = value;
  }

  public IReadOnlyCollection<string> hotUpdateAssemblies
  {
    get
    {
      lock (_lock)
      {
        return _loadedAssemblies.Keys.ToArray();
      }
    }
  }

  public HotUpdateContext()
  {
    _loadContext = new DefaultAssemblyLoadContext("HotUpdateContext", isCollectible: true);
    _loadContext.Resolving += OnAssemblyResolving;
  }

  public HotUpdateContext(string name)
  {
    _loadContext = new DefaultAssemblyLoadContext(name, isCollectible: true);
    _loadContext.Resolving += OnAssemblyResolving;
  }

  private Assembly? OnAssemblyResolving(AssemblyLoadContext context, AssemblyName assemblyName)
  {
    lock (_lock)
    {
      foreach (var kvp in _loadedAssemblies)
      {
        if (kvp.Value.GetName().Name == assemblyName.Name)
        {
          return kvp.Value;
        }
      }
    }
    return null;
  }

  public Assembly LoadAssembly(byte[] assemblyBytes)
  {
    if (_disposed) throw new ObjectDisposedException(nameof(HotUpdateContext));
    if (assemblyBytes is null) throw new ArgumentNullException(nameof(assemblyBytes));

    lock (_lock)
    {
      hotUpdateInProgress = true;
      try
      {
        using var ms = new MemoryStream(assemblyBytes);
        var assembly = _loadContext.LoadFromStream(ms);
        var name = assembly.GetName().Name ?? Guid.NewGuid().ToString();
        _loadedAssemblies[name] = assembly;
        _assemblyBytes[name] = assemblyBytes;
        return assembly;
      }
      finally
      {
        hotUpdateInProgress = false;
      }
    }
  }

  public Assembly LoadAssembly(byte[] assemblyBytes, byte[]? symbolBytes)
  {
    if (_disposed) throw new ObjectDisposedException(nameof(HotUpdateContext));
    if (assemblyBytes is null) throw new ArgumentNullException(nameof(assemblyBytes));

    lock (_lock)
    {
      hotUpdateInProgress = true;
      try
      {
        using var asmMs = new MemoryStream(assemblyBytes);
        using var symMs = symbolBytes != null ? new MemoryStream(symbolBytes) : null;
        var assembly = _loadContext.LoadFromStream(asmMs, symMs);
        var name = assembly.GetName().Name ?? Guid.NewGuid().ToString();
        _loadedAssemblies[name] = assembly;
        _assemblyBytes[name] = assemblyBytes;
        return assembly;
      }
      finally
      {
        hotUpdateInProgress = false;
      }
    }
  }

  public Assembly LoadFromStream(Stream assemblyStream)
  {
    if (_disposed) throw new ObjectDisposedException(nameof(HotUpdateContext));
    if (assemblyStream is null) throw new ArgumentNullException(nameof(assemblyStream));

    lock (_lock)
    {
      hotUpdateInProgress = true;
      try
      {
        var assembly = _loadContext.LoadFromStream(assemblyStream);
        var name = assembly.GetName().Name ?? Guid.NewGuid().ToString();
        _loadedAssemblies[name] = assembly;
        return assembly;
      }
      finally
      {
        hotUpdateInProgress = false;
      }
    }
  }

  public Assembly LoadFromStream(Stream assemblyStream, Stream? symbolStream)
  {
    if (_disposed) throw new ObjectDisposedException(nameof(HotUpdateContext));
    if (assemblyStream is null) throw new ArgumentNullException(nameof(assemblyStream));

    lock (_lock)
    {
      hotUpdateInProgress = true;
      try
      {
        var assembly = _loadContext.LoadFromStream(assemblyStream, symbolStream);
        var name = assembly.GetName().Name ?? Guid.NewGuid().ToString();
        _loadedAssemblies[name] = assembly;
        return assembly;
      }
      finally
      {
        hotUpdateInProgress = false;
      }
    }
  }

  public Assembly LoadAssembly(string assemblyName, byte[] assemblyBytes, byte[]? symbolBytes = null)
  {
    if (_disposed) throw new ObjectDisposedException(nameof(HotUpdateContext));
    if (string.IsNullOrWhiteSpace(assemblyName)) throw new ArgumentException("Assembly name cannot be null or empty.", nameof(assemblyName));
    if (assemblyBytes is null) throw new ArgumentNullException(nameof(assemblyBytes));

    lock (_lock)
    {
      if (_loadedAssemblies.ContainsKey(assemblyName))
      {
        throw new InvalidOperationException($"Hot update assembly '{assemblyName}' is already loaded. Unload it first.");
      }

      hotUpdateInProgress = true;
      try
      {
        Assembly assembly;
        if (symbolBytes != null)
        {
          using var asmMs = new MemoryStream(assemblyBytes);
          using var symMs = new MemoryStream(symbolBytes);
          assembly = _loadContext.LoadFromStream(asmMs, symMs);
        }
        else
        {
          using var ms = new MemoryStream(assemblyBytes);
          assembly = _loadContext.LoadFromStream(ms);
        }

        _assemblyBytes[assemblyName] = assemblyBytes;
        _loadedAssemblies[assemblyName] = assembly;
        return assembly;
      }
      finally
      {
        hotUpdateInProgress = false;
      }
    }
  }

  public Assembly LoadAssemblyFromFile(string assemblyName, string assemblyPath, string? symbolPath = null)
  {
    if (_disposed) throw new ObjectDisposedException(nameof(HotUpdateContext));
    if (string.IsNullOrWhiteSpace(assemblyName)) throw new ArgumentException("Assembly name cannot be null or empty.", nameof(assemblyName));
    if (string.IsNullOrWhiteSpace(assemblyPath)) throw new ArgumentException("Assembly path cannot be null or empty.", nameof(assemblyPath));

    if (!File.Exists(assemblyPath))
    {
      throw new FileNotFoundException($"Assembly file not found: {assemblyPath}", assemblyPath);
    }

    var assemblyBytes = File.ReadAllBytes(assemblyPath);
    byte[]? symbolBytes = null;

    if (symbolPath is not null && File.Exists(symbolPath))
    {
      symbolBytes = File.ReadAllBytes(symbolPath);
    }

    return LoadAssembly(assemblyName, assemblyBytes, symbolBytes);
  }

  public object? ExecuteEntryPoint(Type type, string methodName = "Main", params object[] args)
  {
    if (_disposed) throw new ObjectDisposedException(nameof(HotUpdateContext));
    if (type is null) throw new ArgumentNullException(nameof(type));

    var method = type.GetMethod(methodName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
    if (method == null)
    {
      throw new MissingMethodException(type.FullName, methodName);
    }

    return method.Invoke(null, args.Length > 0 ? args : null);
  }

  public object? InvokeMethod(Type type, string methodName, object? instance, params object[] args)
  {
    if (_disposed) throw new ObjectDisposedException(nameof(HotUpdateContext));
    if (type is null) throw new ArgumentNullException(nameof(type));
    if (string.IsNullOrWhiteSpace(methodName)) throw new ArgumentException("Method name cannot be null or empty.", nameof(methodName));

    var method = type.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
    if (method == null)
    {
      throw new MissingMethodException(type.FullName, methodName);
    }

    return method.Invoke(instance, args.Length > 0 ? args : null);
  }

  public object? InvokeMethod(Assembly assembly, string typeName, string methodName, object? instance, params object[] args)
  {
    if (_disposed) throw new ObjectDisposedException(nameof(HotUpdateContext));
    if (assembly is null) throw new ArgumentNullException(nameof(assembly));
    if (string.IsNullOrWhiteSpace(typeName)) throw new ArgumentException("Type name cannot be null or empty.", nameof(typeName));

    var type = assembly.GetType(typeName);
    if (type == null)
    {
      throw new TypeLoadException($"Type '{typeName}' not found in assembly.");
    }

    return InvokeMethod(type, methodName, instance, args);
  }

  public void Unload()
  {
    if (_disposed) return;

    lock (_lock)
    {
      _loadedAssemblies.Clear();
      _assemblyBytes.Clear();
    }

    _loadContext.Unload();
    _disposed = true;
  }

  public bool Unload(string assemblyName)
  {
    if (_disposed) throw new ObjectDisposedException(nameof(HotUpdateContext));
    if (string.IsNullOrWhiteSpace(assemblyName)) throw new ArgumentException("Assembly name cannot be null or empty.", nameof(assemblyName));

    lock (_lock)
    {
      if (!_loadedAssemblies.ContainsKey(assemblyName))
      {
        return false;
      }

      _loadedAssemblies.Remove(assemblyName);
      _assemblyBytes.Remove(assemblyName);
      return true;
    }
  }

  public Assembly ReloadAssembly(string assemblyName, byte[] assemblyBytes, byte[]? symbolBytes = null)
  {
    if (_disposed) throw new ObjectDisposedException(nameof(HotUpdateContext));

    Unload(assemblyName);
    return LoadAssembly(assemblyName, assemblyBytes, symbolBytes);
  }

  public Assembly? GetAssembly(string assemblyName)
  {
    if (_disposed) throw new ObjectDisposedException(nameof(HotUpdateContext));
    if (string.IsNullOrWhiteSpace(assemblyName)) throw new ArgumentException("Assembly name cannot be null or empty.", nameof(assemblyName));

    lock (_lock)
    {
      if (_loadedAssemblies.TryGetValue(assemblyName, out var assembly))
      {
        return assembly;
      }

      return null;
    }
  }

  public IEnumerable<string> GetLoadedAssemblyNames()
  {
    lock (_lock)
    {
      return _loadedAssemblies.Keys.ToList();
    }
  }

  public int Count
  {
    get
    {
      lock (_lock)
      {
        return _loadedAssemblies.Count;
      }
    }
  }

  public bool IsLoaded(string assemblyName)
  {
    if (_disposed) throw new ObjectDisposedException(nameof(HotUpdateContext));
    if (string.IsNullOrWhiteSpace(assemblyName)) throw new ArgumentException("Assembly name cannot be null or empty.", nameof(assemblyName));

    lock (_lock)
    {
      return _loadedAssemblies.ContainsKey(assemblyName);
    }
  }

  public object? CreateInstance(string assemblyName, string typeName, params object[] args)
  {
    if (_disposed) throw new ObjectDisposedException(nameof(HotUpdateContext));

    var assembly = GetAssembly(assemblyName);
    if (assembly is null)
    {
      return null;
    }

    var type = assembly.GetType(typeName);
    if (type is null)
    {
      return null;
    }

    return args.Length > 0
      ? Activator.CreateInstance(type, args)
      : Activator.CreateInstance(type);
  }

  public void Dispose()
  {
    Unload();
  }
}
