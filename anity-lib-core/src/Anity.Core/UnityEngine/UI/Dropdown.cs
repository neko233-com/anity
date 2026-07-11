using System;
using System.Collections.Generic;
using UnityEngine.EventSystems;

namespace UnityEngine.UI;

public class Dropdown : Selectable, IPointerClickHandler, ISubmitHandler, ICancelHandler
{
  public class OptionData
  {
    public string text { get; set; } = string.Empty;
    public Sprite? image { get; set; }

    public OptionData() { }

    public OptionData(string text)
    {
      this.text = text;
    }

    public OptionData(Sprite? image)
    {
      this.image = image;
    }

    public OptionData(string text, Sprite? image)
    {
      this.text = text;
      this.image = image;
    }
  }

  public class OptionDataList
  {
    public List<OptionData> options { get; set; } = new();
    public OptionDataList() { }
  }

  private RectTransform? _template;
  private Text? _captionText;
  private Image? _captionImage;
  private Text? _itemText;
  private Image? _itemImage;
  private float _alphaFadeSpeed = 0.15f;
  private List<OptionData> _options = new();
  private DropdownEvent _onValueChanged = new();
  private int _value;
  private GameObject? _dropdown;
  private GameObject? _blocker;
  private readonly List<DropdownItem> _items = new();
  private bool _validTemplate;

  public RectTransform? template
  {
    get => _template;
    set
    {
      _template = value;
      RefreshShownValue();
    }
  }

  public Text? captionText
  {
    get => _captionText;
    set => _captionText = value;
  }

  public Image? captionImage
  {
    get => _captionImage;
    set => _captionImage = value;
  }

  public Text? itemText
  {
    get => _itemText;
    set => _itemText = value;
  }

  public Image? itemImage
  {
    get => _itemImage;
    set => _itemImage = value;
  }

  public List<OptionData> options
  {
    get => _options;
    set
    {
      _options = value ?? new List<OptionData>();
      RefreshShownValue();
    }
  }

  public float alphaFadeSpeed
  {
    get => _alphaFadeSpeed;
    set => _alphaFadeSpeed = value;
  }

  public int value
  {
    get => _value;
    set => Set(value);
  }

  public DropdownEvent onValueChanged
  {
    get => _onValueChanged;
    set => _onValueChanged = value;
  }

  public bool IsExpanded => _dropdown != null;

  private void Set(int value, bool sendCallback = true)
  {
    if (_value == value) return;
    _value = Mathf.Clamp(value, 0, Mathf.Max(0, _options.Count - 1));
    RefreshShownValue();
    if (sendCallback)
    {
      _onValueChanged?.Invoke(_value);
    }
  }

  public void RefreshShownValue()
  {
    var validIdx = Mathf.Clamp(_value, 0, Mathf.Max(0, _options.Count - 1));
    var data = _options.Count == 0 ? null : _options[validIdx];

    if (_captionText != null)
    {
      _captionText.text = data != null ? data.text : string.Empty;
    }

    if (_captionImage != null)
    {
      _captionImage.sprite = data?.image;
      _captionImage.enabled = data != null && data.image != null;
    }
  }

  public void AddOptions(List<OptionData> options)
  {
    if (options == null) return;
    _options.AddRange(options);
    RefreshShownValue();
  }

  public void AddOptions(List<string> options)
  {
    if (options == null) return;
    foreach (var t in options)
    {
      _options.Add(new OptionData(t));
    }
    RefreshShownValue();
  }

  public void AddOptions(List<Sprite> options)
  {
    if (options == null) return;
    foreach (var s in options)
    {
      _options.Add(new OptionData(s));
    }
    RefreshShownValue();
  }

  public void ClearOptions()
  {
    _options.Clear();
    _value = 0;
    RefreshShownValue();
  }

