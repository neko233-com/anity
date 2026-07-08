using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;

namespace Anity.Core.Runtime.HotUpdate;

/// <summary>
/// Manages hot-updatable assembly contexts using AssemblyLoadContext.
/// This enables runtime code replacement without restarting the application.
/// </summary>
public sealed class HotUpdateContext : IDisposable
{
  private readonly Dictionary<string, ManagedLoadContext> _contexts = new(StringComparer.OrdinalIgnoreCase);
  private readonly object _lock = new();
  private bool _disposed;

  /// <summary>
  /// Gets the number of loaded hot update contexts.
  /// </summary>
  public int Count
  {
    get
    {
      lock (_lock)
      {
        return _contexts.Count;
      }
    }
  }

  /// <summary>
  /// Gets all loaded assembly names.
  /// </summary>
  public IReadOnlyCollection<string> LoadedAssemblies
  {
    get
    {
      lock (_lock)
      {
        return _contexts.Keys.ToArray();
      }
    }
  }

  /// <summary>
  /// Loads an assembly from a byte array for hot update.
  /// </summary>
  /// <param name="assemblyName">Unique name for this hot update assembly.</param>
  /// <param name="assemblyBytes">The assembly DLL bytes.</param>
  /// <param name="symbolBytes">Optional PDB symbol bytes.</param>
  /// <param name="dependencies">Optional additional assemblies to load.</param>
  /// <returns>The loaded assembly.</returns>
  public Assembly LoadAssembly(
    string assemblyName,
    byte[] assemblyBytes,
    byte[]? symbolBytes = null,
    IEnumerable<(string Name, byte[] Bytes)>? dependencies = null)
  {
    ObjectDisposedException.ThrowIf(_disposed, this);
    ArgumentException.ThrowIfNullOrWhiteSpace(assemblyName);
    ArgumentNullException.ThrowIfNull(assemblyBytes);

    lock (_lock)
    {
      if (_contexts.ContainsKey(assemblyName))
      {
        throw new InvalidOperationException($"Hot update assembly '{assemblyName}' is already loaded. Unload it first.");
      }

      var context = new ManagedLoadContext(assemblyName);

      // Load dependencies first
      if (dependencies is not null)
      {
        foreach (var (depName, depBytes) in dependencies)
        {
          context.LoadFromStream(new MemoryStream(depBytes));
        }
      }

      // Load the main assembly
      var assembly = symbolBytes is not null
        ? context.LoadFromStream(new MemoryStream(assemblyBytes), new MemoryStream(symbolBytes))
        : context.LoadFromStream(new MemoryStream(assemblyBytes));

      _contexts[assemblyName] = context;
      return assembly;
    }
  }

  /// <summary>
  /// Loads an assembly from a file path for hot update.
  /// </summary>
  /// <param name="assemblyName">Unique name for this hot update assembly.</param>
  /// <param name="assemblyPath">Path to the assembly DLL file.</param>
  /// <param name="symbolPath">Optional path to the PDB symbol file.</param>
  /// <returns>The loaded assembly.</returns>
  public Assembly LoadAssemblyFromFile(
    string assemblyName,
    string assemblyPath,
    string? symbolPath = null)
  {
    ObjectDisposedException.ThrowIf(_disposed, this);
    ArgumentException.ThrowIfNullOrWhiteSpace(assemblyName);
    ArgumentException.ThrowIfNullOrWhiteSpace(assemblyPath);

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

  /// <summary>
  /// Unloads a hot update assembly by name.
  /// </summary>
  /// <param name="assemblyName">The name of the assembly to unload.</param>
  /// <returns>True if the assembly was unloaded; false if it wasn't found.</returns>
  public bool Unload(string assemblyName)
  {
    ObjectDisposedException.ThrowIf(_disposed, this);
    ArgumentException.ThrowIfNullOrWhiteSpace(assemblyName);

    lock (_lock)
    {
      if (!_contexts.TryGetValue(assemblyName, out var context))
      {
        return false;
      }

      context.Unload();
      _contexts.Remove(assemblyName);
      return true;
    }
  }

  /// <summary>
  /// Reloads a hot update assembly (unload + load).
  /// </summary>
  /// <param name="assemblyName">Unique name for this hot update assembly.</param>
  /// <param name="assemblyBytes">The assembly DLL bytes.</param>
  /// <param name="symbolBytes">Optional PDB symbol bytes.</param>
  /// <returns>The reloaded assembly.</returns>
  public Assembly ReloadAssembly(
    string assemblyName,
    byte[] assemblyBytes,
    byte[]? symbolBytes = null)
  {
    ObjectDisposedException.ThrowIf(_disposed, this);

    Unload(assemblyName);
    return LoadAssembly(assemblyName, assemblyBytes, symbolBytes);
  }

  /// <summary>
  /// Gets a loaded assembly by name.
  /// </summary>
  /// <param name="assemblyName">The name of the assembly to retrieve.</param>
  /// <returns>The loaded assembly, or null if not found.</returns>
  public Assembly? GetAssembly(string assemblyName)
  {
    ObjectDisposedException.ThrowIf(_disposed, this);
    ArgumentException.ThrowIfNullOrWhiteSpace(assemblyName);

    lock (_lock)
    {
      if (_contexts.TryGetValue(assemblyName, out var context))
      {
        return context.Assemblies.FirstOrDefault(a => !a.IsDynamic);
      }

      return null;
    }
  }

  /// <summary>
  /// Checks if a hot update assembly is loaded.
  /// </summary>
  /// <param name="assemblyName">The name of the assembly to check.</param>
  /// <returns>True if the assembly is loaded.</returns>
  public bool IsLoaded(string assemblyName)
  {
    ObjectDisposedException.ThrowIf(_disposed, this);
    ArgumentException.ThrowIfNullOrWhiteSpace(assemblyName);

    lock (_lock)
    {
      return _contexts.ContainsKey(assemblyName);
    }
  }

  /// <summary>
  /// Creates an instance of a type from a hot update assembly.
  /// </summary>
  /// <param name="assemblyName">The name of the assembly.</param>
  /// <param name="typeName">The full name of the type.</param>
  /// <param name="args">Constructor arguments.</param>
  /// <returns>The created instance, or null if not found.</returns>
  public object? CreateInstance(string assemblyName, string typeName, params object[] args)
  {
    ObjectDisposedException.ThrowIf(_disposed, this);

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
    if (_disposed)
    {
      return;
    }

    lock (_lock)
    {
      foreach (var context in _contexts.Values)
      {
        context.Unload();
      }

      _contexts.Clear();
      _disposed = true;
    }
  }
}

/// <summary>
/// Custom AssemblyLoadContext for managing hot update assemblies.
/// </summary>
internal sealed class ManagedLoadContext : AssemblyLoadContext
{
  private readonly string _name;

  public ManagedLoadContext(string name)
    : base(name, isCollectible: true)
  {
    _name = name;
  }

  protected override Assembly? Load(AssemblyName assemblyName)
  {
    // Return null to use the default context for framework assemblies
    return null;
  }

  public override string ToString()
  {
    return $"HotUpdateContext({_name})";
  }
}
