using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace UnityEngine.UIElements;

public class TreeView : VisualElement
{
  private IList _treeData;
  private readonly List<int> _expandedIds = new();
  private int _selectedId = -1;
  private readonly HashSet<int> _selectedIds = new();
  private readonly List<int> _visibleIds = new();
  private readonly Dictionary<int, int> _idToIndex = new();
  private readonly Dictionary<int, object> _idToItem = new();
  private readonly Dictionary<int, int> _idToDepth = new();
  private readonly Dictionary<int, VisualElement> _idToElement = new();
  private int _anchorId = -1;

  public IList itemsSource { get; set; }
  public Func<VisualElement> makeItem { get; set; }
  public Action<VisualElement, int> bindItem { get; set; }
  public Action<VisualElement, int> unbindItem { get; set; }
  public Action<VisualElement> destroyItem { get; set; }

  public int selectedIndex
  {
    get => _selectedId >= 0 ? GetIndexForId(_selectedId) : -1;
    set
    {
      if (value >= 0 && value < _visibleIds.Count)
        SetSelection(_visibleIds[value]);
      else
        ClearSelection();
    }
  }

  public int selectedId
  {
    get => _selectedId;
    set
    {
      if (_selectedId != value)
      {
        _selectedId = value;
        selectedIdsChanged?.Invoke(new[] { value });
      }
    }
  }

  public object selectedItem
  {
    get => _selectedId >= 0 && _idToItem.TryGetValue(_selectedId, out var item) ? item : null;
    set
    {
      if (value == null)
      {
        ClearSelection();
        return;
      }
      foreach (var kvp in _idToItem)
      {
        if (Equals(kvp.Value, value))
        {
          SetSelection(kvp.Key);
          return;
        }
      }
    }
  }

  public IEnumerable<int> selectedIds => _selectedIds;
  public IEnumerable<object> selectedItems => _selectedIds.Where(id => _idToItem.ContainsKey(id)).Select(id => _idToItem[id]);
  public bool autoExpand { get; set; }
  public bool showAlternatingRowBackgrounds { get; set; }
  public bool showBorder { get; set; }
  public float fixedItemHeight { get; set; } = -1;
  public float resolvedItemHeight => fixedItemHeight > 0 ? fixedItemHeight : 24f;
  public ScrollView scrollView { get; private set; }
  public bool reorderable { get; set; }
  public SelectionType selectionType { get; set; } = SelectionType.Single;
  public int itemCount => _visibleIds.Count;

  public string itemUssClassName { get; } = "unity-tree-view__item";
  public string itemToggleUssClassName { get; } = "unity-tree-view__item-toggle";
  public string itemIndentsContainerUssClassName { get; } = "unity-tree-view__item-indents";
  public string itemContentContainerUssClassName { get; } = "unity-tree-view__item-content";
  public string itemSelectedVariantUssClassName { get; } = "unity-tree-view__item--selected";

  public event Action<IEnumerable<int>> selectedIdsChanged;
  public event Action<IEnumerable<object>> selectionChanged;
  public event Action<IEnumerable<int>> itemsChosen;
  public event Action<int> itemExpanded;
  public event Action<int> itemCollapsed;
  public event Action itemsSourceChanged;

  public TreeView()
  {
    scrollView = new ScrollView();
    Add(scrollView);
    scrollView.verticalScroller.valueChanged += OnScrollChanged;
    focusable = true;
    RegisterCallback<KeyDownEvent>(OnKeyDown);
  }

  public TreeView(IList itemsSource, float itemHeight = -1f, Func<VisualElement> makeItem = null, Action<VisualElement, int> bindItem = null) : this()
  {
    this.itemsSource = itemsSource;
    if (itemHeight > 0) fixedItemHeight = itemHeight;
    this.makeItem = makeItem;
    this.bindItem = bindItem;
    Rebuild();
  }

  private void OnScrollChanged(float offset)
  {
  }

  public void Rebuild()
  {
    RefreshItems();
    itemsSourceChanged?.Invoke();
  }

