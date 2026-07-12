using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace UnityEditor.Search;

public enum SearchItemOptions
{
    None = 0,
    AlwaysRefresh = 1,
    Fuzzy = 2
}

public class SearchItem
{
    public string id { get; set; } = string.Empty;
    public string label { get; set; } = string.Empty;
    public string description { get; set; } = string.Empty;
    public Texture2D? thumbnail { get; set; }
    public object? data { get; set; }
    public string providerId { get; set; } = string.Empty;
    public float score { get; set; }
    public Action<SearchItem>? selectAction { get; set; }

    public override string ToString() => label;
}

public class SearchContext
{
    public string searchText { get; set; } = string.Empty;
    public SearchItemOptions options { get; set; } = SearchItemOptions.Fuzzy;
    public int maxResults { get; set; } = 50;
}

public delegate IEnumerable<SearchItem> SearchProviderHandler(SearchContext context);

public class SearchProvider
{
    public string id { get; }
    public string name { get; set; }
    public int priority { get; set; }
    public SearchProviderHandler? fetchItems { get; set; }
    public Func<string, bool>? filter { get; set; }

    public SearchProvider(string id, string name)
    {
        this.id = id;
        this.name = name;
    }
}

/// <summary>
/// Unity Quick Search / SearchService — global Ctrl+K search across assets, hierarchy, menu, settings.
/// </summary>
public static class SearchService
{
    private static readonly List<SearchProvider> _providers = new();
    private static bool _initialized;

    public static event Action<string>? searchOpened;
    public static event Action? searchClosed;

    internal static void RaiseSearchClosed() => searchClosed?.Invoke();

    public static IReadOnlyList<SearchProvider> Providers
    {
        get
        {
            EnsureProviders();
            return _providers;
        }
    }

    public static void RegisterProvider(SearchProvider provider)
    {
        if (provider == null || string.IsNullOrEmpty(provider.id)) return;
        _providers.RemoveAll(p => p.id == provider.id);
        _providers.Add(provider);
        _providers.Sort((a, b) => a.priority.CompareTo(b.priority));
    }

    public static void EnsureProviders()
    {
        if (_initialized) return;
        _initialized = true;
        RegisterDefaultProviders();
    }

    public static IList<SearchItem> Search(string query, int maxResults = 50)
    {
        EnsureProviders();
        var context = new SearchContext
        {
            searchText = query ?? string.Empty,
            maxResults = maxResults,
            options = SearchItemOptions.Fuzzy
        };

        var results = new List<SearchItem>();
        foreach (var provider in _providers)
        {
            if (provider.fetchItems == null) continue;
            try
            {
                foreach (var item in provider.fetchItems(context))
                {
                    if (item == null) continue;
                    item.providerId = provider.id;
                    results.Add(item);
                    if (results.Count >= maxResults * 2) break;
                }
            }
            catch
            {
                // provider failure should not break global search
            }
        }

        return results
            .OrderByDescending(i => i.score)
            .ThenBy(i => i.label, StringComparer.OrdinalIgnoreCase)
            .Take(maxResults)
            .ToList();
    }

    public static void ShowWindow(string? initialQuery = null)
    {
        EnsureProviders();
        searchOpened?.Invoke(initialQuery ?? string.Empty);
        SearchWindow.Show(initialQuery);
    }

    /// <summary>Ctrl+K / Cmd+K entry.</summary>
    public static void OpenQuickSearch() => ShowWindow(null);

    private static void RegisterDefaultProviders()
    {
        RegisterProvider(new SearchProvider("asset", "Project Assets")
        {
            priority = 10,
            fetchItems = ctx => SearchAssets(ctx)
        });
        RegisterProvider(new SearchProvider("scene", "Hierarchy / Scene")
        {
            priority = 20,
            fetchItems = ctx => SearchHierarchy(ctx)
        });
        RegisterProvider(new SearchProvider("menu", "Menu Commands")
        {
            priority = 30,
            fetchItems = ctx => SearchMenus(ctx)
        });
        RegisterProvider(new SearchProvider("settings", "Settings")
        {
            priority = 40,
            fetchItems = ctx => SearchSettings(ctx)
        });
        RegisterProvider(new SearchProvider("window", "Editor Windows")
        {
            priority = 25,
            fetchItems = ctx => SearchWindows(ctx)
        });
    }

    private static IEnumerable<SearchItem> SearchAssets(SearchContext ctx)
    {
        string q = ctx.searchText?.Trim() ?? string.Empty;
        string[] guids = string.IsNullOrEmpty(q)
            ? AssetDatabase.FindAssets(string.Empty)
            : AssetDatabase.FindAssets(q);

        int count = 0;
        foreach (var guid in guids)
        {
            if (count >= ctx.maxResults) yield break;
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path)) continue;
            if (!string.IsNullOrEmpty(q) && path.IndexOf(q, StringComparison.OrdinalIgnoreCase) < 0
                && System.IO.Path.GetFileName(path).IndexOf(q, StringComparison.OrdinalIgnoreCase) < 0)
                continue;

