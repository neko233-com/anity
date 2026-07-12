using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace UnityEngine.UIElements;

public class ListView : VisualElement
{
  private readonly List<VisualElement> _visibleItems = new();
  private int _firstVisibleIndex;
  private float _scrollOffset;
  private int _selectedIndex = -1;
  private readonly HashSet<int> _selectedIndices = new();
  private int _anchorIndex = -1;
  private bool _dragging;
  private int _dragFromIndex = -1;
  private Vector2 _dragStartPosition;

  public IList itemsSource { get; set; }
  public Func<VisualElement> makeItem { get; set; }
  public Action<VisualElement, int> bindItem { get; set; }
  public Action<VisualElement, int> unbindItem { get; set; }
  public Action<VisualElement> destroyItem { get; set; }

  public int selectedIndex
  {
    get => _selectedIndex;
    set
    {
      if (_selectedIndex != value)
      {
        _selectedIndex = value;
        selectedItem = itemsSource != null && value >= 0 && value < itemsSource.Count ? itemsSource[value] : null;
        onSelectedIndicesChanged?.Invoke(_selectedIndices);
      }
    }
  }

  public object selectedItem { get; set; }
  public IEnumerable<int> selectedIndices => _selectedIndices;
  public IEnumerable<object> selectedItems => _selectedIndices.Select(i => itemsSource?[i]).Where(o => o != null);
  public SelectionType selectionType { get; set; } = SelectionType.Single;
  public bool reorderable { get; set; }
  public bool showBorder { get; set; }
  public bool showAlternatingRowBackgrounds { get; set; }
  public bool showFoldoutHeader { get; set; }
  public bool showAddRemoveFooter { get; set; }
  public bool showBoundCollectionSize { get; set; }
  public bool horizontalScrollingEnabled { get; set; }
  public float fixedItemHeight { get; set; } = -1;
  public float resolvedItemHeight => fixedItemHeight > 0 ? fixedItemHeight : 24f;
  public AlternatingRowBackground alternatingRowBackground { get; set; }
  public CollectionVirtualizationMethod virtualizationMethod { get; set; } = CollectionVirtualizationMethod.FixedHeight;
  public bool horizontalScrolling { get; set; }
  public int itemCount => itemsSource?.Count ?? 0;
  public ScrollView scrollView { get; private set; }
  public Action<PointerDownEvent> onItemPointerDown { get; set; }

  public string reorderableUssClassName { get; set; } = "unity-list-view__reorderable-item";
  public string alternatingRowBackgroundUssClassName { get; set; } = "unity-list-view__item--alternate";
  public string itemUssClassName { get; set; } = "unity-list-view__item";
  public string itemsHeightExpandedUssClassName { get; set; } = "unity-list-view__items-height-expanded";
  public string ussClassName { get; } = "unity-list-view";
  public string listViewWithoutHeaderUssClassName { get; } = "unity-list-view--without-header";
  public string itemSelectedVariantUssClassName { get; } = "unity-list-view__item--selected";
  public string itemAlternativeBackgroundUssClassName { get; } = "unity-list-view__item--alternative-background";

  public Func<int, float> getHighestEnabledIndex;
  public Func<int, float> getLowestEnabledIndex;

  public event Action<IEnumerable<int>> onItemsChosen;
  public event Action<IEnumerable<object>> onSelectionChange;
  public event Action<IEnumerable<int>> onSelectedIndicesChanged;
  public event Action<IEnumerable<int>> itemsChosen;
  public event Action<IEnumerable<object>> selectionChanged;
  public event Action<int> itemIndexChanged;
  public event Action<IEnumerable<int>> itemsAdded;
  public event Action<IEnumerable<int>> itemsRemoved;
  public event Action itemsSourceSizeChanged;
  public event Action itemsSourceChanged;

  public ListView()
  {
    scrollView = new ScrollView();
    Add(scrollView);
    scrollView.verticalScroller.valueChanged += OnScrollChanged;
    focusable = true;
    RegisterCallback<KeyDownEvent>(OnKeyDown);
  }

