using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Xunit;

namespace Anity.Core.Tests;

[Collection(ComponentAttributeBehaviorCollection.Name)]
public sealed class RectTransformUtilityParityTests
{
    private const float Tolerance = 0.001f;

    [Fact]
    public void NamespaceTypeShapeAndNativeMetadataMatchUnity()
    {
        Type type = typeof(RectTransformUtility);
        Assert.Equal("UnityEngine.RectTransformUtility", type.FullName);
        Assert.True(type.IsSealed);
        Assert.False(type.IsAbstract);
        Assert.Empty(type.GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        Assert.Null(type.Assembly.GetType("UnityEngine.UI.RectTransformUtility"));
        string[] attributes = type.GetCustomAttributes(false).Select(attribute => attribute.ToString()!).ToArray();
        Assert.Equal(5, attributes.Length);
    }

    [Fact]
    public void NullCameraRayAndWorldProjectionMatchUnityProbe()
    {
        Ray ray = RectTransformUtility.ScreenPointToRay(null, new Vector2(12, 34));
        AssertVector3(new Vector3(12, 34, -100), ray.origin);
        AssertVector3(Vector3.forward, ray.direction);
        AssertVector2(new Vector2(7, -3), RectTransformUtility.WorldToScreenPoint(null, new Vector3(7, -3, 11)));
    }

    [Fact]
    public void NullCameraScreenPointIntersectsRectPlane()
    {
        WithRect(rect =>
        {
            rect.position = new Vector3(10, 20, 5);
            Assert.True(RectTransformUtility.ScreenPointToWorldPointInRectangle(rect, new Vector2(10, 20), null, out Vector3 world));
            Assert.True(RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, new Vector2(10, 20), null, out Vector2 local));
            AssertVector3(new Vector3(10, 20, 5), world);
            AssertVector2(Vector2.zero, local);
        });
    }

    [Fact]
    public void RectangleContainsUsesInclusiveEdges()
    {
        WithRect(rect =>
        {
            rect.sizeDelta = new Vector2(100, 80);
            rect.position = new Vector3(10, 20, 0);
            Assert.True(RectTransformUtility.RectangleContainsScreenPoint(rect, new Vector2(-40, -20)));
            Assert.True(RectTransformUtility.RectangleContainsScreenPoint(rect, new Vector2(60, 60), null));
            Assert.False(RectTransformUtility.RectangleContainsScreenPoint(rect, new Vector2(60.01f, 20), null));
        });
    }

    [Fact]
    public void RectangleOffsetInsetsPositiveAndExpandsNegativeLikeUnityProbe()
    {
        WithRect(rect =>
        {
            rect.sizeDelta = new Vector2(100, 80);
            rect.position = new Vector3(10, 20, 0);
            Assert.False(RectTransformUtility.RectangleContainsScreenPoint(rect, new Vector2(-35, 20), null, new Vector4(10, 0, 0, 0)));
            Assert.True(RectTransformUtility.RectangleContainsScreenPoint(rect, new Vector2(-35, 20), null, new Vector4(0, 0, 10, 0)));
            Assert.False(RectTransformUtility.RectangleContainsScreenPoint(rect, new Vector2(55, 20), null, new Vector4(0, 0, 10, 0)));
            Assert.True(RectTransformUtility.RectangleContainsScreenPoint(rect, new Vector2(-45, 20), null, new Vector4(-10, 0, 0, 0)));
        });
    }

    [Fact]
    public void FlipLayoutOnAxisMatchesUnityProbe()
    {
        WithRect(rect =>
        {
            ConfigureForFlip(rect);
            RectTransformUtility.FlipLayoutOnAxis(rect, 0, false, false);
            AssertVector2(new Vector2(.3f, .2f), rect.anchorMin);
            AssertVector2(new Vector2(.9f, .9f), rect.anchorMax);
            AssertVector2(new Vector2(.75f, .75f), rect.pivot);
            AssertVector2(new Vector2(-13, -17), rect.anchoredPosition);
        });
    }

    [Fact]
    public void FlipLayoutOnAxisKeepPositioningOnlyChangesPivot()
    {
        WithRect(rect =>
        {
            ConfigureForFlip(rect);
            RectTransformUtility.FlipLayoutOnAxis(rect, 1, true, false);
            AssertVector2(new Vector2(.1f, .2f), rect.anchorMin);
            AssertVector2(new Vector2(.7f, .9f), rect.anchorMax);
            AssertVector2(new Vector2(.25f, .25f), rect.pivot);
            AssertVector2(new Vector2(13, -17), rect.anchoredPosition);
        });
    }

