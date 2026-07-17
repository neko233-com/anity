using System.Collections.Generic;

namespace UnityEngine;

[Bindings.NativeHeader("Modules/UI/Canvas.h")]
[Bindings.NativeHeader("Modules/UI/RectTransformUtil.h")]
[Bindings.NativeHeader("Runtime/Camera/Camera.h")]
[Bindings.NativeHeader("Runtime/Transform/RectTransform.h")]
[Bindings.StaticAccessor("UI", Bindings.StaticAccessorType.DoubleColon)]
public sealed class RectTransformUtility
{
    private RectTransformUtility()
    {
    }

    public static bool RectangleContainsScreenPoint(RectTransform rect, Vector2 screenPoint)
    {
        return RectangleContainsScreenPoint(rect, screenPoint, null);
    }

    public static bool RectangleContainsScreenPoint(RectTransform rect, Vector2 screenPoint, Camera? cam)
    {
        if (rect is null) return false;

        if (!ScreenPointToLocalPointInRectangle(rect, screenPoint, cam, out var localPoint))
            return false;

        var r = rect.rect;
        return localPoint.x >= r.xMin && localPoint.x <= r.xMax &&
               localPoint.y >= r.yMin && localPoint.y <= r.yMax;
    }

    public static bool RectangleContainsScreenPoint(RectTransform rect, Vector2 screenPoint, Camera? cam, Vector4 offset)
    {
        if (rect is null) return false;

        if (!ScreenPointToLocalPointInRectangle(rect, screenPoint, cam, out var pt))
            return false;

        var r = rect.rect;
        return pt.x >= r.xMin + offset.x && pt.x <= r.xMax - offset.z &&
               pt.y >= r.yMin + offset.y && pt.y <= r.yMax - offset.w;
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
        return false;
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
        if (rect is null) return;

        Vector2 pivot = rect.pivot;
        pivot[axis] = 1f - pivot[axis];
        rect.pivot = pivot;

        if (!keepPositioning)
        {
            Vector2 position = rect.anchoredPosition;
            position[axis] = -position[axis];
            rect.anchoredPosition = position;

            Vector2 oldMin = rect.anchorMin;
            Vector2 oldMax = rect.anchorMax;
            oldMin[axis] = 1f - rect.anchorMax[axis];
            oldMax[axis] = 1f - rect.anchorMin[axis];
            rect.anchorMin = oldMin;
            rect.anchorMax = oldMax;
        }

        if (!recursive) return;
        for (int i = 0; i < rect.childCount; i++)
        {
            if (rect.GetChild(i) is RectTransform child)
                FlipLayoutOnAxis(child, axis, false, true);
        }
    }

    public static void FlipLayoutAxes(RectTransform rect, bool keepPositioning, bool recursive)
    {
        if (rect is null) return;

        rect.pivot = new Vector2(rect.pivot.y, rect.pivot.x);
        rect.sizeDelta = new Vector2(rect.sizeDelta.y, rect.sizeDelta.x);

        if (!keepPositioning)
        {
            rect.anchoredPosition = new Vector2(rect.anchoredPosition.y, rect.anchoredPosition.x);
            rect.anchorMin = new Vector2(rect.anchorMin.y, rect.anchorMin.x);
            rect.anchorMax = new Vector2(rect.anchorMax.y, rect.anchorMax.x);
        }

        if (!recursive) return;
        for (int i = 0; i < rect.childCount; i++)
        {
            if (rect.GetChild(i) is RectTransform child)
                FlipLayoutAxes(child, false, true);
        }
    }

    public static Bounds CalculateRelativeRectTransformBounds(Transform trans)
    {
        return CalculateRelativeRectTransformBounds(trans, trans);
    }

