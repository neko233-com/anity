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

  public VisualElement CloneTree()
  {
    return new VisualElement();
  }

  public VisualElement CloneTree(VisualElement target)
  {
    var root = new VisualElement();
    target.Add(root);
    return root;
  }

  public void CloneTree(VisualElement target, List<VisualElementAsset> overrides, Dictionary<string, List<VisualElementAsset>> attributeOverrides)
  {
    // Stub
  }

  public void CloneTree(VisualElement target, int firstElementIndex, List<VisualElementAsset> overrides, Dictionary<string, List<VisualElementAsset>> attributeOverrides)
  {
    // Stub
  }

  public bool TryCloneTree(VisualElement target, out Exception error)
  {
    error = null;
    target.Add(new VisualElement());
    return true;
  }

  public void Import(VisualElement element)
  {
    // Stub
  }

  public void ImportTree(VisualElement root)
  {
    // Stub
  }

  public void ReleaseRenderers()
  {
    // Stub
  }

  public VisualElement Instantiate(VisualElement parent = null)
  {
    var element = new VisualElement();
    parent?.Add(element);
    return element;
  }
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
  public List<int> childIds;
}
