using System;
using System.Collections.Generic;
using System.IO;

namespace UnityEditor.PackageManager;

public static class Client
{
  private static readonly List<PackageInfo> _localPackages = new()
  {
    new("com.unity.textmeshpro", "3.0.6", PackageSource.Registry, "TextMesh Pro")
  };

  private static readonly Dictionary<string, string> _packagePaths = new();

  public static event Action<PackageInfo[]>? registeredPackagesChanged;
  public static event Action? packagesChanged;

  public static Requests.ListRequest List()
  {
    var request = new Requests.ListRequest();
    request.Result = new Requests.PackageCollection(_localPackages.ToArray());
    request.IsCompleted = true;
    request.Status = Requests.StatusCode.Success;
    return request;
  }

  public static Requests.ListRequest List(bool includeIndirectDependencies)
  {
    _ = includeIndirectDependencies;
    return List();
  }

  public static Requests.ListRequest List(bool includeIndirectDependencies, bool includeNested)
  {
    _ = includeIndirectDependencies;
    _ = includeNested;
    return List();
  }

  public static Requests.ListRequest List(bool includeIndirectDependencies, bool includeNested, bool includeDependencies)
  {
    _ = includeIndirectDependencies;
    _ = includeNested;
    _ = includeDependencies;
    return List();
  }

  public static Requests.SearchRequest Search(string filter, bool offlineMode = false)
  {
    _ = offlineMode;
    var request = new Requests.SearchRequest();
    request.Result = new Requests.PackageCollection(_localPackages.ToArray());
    request.IsCompleted = true;
    request.Status = Requests.StatusCode.Success;
    request.Error = new Requests.Error(0, string.Empty);
    request.SearchText = filter ?? string.Empty;
    return request;
  }

  public static Requests.SearchRequest Search(string filter, int pageSize, int pageNumber)
  {
    _ = pageSize;
    _ = pageNumber;
    var request = new Requests.SearchRequest();
    request.Result = new Requests.PackageCollection(_localPackages.ToArray());
    request.IsCompleted = true;
    request.Status = Requests.StatusCode.Success;
    request.Error = new Requests.Error(0, string.Empty);
    request.SearchText = filter ?? string.Empty;
    return request;
  }

  public static Requests.AddRequest Add(string packageNameOrUrl)
  {
    var request = new Requests.AddRequest
    {
      Result = new PackageInfo(packageNameOrUrl, "1.0.0", PackageSource.Registry, packageNameOrUrl)
    };
    request.IsCompleted = true;
    request.Status = Requests.StatusCode.Success;
    _localPackages.Add(request.Result);
    return request;
  }

  public static Requests.AddRequest Add(string packageNameOrUrl, string version)
  {
    var request = new Requests.AddRequest
    {
      Result = new PackageInfo(packageNameOrUrl, version, PackageSource.Registry, packageNameOrUrl)
    };
    request.IsCompleted = true;
    request.Status = Requests.StatusCode.Success;
    _localPackages.Add(request.Result);
    return request;
  }

  public static Requests.AddRequest AddAndRemove(string packageNameOrUrl, string[]? packagesToRemove = null)
  {
    _ = packagesToRemove;
    return Add(packageNameOrUrl);
  }

  public static Requests.AddRequest AddAndRemove(string packageNameOrUrl, string version, string[]? packagesToRemove = null)
  {
    _ = packagesToRemove;
    return Add(packageNameOrUrl, version);
  }

  public static Requests.RemoveRequest Remove(string packageNameOrUrl)
  {
    var request = new Requests.RemoveRequest();
    request.IsCompleted = true;
    request.Status = Requests.StatusCode.Success;
    _localPackages.RemoveAll(p => p.name == packageNameOrUrl);
    return request;
  }

  public static Requests.EmbedRequest Embed(string packageNameOrUrl)
  {
    var request = new Requests.EmbedRequest
    {
      Result = new PackageInfo(packageNameOrUrl, "1.0.0", PackageSource.Embedded, packageNameOrUrl)
    };
    request.IsCompleted = true;
    request.Status = Requests.StatusCode.Success;
    _localPackages.Add(request.Result);
    return request;
  }

