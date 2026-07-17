using UnityEngine;
using UnityEngine.UI;
using Xunit;

namespace Anity.Core.Tests;

/// <summary>Canvas Overlay/Camera/World + CanvasScaler — ≥12 edge cases.</summary>
[Collection("ScreenState")] // Screen is process-global; serialize against other Screen mutators
public class CanvasTests
{
    public CanvasTests()
    {
        Screen.width = 1920;
        Screen.height = 1080;
        Screen.dpi = 96f;
    }

    private static (GameObject go, Canvas canvas, CanvasScaler scaler, RectTransform rt) CreateRoot(RenderMode mode)
    {
        var go = new GameObject("Canvas");
        var rt = go.AddComponent<RectTransform>();
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = mode;
        var scaler = go.AddComponent<CanvasScaler>();
        return (go, canvas, scaler, rt);
    }

    [Fact]
    public void Overlay_Default_IsRoot()
    {
        var (_, canvas, _, _) = CreateRoot(RenderMode.ScreenSpaceOverlay);
        Assert.Equal(RenderMode.ScreenSpaceOverlay, canvas.renderMode);
        Assert.True(canvas.isRootCanvas);
    }

    [Fact]
    public void Overlay_PixelRect_MatchesScreen()
    {
        var (_, canvas, _, _) = CreateRoot(RenderMode.ScreenSpaceOverlay);
        Assert.Equal(Screen.width, (int)canvas.pixelRect.width);
        Assert.Equal(Screen.height, (int)canvas.pixelRect.height);
    }

    [Fact]
    public void Scaler_ScaleWithScreenSize_MatchWidth()
    {
        var (_, canvas, scaler, rt) = CreateRoot(RenderMode.ScreenSpaceOverlay);
        scaler.uiScaleMode = UnityEngine.UI.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0f; // width
        scaler.HandleNow();
        Assert.InRange(canvas.scaleFactor, 0.99f, 1.01f);
        Assert.True(rt.sizeDelta.x > 0);
    }

    [Fact]
    public void Scaler_Expand_UsesMinScale()
    {
        int ow = Screen.width, oh = Screen.height;
        try
        {
            Screen.width = 1280;
            Screen.height = 720;
            var (_, canvas, scaler, _) = CreateRoot(RenderMode.ScreenSpaceOverlay);
            scaler.uiScaleMode = UnityEngine.UI.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = ScreenMatchMode.Expand;
            float s = scaler.CalculateScaleFactor();
            float expected = Mathf.Min((float)Screen.width / 1920f, (float)Screen.height / 1080f);
            Assert.InRange(s, expected - 0.001f, expected + 0.001f);
            Assert.Equal(s, canvas.scaleFactor, 3);
        }
        finally
        {
            Screen.width = ow;
            Screen.height = oh;
        }
    }

    [Fact]
    public void Scaler_Shrink_UsesMaxScale()
    {
        int ow = Screen.width, oh = Screen.height;
        try
        {
            Screen.width = 2560;
            Screen.height = 1440;
            var (_, canvas, scaler, _) = CreateRoot(RenderMode.ScreenSpaceOverlay);
            scaler.uiScaleMode = UnityEngine.UI.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = ScreenMatchMode.Shrink;
            float s = scaler.CalculateScaleFactor();
            float expected = Mathf.Max((float)Screen.width / 1920f, (float)Screen.height / 1080f);
            Assert.InRange(s, expected - 0.001f, expected + 0.001f);
        }
        finally
        {
            Screen.width = ow;
            Screen.height = oh;
        }
    }

    [Fact]
    public void Scaler_ConstantPixelSize()
    {
        var (_, canvas, scaler, _) = CreateRoot(RenderMode.ScreenSpaceOverlay);
        scaler.uiScaleMode = UnityEngine.UI.ScaleMode.ConstantPixelSize;
        scaler.uiScaleFactor = 2f;
        scaler.HandleNow();
        Assert.Equal(2f, canvas.scaleFactor);
    }

    [Fact]
    public void Scaler_ConstantPhysicalSize_Points()
    {
        Screen.dpi = 96f;
        var (_, canvas, scaler, _) = CreateRoot(RenderMode.ScreenSpaceOverlay);
        scaler.uiScaleMode = UnityEngine.UI.ScaleMode.ConstantPhysicalSize;
        scaler.physicalUnit = Unit.Points;
        scaler.physicalUnitFactor = 1f;
        float s = scaler.CalculateScaleFactor();
        Assert.True(s > 0f);
        Assert.Equal(s, canvas.scaleFactor);
    }