    [Fact]
    public void FlipLayoutAxesMatchesUnityProbe()
    {
        WithRect(rect =>
        {
            ConfigureForFlip(rect);
            RectTransformUtility.FlipLayoutAxes(rect, false, false);
            AssertVector2(new Vector2(.2f, .1f), rect.anchorMin);
            AssertVector2(new Vector2(.9f, .7f), rect.anchorMax);
            AssertVector2(new Vector2(.75f, .25f), rect.pivot);
            AssertVector2(new Vector2(-17, 13), rect.anchoredPosition);
            AssertVector2(new Vector2(80, 100), rect.sizeDelta);
        });
    }

    [Fact]
    public void RecursiveFlipAlsoUpdatesRectChildren()
    {
        var parentObject = new GameObject("parent", typeof(RectTransform));
        var childObject = new GameObject("child", typeof(RectTransform));
        childObject.transform.SetParent(parentObject.transform, false);
        try
        {
            var parent = (RectTransform)parentObject.transform;
            var child = (RectTransform)childObject.transform;
            child.pivot = new Vector2(.2f, .7f);
            RectTransformUtility.FlipLayoutOnAxis(parent, 0, true, true);
            Assert.Equal(.8f, child.pivot.x, 3);
            Assert.Equal(.7f, child.pivot.y, 3);
        }
        finally { UnityEngine.Object.DestroyImmediate(parentObject); }
    }

    [Fact]
    public void RelativeBoundsIncludeRootAndAllDescendantRects()
    {
        var fixture = CreateBoundsFixture();
        try
        {
            Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(fixture.Root);
            AssertVector3(Vector3.zero, bounds.center);
            AssertVector3(new Vector3(200, 200, 0), bounds.size);
        }
        finally { UnityEngine.Object.DestroyImmediate(fixture.Root.gameObject); }
    }

    [Fact]
    public void RelativeBoundsFromChildExcludeRootRect()
    {
        var fixture = CreateBoundsFixture();
        try
        {
            Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(fixture.Root, fixture.Child);
            AssertVector3(new Vector3(30, 30, 0), bounds.center);
            AssertVector3(new Vector3(40, 60, 0), bounds.size);
        }
        finally { UnityEngine.Object.DestroyImmediate(fixture.Root.gameObject); }
    }

    [Fact]
    public void RelativeBoundsWithoutRectDescendantsAreZero()
    {
        var gameObject = new GameObject("plain");
        try
        {
            Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(gameObject.transform);
            AssertVector3(Vector3.zero, bounds.center);
            AssertVector3(Vector3.zero, bounds.size);
        }
        finally { UnityEngine.Object.DestroyImmediate(gameObject); }
    }

    [Fact]
    public void PixelAdjustmentIsDisabledForNonPixelPerfectAndWorldSpace()
    {
        var fixture = CreatePixelFixture();
        try
        {
            fixture.Canvas.pixelPerfect = false;
            AssertVector2(new Vector2(1.2f, 2.3f), RectTransformUtility.PixelAdjustPoint(new Vector2(1.2f, 2.3f), fixture.Rect, fixture.Canvas));
            fixture.Canvas.pixelPerfect = true;
            fixture.Canvas.renderMode = RenderMode.WorldSpace;
            AssertVector2(new Vector2(1.2f, 2.3f), RectTransformUtility.PixelAdjustPoint(new Vector2(1.2f, 2.3f), fixture.Rect, fixture.Canvas));
        }
        finally { UnityEngine.Object.DestroyImmediate(fixture.Canvas.gameObject); }
    }

    [Fact]
    public void PixelAdjustPointMatchesUnityOverlayProbe()
    {
        var fixture = CreatePixelFixture();
        try
        {
            fixture.Canvas.pixelPerfect = true;
            AssertVector2(new Vector2(1.3f, 2.2f),
                RectTransformUtility.PixelAdjustPoint(new Vector2(1.2f, 2.3f), fixture.Rect, fixture.Canvas), .0001f);
        }
        finally { UnityEngine.Object.DestroyImmediate(fixture.Canvas.gameObject); }
    }

