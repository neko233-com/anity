namespace UnityEngine;

public class SortingGroup : Behaviour
{
    private int _sortingLayerID;
    private int _sortingOrder;
    private string _sortingLayerName = string.Empty;
    private bool _sortAtRoot;
    private bool _invalidSortingCache;

    public int sortingLayerID
    {
        get => _sortingLayerID;
        set
        {
            _sortingLayerID = value;
            _invalidSortingCache = true;
        }
    }

    public int sortingOrder
    {
        get => _sortingOrder;
        set
        {
            _sortingOrder = value;
            _invalidSortingCache = true;
        }
    }

    public string sortingLayerName
    {
        get => _sortingLayerName;
        set
        {
            _sortingLayerName = value ?? string.Empty;
            _invalidSortingCache = true;
        }
    }

    public bool sortAtRoot
    {
        get => _sortAtRoot;
        set => _sortAtRoot = value;
    }

    public bool sortingAtRoot
    {
        get => _sortAtRoot;
        set => _sortAtRoot = value;
    }

    public bool invalidSortingCache => _invalidSortingCache;

    public Matrix4x4 worldToLocalMatrix => transform?.worldToLocalMatrix ?? Matrix4x4.identity;
    public Matrix4x4 localToWorldMatrix => transform?.localToWorldMatrix ?? Matrix4x4.identity;

    public void UpdateSortingGroup()
    {
        _invalidSortingCache = false;
    }
}
