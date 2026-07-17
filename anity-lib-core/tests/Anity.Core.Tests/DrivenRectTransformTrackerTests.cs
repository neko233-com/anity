using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using Xunit;

namespace Anity.Core.Tests;

[Collection(ComponentAttributeBehaviorCollection.Name)]
public sealed class DrivenRectTransformTrackerTests
{
    [Fact]
    public void EnumNamesAndValuesMatchUnity2022Probe()
    {
        var expected = new (string Name, int Value)[]
        {
            ("None", 0), ("AnchoredPositionX", 2), ("AnchoredPositionY", 4),
            ("AnchoredPosition", 6), ("AnchoredPositionZ", 8), ("AnchoredPosition3D", 14),
            ("Rotation", 16), ("ScaleX", 32), ("ScaleY", 64), ("ScaleZ", 128),
            ("Scale", 224), ("AnchorMinX", 256), ("AnchorMinY", 512),
            ("AnchorMin", 768), ("AnchorMaxX", 1024), ("AnchorMaxY", 2048),
            ("AnchorMax", 3072), ("Anchors", 3840), ("SizeDeltaX", 4096),
            ("SizeDeltaY", 8192), ("SizeDelta", 12288), ("PivotX", 16384),
            ("PivotY", 32768), ("Pivot", 49152), ("All", -1)
        };

        Assert.True(typeof(DrivenTransformProperties).IsDefined(typeof(FlagsAttribute), false));
        Assert.Equal(expected.Select(item => item.Name), Enum.GetNames(typeof(DrivenTransformProperties)));
        Assert.Equal(expected.Select(item => item.Value),
            Enum.GetValues(typeof(DrivenTransformProperties)).Cast<DrivenTransformProperties>().Select(value => (int)value));
    }

    [Fact]
    public void PublicSurfaceContainsUnityMethods()
    {
        string[] methods = typeof(DrivenRectTransformTracker)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Select(method => method.ToString()!)
            .OrderBy(value => value)
            .ToArray();
        Assert.Equal(new[]
        {
            "Void Add(UnityEngine.Object, UnityEngine.RectTransform, UnityEngine.DrivenTransformProperties)",
            "Void Clear()",
            "Void Clear(Boolean)",
            "Void StartRecordingUndo()",
            "Void StopRecordingUndo()"
        }, methods);
    }

    [Fact]
    public void DefaultTrackerClearIsNoOp()
    {
        default(DrivenRectTransformTracker).Clear();
    }

    [Fact]
    public void AddSetsDrivenObject()
    {
        WithRect((driver, rect) =>
        {
            var tracker = new DrivenRectTransformTracker();
            tracker.Add(driver, rect, DrivenTransformProperties.AnchoredPosition);
            Assert.Same(driver, rect.drivenByObject);
            Assert.Equal(DrivenTransformProperties.AnchoredPosition, ReadDrivenProperties(rect));
        });
    }

    [Fact]
    public void NullDriverIsAccepted()
    {
        WithRect((_, rect) =>
        {
            var tracker = new DrivenRectTransformTracker();
            tracker.Add(null!, rect, DrivenTransformProperties.SizeDelta);
            Assert.Null(rect.drivenByObject);
            Assert.Equal(DrivenTransformProperties.SizeDelta, ReadDrivenProperties(rect));
        });
    }

    [Fact]
    public void NullRectThrowsNullReferenceLikeUnityProbe()
    {
        var tracker = new DrivenRectTransformTracker();
        var driver = new GameObject("driver");
        try
        {
            Assert.Throws<NullReferenceException>(() => tracker.Add(driver, null!, DrivenTransformProperties.All));
        }
        finally { UnityEngine.Object.DestroyImmediate(driver); }
    }

    [Fact]
    public void ClearReleasesDrivenObjectAndProperties()
    {
        WithRect((driver, rect) =>
        {
            var tracker = new DrivenRectTransformTracker();
            tracker.Add(driver, rect, DrivenTransformProperties.Pivot);
            tracker.Clear();
            Assert.Null(rect.drivenByObject);
            Assert.Equal(DrivenTransformProperties.None, ReadDrivenProperties(rect));
        });
    }