  public ListView(IList itemsSource, float itemHeight = -1f, Func<VisualElement> makeItem = null, Action<VisualElement, int> bindItem = null) : this()
  {
    this.itemsSource = itemsSource;
    if (itemHeight > 0) fixedItemHeight = itemHeight;
    this.makeItem = makeItem;
    this.bindItem = bindItem;
    Rebuild();
  }

  public void Setup(IList itemsSource, float itemHeight, Func<VisualElement> makeItem, Action<VisualElement, int> bindItem, Action<VisualElement, int> unbindItem = null, Action<VisualElement> destroyItem = null)
  {
    this.itemsSource = itemsSource;
    fixedItemHeight = itemHeight;
    this.makeItem = makeItem;
    this.bindItem = bindItem;
    this.unbindItem = unbindItem;
    this.destroyItem = destroyItem;
    Rebuild();
  }

  public void SetupDragAndDrop()
  {
  }

  private void OnScrollChanged(float offset)
  {
    _scrollOffset = offset;
    RefreshVisibleItems();
  }

  public void Rebuild()
  {
    ClearItems();
    RefreshItems();
    itemsSourceChanged?.Invoke();
  }

  public void RefreshItems()
  {
    var count = itemsSource?.Count ?? 0;
    float contentHeight = count * resolvedItemHeight;
    scrollView.contentContainer.style.height = contentHeight;

    for (int i = 0; i < _visibleItems.Count; i++)
    {
      var item = _visibleItems[i];
      int dataIndex = _firstVisibleIndex + i;
      if (dataIndex < count && bindItem != null && itemsSource != null)
      {
        bindItem(item, dataIndex);
      }
    }

    RefreshVisibleItems();
    UpdateScrollerRange();
  }

  private void UpdateScrollerRange()
  {
    var count = itemsSource?.Count ?? 0;
    float itemHeight = resolvedItemHeight;
    float viewportHeight = scrollView.contentViewportHeight > 0 ? scrollView.contentViewportHeight : 300;
    float contentHeight = count * itemHeight;
    float maxOffset = Mathf.Max(0, contentHeight - viewportHeight);
    scrollView.verticalScroller.highValue = maxOffset;
  }

  private void RefreshVisibleItems()
  {
    if (itemsSource == null || makeItem == null) return;

    float viewportHeight = scrollView.contentViewportHeight > 0 ? scrollView.contentViewportHeight : 300;
    float itemHeight = resolvedItemHeight;
    int count = itemsSource.Count;

    int newFirstVisible = Mathf.Max(0, Mathf.FloorToInt(_scrollOffset / itemHeight));
    int visibleCount = Mathf.Min(count - newFirstVisible, Mathf.CeilToInt(viewportHeight / itemHeight) + 2);

    if (newFirstVisible != _firstVisibleIndex || _visibleItems.Count != visibleCount)
    {
      _firstVisibleIndex = newFirstVisible;
      UpdateVisibleItems(visibleCount);
    }
    else
    {
      for (int i = 0; i < _visibleItems.Count; i++)
      {
        int dataIndex = _firstVisibleIndex + i;
        if (dataIndex < count)
        {
          _visibleItems[i].style.top = dataIndex * itemHeight;
          _visibleItems[i].SetProperty("data-index", dataIndex);
          UpdateItemSelectionState(_visibleItems[i], dataIndex);
          if (bindItem != null) bindItem(_visibleItems[i], dataIndex);
        }
      }
    }
  }

