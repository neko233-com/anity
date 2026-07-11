using System;
using System.Collections.Generic;

namespace UnityEditor;

public sealed class SettingsProvider
{
  private static readonly Dictionary<string, SettingsProvider> _providers = new(StringComparer.OrdinalIgnoreCase);

  public string path { get; }
  public SettingsScope scopes { get; }
  public string label { get; }
  public IEnumerable<string>? keywords { get; init; }
  public int priority { get; set; }
  public string? providerName { get; set; }

  public Action? guiHandler;
  public Action<string, object>? activateHandler;
  public Action? deactivateHandler;
  public Func<string, bool>? searchHandler;
  public Action? saveAction;
  public Action? loadAction;

  public SettingsProvider(string path, SettingsScope scopes, IEnumerable<string>? keywords = null, Action? guiHandler = null)
  {
    this.path = path;
    this.scopes = scopes;
    this.keywords = keywords;
    this.label = path.Contains('/') ? path[(path.LastIndexOf('/') + 1)..] : path;
    this.guiHandler = guiHandler;
  }

  public static SettingsProvider CreateAndLoadSettingsProvider(string path, SettingsScope scope)
  {
    return new SettingsProvider(path, scope);
  }

  public static SettingsProvider RegisterSettingsProvider(SettingsProvider provider)
  {
    if (provider is null)
    {
      throw new ArgumentNullException(nameof(provider));
    }

    _providers[provider.path] = provider;
    return provider;
  }

  public static void UnregisterSettingsProvider(string path)
  {
    _providers.Remove(path);
  }

  public static bool GetSettingsProvider(string path, out SettingsProvider? provider)
  {
    return _providers.TryGetValue(path, out provider);
  }

  public static SettingsProvider[] GetSettingsProviders()
  {
    var list = new SettingsProvider[_providers.Count];
    _providers.Values.CopyTo(list, 0);
    return list;
  }

  public static SettingsProvider[] GetSettingsProviders(SettingsScope scope)
  {
    var result = new List<SettingsProvider>();
    foreach (var provider in _providers.Values)
    {
      if (provider.scopes.HasFlag(scope))
      {
        result.Add(provider);
      }
    }
    return result.ToArray();
  }

  public static int GetSettingProviderCount()
  {
    return _providers.Count;
  }

  public void OnActivate(string searchContext, object rootElement)
  {
    activateHandler?.Invoke(searchContext, rootElement);
  }

  public void OnGUI()
  {
    guiHandler?.Invoke();
  }

  public void OnDeactivate()
  {
    deactivateHandler?.Invoke();
  }

  public bool Search(string searchContext)
  {
    return searchHandler?.Invoke(searchContext) ?? false;
  }

  public void Repaint()
  {
    Editor.repaintRequested = true;
  }

  public void SaveSettings()
  {
    saveAction?.Invoke();
  }

  public void LoadSettings()
  {
    loadAction?.Invoke();
  }
}

public enum SettingsScope
{
  User = 1,
  Project = 2
}
