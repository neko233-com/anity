using System;
using System.Collections;

namespace UnityEngine.UIElements;

public class ListView : VisualElement
{
  public IList itemsSource { get; set; }
  public Func<VisualElement> makeItem { get; set; }
  public Action<VisualElement, int> bindItem { get; set; }
  public int selectedIndex { get; set; } = -1;
  public object selectedItem { get; set; }
  public bool selectionType { get; set; }
  public bool reorderable { get; set; }
  public bool showBorder { get; set; }
  public bool showAlternatingRowBackgrounds { get; set; }
  public bool showFoldoutHeader { get; set; }
  public bool showAddRemoveFooter { get; set; }
  public float fixedItemHeight { get; set; } = -1;
  public AlternatingRowBackground alternatingRowBackground { get; set; }
  public int virtualizationMethod { get; set; }
  public bool horizontalScrolling { get; set; }
  public string reorderableUssClassName { get; set; } = "reorderable";
  public string alternatingRowBackgroundUssClassName { get; set; } = "alternating-row-background";
  public string itemUssClassName { get; set; } = "list-view-item";
  public string itemsHeightExpandedUssClassName { get; set; } = "items-height-expanded";

  public Func<int, float> getHighestEnabledIndex;
  public Func<int, float> getLowestEnabledIndex;

  public event Action<int> selectedIndicesChanged;
  public event Action<IEnumerable<int>> itemsChosen;
  public event Action<int> itemIndexChanged;
  public event Action itemsSourceSizeChanged;
  public event Action itemsSourceChanged;

  public void Rebuild()
  {
    // Stub
  }

  public void RefreshItems()
  {
    // Stub
  }

  public void SetSelection(int index)
  {
    selectedIndex = index;
  }

  public void ClearSelection()
  {
    selectedIndex = -1;
    selectedItem = null;
  }

  public void ScrollToItem(int index)
  {
    // Stub
  }

  public void ReorderItem(int fromIndex, int toIndex)
  {
    // Stub
  }
}

public enum AlternatingRowBackground
{
  None = 0,
  ContentOnly = 1,
  All = 2
}