    public static Bounds CalculateRelativeRectTransformBounds(Transform root, Transform child)
    {
        if (root is null || child is null)
            return new Bounds(Vector3.zero, Vector3.zero);

        RectTransform[] rects = child.GetComponentsInChildren<RectTransform>(false);
        if (rects.Length == 0)
            return new Bounds(Vector3.zero, Vector3.zero);

        Matrix4x4 toLocal = root.worldToLocalMatrix;
        var corners = new Vector3[4];
        Vector3 min = new(float.MaxValue, float.MaxValue, float.MaxValue);
        Vector3 max = new(float.MinValue, float.MinValue, float.MinValue);
        for (int i = 0; i < rects.Length; i++)
        {
            rects[i].GetWorldCorners(corners);
            for (int j = 0; j < 4; j++)
            {
                Vector3 point = toLocal.MultiplyPoint3x4(corners[j]);
                min = Vector3.Min(min, point);
                max = Vector3.Max(max, point);
            }
        }

        var bounds = new Bounds(min, Vector3.zero);
        bounds.Encapsulate(max);
        return bounds;
    }

    public static Ray ScreenPointToRay(Camera? cam, Vector2 screenPos)
    {
        if (cam is not null && cam.transform is not null)
        {
            float width = Mathf.Max(1f, cam.pixelWidth);
            float height = Mathf.Max(1f, cam.pixelHeight);
            float normalizedX = screenPos.x / width * 2f - 1f;
            float normalizedY = screenPos.y / height * 2f - 1f;
            Transform transform = cam.transform;
            if (cam.orthographic)
            {
                float vertical = cam.orthographicSize;
                float horizontal = vertical * cam.aspect;
                Vector3 origin = transform.position + transform.right * (normalizedX * horizontal) +
                                 transform.up * (normalizedY * vertical) + transform.forward * cam.nearClipPlane;
                return new Ray(origin, transform.forward);
            }

            float tangent = Mathf.Tan(cam.fieldOfView * Mathf.Deg2Rad * 0.5f);
            Vector3 unnormalizedDirection = transform.forward +
                                            transform.right * (normalizedX * tangent * cam.aspect) +
                                            transform.up * (normalizedY * tangent);
            Vector3 rayOrigin = transform.position + unnormalizedDirection * cam.nearClipPlane;
            return new Ray(rayOrigin, unnormalizedDirection.normalized);
        }
        return new Ray(new Vector3(screenPos.x, screenPos.y, -100f), Vector3.forward);
    }

    public static Vector2 WorldToScreenPoint(Camera? cam, Vector3 worldPoint)
    {
        if (cam is null)
            return new Vector2(worldPoint.x, worldPoint.y);
        Vector3 screenPoint = cam.WorldToScreenPoint(worldPoint);
        return new Vector2(screenPoint.x, screenPoint.y);
    }

    public static Vector2 PixelAdjustPoint(Vector2 point, Transform elementTransform, Canvas canvas)
    {
        if (elementTransform is null || canvas is null || !canvas.pixelPerfect ||
            canvas.renderMode == RenderMode.WorldSpace || canvas.scaleFactor == 0f)
            return point;

        Vector3 world = elementTransform.TransformPoint(new Vector3(point.x, point.y, 0f));
        Camera? camera = canvas.renderMode == RenderMode.ScreenSpaceCamera ? canvas.worldCamera : null;
        Vector2 screen = WorldToScreenPoint(camera, world);
        screen.x = Mathf.Round(screen.x);
        screen.y = Mathf.Round(screen.y);

        Vector3 adjustedWorld;
        if (camera is null)
        {
            adjustedWorld = new Vector3(screen.x, screen.y, world.z);
        }
        else
        {
            Vector3 projected = camera.WorldToScreenPoint(world);
            adjustedWorld = camera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, projected.z));
        }

        Vector3 adjustedLocal = elementTransform.InverseTransformPoint(adjustedWorld);
        return new Vector2(adjustedLocal.x, adjustedLocal.y);
    }

    public static Rect PixelAdjustRect(RectTransform rectTransform, Canvas canvas)
    {
        if (rectTransform is null)
            return default;
        Rect rect = rectTransform.rect;
        if (canvas is null || !canvas.pixelPerfect || canvas.renderMode == RenderMode.WorldSpace || canvas.scaleFactor == 0f)
            return rect;

        Vector2 min = PixelAdjustPoint(new Vector2(rect.xMin, rect.yMin), rectTransform, canvas);
        Vector2 max = PixelAdjustPoint(new Vector2(rect.xMax, rect.yMax), rectTransform, canvas);
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

}