            float score = FuzzyScore(System.IO.Path.GetFileName(path), q);
            yield return new SearchItem
            {
                id = guid,
                label = System.IO.Path.GetFileName(path),
                description = path,
                score = score,
                data = path,
                selectAction = item =>
                {
                    var obj = AssetDatabase.LoadMainAssetAtPath(item.data as string ?? path) as UnityEngine.Object;
                    if (obj != null)
                    {
                        Selection.activeObject = obj;
                        EditorGUIUtility.PingObject(obj);
                    }
                }
            };
            count++;
        }
    }

    private static IEnumerable<SearchItem> SearchHierarchy(SearchContext ctx)
    {
        string q = ctx.searchText?.Trim() ?? string.Empty;
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (!scene.IsValid()) yield break;

        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var item in SearchTransformTree(root.transform, q, ctx.maxResults))
                yield return item;
        }
    }

    private static IEnumerable<SearchItem> SearchTransformTree(Transform t, string q, int max)
    {
        if (t == null) yield break;
        string name = t.gameObject.name;
        if (string.IsNullOrEmpty(q) || name.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0)
        {
            yield return new SearchItem
            {
                id = t.gameObject.GetInstanceID().ToString(),
                label = name,
                description = GetHierarchyPath(t),
                score = FuzzyScore(name, q),
                data = t.gameObject,
                selectAction = item =>
                {
                    if (item.data is GameObject go)
                        Selection.activeGameObject = go;
                }
            };
        }
        for (int i = 0; i < t.childCount; i++)
        {
            foreach (var child in SearchTransformTree(t.GetChild(i), q, max))
                yield return child;
        }
    }

    private static IEnumerable<SearchItem> SearchMenus(SearchContext ctx)
    {
        string q = ctx.searchText?.Trim() ?? string.Empty;
        string[] menus =
        {
            "File/New Scene", "File/Open Scene", "File/Save", "File/Build Settings",
            "Edit/Project Settings", "Edit/Preferences",
            "Assets/Create/Folder", "Assets/Create/Material", "Assets/Create/Prefab",
            "GameObject/Create Empty", "GameObject/3D Object/Cube", "GameObject/Light/Directional Light",
            "Component/Physics/Rigidbody", "Component/UI/Button",
            "Window/General/Scene", "Window/General/Game", "Window/General/Inspector",
            "Window/General/Hierarchy", "Window/General/Project", "Window/General/Console",
            "Window/Search/New Search Window"
        };

        foreach (var m in menus)
        {
            if (!string.IsNullOrEmpty(q) && m.IndexOf(q, StringComparison.OrdinalIgnoreCase) < 0)
                continue;
            yield return new SearchItem
            {
                id = m,
                label = m,
                description = "Menu",
                score = FuzzyScore(m, q),
                data = m
            };
        }
    }

    private static IEnumerable<SearchItem> SearchSettings(SearchContext ctx)
    {
        string q = ctx.searchText?.Trim() ?? string.Empty;
        string[] keys =
        {
            "Player", "Quality", "Graphics", "Physics", "Physics 2D", "Tags and Layers",
            "Audio", "Time", "Input Manager", "Editor", "XR", "Package Manager"
        };
        foreach (var k in keys)
        {
            if (!string.IsNullOrEmpty(q) && k.IndexOf(q, StringComparison.OrdinalIgnoreCase) < 0)
                continue;
            yield return new SearchItem
            {
                id = "settings:" + k,
                label = k,
                description = "Project Settings",
                score = FuzzyScore(k, q),
                data = k
            };
        }
    }

    private static IEnumerable<SearchItem> SearchWindows(SearchContext ctx)
    {
        string q = ctx.searchText?.Trim() ?? string.Empty;
        (string name, Action open)[] windows =
        {
            ("Scene", () => SceneView.ShowWindow()),
            ("Game", () => GameView.ShowWindow()),
            ("Inspector", () => InspectorWindow.ShowWindow()),
            ("Hierarchy", () => HierarchyWindow.ShowWindow()),
            ("Project", () => ProjectWindow.ShowWindow()),
            ("Console", () => ConsoleWindow.ShowWindow()),
            ("Search", () => SearchWindow.Show(null)),
            ("Animation", () => { }),
            ("Profiler", () => { }),
            ("Lighting", () => { })
        };
        foreach (var (name, open) in windows)
        {
            if (!string.IsNullOrEmpty(q) && name.IndexOf(q, StringComparison.OrdinalIgnoreCase) < 0)
                continue;
            yield return new SearchItem
            {
                id = "window:" + name,
                label = name,
                description = "Editor Window",
                score = FuzzyScore(name, q) + 10f,
                selectAction = _ => open()
            };
        }
    }

    public static float FuzzyScore(string text, string query)
    {
        if (string.IsNullOrEmpty(query)) return 1f;
        if (string.IsNullOrEmpty(text)) return 0f;
        if (text.Equals(query, StringComparison.OrdinalIgnoreCase)) return 100f;
        if (text.StartsWith(query, StringComparison.OrdinalIgnoreCase)) return 80f;
        int idx = text.IndexOf(query, StringComparison.OrdinalIgnoreCase);
        if (idx >= 0) return 60f - idx * 0.1f;

        // subsequence fuzzy
        int ti = 0, matches = 0;
        string t = text.ToLowerInvariant();
        string q = query.ToLowerInvariant();
        for (int qi = 0; qi < q.Length && ti < t.Length; qi++)
        {
            while (ti < t.Length && t[ti] != q[qi]) ti++;
            if (ti < t.Length) { matches++; ti++; }
        }
        return matches == q.Length ? 30f * matches / Math.Max(1, t.Length) : 0f;
    }

    private static string GetHierarchyPath(Transform t)
    {
        var parts = new Stack<string>();
        while (t != null)
        {
            parts.Push(t.name);
            t = t.parent;
        }
        return string.Join("/", parts);
    }
}