    [Fact]
    public void PixelAdjustRectMatchesUnityOverlayProbe()
    {
        var fixture = CreatePixelFixture();
        try
        {
            fixture.Canvas.pixelPerfect = true;
            Rect rect = RectTransformUtility.PixelAdjustRect(fixture.Rect, fixture.Canvas);
            Assert.Equal(-2.2f, rect.x, 3);
            Assert.Equal(-16.8f, rect.y, 3);
            Assert.Equal(11f, rect.width, 3);
            Assert.Equal(21f, rect.height, 3);
        }
        finally { UnityEngine.Object.DestroyImmediate(fixture.Canvas.gameObject); }
    }

    [Fact]
    public void OrthographicCameraWorldScreenRectRoundTripMatchesUnityProbe()
    {
        var cameraObject = new GameObject("camera", typeof(Camera));
        var rectObject = new GameObject("rect", typeof(RectTransform));
        try
        {
            var camera = cameraObject.GetComponent<Camera>();
            camera.transform.position = new Vector3(0, 0, -10);
            camera.transform.rotation = Quaternion.identity;
            camera.orthographic = true;
            camera.orthographicSize = 5;
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(camera, Vector3.zero);
            Assert.True(RectTransformUtility.ScreenPointToWorldPointInRectangle(
                (RectTransform)rectObject.transform, screen, camera, out Vector3 world));
            AssertVector3(Vector3.zero, world);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(cameraObject);
            UnityEngine.Object.DestroyImmediate(rectObject);
        }
    }

    private static void ConfigureForFlip(RectTransform rect)
    {
        rect.sizeDelta = new Vector2(100, 80);
        rect.anchorMin = new Vector2(.1f, .2f);
        rect.anchorMax = new Vector2(.7f, .9f);
        rect.pivot = new Vector2(.25f, .75f);
        rect.anchoredPosition = new Vector2(13, -17);
    }

    private static (RectTransform Root, RectTransform Child) CreateBoundsFixture()
    {
        var rootObject = new GameObject("root", typeof(RectTransform));
        var childObject = new GameObject("child", typeof(RectTransform));
        var grandObject = new GameObject("grand", typeof(RectTransform));
        var root = (RectTransform)rootObject.transform;
        var child = (RectTransform)childObject.transform;
        var grand = (RectTransform)grandObject.transform;
        root.sizeDelta = new Vector2(200, 200);
        child.SetParent(root, false);
        child.sizeDelta = new Vector2(40, 20);
        child.anchoredPosition = new Vector2(30, 10);
        grand.SetParent(child, false);
        grand.sizeDelta = new Vector2(10, 60);
        grand.anchoredPosition = new Vector2(-15, 20);
        return (root, child);
    }

    private static (Canvas Canvas, RectTransform Rect) CreatePixelFixture()
    {
        var canvasObject = new GameObject("canvas", typeof(RectTransform), typeof(Canvas));
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.scaleFactor = 2f;
        var pixelObject = new GameObject("pixel", typeof(RectTransform));
        var rect = (RectTransform)pixelObject.transform;
        rect.SetParent(canvasObject.transform, false);
        rect.sizeDelta = new Vector2(11.3f, 20.7f);
        rect.pivot = new Vector2(.2f, .8f);
        rect.anchoredPosition = new Vector2(1.2f, 2.3f);
        return (canvas, rect);
    }

    private static void WithRect(Action<RectTransform> action)
    {
        var gameObject = new GameObject("rect", typeof(RectTransform));
        try { action((RectTransform)gameObject.transform); }
        finally { UnityEngine.Object.DestroyImmediate(gameObject); }
    }

    private static void AssertVector2(Vector2 expected, Vector2 actual, float tolerance = Tolerance)
    {
        Assert.InRange(actual.x, expected.x - tolerance, expected.x + tolerance);
        Assert.InRange(actual.y, expected.y - tolerance, expected.y + tolerance);
    }

    private static void AssertVector3(Vector3 expected, Vector3 actual, float tolerance = Tolerance)
    {
        Assert.InRange(actual.x, expected.x - tolerance, expected.x + tolerance);
        Assert.InRange(actual.y, expected.y - tolerance, expected.y + tolerance);
        Assert.InRange(actual.z, expected.z - tolerance, expected.z + tolerance);
    }
}
