namespace UnityEngine;

public class SpriteRenderer : Renderer
{
    private Sprite _sprite;
    private Color _color = Color.white;
    private SpriteSortPoint _sortPoint = SpriteSortPoint.Center;
    private SpriteDrawMode _drawMode = SpriteDrawMode.Simple;
    private Vector2 _size = Vector2.one;
    private SpriteTileMode _tileMode = SpriteTileMode.Continuous;
    private SpriteMaskInteraction _maskInteraction = SpriteMaskInteraction.None;
    private int _sortingOrder;
    private int _sortingLayerID;
    private string _sortingLayerName = "Default";
    private bool _flipX;
    private bool _flipY;

    public Sprite sprite
    {
        get => _sprite;
        set => _sprite = value;
    }

    public Color color
    {
        get => _color;
        set => _color = value;
    }

    public SpriteSortPoint sortPoint
    {
        get => _sortPoint;
        set => _sortPoint = value;
    }

    public SpriteDrawMode drawMode
    {
        get => _drawMode;
        set => _drawMode = value;
    }

    public Vector2 size
    {
        get => _size;
        set => _size = value;
    }

    public SpriteTileMode tileMode
    {
        get => _tileMode;
        set => _tileMode = value;
    }

    public SpriteMaskInteraction maskInteraction
    {
        get => _maskInteraction;
        set => _maskInteraction = value;
    }

    public bool flipX
    {
        get => _flipX;
        set => _flipX = value;
    }

    public bool flipY
    {
        get => _flipY;
        set => _flipY = value;
    }

    public new int sortingOrder
    {
        get => _sortingOrder;
        set => _sortingOrder = value;
    }

    public new int sortingLayerID
    {
        get => _sortingLayerID;
        set => _sortingLayerID = value;
    }

    public new string sortingLayerName
    {
        get => _sortingLayerName;
        set => _sortingLayerName = value ?? "Default";
    }

    public SpriteRenderer()
    {
    }
}

public enum SpriteSortPoint
{
    Center,
    Pivot,
    BottomLeft,
    BottomRight,
    TopLeft,
    TopRight
}

public enum SpriteDrawMode
{
    Simple,
    Sliced,
    Tiled
}

public enum SpriteTileMode
{
    Continuous,
    Adaptive
}

public enum SpriteMaskInteraction
{
    None,
    VisibleInsideMask,
    VisibleOutsideMask
}

public class SpriteMask : Renderer
{
    private Sprite _sprite;
    private int _alphaCutoff = 128;
    private int _frontSortingLayerID;
    private int _frontSortingOrder = 1;
    private int _backSortingLayerID;
    private int _backSortingOrder;
    private bool _customRange;
    private SpriteSortPoint _sortPoint = SpriteSortPoint.Center;
    private bool _flipX;
    private bool _flipY;

    public Sprite sprite
    {
        get => _sprite;
        set => _sprite = value;
    }

    public int alphaCutoff
    {
        get => _alphaCutoff;
        set => _alphaCutoff = value;
    }

    public int frontSortingLayerID
    {
        get => _frontSortingLayerID;
        set => _frontSortingLayerID = value;
    }

    public int frontSortingOrder
    {
        get => _frontSortingOrder;
        set => _frontSortingOrder = value;
    }

    public int backSortingLayerID
    {
        get => _backSortingLayerID;
        set => _backSortingLayerID = value;
    }

    public int backSortingOrder
    {
        get => _backSortingOrder;
        set => _backSortingOrder = value;
    }

    public bool customRange
    {
        get => _customRange;
        set => _customRange = value;
    }

    public SpriteSortPoint sortPoint
    {
        get => _sortPoint;
        set => _sortPoint = value;
    }

    public bool flipX
    {
        get => _flipX;
        set => _flipX = value;
    }

    public bool flipY
    {
        get => _flipY;
        set => _flipY = value;
    }

    public SpriteMask()
    {
    }
}

public class SpriteAtlas : Object
{
    private Sprite[] _sprites;
    private string _name;

    public string name
    {
        get => _name;
        set => _name = value;
    }

    public SpriteAtlas()
    {
    }

    public int spriteCount => _sprites?.Length ?? 0;

    public Sprite GetSprite(string name)
    {
        if (_sprites == null) return null;
        foreach (var s in _sprites)
        {
            if (s != null && s.name == name)
                return s;
        }
        return null;
    }

    public Sprite[] GetSprites(Sprite[] sprites)
    {
        if (_sprites == null) return new Sprite[0];
        _sprites.CopyTo(sprites, 0);
        return _sprites;
    }

    public bool CanBindTo(Sprite sprite)
    {
        return sprite != null;
    }

    public bool IsVariant => false;
    public bool isVariant => false;

    public Texture2D texture => null;
}

public class SpriteAtlasManager
{
    public static SpriteAtlasManager instance { get; } = new SpriteAtlasManager();

    public event System.Action<string, System.Action<SpriteAtlas>> atlasRequested;

    public void Register(SpriteAtlas atlas)
    {
    }
}

public enum SpriteMeshType
{
    FullRect,
    Tight
}

public enum SpritePackingMode
{
    Tight,
    Rectangle
}

public enum SpritePackingRotation
{
    None,
    FlipHorizontal,
    FlipVertical,
    Rotate180
}

public enum SpriteAlignment
{
    Center,
    TopLeft,
    TopCenter,
    TopRight,
    LeftCenter,
    RightCenter,
    BottomLeft,
    BottomCenter,
    BottomRight,
    Custom
}

public struct SpriteMetaData
{
    public string name;
    public Rect rect;
    public Vector2 pivot;
    public int alignment;
    public Vector4 border;
    public float pixelsPerUnit;
    public SpriteMeshType spriteMeshType;
    public string outline;
    public float tessellationDetail;
    public int spriteID;
}

public class SpriteUtility
{
    public static Vector2 GetSpriteSize(Sprite sprite)
    {
        return sprite != null ? new Vector2(sprite.rect.width, sprite.rect.height) : Vector2.zero;
    }

    public static Vector2 GetPivot(Sprite sprite)
    {
        return sprite != null ? sprite.pivot : Vector2.zero;
    }
}