  public void Show()
  {
    if (_template == null) return;
    if (IsExpanded) return;

    var canvas = GetComponentInParent<Canvas>();
    var root = canvas != null ? canvas.transform : transform;

    _dropdown = CreateDropdownList(root);
    if (_dropdown == null) return;

    _dropdown.SetActive(true);

    if (_itemText == null)
      _itemText = GetComponentInChildren<Text>(_template.gameObject);
    if (_itemImage == null)
      _itemImage = GetComponentInChildren<Image>(_template.gameObject);

    var content = FindDescendant(_dropdown.transform, "Content");
    Transform? templateItem = null;
    if (content != null && content.childCount > 0)
    {
      templateItem = content.GetChild(0);
      _validTemplate = true;
    }
    else
    {
      _validTemplate = false;
    }

    if (_validTemplate && templateItem != null)
        {
            templateItem.gameObject.SetActive(true);
            for (var i = 0; i < _options.Count; i++)
            {
                var itemGo = CreateItem(templateItem.gameObject);
                var dropdownItem = itemGo.GetComponent<DropdownItem>() ?? itemGo.AddComponent<DropdownItem>();
                dropdownItem.text = GetComponentInChildren<Text>(itemGo);
                dropdownItem.image = GetComponentInChildren<Image>(itemGo);
                dropdownItem.index = i;
                _items.Add(dropdownItem);

                if (dropdownItem.text != null)
                {
                    dropdownItem.text.text = _options[i].text;
                    if (_itemText != null)
                    {
                        dropdownItem.text.fontSize = _itemText.fontSize;
                        dropdownItem.text.font = _itemText.font;
                        dropdownItem.text.color = _itemText.color;
                    }
                }

                if (dropdownItem.image != null)
                {
                    dropdownItem.image.sprite = _options[i].image;
                    dropdownItem.image.enabled = _options[i].image != null;
                }

                itemGo.SetActive(true);
            }
            templateItem.gameObject.SetActive(false);
        }

        _blocker = CreateBlocker(root);
  }

  public void Hide()
  {
    if (_dropdown != null)
    {
      Object.Destroy(_dropdown);
      _dropdown = null;
    }
    if (_blocker != null)
    {
      Object.Destroy(_blocker);
      _blocker = null;
    }
    _items.Clear();
  }

  private GameObject? CreateDropdownList(Transform root)
  {
    var go = new GameObject("Dropdown List");
    var rt = go.AddComponent<RectTransform>();
    rt.SetParent(root, false);
    rt.anchorMin = Vector2.zero;
    rt.anchorMax = Vector2.one;
    rt.sizeDelta = Vector2.zero;
    var canvas = go.AddComponent<Canvas>();
    canvas.overrideSorting = true;
    canvas.sortingOrder = 30000;
    go.AddComponent<CanvasGroup>();
    var img = go.AddComponent<Image>();
    img.color = new Color(1f, 1f, 1f, 1f);
    var scrollRect = go.AddComponent<ScrollRect>();
    var contentGo = new GameObject("Content");
    var contentRt = contentGo.AddComponent<RectTransform>();
    contentRt.SetParent(rt, false);
    scrollRect.content = contentRt;
    var viewportGo = new GameObject("Viewport");
    var viewportRt = viewportGo.AddComponent<RectTransform>();
    viewportRt.SetParent(rt, false);
    viewportRt.anchorMin = Vector2.zero;
    viewportRt.anchorMax = Vector2.one;
    viewportRt.sizeDelta = Vector2.zero;
    var mask = viewportGo.AddComponent<RectMask2D>();
    contentRt.SetParent(viewportRt, false);
    scrollRect.viewport = viewportRt;

    var itemGo = new GameObject("Item");
    var itemRt = itemGo.AddComponent<RectTransform>();
    itemRt.SetParent(contentRt, false);
    itemRt.sizeDelta = new Vector2(0f, 20f);
    itemRt.anchorMin = new Vector2(0f, 1f);
    itemRt.anchorMax = new Vector2(1f, 1f);
    itemRt.pivot = new Vector2(0.5f, 1f);
    var itemToggle = itemGo.AddComponent<Toggle>();
    var bg = new GameObject("Item Background");
    var bgRt = bg.AddComponent<RectTransform>();
    bgRt.SetParent(itemRt, false);
    bgRt.anchorMin = Vector2.zero;
    bgRt.anchorMax = Vector2.one;
    bgRt.sizeDelta = Vector2.zero;
    var bgImg = bg.AddComponent<Image>();
    bgImg.color = new Color(0.9f, 0.9f, 0.9f, 1f);
    itemToggle.targetGraphic = bgImg;
    var checkmark = new GameObject("Item Checkmark");
    var checkRt = checkmark.AddComponent<RectTransform>();
    checkRt.SetParent(bgRt, false);
    var checkImg = checkmark.AddComponent<Image>();
    itemToggle.graphic = checkImg;
    var labelGo = new GameObject("Item Label");
    var labelRt = labelGo.AddComponent<RectTransform>();
    labelRt.SetParent(bgRt, false);
    labelRt.anchorMin = Vector2.zero;
    labelRt.anchorMax = Vector2.one;
    labelRt.sizeDelta = Vector2.zero;
    var labelText = labelGo.AddComponent<Text>();
    labelText.fontSize = 14;
    labelText.alignment = TextAnchor.MiddleLeft;

    go.SetActive(false);
    return go;
  }

