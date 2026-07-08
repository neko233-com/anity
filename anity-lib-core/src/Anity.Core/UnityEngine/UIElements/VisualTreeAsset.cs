using System;
using System.Collections.Generic;

namespace UnityEngine.UIElements;

[Serializable]
public class VisualTreeAsset : ScriptableObject
{
  [SerializeField]
  private List<VisualElementAsset> _visualElementAssets = new();

  [SerializeField]
  private List<StyleSheet> _stylesheets = new();

  public IEnumerable<VisualElementAsset> visualElementAssets => _visualElementAssets;
  public IEnumerable<StyleSheet> stylesheets => _stylesheets;
  public bool enableInstancing { get; set; }

  public int GetVisualElementCount()
  {
    return _visualElementAssets.Count;
  }

  public VisualElementAsset? GetVisualElementAsset(int id)
  {
    return _visualElementAssets.Find(a => a.id == id);
  }

  public List<VisualElementAsset> GetVisualElementAssets()
  {
    return new List<VisualElementAsset>(_visualElementAssets);
  }

  public List<StyleSheet> GetStylesheets()
  {
    return new List<StyleSheet>(_stylesheets);
  }

  public VisualElement CloneTree()
  {
    var root = new VisualElement("root");
    foreach (var stylesheet in _stylesheets)
    {
      root.BindStyleSheet(stylesheet);
    }
    return root;
  }

  public VisualElement CloneTree(VisualElement target)
  {
    var root = CloneTree();
    target.Add(root);
    return root;
  }

  public void CloneTree(VisualElement target, List<VisualElementAsset> overrides, Dictionary<string, List<VisualElementAsset>> attributeOverrides)
  {
    var root = CloneTree();
    ApplyOverrides(root, overrides, attributeOverrides);
    target.Add(root);
  }

  public void CloneTree(VisualElement target, int firstElementIndex, List<VisualElementAsset> overrides, Dictionary<string, List<VisualElementAsset>> attributeOverrides)
  {
    var root = CloneTree();
    ApplyOverrides(root, overrides, attributeOverrides);
    target.Add(root);
  }

  public bool TryCloneTree(VisualElement target, out Exception error)
  {
    try
    {
      CloneTree(target);
      error = null;
      return true;
    }
    catch (Exception ex)
    {
      error = ex;
      return false;
    }
  }

  public void Import(VisualElement element)
  {
    _visualElementAssets.Clear();
    ImportElement(element, 0, -1);
  }

  public void ImportTree(VisualElement root)
  {
    _visualElementAssets.Clear();
    ImportElement(root, 0, -1);
  }

  public void ReleaseRenderers()
  {
    foreach (var asset in _visualElementAssets)
    {
      asset.Release();
    }
  }

  public VisualElement Instantiate(VisualElement parent = null)
  {
    var element = CloneTree();
    parent?.Add(element);
    return element;
  }

  public VisualElement Instantiate(InstantiationMode mode)
  {
    return CloneTree();
  }

  public void AddVisualElementAsset(VisualElementAsset asset)
  {
    if (asset is not null)
    {
      _visualElementAssets.Add(asset);
    }
  }

  public void RemoveVisualElementAsset(VisualElementAsset asset)
  {
    _visualElementAssets.Remove(asset);
  }

  public void AddStyleSheet(StyleSheet styleSheet)
  {
    if (styleSheet is not null && !_stylesheets.Contains(styleSheet))
    {
      _stylesheets.Add(styleSheet);
    }
  }

  public void RemoveStyleSheet(StyleSheet styleSheet)
  {
    _stylesheets.Remove(styleSheet);
  }

  public static VisualTreeAsset FromUxml(string uxmlContent)
  {
    _ = uxmlContent;
    return new VisualTreeAsset();
  }

  public string ToUxml()
  {
    return string.Empty;
  }

  private void ApplyOverrides(VisualElement root, List<VisualElementAsset> overrides, Dictionary<string, List<VisualElementAsset>> attributeOverrides)
  {
    _ = root;
    _ = overrides;
    _ = attributeOverrides;
  }

  private int ImportElement(VisualElement element, int index, int parentId)
  {
    var asset = new VisualElementAsset
    {
      id = index,
      name = element.name,
      fullTypeName = element.GetType().FullName,
      orderInDocument = index,
      parentId = parentId
    };
    _visualElementAssets.Add(asset);

    int childIndex = index + 1;
    foreach (var child in element.Children())
    {
      childIndex = ImportElement(child, childIndex, index);
      asset.childIds.Add(childIndex);
    }

    return childIndex;
  }
}

public enum InstantiationMode
{
  Editor,
  Runtime
}

[Serializable]
public class VisualElementAsset
{
  public string name;
  public int id;
  public string fullName;
  public string text;
  public int orderInDocument;
  public int parentId;
  public string fullTypeName;
  public int childrenIdHash;
  public List<int> childIds = new();
  public Dictionary<string, string> attributes = new();
  public List<StyleProperty> styleProperties = new();
  public List<string> classList = new();

  public Type GetInstanceType()
  {
    if (string.IsNullOrEmpty(fullTypeName))
    {
      return typeof(VisualElement);
    }

    return Type.GetType(fullTypeName) ?? typeof(VisualElement);
  }

  public VisualElement Instantiate()
  {
    var type = GetInstanceType();
    var element = (Activator.CreateInstance(type) as VisualElement) ?? new VisualElement();

    if (!string.IsNullOrEmpty(name))
    {
      element.name = name;
    }

    foreach (var cls in classList)
    {
      element.AddClass(cls);
    }

    return element;
  }

  public void Release()
  {
    attributes.Clear();
    classList.Clear();
    styleProperties.Clear();
    childIds.Clear();
  }

  public void SetAttribute(string name, string value)
  {
    attributes[name] = value;
  }

  public string GetAttribute(string name, string defaultValue = "")
  {
    return attributes.TryGetValue(name, out var value) ? value : defaultValue;
  }

  public void AddClass(string className)
  {
    if (!classList.Contains(className))
    {
      classList.Add(className);
    }
  }

  public void RemoveClass(string className)
  {
    classList.Remove(className);
  }
}
