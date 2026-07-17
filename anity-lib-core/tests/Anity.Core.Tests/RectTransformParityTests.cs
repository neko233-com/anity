using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Xunit;

namespace Anity.Core.Tests;

[Collection(ComponentAttributeBehaviorCollection.Name)]
public sealed class RectTransformParityTests
{
    private const float Tolerance = 0.001f;

    [Fact]
    public void GameObjectRectTransformReplacesBaseTransformLikeUnity()
    {
        var gameObject = new GameObject("rect", typeof(RectTransform));
        try
        {
            RectTransform rect = Assert.IsType<RectTransform>(gameObject.transform);
            Assert.Same(rect, gameObject.GetComponent<Transform>());
            Assert.Same(rect, gameObject.GetComponent<RectTransform>());
            Assert.Single(gameObject.GetComponents<Transform>());
        }
        finally { UnityEngine.Object.DestroyImmediate(gameObject); }
    }

    [Fact]
    public void AddingRectTransformPreservesExistingTransformStateAndChildren()
    {
        var parent = new GameObject("parent");
        var child = new GameObject("child");
        parent.transform.localPosition = new Vector3(7, -3, 11);
        parent.transform.localRotation = Quaternion.Euler(10, 20, 30);
        parent.transform.localScale = new Vector3(2, 3, 4);
        child.transform.SetParent(parent.transform, false);
        try
        {
            RectTransform rect = parent.AddComponent<RectTransform>();
            Assert.Same(rect, parent.transform);
            AssertVector(new Vector3(7, -3, 11), rect.localPosition);
            AssertVector(new Vector3(2, 3, 4), rect.localScale);
            Assert.Same(rect, child.transform.parent);
            Assert.Same(child.transform, rect.GetChild(0));
        }
        finally { UnityEngine.Object.DestroyImmediate(parent); }
    }

    [Fact]
    public void NestedPublicTypesAndForceMethodMatchUnitySurface()
    {
        Assert.Equal(new[] { "Horizontal", "Vertical" }, Enum.GetNames(typeof(RectTransform.Axis)));
        Assert.Equal(new[] { "Left", "Right", "Top", "Bottom" }, Enum.GetNames(typeof(RectTransform.Edge)));
        Assert.Equal(typeof(void), typeof(RectTransform.ReapplyDrivenProperties).GetMethod("Invoke")!.ReturnType);
        MethodInfo method = typeof(RectTransform).GetMethod(nameof(RectTransform.ForceUpdateRectTransforms))!;
        Assert.Contains(method.GetCustomAttributes(false), attribute => attribute.GetType().FullName == "UnityEngine.Bindings.NativeMethodAttribute");
        Assert.Null(typeof(RectTransform).Assembly.GetType("UnityEngine.Axis"));
        Assert.Null(typeof(RectTransform).Assembly.GetType("UnityEngine.Edge"));
    }

    [Fact]
    public void DefaultsMatchUnity2022Probe()
    {
        var gameObject = new GameObject("rect", typeof(RectTransform));
        try
        {
            var rect = (RectTransform)gameObject.transform;
            AssertVector2(new Vector2(0.5f, 0.5f), rect.anchorMin);
            AssertVector2(new Vector2(0.5f, 0.5f), rect.anchorMax);
            AssertVector2(new Vector2(0.5f, 0.5f), rect.pivot);
            AssertVector2(new Vector2(100, 100), rect.sizeDelta);
            AssertVector2(Vector2.zero, rect.anchoredPosition);
            AssertRect(new Rect(-50, -50, 100, 100), rect.rect);
            AssertVector2(new Vector2(-50, -50), rect.offsetMin);
            AssertVector2(new Vector2(50, 50), rect.offsetMax);
            Assert.Null(rect.drivenByObject);
        }
        finally { UnityEngine.Object.DestroyImmediate(gameObject); }
    }

    [Fact]
    public void StretchAnchorsPivotAndPositionMatchUnityProbe()
    {
        var fixture = CreateConfiguredFixture();
        try
        {
            RectTransform child = fixture.Child;
            AssertVector(new Vector3(63, -103, 0), child.localPosition);
            AssertVector2(new Vector2(15, -25), child.anchoredPosition);
            AssertVector2(new Vector2(3, -13), child.offsetMin);
            AssertVector2(new Vector2(43, -33), child.offsetMax);
            AssertRect(new Rect(-180, -240, 600, 400), child.rect);
        }
        finally { UnityEngine.Object.DestroyImmediate(fixture.Parent.gameObject); }
    }