    [Fact]
    public void StructCopySharesTrackedRegistrationLikeUnityProbe()
    {
        WithRect((driver, rect) =>
        {
            var tracker = new DrivenRectTransformTracker();
            tracker.Add(driver, rect, DrivenTransformProperties.Scale);
            var copy = tracker;
            copy.Clear();
            Assert.Null(rect.drivenByObject);
            tracker.Clear();
        });
    }

    [Fact]
    public void ObsoleteClearOverloadDelegatesToClear()
    {
        WithRect((driver, rect) =>
        {
            var tracker = new DrivenRectTransformTracker();
            tracker.Add(driver, rect, DrivenTransformProperties.Rotation);
#pragma warning disable CS0618
            tracker.Clear(false);
#pragma warning restore CS0618
            Assert.Null(rect.drivenByObject);
        });
    }

    [Fact]
    public void ClearReleasesEveryTrackedRect()
    {
        var driver = new GameObject("driver");
        var firstObject = new GameObject("first", typeof(RectTransform));
        var secondObject = new GameObject("second", typeof(RectTransform));
        try
        {
            var first = (RectTransform)firstObject.transform;
            var second = (RectTransform)secondObject.transform;
            var tracker = new DrivenRectTransformTracker();
            tracker.Add(driver, first, DrivenTransformProperties.AnchorMin);
            tracker.Add(driver, second, DrivenTransformProperties.AnchorMax);
            tracker.Clear();
            Assert.Null(first.drivenByObject);
            Assert.Null(second.drivenByObject);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(driver);
            UnityEngine.Object.DestroyImmediate(firstObject);
            UnityEngine.Object.DestroyImmediate(secondObject);
        }
    }

    [Fact]
    public void UndoRecordingMethodsAreCallable()
    {
        DrivenRectTransformTracker.StartRecordingUndo();
        DrivenRectTransformTracker.StopRecordingUndo();
    }

    [Fact]
    public void ContentSizeFitterTracksAndReleasesItsRect()
    {
        var gameObject = new GameObject("fitter", typeof(RectTransform));
        try
        {
            var fitter = gameObject.AddComponent<ContentSizeFitter>();
            fitter.SetLayoutHorizontal();
            Assert.Same(fitter, ((RectTransform)gameObject.transform).drivenByObject);
            fitter.enabled = false;
            Assert.Null(((RectTransform)gameObject.transform).drivenByObject);
        }
        finally { UnityEngine.Object.DestroyImmediate(gameObject); }
    }

    [Fact]
    public void AspectRatioFitterTracksControlledAxisAndReleasesOnDisable()
    {
        var gameObject = new GameObject("aspect", typeof(RectTransform));
        try
        {
            var fitter = gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.Mode.WidthControlsHeight;
            Assert.Same(fitter, ((RectTransform)gameObject.transform).drivenByObject);
            fitter.enabled = false;
            Assert.Null(((RectTransform)gameObject.transform).drivenByObject);
        }
        finally { UnityEngine.Object.DestroyImmediate(gameObject); }
    }

    [Fact]
    public void LayoutGroupTracksChildAndReleasesOnDisable()
    {
        var parent = new GameObject("layout", typeof(RectTransform));
        var child = new GameObject("child", typeof(RectTransform));
        child.transform.SetParent(parent.transform, false);
        try
        {
            var group = parent.AddComponent<HorizontalLayoutGroup>();
            group.CalculateLayoutInputHorizontal();
            group.SetLayoutHorizontal();
            Assert.Same(group, ((RectTransform)child.transform).drivenByObject);
            group.enabled = false;
            Assert.Null(((RectTransform)child.transform).drivenByObject);
        }
        finally { UnityEngine.Object.DestroyImmediate(parent); }
    }

    private static DrivenTransformProperties ReadDrivenProperties(RectTransform rect)
    {
        return (DrivenTransformProperties)typeof(RectTransform)
            .GetProperty("drivenProperties", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(rect)!;
    }

    private static void WithRect(Action<GameObject, RectTransform> action)
    {
        var driver = new GameObject("driver");
        var rectObject = new GameObject("rect", typeof(RectTransform));
        try { action(driver, (RectTransform)rectObject.transform); }
        finally
        {
            UnityEngine.Object.DestroyImmediate(driver);
            UnityEngine.Object.DestroyImmediate(rectObject);
        }
    }
}