/// <summary>
/// Quick Search window (Unity Ctrl+K).
/// </summary>
public sealed class SearchWindow : EditorWindow
{
    private string _query = string.Empty;
    private Vector2 _scroll;
    private IList<SearchItem> _results = Array.Empty<SearchItem>();
    private int _selectedIndex;
    private static SearchWindow? _instance;

    public static SearchWindow? instance => _instance;

    public SearchWindow()
    {
        titleContent = new GUIContent("Search");
        minSize = new Vector2(420f, 320f);
        _instance = this;
    }

    public static SearchWindow Show(string? initialQuery)
    {
        var win = GetWindow<SearchWindow>(true, "Search");
        win._query = initialQuery ?? string.Empty;
        win.Refresh();
        win.Focus();
        return win;
    }

    [MenuItem("Window/Search/New Search Window %#k")] // Ctrl+Shift+K fallback
    public static void ShowFromMenu() => Show(null);

    [MenuItem("Edit/Search All... _%k")] // Ctrl+K
    public static void ShowQuickSearch() => SearchService.OpenQuickSearch();

    protected override void OnGUI()
    {
        GUILayout.BeginHorizontal(EditorStyles.toolbar);
        GUILayout.Label("🔍", GUILayout.Width(20f));
        string newQuery = GUILayout.TextField(_query, EditorStyles.toolbarSearchField);
        if (newQuery != _query)
        {
            _query = newQuery;
            _selectedIndex = 0;
            Refresh();
        }
        if (GUILayout.Button("X", EditorStyles.toolbarButton, GUILayout.Width(22f)))
        {
            _query = string.Empty;
            Refresh();
        }
        GUILayout.EndHorizontal();

        GUILayout.Label($"Results ({_results.Count}) — assets, hierarchy, menus, windows", EditorStyles.miniLabel);

        _scroll = GUILayout.BeginScrollView(_scroll);
        for (int i = 0; i < _results.Count; i++)
        {
            var item = _results[i];
            bool selected = i == _selectedIndex;
            var style = selected ? EditorStyles.selectionRect : EditorStyles.label;
            GUILayout.BeginHorizontal(style);
            if (GUILayout.Button($"{item.label}", EditorStyles.label))
            {
                _selectedIndex = i;
                Execute(item);
            }
            GUILayout.FlexibleSpace();
            GUILayout.Label(item.providerId, EditorStyles.miniLabel, GUILayout.Width(70f));
            GUILayout.EndHorizontal();
            if (!string.IsNullOrEmpty(item.description))
                GUILayout.Label("  " + item.description, EditorStyles.miniLabel);
        }
        GUILayout.EndScrollView();

        GUILayout.BeginHorizontal(EditorStyles.toolbar);
        GUILayout.Label("Enter = open · Esc = close · Ctrl+K = Quick Search", EditorStyles.miniLabel);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Open", EditorStyles.toolbarButton, GUILayout.Width(50f)) && _results.Count > 0)
            Execute(_results[Math.Clamp(_selectedIndex, 0, _results.Count - 1)]);
        if (GUILayout.Button("Close", EditorStyles.toolbarButton, GUILayout.Width(50f)))
            Close();
        GUILayout.EndHorizontal();
    }

    private void Refresh()
    {
        _results = SearchService.Search(_query, 80);
    }

    private void Execute(SearchItem item)
    {
        if (item == null) return;
        if (item.selectAction != null)
            item.selectAction(item);
        else if (item.data is string path && path.Contains('/'))
        {
            var obj = AssetDatabase.LoadMainAssetAtPath(path) as UnityEngine.Object;
            if (obj != null) Selection.activeObject = obj;
        }
        Close();
        SearchService.RaiseSearchClosed();
    }
}