    [Fact]
    public void AnchoredPosition3DSynchronizesXYAndLocalZ()
    {
        var fixture = CreateConfiguredFixture();
        try
        {
            fixture.Child.anchoredPosition3D = new Vector3(31, -47, 9);
            AssertVector2(new Vector2(31, -47), fixture.Child.anchoredPosition);
            AssertVector(new Vector3(79, -125, 9), fixture.Child.localPosition);
            AssertVector(new Vector3(31, -47, 9), fixture.Child.anchoredPosition3D);
        }
        finally { UnityEngine.Object.DestroyImmediate(fixture.Parent.gameObject); }
    }

    [Fact]
    public void LocalPositionSetterRecomputesAnchoredPosition()
    {
        var fixture = CreateConfiguredFixture();
        try
        {
            fixture.Child.localPosition = new Vector3(123, -88, 13);
            AssertVector2(new Vector2(75, -10), fixture.Child.anchoredPosition);
            AssertVector(new Vector3(75, -10, 13), fixture.Child.anchoredPosition3D);
            AssertVector2(new Vector2(63, 2), fixture.Child.offsetMin);
            AssertVector2(new Vector2(103, -18), fixture.Child.offsetMax);
        }
        finally { UnityEngine.Object.DestroyImmediate(fixture.Parent.gameObject); }
    }

    [Fact]
    public void OffsetMinSetterChangesSizeAndPositionLikeUnity()
    {
        var fixture = CreateConfiguredFixture();
        try
        {
            fixture.Child.localPosition = new Vector3(123, -88, 13);
            fixture.Child.offsetMin = new Vector2(-11, 22);
            AssertVector2(new Vector2(114, -40), fixture.Child.sizeDelta);
            AssertVector2(new Vector2(23.2f, -2), fixture.Child.anchoredPosition);
            AssertVector2(new Vector2(-11, 22), fixture.Child.offsetMin);
            AssertVector2(new Vector2(103, -18), fixture.Child.offsetMax);
            AssertRect(new Rect(-202.2f, -228, 674, 380), fixture.Child.rect);
        }
        finally { UnityEngine.Object.DestroyImmediate(fixture.Parent.gameObject); }
    }

    [Fact]
    public void OffsetMaxSetterPreservesOffsetMinLikeUnity()
    {
        var fixture = CreateConfiguredFixture();
        try
        {
            fixture.Child.localPosition = new Vector3(123, -88, 13);
            fixture.Child.offsetMin = new Vector2(-11, 22);
            fixture.Child.offsetMax = new Vector2(33, -44);
            AssertVector2(new Vector2(44, -66), fixture.Child.sizeDelta);
            AssertVector2(new Vector2(2.2f, -17.6f), fixture.Child.anchoredPosition);
            AssertVector2(new Vector2(-11, 22), fixture.Child.offsetMin);
            AssertVector2(new Vector2(33, -44), fixture.Child.offsetMax);
            AssertRect(new Rect(-181.2f, -212.4f, 604, 354), fixture.Child.rect);
        }
        finally { UnityEngine.Object.DestroyImmediate(fixture.Parent.gameObject); }
    }