  private GameObject CreateItem(GameObject template)
  {
    var go = new GameObject(template.name);
    CopyTransform(template.transform, go.transform);
    foreach (var comp in template.GetComponents<Component>())
    {
      if (comp is Transform) continue;
      try { go.AddComponent(comp.GetType()); } catch { }
    }
    return go;
  }

  private void CopyTransform(Transform src, Transform dst)
  {
    if (dst is RectTransform dRt && src is RectTransform sRt)
    {
      dRt.anchorMin = sRt.anchorMin;
      dRt.anchorMax = sRt.anchorMax;
      dRt.anchoredPosition = sRt.anchoredPosition;
      dRt.sizeDelta = sRt.sizeDelta;
      dRt.pivot = sRt.pivot;
    }
    for (var i = 0; i < src.childCount; i++)
    {
      var srcChild = src.GetChild(i);
      var childGo = new GameObject(srcChild.gameObject.name);
      var childRt = childGo.AddComponent<RectTransform>();
      childRt.SetParent(dst, false);
      CopyTransform(srcChild, childRt);
      foreach (var comp in srcChild.GetComponents<Component>())
      {
        if (comp is Transform) continue;
        try { childGo.AddComponent(comp.GetType()); } catch { }
      }
    }
  }

  private GameObject CreateBlocker(Transform root)
  {
    var go = new GameObject("Blocker");
    var rt = go.AddComponent<RectTransform>();
    rt.SetParent(root, false);
    rt.anchorMin = Vector2.zero;
    rt.anchorMax = Vector2.one;
    rt.sizeDelta = Vector2.zero;
    var canvas = go.AddComponent<Canvas>();
    canvas.overrideSorting = true;
    canvas.sortingOrder = 29999;
    var img = go.AddComponent<Image>();
    img.color = new Color(0f, 0f, 0f, 0f);
    img.raycastTarget = true;
    var blocker = go.AddComponent<DropdownBlocker>();
    blocker.dropdown = this;
    return go;
  }

  private Transform? FindDescendant(Transform parent, string name)
  {
    for (var i = 0; i < parent.childCount; i++)
    {
      var child = parent.GetChild(i);
      if (child.gameObject.name == name) return child;
      var found = FindDescendant(child, name);
      if (found != null) return found;
    }
    return null;
  }

  private T? GetComponentInChildren<T>(GameObject go) where T : Component
  {
    var comp = go.GetComponent<T>();
    if (comp != null) return comp;
    for (var i = 0; i < go.transform.childCount; i++)
    {
      var child = go.transform.GetChild(i);
      var found = GetComponentInChildren<T>(child.gameObject);
      if (found != null) return found;
    }
    return null;
  }

  public void OnSelectItem(int index)
  {
    _value = index;
    RefreshShownValue();
    _onValueChanged?.Invoke(index);
    Hide();
  }

  public void OnPointerClick(PointerEventData eventData)
  {
    if (eventData.button != PointerEventData.InputButton.Left) return;
    if (!IsActive() || !IsInteractable()) return;
    Show();
  }

  public new void OnSubmit(BaseEventData eventData)
  {
    if (!IsActive() || !IsInteractable()) return;
    Show();
  }

  public virtual void OnCancel(BaseEventData eventData)
  {
    Hide();
  }

  protected override void OnDisable()
  {
    Hide();
    base.OnDisable();
  }

  protected override void OnDestroy()
  {
    Hide();
    base.OnDestroy();
  }

  public void SetValueWithoutNotify(int input)
  {
    _value = Mathf.Clamp(input, 0, Mathf.Max(0, _options.Count - 1));
    RefreshShownValue();
  }
}

public class DropdownItem : MonoBehaviour, IPointerClickHandler
{
  public Text? text;
  public Image? image;
  public int index;

  public void OnPointerClick(PointerEventData eventData)
  {
    _ = eventData;
    var dropdown = GetComponentInParent<Dropdown>();
    dropdown?.OnSelectItem(index);
  }

  private T? GetComponentInParent<T>() where T : Component
  {
    for (var t = transform; t != null; t = t.parent)
    {
      var c = t.gameObject.GetComponent<T>();
      if (c != null) return c;
    }
    return null;
  }
}

[Serializable]
public class DropdownEvent
{
  public event Action<int>? ValueChanged;

  public void Invoke(int value)
  {
    ValueChanged?.Invoke(value);
  }
}

public class DropdownBlocker : MonoBehaviour, IPointerClickHandler
{
  public Dropdown? dropdown;

  public void OnPointerClick(PointerEventData eventData)
  {
    _ = eventData;
    dropdown?.Hide();
  }
}