    [Fact]
    public void ScreenSpaceCamera_PlacesAtPlaneDistance()
    {
        var camGo = new GameObject("Cam");
        var cam = camGo.AddComponent<Camera>();
        camGo.transform.position = new Vector3(0, 0, -10);
        camGo.transform.rotation = Quaternion.identity;

        var (_, canvas, _, _) = CreateRoot(RenderMode.ScreenSpaceCamera);
        canvas.worldCamera = cam;
        canvas.planeDistance = 5f;
        var expected = cam.transform.position + cam.transform.forward * 5f;
        Assert.True(Vector3.Distance(canvas.transform.position, expected) < 0.01f);
    }

    [Fact]
    public void ScreenSpaceCamera_NullCamera_FallsBackToMain()
    {
        var camGo = new GameObject("MainCamera");
        camGo.tag = "MainCamera";
        camGo.AddComponent<Camera>();
        var (_, canvas, _, _) = CreateRoot(RenderMode.ScreenSpaceCamera);
        canvas.worldCamera = null;
        Assert.NotNull(canvas.worldCamera);
    }

    [Fact]
    public void WorldSpace_KeepsAuthoringScale_WhenNoScalerBlowup()
    {
        var (_, canvas, scaler, rt) = CreateRoot(RenderMode.WorldSpace);
        rt.localScale = Vector3.one;
        Assert.Equal(RenderMode.WorldSpace, canvas.renderMode);
        // scaler world path multiplies localScale
        scaler.uiScaleMode = UnityEngine.UI.ScaleMode.ConstantPixelSize;
        scaler.uiScaleFactor = 1.5f;
        scaler.HandleNow();
        Assert.InRange(rt.localScale.x, 1.49f, 1.51f);
    }

    [Fact]
    public void NestedCanvas_IsNotRoot()
    {
        var (rootGo, root, _, _) = CreateRoot(RenderMode.ScreenSpaceOverlay);
        var childGo = new GameObject("ChildCanvas");
        childGo.transform.SetParent(rootGo.transform);
        childGo.AddComponent<RectTransform>();
        var child = childGo.AddComponent<Canvas>();
        Assert.True(root.isRootCanvas);
        Assert.False(child.isRootCanvas);
        Assert.Equal(root, child.rootCanvas);
    }

    [Fact]
    public void SortingOrder_AffectsSortKey()
    {
        var (_, a, _, _) = CreateRoot(RenderMode.ScreenSpaceOverlay);
        var (_, b, _, _) = CreateRoot(RenderMode.ScreenSpaceOverlay);
        a.sortingOrder = 10;
        b.sortingOrder = 1;
        Assert.NotEqual(a.sortingOrder, b.sortingOrder);
        Assert.Equal(10, a.renderOrder);
    }

    [Fact]
    public void ForceUpdateCanvases_DoesNotThrow()
    {
        CreateRoot(RenderMode.ScreenSpaceOverlay);
        Canvas.ForceUpdateCanvases();
    }

    [Fact]
    public void AdditionalShaderChannels_Flags()
    {
        var (_, canvas, _, _) = CreateRoot(RenderMode.ScreenSpaceOverlay);
        canvas.additionalShaderChannels = AdditionalCanvasShaderChannels.Normal | AdditionalCanvasShaderChannels.Tangent;
        Assert.True((canvas.additionalShaderChannels & AdditionalCanvasShaderChannels.Normal) != 0);
        Assert.True((canvas.additionalShaderChannels & AdditionalCanvasShaderChannels.Tangent) != 0);
        canvas.additionalShaderChannels = AdditionalCanvasShaderChannels.None;
        Assert.Equal(AdditionalCanvasShaderChannels.None, canvas.additionalShaderChannels);
    }

    [Fact]
    public void Overlay_RootSizeDelta_EqualsScreenOverScale()
    {
        Screen.width = 800;
        Screen.height = 600;
        var (_, canvas, scaler, rt) = CreateRoot(RenderMode.ScreenSpaceOverlay);
        scaler.uiScaleMode = UnityEngine.UI.ScaleMode.ConstantPixelSize;
        scaler.uiScaleFactor = 2f;
        scaler.HandleNow();
        Assert.Equal(2f, canvas.scaleFactor);
        Assert.Equal(400f, rt.sizeDelta.x, 1);
        Assert.Equal(300f, rt.sizeDelta.y, 1);
    }
}