  private void UpdateVisibleItems(int visibleCount)
  {
    if (itemsSource == null) return;
    float itemHeight = resolvedItemHeight;

    while (_visibleItems.Count > visibleCount)
    {
      int last = _visibleItems.Count - 1;
      var item = _visibleItems[last];
      unbindItem?.Invoke(item, _firstVisibleIndex + last);
      destroyItem?.Invoke(item);
      scrollView.contentContainer.Remove(item);
      _visibleItems.RemoveAt(last);
    }

    while (_visibleItems.Count < visibleCount)
    {
      var item = makeItem();
      if (item == null) break;
      item.style.position = Position.Absolute;
      item.style.left = 0;
      item.style.right = 0;
      item.style.height = itemHeight;
      item.AddClass(itemUssClassName);
      RegisterItemEvents(item);
      scrollView.contentContainer.Add(item);
      _visibleItems.Add(item);
    }

    for (int i = 0; i < _visibleItems.Count; i++)
    {
      int dataIndex = _firstVisibleIndex + i;
      _visibleItems[i].style.top = dataIndex * itemHeight;
      _visibleItems[i].SetProperty("data-index", dataIndex);
      UpdateItemSelectionState(_visibleItems[i], dataIndex);
      if (dataIndex < itemsSource.Count && bindItem != null)
      {
        bindItem(_visibleItems[i], dataIndex);
      }
    }
  }

  private void RegisterItemEvents(VisualElement item)
  {
    item.RegisterCallback<PointerDownEvent>(OnItemPointerDown);
    item.RegisterCallback<PointerUpEvent>(OnItemPointerUp);
    item.RegisterCallback<PointerMoveEvent>(OnItemPointerMove);
    item.RegisterCallback<ClickEvent>(OnItemClick);
  }

  private void OnItemPointerDown(PointerDownEvent evt)
  {
    var item = evt.target as VisualElement ?? (evt.target as VisualElement)?.parent;
    if (item == null) return;
    int index = GetItemIndexFromElement(item);
    if (index < 0) return;

    onItemPointerDown?.Invoke(evt);

    if (evt.clickCount == 2)
    {
      ChooseItem(index);
      return;
    }

    if (reorderable)
    {
      _dragging = true;
      _dragFromIndex = index;
      _dragStartPosition = evt.position;
    }

    if (selectionType == SelectionType.None) return;

    if (evt.ctrlKey || evt.commandKey)
    {
      if (_selectedIndices.Contains(index))
        RemoveFromSelection(index);
      else
        AddToSelection(index);
      _anchorIndex = index;
    }
    else if (evt.shiftKey && _anchorIndex >= 0 && selectionType == SelectionType.Multiple)
    {
      SelectRange(_anchorIndex, index);
    }
    else
    {
      SetSelection(index);
      _anchorIndex = index;
    }
  }

  private void OnItemPointerUp(PointerUpEvent evt)
  {
    if (_dragging && reorderable && _dragFromIndex >= 0)
    {
      var item = evt.target as VisualElement ?? (evt.target as VisualElement)?.parent;
      int toIndex = GetItemIndexFromElement(item);
      if (toIndex >= 0 && toIndex != _dragFromIndex)
      {
        ReorderItem(_dragFromIndex, toIndex);
      }
    }
    _dragging = false;
    _dragFromIndex = -1;
  }

  private void OnItemPointerMove(PointerMoveEvent evt)
  {
    if (!_dragging || !reorderable) return;
  }

  private void OnItemClick(ClickEvent evt)
  {
  }

  private void ChooseItem(int index)
  {
    if (index < 0) return;
    var items = new List<int> { index };
    var objects = new List<object> { itemsSource?[index] }.Where(o => o != null);
    itemsChosen?.Invoke(items);
    onItemsChosen?.Invoke(items);
  }

  private void SelectRange(int from, int to)
  {
    if (from < 0) from = 0;
    if (to < 0) to = 0;
    int start = Mathf.Min(from, to);
    int end = Mathf.Max(from, to);
    _selectedIndices.Clear();
    for (int i = start; i <= end; i++)
    {
      _selectedIndices.Add(i);
    }
    _selectedIndex = to;
    selectedItem = itemsSource?[to];
    onSelectedIndicesChanged?.Invoke(_selectedIndices);
    selectionChanged?.Invoke(selectedItems);
    onSelectionChange?.Invoke(selectedItems);
    RefreshVisibleItems();
  }

