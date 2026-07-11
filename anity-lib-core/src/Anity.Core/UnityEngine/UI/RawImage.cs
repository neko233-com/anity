using System.Collections.Generic;

namespace UnityEngine.UI;

[AddComponentMenu("UI/Raw Image", 12)]
public class RawImage : MaskableGraphic
{
    private Texture? _texture;
    private Rect _uvRect = new Rect(0, 0, 1, 1);

    public override Texture mainTexture => _texture != null ? _texture : defaultWhiteTexture;

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

    protected override void UpdateGeometry()
    {
        if (canvasRenderer is null) return;
        var vh = new VertexHelper();
        OnPopulateMesh(vh);
        var verts = new List<UIVertex>();
        vh.GetUIVertexStream(verts);
        canvasRenderer.SetVertices(verts);
        vh.Dispose();
    }

    protected virtual void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        var rect = rectTransform != null ? rectTransform.rect : new Rect(0f, 0f, 100f, 100f);
        var color32 = (Color32)color;
        var uvMin = new Vector2(_uvRect.xMin, _uvRect.yMin);
        var uvMax = new Vector2(_uvRect.xMax, _uvRect.yMax);

        vh.AddVert(new Vector3(rect.xMin, rect.yMin, 0f), color32, new Vector4(uvMin.x, uvMin.y, 0f, 0f));
        vh.AddVert(new Vector3(rect.xMin, rect.yMax, 0f), color32, new Vector4(uvMin.x, uvMax.y, 0f, 0f));
        vh.AddVert(new Vector3(rect.xMax, rect.yMax, 0f), color32, new Vector4(uvMax.x, uvMax.y, 0f, 0f));
        vh.AddVert(new Vector3(rect.xMax, rect.yMin, 0f), color32, new Vector4(uvMax.x, uvMin.y, 0f, 0f));
        vh.AddTriangle(0, 1, 2);
        vh.AddTriangle(2, 3, 0);
    }

    public virtual void SetNativeSize()
    {
        if (_texture == null) return;
        var rt = rectTransform;
        if (rt == null) return;
        rt.anchorMin = rt.anchorMax;
        rt.sizeDelta = new Vector2(_texture.width, _texture.height);
    }
}
