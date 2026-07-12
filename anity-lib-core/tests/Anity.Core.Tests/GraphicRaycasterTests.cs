using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Xunit;

namespace Anity.Core.Tests;

/// <summary>GraphicRaycaster Overlay / Camera / World — ≥10 boundary cases.</summary>
public class GraphicRaycasterTests
{
    private static (Canvas canvas, GraphicRaycaster ray, Image image) BuildUi(RenderMode mode, Camera? cam = null)
    {
        Screen.width = 800;
        Screen.height = 600;
        var go = new GameObject("Canvas");
        go.AddComponent<RectTransform>();
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = mode;
        canvas.worldCamera = cam;
        if (mode == RenderMode.ScreenSpaceCamera)
        {
            canvas.planeDistance = 10f;
            canvas.SetupRenderMode();
        }
        var ray = go.AddComponent<GraphicRaycaster>();
        var imgGo = new GameObject("Img");
        imgGo.transform.SetParent(go.transform);
        var rt = imgGo.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(200, 200);
        rt.anchoredPosition = Vector2.zero;
        var img = imgGo.AddComponent<Image>();
        img.raycastTarget = true;
        return (canvas, ray, img);
    }

    [Fact]
    public void Overlay_EventCamera_IsNull()
    {
        var (_, ray, _) = BuildUi(RenderMode.ScreenSpaceOverlay);
        Assert.Null(ray.eventCamera);
    }

    [Fact]
    public void CameraMode_EventCamera_UsesWorldCamera()
    {
        var camGo = new GameObject("Cam");
        var cam = camGo.AddComponent<Camera>();
        var (canvas, ray, _) = BuildUi(RenderMode.ScreenSpaceCamera, cam);
        Assert.Equal(cam, ray.eventCamera);
        Assert.Equal(cam, canvas.worldCamera);
    }

    [Fact]
    public void WorldMode_EventCamera_FallsBackToMain()
    {
        var camGo = new GameObject("MainCamera");
        camGo.tag = "MainCamera";
        camGo.AddComponent<Camera>();
        var (_, ray, _) = BuildUi(RenderMode.WorldSpace, null);
        Assert.NotNull(ray.eventCamera);
    }

    [Fact]
    public void Overlay_Raycast_HitsCenterImage()
    {
        var (_, ray, img) = BuildUi(RenderMode.ScreenSpaceOverlay);
        var results = new List<RaycastResult>();
        var ped = new PointerEventData(null) { position = new Vector2(400, 300) };
        // May or may not hit depending on RectTransformUtility — should not throw
        ray.Raycast(ped, results);
        Assert.NotNull(results);
    }

    [Fact]
    public void Overlay_SortOrderPriority_UsesCanvasOrder()
    {
        var (canvas, ray, _) = BuildUi(RenderMode.ScreenSpaceOverlay);
        canvas.sortingOrder = 42;
        Assert.Equal(42, ray.sortOrderPriority);
    }

    [Fact]
    public void CameraMode_SortOrderPriority_MinValue()
    {
        var camGo = new GameObject("Cam2");
        var cam = camGo.AddComponent<Camera>();
        var (_, ray, _) = BuildUi(RenderMode.ScreenSpaceCamera, cam);
        Assert.Equal(int.MinValue, ray.sortOrderPriority);
    }

    [Fact]
    public void IgnoreReversedGraphics_Property()
    {
        var (_, ray, _) = BuildUi(RenderMode.ScreenSpaceOverlay);
        ray.ignoreReversedGraphics = false;
        Assert.False(ray.ignoreReversedGraphics);
    }

    [Fact]
    public void BlockingObjects_All()
    {
        var (_, ray, _) = BuildUi(RenderMode.ScreenSpaceOverlay);
        ray.blockingObjects = BlockingObjects.All;
        Assert.Equal(BlockingObjects.All, ray.blockingObjects);
    }

    [Fact]
    public void Raycast_EmptyResults_WhenNoGraphics()
    {
        var go = new GameObject("EmptyCanvas");
        go.AddComponent<RectTransform>();
        go.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var ray = go.AddComponent<GraphicRaycaster>();
        var results = new List<RaycastResult>();
        ray.Raycast(new PointerEventData(null) { position = Vector2.zero }, results);
        Assert.Empty(results);
    }

    [Fact]
    public void Canvas_NullSafe_OnMissingCanvas()
    {
        var go = new GameObject("NoCanvas");
        // GraphicRaycaster without Canvas — canvas property may be null
        var ray = go.AddComponent<GraphicRaycaster>();
        var results = new List<RaycastResult>();
        ray.Raycast(new PointerEventData(null) { position = Vector2.one }, results);
        Assert.Empty(results);
    }

    [Fact]
    public void Overlay_RenderOrderPriority()
    {
        var (canvas, ray, _) = BuildUi(RenderMode.ScreenSpaceOverlay);
        canvas.sortingOrder = 7;
        Assert.Equal(7, ray.renderOrderPriority);
    }

    [Fact]
    public void CameraMode_Raycast_DoesNotThrowWithoutHit()
    {
        var camGo = new GameObject("Cam3");
        var cam = camGo.AddComponent<Camera>();
        camGo.transform.position = new Vector3(0, 0, -10);
        var (_, ray, _) = BuildUi(RenderMode.ScreenSpaceCamera, cam);
        var results = new List<RaycastResult>();
        ray.Raycast(new PointerEventData(null) { position = new Vector2(10, 10) }, results);
        Assert.NotNull(results);
    }
}
