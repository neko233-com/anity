using System.Collections.Generic;

namespace UnityEngine.UI;

public static class MaskUtilities
{
    public static void Notify2DMaskStateChanged(Component mask)
    {
        var components = mask.GetComponentsInChildren<Component>();
        for (var i = 0; i < components.Length; i++)
        {
            if (components[i] == null || components[i].gameObject == mask.gameObject) continue;
            if (components[i] is IClippable toNotify)
                toNotify.RecalculateClipping();
        }
    }

    public static void NotifyStencilStateChanged(Component mask)
    {
        var components = mask.GetComponentsInChildren<Component>();
        for (var i = 0; i < components.Length; i++)
        {
            if (components[i] == null || components[i].gameObject == mask.gameObject) continue;
            if (components[i] is IMaskable toNotify)
                toNotify.RecalculateMasking();
        }
    }

    public static Transform FindRootSortOverrideCanvas(Transform start)
    {
        var canvasList = start.GetComponentsInParent<Canvas>(false);
        Canvas canvas = null;
        for (var i = 0; i < canvasList.Length; i++)
        {
            canvas = canvasList[i];
            if (canvas.overrideSorting)
                break;
        }
        return canvas != null ? canvas.transform : null;
    }

    public static int GetStencilDepth(Transform transform, Transform stopAfter)
    {
        var depth = 0;
        var t = transform;
        while (t != null && t != stopAfter)
        {
            var mask = t.GetComponent<Mask>();
            if (mask != null && mask.MaskEnabled())
                depth++;
            t = t.parent;
        }
        return depth;
    }

    public static RectMask2D GetRectMaskForClippable(IClippable clippable)
    {
        var clippableGO = ((Component)clippable).gameObject;
        var rectMasks = clippableGO.GetComponentsInParent<RectMask2D>(false);
        return rectMasks.Length > 0 ? rectMasks[0] : null;
    }

    public static void GetRectMasksForClip(RectMask2D clipper, List<RectMask2D> masks)
    {
        masks.Clear();
        if (clipper == null) return;
        var rectMasks = clipper.GetComponentsInParent<RectMask2D>(false);
        masks.AddRange(rectMasks);
    }

    public static bool IsDescendantOrSelf(Transform father, Transform child)
    {
        if (father == null || child == null)
            return false;
        if (father == child)
            return true;
        return child.IsChildOf(father);
    }
}