  private int GetItemIndexFromElement(VisualElement element)
  {
    while (element != null && element != scrollView.contentContainer)
    {
      var idx = element.GetProperty<int>("data-index", -1);
      if (idx >= 0) return idx;
      element = element.parent;
    }
    return -1;
  }

  private void UpdateItemSelectionState(VisualElement item, int index)
  {
    bool selected = _selectedIndices.Contains(index);
    item.ToggleClass(itemSelectedVariantUssClassName, selected);
    if (showAlternatingRowBackgrounds)
    {
      item.ToggleClass(itemAlternativeBackgroundUssClassName, index % 2 == 1);
    }
  }

  private void ClearItems()
  {
    for (int i = 0; i < _visibleItems.Count; i++)
    {
      unbindItem?.Invoke(_visibleItems[i], _firstVisibleIndex + i);
      destroyItem?.Invoke(_visibleItems[i]);
    }
    scrollView.contentContainer.Clear();
    _visibleItems.Clear();
    _firstVisibleIndex = 0;
    _scrollOffset = 0;
  }

  public void SetSelection(int index)
  {
    selectedIndex = index;
    _selectedIndices.Clear();
    if (index >= 0)
    {
      _selectedIndices.Add(index);
    }
    _anchorIndex = index;
    selectedItem = index >= 0 && itemsSource != null ? itemsSource[index] : null;
    onSelectedIndicesChanged?.Invoke(_selectedIndices);
    selectionChanged?.Invoke(selectedItems);
    onSelectionChange?.Invoke(selectedItems);
    RefreshVisibleItems();
    ScrollToItem(index);
  }

  public void SetSelection(IEnumerable<int> indices)
  {
    _selectedIndices.Clear();
    foreach (var idx in indices)
    {
      if (idx >= 0 && (itemsSource == null || idx < itemsSource.Count))
        _selectedIndices.Add(idx);
    }
    _selectedIndex = _selectedIndices.Count > 0 ? _selectedIndices.First() : -1;
    _anchorIndex = _selectedIndex;
    selectedItem = _selectedIndex >= 0 && itemsSource != null ? itemsSource[_selectedIndex] : null;
    onSelectedIndicesChanged?.Invoke(_selectedIndices);
    selectionChanged?.Invoke(selectedItems);
    onSelectionChange?.Invoke(selectedItems);
    RefreshVisibleItems();
  }

  public void AddToSelection(int index)
  {
    if (selectionType == SelectionType.Single)
    {
      SetSelection(index);
      return;
    }
    if (index >= 0 && (itemsSource == null || index < itemsSource.Count))
    {
      _selectedIndices.Add(index);
      _selectedIndex = index;
      _anchorIndex = index;
      selectedItem = itemsSource?[index];
      onSelectedIndicesChanged?.Invoke(_selectedIndices);
      selectionChanged?.Invoke(selectedItems);
      onSelectionChange?.Invoke(selectedItems);
      RefreshVisibleItems();
    }
  }

  public void RemoveFromSelection(int index)
  {
    _selectedIndices.Remove(index);
    _selectedIndex = _selectedIndices.Count > 0 ? _selectedIndices.First() : -1;
    _anchorIndex = _selectedIndex;
    selectedItem = _selectedIndex >= 0 && itemsSource != null ? itemsSource[_selectedIndex] : null;
    onSelectedIndicesChanged?.Invoke(_selectedIndices);
    selectionChanged?.Invoke(selectedItems);
    onSelectionChange?.Invoke(selectedItems);
    RefreshVisibleItems();
  }

  public void ClearSelection()
  {
    selectedIndex = -1;
    selectedItem = null;
    _anchorIndex = -1;
    _selectedIndices.Clear();
    onSelectedIndicesChanged?.Invoke(_selectedIndices);
    selectionChanged?.Invoke(Enumerable.Empty<object>());
    onSelectionChange?.Invoke(Enumerable.Empty<object>());
    RefreshVisibleItems();
  }