    [Fact]
    public void SetSizeWithCurrentAnchorsSubtractsParentStretch()
    {
        var fixture = CreateConfiguredFixture();
        try
        {
            fixture.Child.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 500);
            Assert.Equal(-60f, fixture.Child.sizeDelta.x, 3);
            Assert.Equal(500f, fixture.Child.rect.width, 3);
            fixture.Child.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 250);
            Assert.Equal(-170f, fixture.Child.sizeDelta.y, 3);
            Assert.Equal(250f, fixture.Child.rect.height, 3);
        }
        finally { UnityEngine.Object.DestroyImmediate(fixture.Parent.gameObject); }
    }

    [Fact]
    public void SetInsetFromLeftMatchesUnityProbe()
    {
        var fixture = CreateConfiguredFixture();
        try
        {
            fixture.Child.SetInsetAndSizeFromParentEdge(RectTransform.Edge.Left, 17, 91);
            AssertVector2(new Vector2(0, 0.2f), fixture.Child.anchorMin);
            AssertVector2(new Vector2(0, 0.9f), fixture.Child.anchorMax);
            AssertVector2(new Vector2(91, -20), fixture.Child.sizeDelta);
            AssertVector2(new Vector2(44.3f, -25), fixture.Child.anchoredPosition);
            Assert.Equal(17f, fixture.Child.offsetMin.x, 3);
        }
        finally { UnityEngine.Object.DestroyImmediate(fixture.Parent.gameObject); }
    }

    [Fact]
    public void SetInsetFromRightMatchesUnityProbe()
    {
        var fixture = CreateConfiguredFixture();
        try
        {
            fixture.Child.SetInsetAndSizeFromParentEdge(RectTransform.Edge.Right, 17, 91);
            Assert.Equal(1f, fixture.Child.anchorMin.x);
            Assert.Equal(1f, fixture.Child.anchorMax.x);
            Assert.Equal(-80.7f, fixture.Child.anchoredPosition.x, 3);
            Assert.Equal(-17f, fixture.Child.offsetMax.x, 3);
        }
        finally { UnityEngine.Object.DestroyImmediate(fixture.Parent.gameObject); }
    }

    [Fact]
    public void SetInsetFromTopAndBottomMatchUnityProbe()
    {
        var fixture = CreateConfiguredFixture();
        try
        {
            fixture.Child.SetInsetAndSizeFromParentEdge(RectTransform.Edge.Top, 17, 91);
            Assert.Equal(1f, fixture.Child.anchorMin.y);
            Assert.Equal(1f, fixture.Child.anchorMax.y);
            Assert.Equal(-53.4f, fixture.Child.anchoredPosition.y, 3);
            Assert.Equal(-17f, fixture.Child.offsetMax.y, 3);

            ConfigureChild(fixture.Child);
            fixture.Child.SetInsetAndSizeFromParentEdge(RectTransform.Edge.Bottom, 17, 91);
            Assert.Equal(0f, fixture.Child.anchorMin.y);
            Assert.Equal(0f, fixture.Child.anchorMax.y);
            Assert.Equal(71.6f, fixture.Child.anchoredPosition.y, 3);
            Assert.Equal(17f, fixture.Child.offsetMin.y, 3);
        }
        finally { UnityEngine.Object.DestroyImmediate(fixture.Parent.gameObject); }
    }

    [Fact]
    public void LocalCornersUseUnityBottomLeftTopLeftTopRightBottomRightOrder()
    {
        var fixture = CreateConfiguredFixture();
        try
        {
            Vector3[] corners = new Vector3[4];
            fixture.Child.GetLocalCorners(corners);
            AssertVector(new Vector3(-180, -240, 0), corners[0]);
            AssertVector(new Vector3(-180, 160, 0), corners[1]);
            AssertVector(new Vector3(420, 160, 0), corners[2]);
            AssertVector(new Vector3(420, -240, 0), corners[3]);
        }
        finally { UnityEngine.Object.DestroyImmediate(fixture.Parent.gameObject); }
    }

    [Fact]
    public void NullAndShortCornerArraysAreIgnoredLikeUnity()
    {
        var gameObject = new GameObject("rect", typeof(RectTransform));
        try
        {
            var rect = (RectTransform)gameObject.transform;
            rect.GetLocalCorners(null!);
            rect.GetWorldCorners(null!);
            var shortArray = new Vector3[3];
            rect.GetLocalCorners(shortArray);
            rect.GetWorldCorners(shortArray);
            Assert.All(shortArray, value => Assert.Equal(Vector3.zero, value));
        }
        finally { UnityEngine.Object.DestroyImmediate(gameObject); }
    }

    [Fact]
    public void WorldCornersIncludeAnchorReferenceAndZ()
    {
        var fixture = CreateConfiguredFixture();
        try
        {
            fixture.Child.SetInsetAndSizeFromParentEdge(RectTransform.Edge.Bottom, 17, 91);
            fixture.Child.localPosition = new Vector3(fixture.Child.localPosition.x, fixture.Child.localPosition.y, 13);
            Vector3[] corners = new Vector3[4];
            fixture.Child.GetWorldCorners(corners);
            AssertVector(new Vector3(-117, -433, 13), corners[0]);
            AssertVector(new Vector3(-117, -342, 13), corners[1]);
            AssertVector(new Vector3(483, -342, 13), corners[2]);
            AssertVector(new Vector3(483, -433, 13), corners[3]);
        }
        finally { UnityEngine.Object.DestroyImmediate(fixture.Parent.gameObject); }
    }

    [Fact]
    public void ForceUpdateDoesNotRaiseReapplyEventForUndrivenRect()
    {
        var gameObject = new GameObject("rect", typeof(RectTransform));
        int callbacks = 0;
        RectTransform.ReapplyDrivenProperties handler = _ => callbacks++;
        RectTransform.reapplyDrivenProperties += handler;
        try
        {
            var rect = (RectTransform)gameObject.transform;
            rect.anchoredPosition = new Vector2(1, 2);
            rect.ForceUpdateRectTransforms();
            Assert.Equal(0, callbacks);
            Assert.Null(rect.drivenByObject);
        }
        finally
        {
            RectTransform.reapplyDrivenProperties -= handler;
            UnityEngine.Object.DestroyImmediate(gameObject);
        }
    }

    [Fact]
    public void ChangingAnchorsKeepsLocalPositionAndRecomputesAnchoredPosition()
    {
        var fixture = CreateConfiguredFixture();
        try
        {
            Vector3 local = fixture.Child.localPosition;
            fixture.Child.anchorMin = new Vector2(0.25f, 0.4f);
            fixture.Child.anchorMax = new Vector2(0.75f, 0.6f);
            AssertVector(local, fixture.Child.localPosition);
            Vector2 expectedReference = new Vector2(120, -138);
            AssertVector2(new Vector2(local.x - expectedReference.x, local.y - expectedReference.y), fixture.Child.anchoredPosition);
        }
        finally { UnityEngine.Object.DestroyImmediate(fixture.Parent.gameObject); }
    }

    private static (RectTransform Parent, RectTransform Child) CreateConfiguredFixture()
    {
        var parentObject = new GameObject("rect-parent", typeof(RectTransform));
        var childObject = new GameObject("rect-child", typeof(RectTransform));
        var parent = (RectTransform)parentObject.transform;
        var child = (RectTransform)childObject.transform;
        child.SetParent(parent, false);
        parent.sizeDelta = new Vector2(800, 600);
        parent.pivot = new Vector2(0.25f, 0.75f);
        ConfigureChild(child);
        return (parent, child);
    }

    private static void ConfigureChild(RectTransform child)
    {
        child.anchorMin = new Vector2(0.1f, 0.2f);
        child.anchorMax = new Vector2(0.8f, 0.9f);
        child.pivot = new Vector2(0.3f, 0.6f);
        child.sizeDelta = new Vector2(40, -20);
        child.anchoredPosition = new Vector2(15, -25);
    }

    private static void AssertVector2(Vector2 expected, Vector2 actual)
    {
        Assert.InRange(MathF.Abs(expected.x - actual.x), 0, Tolerance);
        Assert.InRange(MathF.Abs(expected.y - actual.y), 0, Tolerance);
    }

    private static void AssertVector(Vector3 expected, Vector3 actual)
    {
        Assert.InRange(MathF.Abs(expected.x - actual.x), 0, Tolerance);
        Assert.InRange(MathF.Abs(expected.y - actual.y), 0, Tolerance);
        Assert.InRange(MathF.Abs(expected.z - actual.z), 0, Tolerance);
    }

    private static void AssertRect(Rect expected, Rect actual)
    {
        Assert.InRange(MathF.Abs(expected.x - actual.x), 0, Tolerance);
        Assert.InRange(MathF.Abs(expected.y - actual.y), 0, Tolerance);
        Assert.InRange(MathF.Abs(expected.width - actual.width), 0, Tolerance);
        Assert.InRange(MathF.Abs(expected.height - actual.height), 0, Tolerance);
    }
}