  public void RefreshItems()
  {
    _visibleIds.Clear();
    _idToIndex.Clear();
    _idToItem.Clear();
    _idToDepth.Clear();
    scrollView.contentContainer.Clear();
    foreach (var element in _idToElement.Values)
    {
      destroyItem?.Invoke(element);
    }
    _idToElement.Clear();

    if (itemsSource == null)
    {
      UpdateContentHeight();
      return;
    }

    if (autoExpand)
    {
      CollectAllIds(itemsSource);
      foreach (var id in _idToItem.Keys.ToList())
      {
        if (!_expandedIds.Contains(id) && ItemHasChildren(_idToItem[id]))
          _expandedIds.Add(id);
      }
    }

    BuildFlatList(itemsSource, 0);
    RenderVisibleItems();
    UpdateContentHeight();
    UpdateScrollerRange();
  }

  private void CollectAllIds(IList items)
  {
    if (items == null) return;
    foreach (var item in items)
    {
      int id = GetItemId(item);
      if (id >= 0 && !_idToItem.ContainsKey(id))
      {
        _idToItem[id] = item;
        var children = GetItemChildren(item);
        if (children != null)
          CollectAllIds(children);
      }
    }
  }

  private void BuildFlatList(IList items, int depth)
  {
    if (items == null) return;
    foreach (var item in items)
    {
      int id = GetItemId(item);
      if (id < 0) continue;
      _idToItem[id] = item;
      _idToDepth[id] = depth;
      _visibleIds.Add(id);
      _idToIndex[id] = _visibleIds.Count - 1;

      bool hasChildren = ItemHasChildren(item);
      if (hasChildren && IsExpanded(id))
      {
        var children = GetItemChildren(item);
        if (children != null)
          BuildFlatList(children, depth + 1);
      }
    }
  }

  private void RenderVisibleItems()
  {
    float itemHeight = resolvedItemHeight;
    for (int i = 0; i < _visibleIds.Count; i++)
    {
      int id = _visibleIds[i];
      var item = _idToItem[id];
      int depth = _idToDepth.TryGetValue(id, out var d) ? d : 0;
      bool hasChildren = ItemHasChildren(item);
      bool isExpanded = IsExpanded(id);
      var element = CreateItemElement(item, id, depth, hasChildren, isExpanded);
      element.style.top = i * itemHeight;
      element.SetProperty("data-id", id);
      element.SetProperty("data-index", i);
      UpdateItemSelectionState(element, id);
      scrollView.contentContainer.Add(element);
      _idToElement[id] = element;
    }
  }

  private void UpdateContentHeight()
  {
    float contentHeight = _visibleIds.Count * resolvedItemHeight;
    scrollView.contentContainer.style.height = contentHeight;
  }

  private void UpdateScrollerRange()
  {
    float itemHeight = resolvedItemHeight;
    float viewportHeight = scrollView.contentViewportHeight > 0 ? scrollView.contentViewportHeight : 300;
    float contentHeight = _visibleIds.Count * itemHeight;
    float maxOffset = Mathf.Max(0, contentHeight - viewportHeight);
    scrollView.verticalScroller.highValue = maxOffset;
  }

  private void BuildTree(IList items, int depth)
  {
    BuildFlatList(items, depth);
    RenderVisibleItems();
    UpdateContentHeight();
  }

  private int GetItemId(object item)
  {
    if (item == null) return -1;
    if (item is TreeViewItemData tvid) return tvid.id;
    var idProp = item.GetType().GetProperty("id");
    if (idProp != null)
    {
      var val = idProp.GetValue(item);
      if (val is int intId) return intId;
    }
    return item.GetHashCode();
  }