  public void ScrollToItem(int index)
  {
    if (itemsSource == null || index < 0 || index >= itemsSource.Count) return;
    float itemHeight = resolvedItemHeight;
    float viewportHeight = scrollView.contentViewportHeight > 0 ? scrollView.contentViewportHeight : 300;
    float contentHeight = itemsSource.Count * itemHeight;
    float maxOffset = Mathf.Max(0, contentHeight - viewportHeight);
    float itemTop = index * itemHeight;
    float itemBottom = itemTop + itemHeight;

    float targetOffset = _scrollOffset;
    if (itemTop < _scrollOffset)
      targetOffset = itemTop;
    else if (itemBottom > _scrollOffset + viewportHeight)
      targetOffset = itemBottom - viewportHeight;

    _scrollOffset = Mathf.Clamp(targetOffset, 0, maxOffset);
    scrollView.scrollOffset = new Vector2(0, _scrollOffset);
    RefreshVisibleItems();
  }

  public void ScrollToId(int id)
  {
    ScrollToItem(id);
  }

  public void EnsureVisible(int index)
  {
    ScrollToItem(index);
  }

  public void ScrollTo(object item)
  {
    if (itemsSource == null) return;
    for (int i = 0; i < itemsSource.Count; i++)
    {
      if (Equals(itemsSource[i], item))
      {
        ScrollToItem(i);
        return;
      }
    }
  }

  public void ReorderItem(int fromIndex, int toIndex)
  {
    if (itemsSource == null || fromIndex < 0 || fromIndex >= itemsSource.Count ||
        toIndex < 0 || toIndex >= itemsSource.Count || fromIndex == toIndex) return;

    var item = itemsSource[fromIndex];
    itemsSource.RemoveAt(fromIndex);
    itemsSource.Insert(toIndex, item);

    var newSelection = new HashSet<int>();
    foreach (var idx in _selectedIndices)
    {
      int newIdx = idx;
      if (idx == fromIndex) newIdx = toIndex;
      else if (fromIndex < toIndex && idx > fromIndex && idx <= toIndex) newIdx = idx - 1;
      else if (fromIndex > toIndex && idx >= toIndex && idx < fromIndex) newIdx = idx + 1;
      newSelection.Add(newIdx);
    }
    _selectedIndices.Clear();
    foreach (var idx in newSelection) _selectedIndices.Add(idx);
    if (_selectedIndex == fromIndex) _selectedIndex = toIndex;

    RefreshItems();
    itemIndexChanged?.Invoke(toIndex);
  }

  public void ReorderItemInSource(int fromIndex, int toIndex)
  {
    ReorderItem(fromIndex, toIndex);
  }

  public VisualElement GetRootElementForIndex(int index)
  {
    int localIndex = index - _firstVisibleIndex;
    if (localIndex >= 0 && localIndex < _visibleItems.Count) return _visibleItems[localIndex];
    return null;
  }

  public VisualElement GetRootElementForId(int id)
  {
    return GetRootElementForIndex(id);
  }

  public int IndexOf(VisualElement element)
  {
    return GetItemIndexFromElement(element);
  }

  public void AddToItemsSource(object item)
  {
    if (itemsSource == null) return;
    int insertIndex = itemsSource.Count;
    itemsSource.Add(item);
    itemsAdded?.Invoke(new[] { insertIndex });
    itemsSourceSizeChanged?.Invoke();
    RefreshItems();
  }

  public void AddToItemsSource(int index, object item)
  {
    if (itemsSource == null) return;
    itemsSource.Insert(index, item);
    itemsAdded?.Invoke(new[] { index });
    itemsSourceSizeChanged?.Invoke();
    RefreshItems();
  }

  public void RemoveFromItemsSource(object item)
  {
    if (itemsSource == null) return;
    int index = itemsSource.IndexOf(item);
    if (index >= 0)
    {
      itemsSource.Remove(item);
      itemsRemoved?.Invoke(new[] { index });
      itemsSourceSizeChanged?.Invoke();
      RefreshItems();
    }
  }

