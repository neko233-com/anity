using System.Collections.Generic;

namespace UnityEngine.UI;

public static class RectTransformUtility
{
    public static bool RectangleContainsScreenPoint(RectTransform rect, Vector2 screenPoint, Camera? cam)
    {
        if (rect is null) return false;

        if (!ScreenPointToLocalPointInRectangle(rect, screenPoint, cam, out var localPoint))
            return false;

        var r = rect.rect;
        return localPoint.x >= r.xMin && localPoint.x <= r.xMax &&
               localPoint.y >= r.yMin && localPoint.y <= r.yMax;
    }

    public static bool RectangleContainsScreenPoint(RectTransform rect, Vector2 screenPoint, Camera? cam, out Vector4 localPoint)
    {
        localPoint = default;
        if (rect is null) return false;

        if (!ScreenPointToLocalPointInRectangle(rect, screenPoint, cam, out var pt))
            return false;

        var r = rect.rect;
        var contains = pt.x >= r.xMin && pt.x <= r.xMax && pt.y >= r.yMin && pt.y <= r.yMax;
        if (contains)
        {
            localPoint = new Vector4(pt.x, pt.y, 0f, 0f);
        }
        return contains;
    }

    public static bool ScreenPointToWorldPointInRectangle(RectTransform rect, Vector2 screenPoint, Camera? cam, out Vector3 worldPoint)
    {
        worldPoint = default;
        if (rect is null) return false;

        var plane = new Plane(rect.rotation * Vector3.back, rect.position);
        var ray = ScreenPointToRay(cam, screenPoint);

        if (plane.Raycast(ray, out var enter))
        {
            worldPoint = ray.GetPoint(enter);
            return true;
        }

        var depth = Vector3.Dot(rect.rotation * Vector3.back, cam?.transform.forward ?? Vector3.back);
        worldPoint = ScreenPointToGuessedWorldPoint(screenPoint, cam, depth);
        return true;
    }

    public static bool ScreenPointToLocalPointInRectangle(RectTransform rect, Vector2 screenPoint, Camera? cam, out Vector2 localPoint)
    {
        localPoint = default;
        if (ScreenPointToWorldPointInRectangle(rect, screenPoint, cam, out var worldPoint))
        {
            var local = rect.InverseTransformPoint(worldPoint);
            localPoint = new Vector2(local.x, local.y);
            return true;
        }
        return false;
    }

    public static void FlipLayoutOnAxis(RectTransform rect, int axis, bool keepPositioning, bool recursive)
    {
        _ = rect;
        _ = axis;
        _ = keepPositioning;
        _ = recursive;
    }

    public static void FlipLayoutAxes(RectTransform rect, bool keepPositioning, bool recursive)
    {
        _ = rect;
        _ = keepPositioning;
        _ = recursive;
    }

    public static Bounds CalculateRelativeRectTransformBounds(Transform trans)
    {
        _ = trans;
        return new Bounds(Vector3.zero, Vector3.zero);
    }

    public static Bounds CalculateRelativeRectTransformBounds(Transform root, Transform child)
    {
        _ = root;
        _ = child;
        return new Bounds(Vector3.zero, Vector3.zero);
    }

    private static Ray ScreenPointToRay(Camera? cam, Vector2 screenPos)
    {
        if (cam is not null)
        {
            return cam.ScreenPointToRay(new Vector3(screenPos.x, screenPos.y, 0f));
        }
        return new Ray(new Vector3(screenPos.x, screenPos.y, -100f), Vector3.forward);
    }

    private static Vector3 ScreenPointToGuessedWorldPoint(Vector2 screenPos, Camera? cam, float distance)
    {
        if (cam is not null)
        {
            return cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, Mathf.Abs(distance)));
        }
        return new Vector3(screenPos.x, screenPos.y, 0f);
    }
}
