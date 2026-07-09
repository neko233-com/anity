using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Anity.Core.Runtime.HotUpdate;

/// <summary>
/// Manages hot-updatable assembly contexts.
/// This enables runtime code replacement without restarting the application.
/// Note: Full AssemblyLoadContext support requires .NET Core 3.0+.
/// This implementation provides a simplified version for .NET Standard 2.1.
/// </summary>
public sealed class HotUpdateContext : IDisposable
{
  private readonly Dictionary<string, Assembly> _loadedAssemblies = new(StringComparer.OrdinalIgnoreCase);
  private readonly Dictionary<string, byte[]> _assemblyBytes = new(StringComparer.OrdinalIgnoreCase);
  private readonly object _lock = new();
  private bool _disposed;

  /// <summary>
  /// Gets all loaded assembly names.
  /// </summary>
  public IReadOnlyCollection<string> LoadedAssemblies
  {
    get
    {
      lock (_lock)
      {
        return _loadedAssemblies.Keys.ToArray();
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
    if (_disposed) throw new ObjectDisposedException(nameof(HotUpdateContext));
    if (string.IsNullOrWhiteSpace(assemblyName)) throw new ArgumentException("Assembly name cannot be null or empty.", nameof(assemblyName));
    if (assemblyBytes is null) throw new ArgumentNullException(nameof(assemblyBytes));

    lock (_lock)
    {
      if (_loadedAssemblies.ContainsKey(assemblyName))
      {
        throw new InvalidOperationException($"Hot update assembly '{assemblyName}' is already loaded. Unload it first.");
      }

      // Store the assembly bytes for potential reload
      _assemblyBytes[assemblyName] = assemblyBytes;

      // Load the assembly using Assembly.Load
      // Note: This is a simplified implementation. For production use,
      // consider using AssemblyLoadContext on .NET Core 3.0+
      var assembly = Assembly.Load(assemblyBytes, symbolBytes);

      _loadedAssemblies[assemblyName] = assembly;
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

  /// <summary>
  /// Unloads a hot update assembly by name.
  /// </summary>
  /// <param name="assemblyName">The name of the assembly to unload.</param>
  /// <returns>True if the assembly was unloaded; false if it wasn't found.</returns>
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
    if (_disposed) throw new ObjectDisposedException(nameof(HotUpdateContext));

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

  /// <summary>
  /// Gets all loaded assembly names.
  /// </summary>
  public IEnumerable<string> GetLoadedAssemblyNames()
  {
    lock (_lock)
    {
      return _loadedAssemblies.Keys.ToList();
    }
  }

  /// <summary>
  /// Gets the count of loaded assemblies.
  /// </summary>
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

  /// <summary>
  /// Checks if a hot update assembly is loaded.
  /// </summary>
  /// <param name="assemblyName">The name of the assembly to check.</param>
  /// <returns>True if the assembly is loaded.</returns>
  public bool IsLoaded(string assemblyName)
  {
    if (_disposed) throw new ObjectDisposedException(nameof(HotUpdateContext));
    if (string.IsNullOrWhiteSpace(assemblyName)) throw new ArgumentException("Assembly name cannot be null or empty.", nameof(assemblyName));

    lock (_lock)
    {
      return _loadedAssemblies.ContainsKey(assemblyName);
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
    if (_disposed)
    {
      return;
    }

    lock (_lock)
    {
      _loadedAssemblies.Clear();
      _assemblyBytes.Clear();
      _disposed = true;
    }
  }
}