  public static Requests.UpdateRequest Update(string packageNameOrUrl, bool testMode)
  {
    _ = testMode;
    var request = new Requests.UpdateRequest
    {
      Result = new PackageInfo(packageNameOrUrl, "1.0.0", PackageSource.Registry, packageNameOrUrl)
    };
    request.IsCompleted = true;
    request.Status = Requests.StatusCode.Success;
    return request;
  }

  public static Requests.UpdateRequest Update(string packageNameOrUrl, string version)
  {
    var request = new Requests.UpdateRequest
    {
      Result = new PackageInfo(packageNameOrUrl, version, PackageSource.Registry, packageNameOrUrl)
    };
    request.IsCompleted = true;
    request.Status = Requests.StatusCode.Success;
    return request;
  }

  public static Requests.AddRequest Reinstall(string packageNameOrUrl)
  {
    return Add(packageNameOrUrl);
  }

  public static PackageInfo? GetPackageInfo(string packageName)
  {
    return _localPackages.Find(p => p.name == packageName);
  }

  public static PackageInfo[] GetAllPackages()
  {
    return _localPackages.ToArray();
  }

  public static bool IsPackageInstalled(string packageName)
  {
    return _localPackages.Exists(p => p.name == packageName);
  }

  public static string? GetPackagePath(string packageName)
  {
    return _packagePaths.TryGetValue(packageName, out var path) ? path : null;
  }

  public static void SetPackagePath(string packageName, string path)
  {
    _packagePaths[packageName] = path;
  }

  public static Requests.UnityProjectRequest ClearCache()
  {
    return new Requests.UnityProjectRequest { IsCompleted = true, Status = Requests.StatusCode.Success };
  }

  public static void RegisterPackageManagerWindow(object? window)
  {
    _ = window;
  }

  public static Requests.ListRequest ResolveDependencies(string packageName)
  {
    _ = packageName;
    return List();
  }

  public static bool ValidatePackage(string packageName)
  {
    if (string.IsNullOrWhiteSpace(packageName))
    {
      return false;
    }

    return _localPackages.Exists(p => p.name == packageName);
  }

  internal static void InvokeRegisteredPackagesChanged(PackageInfo[] packages)
  {
    registeredPackagesChanged?.Invoke(packages);
  }

  internal static void InvokePackagesChanged()
  {
    packagesChanged?.Invoke();
  }
}

public sealed class PackageInfo
{
  public PackageInfo(string name, string version, PackageSource source, string displayName)
  {
    this.name = name;
    this.version = version;
    this.source = source;
    this.displayName = displayName;
  }

  public string name { get; }
  public string displayName { get; }
  public string version { get; }
  public PackageSource source { get; }
  public string description { get; set; } = string.Empty;
  public bool isDirectDependency { get; set; }
  public bool isExperimental { get; set; }
  public string[]? versions { get; set; }
  public PackageStatus status { get; set; } = PackageStatus.Available;
  public string? repository { get; set; }
  public PackageInfo[]? dependencies { get; set; }
  public string[]? keywords { get; set; }
  public string? license { get; set; }
  public string? author { get; set; }
  public string? documentationUrl { get; set; }
  public string? changelogUrl { get; set; }
  public string? licensesUrl { get; set; }
  public string? path { get; set; }
  public string? depth { get; set; }
  public string? resolvedPath { get; set; }
  public string? sourceGitRevision { get; set; }
  public string? distribution { get; set; }
  public string? registry { get; set; }

  public static PackageInfo? FindForAssetPath(string assetPath)
  {
    _ = assetPath;
    return null;
  }

  public static PackageInfo? FindForAssembly(string assemblyName)
  {
    _ = assemblyName;
    return null;
  }

  public static PackageInfo[] GetAll()
  {
    return Array.Empty<PackageInfo>();
  }

  public static PackageInfo? Find(string packageName)
  {
    return Client.GetPackageInfo(packageName);
  }

  public override string ToString()
  {
    return $"{name}@{version}";
  }
}

public enum PackageSource
{
  Unknown,
  Registry,
  Local,
  Embedded,
  BuiltIn,
  Git,
  File,
  LocalTarball
}

public enum PackageStatus
{
  Unknown,
  Available,
  InDevelopment,
  ReadyToInstall,
  Error
}

public enum ResolvePackageError
{
  None,
  Unknown,
  Unavailable,
  IncompatibleVersion,
  DependencyUnresolvable,
  IncompatibleRequirements,
  IntegrityCheckFailed
}
