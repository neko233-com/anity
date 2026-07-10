namespace UnityEngine.UI;

/// <summary>
/// Displays a Texture without slicing.
/// </summary>
public class RawImage : MaskableGraphic
{
    private Texture? _texture;
    private Rect _uvRect = new Rect(0, 0, 1, 1);

    public Texture? texture
    {
        get => _texture;
        set
        {
            if (_texture != value)
            {
                _texture = value;
                SetVerticesDirty();
                SetMaterialDirty();
            }
        }
    }

    public Rect uvRect
    {
        get => _uvRect;
        set
        {
            if (_uvRect != value)
            {
                _uvRect = value;
                SetVerticesDirty();
            }
        }
    }
}