  private bool ItemHasChildren(object item)
  {
    if (item == null) return false;
    if (item is TreeViewItemData tvid) return tvid.hasChildren;
    var type = item.GetType();
    if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(TreeViewItemData<>))
    {
      var hasChildrenProp = type.GetProperty("hasChildren");
      if (hasChildrenProp != null && hasChildrenProp.GetValue(item) is bool has) return has;
    }
    var prop = type.GetProperty("hasChildren");
    if (prop != null && prop.GetValue(item) is bool hasBool) return hasBool;
    var childrenProp = type.GetProperty("children");
    if (childrenProp != null)
    {
      var childrenVal = childrenProp.GetValue(item);
      if (childrenVal is IList list) return list.Count > 0;
      if (childrenVal is IEnumerable enumerable) return enumerable.Cast<object>().Any();
    }
    return false;
  }

  private IList GetItemChildren(object item)
  {
    if (item == null) return null;
    if (item is TreeViewItemData tvid) return tvid.children;
    var type = item.GetType();
    var prop = type.GetProperty("children");
    if (prop == null) return null;
    var val = prop.GetValue(item);
    if (val is IList list) return list;
    if (val != null)
    {
      var wrapperType = typeof(GenericListWrapper<>).MakeGenericType(val.GetType().GetGenericArguments()[0]);
      return (IList)Activator.CreateInstance(wrapperType, val);
    }
    return null;
  }

  private class GenericListWrapper<T> : IList
  {
    private readonly IList<T> _inner;
    public GenericListWrapper(IList<T> inner) { _inner = inner; }
    public int Count => _inner.Count;
    public bool IsSynchronized => false;
    public object SyncRoot => this;
    public bool IsFixedSize => false;
    public bool IsReadOnly => false;
    public object this[int index]
    {
      get => _inner[index];
      set => _inner[index] = (T)value;
    }
    public int Add(object value) { _inner.Add((T)value); return _inner.Count - 1; }
    public void Clear() { _inner.Clear(); }
    public bool Contains(object value) => _inner.Contains((T)value);
    public int IndexOf(object value) => _inner.IndexOf((T)value);
    public void Insert(int index, object value) { _inner.Insert(index, (T)value); }
    public void Remove(object value) { _inner.Remove((T)value); }
    public void RemoveAt(int index) { _inner.RemoveAt(index); }
    public void CopyTo(Array array, int index)
    {
      for (int i = 0; i < _inner.Count; i++) array.SetValue(_inner[i], index + i);
    }
    public IEnumerator GetEnumerator() => _inner.GetEnumerator();
  }

  private VisualElement CreateItemElement(object dataItem, int id, int depth, bool hasChildren, bool expanded)
  {
    var container = new VisualElement();
    container.style.flexDirection = FlexDirection.Row;
    container.style.position = Position.Absolute;
    container.style.left = 0;
    container.style.right = 0;
    container.style.height = resolvedItemHeight;
    container.style.paddingLeft = depth * 15f;
    container.AddClass(itemUssClassName);

    if (hasChildren)
    {
      var toggle = new Button();
      toggle.text = expanded ? "▼" : "▶";
      toggle.style.width = 16;
      toggle.style.height = 16;
      toggle.AddClass(itemToggleUssClassName);
      int toggleId = id;
      toggle.clicked += () => ToggleExpanded(toggleId);
      container.Add(toggle);
    }
    else
    {
      var spacer = new VisualElement();
      spacer.style.width = 16;
      container.Add(spacer);
    }

    container.RegisterCallback<PointerDownEvent>(OnItemPointerDown);

    if (makeItem != null)
    {
      var content = makeItem();
      content.AddClass(itemContentContainerUssClassName);
      bindItem?.Invoke(content, id);
      container.Add(content);
    }
    return container;
  }

  private void OnItemPointerDown(PointerDownEvent evt)
  {
    var element = evt.target as VisualElement ?? (evt.target as VisualElement)?.parent;
    while (element != null && element != scrollView.contentContainer)
    {
      var id = element.GetProperty<int>("data-id", -1);
      if (id >= 0)
      {
        HandleItemClick(id, evt);
        return;
      }
      element = element.parent;
    }
  }

  private void HandleItemClick(int id, PointerDownEvent evt)
  {
    if (selectionType == SelectionType.None) return;

    if (evt.clickCount == 2)
    {
      ChooseItem(id);
      if (ItemHasChildren(_idToItem.TryGetValue(id, out var item) ? item : null))
        ToggleExpanded(id);
      return;
    }

    if (evt.ctrlKey || evt.commandKey)
    {
      if (_selectedIds.Contains(id))
        RemoveFromSelection(id);
      else
        AddToSelection(id);
      _anchorId = id;
    }
    else if (evt.shiftKey && _anchorId >= 0 && selectionType == SelectionType.Multiple)
    {
      SelectRange(_anchorId, id);
    }
    else
    {
      SetSelection(id);
      _anchorId = id;
    }
  }

  private void ChooseItem(int id)
  {
    if (id < 0) return;
    var ids = new List<int> { id };
    var objects = new List<object>();
    if (_idToItem.TryGetValue(id, out var item) && item != null)
      objects.Add(item);
    itemsChosen?.Invoke(ids);
  }

  private void SelectRange(int fromId, int toId)
  {
    int fromIndex = GetIndexForId(fromId);
    int toIndex = GetIndexForId(toId);
    if (fromIndex < 0 || toIndex < 0) return;

    int start = Mathf.Min(fromIndex, toIndex);
    int end = Mathf.Max(fromIndex, toIndex);
    _selectedIds.Clear();
    for (int i = start; i <= end; i++)
    {
      _selectedIds.Add(_visibleIds[i]);
    }
    _selectedId = toId;
    UpdateAllItemSelectionStates();
    selectedIdsChanged?.Invoke(_selectedIds);
    selectionChanged?.Invoke(selectedItems);
  }

  private void UpdateAllItemSelectionStates()
  {
    foreach (var id in _visibleIds)
    {
      if (_idToElement.TryGetValue(id, out var element))
        UpdateItemSelectionState(element, id);
    }
  }

  private void UpdateItemSelectionState(VisualElement element, int id)
  {
    bool selected = _selectedIds.Contains(id);
    element.ToggleClass(itemSelectedVariantUssClassName, selected);
  }

  public bool IsExpanded(int id) => _expandedIds.Contains(id);

  public void ExpandItem(int id)
  {
    if (!_expandedIds.Contains(id))
    {
      _expandedIds.Add(id);
      itemExpanded?.Invoke(id);
      RefreshItems();
    }
  }

  public void CollapseItem(int id)
  {
    if (_expandedIds.Remove(id))
    {
      itemCollapsed?.Invoke(id);
      RefreshItems();
    }
  }

  public void ToggleExpanded(int id)
  {
    if (IsExpanded(id)) CollapseItem(id);
    else ExpandItem(id);
  }

  public void ExpandAll()
  {
    CollectAllIds(itemsSource);
    _expandedIds.Clear();
    foreach (var kvp in _idToItem)
    {
      if (ItemHasChildren(kvp.Value))
        _expandedIds.Add(kvp.Key);
    }
    RefreshItems();
  }

  public void CollapseAll()
  {
    _expandedIds.Clear();
    RefreshItems();
  }

  public void SetSelection(int id)
  {
    _selectedId = id;
    _selectedIds.Clear();
    if (id >= 0 && _idToItem.ContainsKey(id)) _selectedIds.Add(id);
    _anchorId = id;
    UpdateAllItemSelectionStates();
    selectedIdsChanged?.Invoke(_selectedIds);
    selectionChanged?.Invoke(selectedItems);
    ScrollToItem(id);
  }

  public void SetSelection(IEnumerable<int> ids)
  {
    _selectedIds.Clear();
    foreach (var id in ids)
    {
      if (id >= 0 && _idToItem.ContainsKey(id))
        _selectedIds.Add(id);
    }
    _selectedId = _selectedIds.Count > 0 ? _selectedIds.First() : -1;
    _anchorId = _selectedId;
    UpdateAllItemSelectionStates();
    selectedIdsChanged?.Invoke(_selectedIds);
    selectionChanged?.Invoke(selectedItems);
  }

  public void AddToSelection(int id)
  {
    if (selectionType == SelectionType.Single)
    {
      SetSelection(id);
      return;
    }
    if (id >= 0 && _idToItem.ContainsKey(id))
    {
      _selectedIds.Add(id);
      _selectedId = id;
      _anchorId = id;
      if (_idToElement.TryGetValue(id, out var element))
        UpdateItemSelectionState(element, id);
      selectedIdsChanged?.Invoke(_selectedIds);
      selectionChanged?.Invoke(selectedItems);
    }
  }

  public void RemoveFromSelection(int id)
  {
    _selectedIds.Remove(id);
    _selectedId = _selectedIds.Count > 0 ? _selectedIds.First() : -1;
    _anchorId = _selectedId;
    if (_idToElement.TryGetValue(id, out var element))
      UpdateItemSelectionState(element, id);
    selectedIdsChanged?.Invoke(_selectedIds);
    selectionChanged?.Invoke(selectedItems);
  }

  public void ClearSelection()
  {
    _selectedId = -1;
    _anchorId = -1;
    _selectedIds.Clear();
    UpdateAllItemSelectionStates();
    selectedIdsChanged?.Invoke(Enumerable.Empty<int>());
    selectionChanged?.Invoke(Enumerable.Empty<object>());
  }

  public void ScrollToItem(int id)
  {
    if (!_idToIndex.TryGetValue(id, out int index)) return;
    float itemHeight = resolvedItemHeight;
    float viewportHeight = scrollView.contentViewportHeight > 0 ? scrollView.contentViewportHeight : 300;
    float contentHeight = _visibleIds.Count * itemHeight;
    float maxOffset = Mathf.Max(0, contentHeight - viewportHeight);
    float itemTop = index * itemHeight;
    float itemBottom = itemTop + itemHeight;

    float targetOffset = scrollView.scrollOffset.y;
    if (itemTop < targetOffset)
      targetOffset = itemTop;
    else if (itemBottom > targetOffset + viewportHeight)
      targetOffset = itemBottom - viewportHeight;

    targetOffset = Mathf.Clamp(targetOffset, 0, maxOffset);
    scrollView.scrollOffset = new Vector2(0, targetOffset);
  }

  public void ScrollToId(int id)
  {
    ScrollToItem(id);
  }

  public void EnsureVisible(int id)
  {
    if (!_idToItem.ContainsKey(id)) return;
    EnsureParentsExpanded(id);
    ScrollToItem(id);
  }

  private void EnsureParentsExpanded(int id)
  {
    int parentId = GetParentId(id);
    while (parentId >= 0)
    {
      if (!_expandedIds.Contains(parentId))
        _expandedIds.Add(parentId);
      parentId = GetParentId(parentId);
    }
    if (!_idToIndex.ContainsKey(id))
      RefreshItems();
  }

  private int GetParentId(int id)
  {
    if (itemsSource == null) return -1;
    return FindParentInList(itemsSource, id, -1);
  }

  private int FindParentInList(IList items, int targetId, int currentParentId)
  {
    if (items == null) return -1;
    foreach (var item in items)
    {
      int itemId = GetItemId(item);
      if (itemId == targetId) return currentParentId;
      var children = GetItemChildren(item);
      if (children != null)
      {
        var found = FindParentInList(children, targetId, itemId);
        if (found >= 0 || (found == -1 && children.Cast<object>().Any(c => GetItemId(c) == targetId)))
        {
          if (found >= 0) return found;
          return itemId;
        }
      }
    }
    return -1;
  }

  public IEnumerable<int> GetItemIdsInRange(int firstIndex, int count)
  {
    var result = new List<int>();
    for (int i = firstIndex; i < firstIndex + count && i < _visibleIds.Count; i++)
    {
      if (i >= 0) result.Add(_visibleIds[i]);
    }
    return result;
  }

  public int GetIdForIndex(int index)
  {
    if (index >= 0 && index < _visibleIds.Count)
      return _visibleIds[index];
    return -1;
  }

  public int GetIndexForId(int id)
  {
    if (_idToIndex.TryGetValue(id, out int index))
      return index;
    return -1;
  }

  public VisualElement GetRootElementForId(int id)
  {
    return _idToElement.TryGetValue(id, out var element) ? element : null;
  }

  public VisualElement GetRootElementForIndex(int index)
  {
    int id = GetIdForIndex(index);
    return GetRootElementForId(id);
  }

  private void OnKeyDown(KeyDownEvent evt)
  {
    if (_visibleIds.Count == 0) return;
    int currentId = _selectedId;
    int currentIndex = currentId >= 0 ? GetIndexForId(currentId) : -1;
    if (currentIndex < 0 && _visibleIds.Count > 0)
    {
      SetSelection(_visibleIds[0]);
      return;
    }
    if (currentIndex < 0) return;

    bool shift = evt.shiftKey;
    bool ctrl = evt.ctrlKey || evt.commandKey;
    float viewportHeight = scrollView.contentViewportHeight > 0 ? scrollView.contentViewportHeight : 300;
    int pageSize = Mathf.Max(1, Mathf.FloorToInt(viewportHeight / resolvedItemHeight) - 1);

    switch (evt.keyCode)
    {
      case KeyCode.UpArrow:
        evt.PreventDefault();
        if (currentIndex > 0)
        {
          int newIndex = currentIndex - 1;
          int newId = _visibleIds[newIndex];
          if (shift && selectionType == SelectionType.Multiple)
            SelectRange(_anchorId, newId);
          else
            SetSelection(newId);
        }
        break;
      case KeyCode.DownArrow:
        evt.PreventDefault();
        if (currentIndex < _visibleIds.Count - 1)
        {
          int newIndex = currentIndex + 1;
          int newId = _visibleIds[newIndex];
          if (shift && selectionType == SelectionType.Multiple)
            SelectRange(_anchorId, newId);
          else
            SetSelection(newId);
        }
        break;
      case KeyCode.LeftArrow:
        evt.PreventDefault();
        {
          if (ItemHasChildren(_idToItem.TryGetValue(currentId, out var item) ? item : null) && IsExpanded(currentId))
            CollapseItem(currentId);
          else
          {
            int parentId = GetParentId(currentId);
            if (parentId >= 0)
              SetSelection(parentId);
          }
        }
        break;
      case KeyCode.RightArrow:
        evt.PreventDefault();
        {
          var currentItem = _idToItem.TryGetValue(currentId, out var ci) ? ci : null;
          if (ItemHasChildren(currentItem))
          {
            if (!IsExpanded(currentId))
              ExpandItem(currentId);
            else if (currentIndex < _visibleIds.Count - 1)
              SetSelection(_visibleIds[currentIndex + 1]);
          }
        }
        break;
      case KeyCode.Home:
        evt.PreventDefault();
        if (shift && selectionType == SelectionType.Multiple)
          SelectRange(_anchorId, _visibleIds[0]);
        else
          SetSelection(_visibleIds[0]);
        break;
      case KeyCode.End:
        evt.PreventDefault();
        int lastId = _visibleIds[^1];
        if (shift && selectionType == SelectionType.Multiple)
          SelectRange(_anchorId, lastId);
        else
          SetSelection(lastId);
        break;
      case KeyCode.PageUp:
        evt.PreventDefault();
        {
          int newIndex = Mathf.Max(0, currentIndex - pageSize);
          int newId = _visibleIds[newIndex];
          if (shift && selectionType == SelectionType.Multiple)
            SelectRange(_anchorId, newId);
          else
            SetSelection(newId);
        }
        break;
      case KeyCode.PageDown:
        evt.PreventDefault();
        {
          int newIndex = Mathf.Min(_visibleIds.Count - 1, currentIndex + pageSize);
          int newId = _visibleIds[newIndex];
          if (shift && selectionType == SelectionType.Multiple)
            SelectRange(_anchorId, newId);
          else
            SetSelection(newId);
        }
        break;
      case KeyCode.Space:
      case KeyCode.Return:
        evt.PreventDefault();
        ChooseItem(currentId);
        break;
      case KeyCode.A when ctrl:
        evt.PreventDefault();
        if (selectionType == SelectionType.Multiple)
        {
          _selectedIds.Clear();
          foreach (var id in _visibleIds) _selectedIds.Add(id);
          _selectedId = _visibleIds[^1];
          _anchorId = _visibleIds[0];
          UpdateAllItemSelectionStates();
          selectedIdsChanged?.Invoke(_selectedIds);
          selectionChanged?.Invoke(selectedItems);
        }
        break;
    }
  }

  protected override void OnHierarchyChange()
  {
    base.OnHierarchyChange();
    schedule.Execute(() =>
    {
      UpdateContentHeight();
      UpdateScrollerRange();
    });
  }
}