  public void RemoveFromItemsSource(int index)
  {
    if (itemsSource == null || index < 0 || index >= itemsSource.Count) return;
    itemsSource.RemoveAt(index);
    itemsRemoved?.Invoke(new[] { index });
    itemsSourceSizeChanged?.Invoke();
    RefreshItems();
  }

  private void OnKeyDown(KeyDownEvent evt)
  {
    if (itemsSource == null || itemsSource.Count == 0) return;
    int count = itemsSource.Count;
    int currentIndex = _selectedIndex;

    bool ctrl = evt.ctrlKey || evt.commandKey;
    bool shift = evt.shiftKey;
    float viewportHeight = scrollView.contentViewportHeight > 0 ? scrollView.contentViewportHeight : 300;
    int pageSize = Mathf.Max(1, Mathf.FloorToInt(viewportHeight / resolvedItemHeight) - 1);

    switch (evt.keyCode)
    {
      case KeyCode.UpArrow:
        evt.PreventDefault();
        if (currentIndex > 0)
        {
          int newIndex = currentIndex - 1;
          if (shift && selectionType == SelectionType.Multiple)
            SelectRange(_anchorIndex, newIndex);
          else
            SetSelection(newIndex);
        }
        break;
      case KeyCode.DownArrow:
        evt.PreventDefault();
        if (currentIndex < count - 1)
        {
          int newIndex = currentIndex + 1;
          if (shift && selectionType == SelectionType.Multiple)
            SelectRange(_anchorIndex, newIndex);
          else
            SetSelection(newIndex);
        }
        break;
      case KeyCode.Home:
        evt.PreventDefault();
        if (shift && selectionType == SelectionType.Multiple)
          SelectRange(_anchorIndex, 0);
        else
          SetSelection(0);
        break;
      case KeyCode.End:
        evt.PreventDefault();
        if (shift && selectionType == SelectionType.Multiple)
          SelectRange(_anchorIndex, count - 1);
        else
          SetSelection(count - 1);
        break;
      case KeyCode.PageUp:
        evt.PreventDefault();
        {
          int newIndex = Mathf.Max(0, currentIndex - pageSize);
          if (shift && selectionType == SelectionType.Multiple)
            SelectRange(_anchorIndex, newIndex);
          else
            SetSelection(newIndex);
        }
        break;
      case KeyCode.PageDown:
        evt.PreventDefault();
        {
          int newIndex = Mathf.Min(count - 1, currentIndex + pageSize);
          if (shift && selectionType == SelectionType.Multiple)
            SelectRange(_anchorIndex, newIndex);
          else
            SetSelection(newIndex);
        }
        break;
      case KeyCode.Space:
      case KeyCode.Return:
        evt.PreventDefault();
        if (currentIndex >= 0)
        {
          ChooseItem(currentIndex);
        }
        break;
      case KeyCode.A when ctrl:
        evt.PreventDefault();
        if (selectionType == SelectionType.Multiple)
        {
          _selectedIndices.Clear();
          for (int i = 0; i < count; i++) _selectedIndices.Add(i);
          _selectedIndex = count - 1;
          _anchorIndex = 0;
          selectedItem = itemsSource?[_selectedIndex];
          onSelectedIndicesChanged?.Invoke(_selectedIndices);
          selectionChanged?.Invoke(selectedItems);
          onSelectionChange?.Invoke(selectedItems);
          RefreshVisibleItems();
        }
        break;
    }
  }

  protected override void OnHierarchyChange()
  {
    base.OnHierarchyChange();
    schedule.Execute(() =>
    {
      UpdateScrollerRange();
      RefreshVisibleItems();
    });
  }
}

public enum AlternatingRowBackground
{
  None = 0,
  ContentOnly = 1,
  All = 2
}

public enum CollectionVirtualizationMethod
{
  FixedHeight = 0,
  DynamicHeight = 1
}

public enum SelectionType
{
  None = 0,
  Single = 1,
  Multiple = 2
}
