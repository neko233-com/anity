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

  public Action? guiHandler;

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

  public void OnActivate(string searchContext, object rootElement)
  {
    _ = searchContext;
    _ = rootElement;
  }

  public void OnGUI()
  {
    guiHandler?.Invoke();
  }

  public void OnDeactivate()
  {
  }

  public void Repaint()
  {
  }
}

public enum SettingsScope
{
  User = 1,
  Project = 2
}