public struct TreeViewItemData
{
  public int id { get; }
  public object data { get; }
  public IList children { get; internal set; }
  public bool hasChildren => children != null && children.Count > 0;

  public TreeViewItemData(int id, object data, IList children = null)
  {
    this.id = id;
    this.data = data;
    this.children = children;
  }
}

public struct TreeViewItemData<T>
{
  public int id { get; }
  public T data { get; }
  public IList<TreeViewItemData<T>> children { get; internal set; }
  public bool hasChildren => children != null && children.Count > 0;

  public TreeViewItemData(int id, T data, IList<TreeViewItemData<T>> children = null)
  {
    this.id = id;
    this.data = data;
    this.children = children;
  }
}

public class TreeViewController : BaseTreeViewController
{
}

public class BaseTreeViewController
{
  public virtual IEnumerable<int> GetAllItemIds() => Enumerable.Empty<int>();
  public virtual int GetChildCount(int id) => 0;
  public virtual int GetChildIndexForId(int id) => -1;
  public virtual int GetIdForIndex(int index) => -1;
  public virtual int GetParentId(int id) => -1;
  public virtual object GetItemDataForIndex(int index) => null;
  public virtual object GetItemDataForId(int id) => null;
  public virtual bool HasChildren(int id) => false;
  public virtual void Move(int id, int parentId, int childIndex) { }
  public virtual bool TryRemoveItem(int id) => false;
}
