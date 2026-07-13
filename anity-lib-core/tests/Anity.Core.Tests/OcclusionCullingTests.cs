using System;
using UnityEngine;
using Xunit;

namespace Anity.Core.Tests;

/// <summary>Umbra-style OcclusionCulling + StaticOcclusionCulling + CullingGroup — ≥12 cases.</summary>
public class OcclusionCullingTests : IDisposable
{
    public OcclusionCullingTests()
    {
        OcclusionCulling.Clear();
        StaticOcclusionCulling.Clear();
        OcclusionCulling.enabled = true;
    }

    public void Dispose()
    {
        OcclusionCulling.Clear();
        StaticOcclusionCulling.Clear();
    }

    [Fact]
    public void Enabled_DefaultTrue()
    {
        Assert.True(OcclusionCulling.enabled);
    }

    [Fact]
    public void Disabled_AlwaysVisible()
    {
        OcclusionCulling.enabled = false;
        Assert.True(OcclusionCulling.IsVisible(Vector3.zero, new Bounds(Vector3.one * 100f, Vector3.one)));
    }

    [Fact]
    public void Bake_CreatesCells()
    {
        OcclusionCulling.Bake(new Bounds(Vector3.zero, Vector3.one * 40f), 4);
        Assert.True(OcclusionCulling.cellCount > 0);
    }

    [Fact]
    public void Bake_SubdivisionsClamped()
    {
        OcclusionCulling.Bake(new Bounds(Vector3.zero, Vector3.one * 10f), 0);
        Assert.True(OcclusionCulling.cellCount >= 1);
        OcclusionCulling.Clear();
        OcclusionCulling.Bake(new Bounds(Vector3.zero, Vector3.one * 10f), 100);
        Assert.True(OcclusionCulling.cellCount > 0);
    }

    [Fact]
    public void IsVisible_IntersectingCell_True()
    {
        OcclusionCulling.Bake(new Bounds(Vector3.zero, new Vector3(20, 10, 20)), 4);
        Assert.True(OcclusionCulling.IsVisible(Vector3.zero, new Bounds(Vector3.zero, Vector3.one * 2f)));
    }

    [Fact]
    public void SetCellVisible_False_StillConservative()
    {
        OcclusionCulling.Bake(new Bounds(Vector3.zero, Vector3.one * 20f), 2);
        for (int i = 0; i < OcclusionCulling.cellCount; i++)
            OcclusionCulling.SetCellVisible(i, false);
        // No visible cell intersection → conservative true
        Assert.True(OcclusionCulling.IsVisible(Vector3.zero, new Bounds(new Vector3(100, 0, 0), Vector3.one)));
    }

    [Fact]
    public void GetCellBounds_ValidIndex()
    {
        OcclusionCulling.Bake(new Bounds(Vector3.zero, Vector3.one * 16f), 2);
        var b = OcclusionCulling.GetCellBounds(0);
        Assert.True(b.size.magnitude > 0);
    }

    [Fact]
    public void GetCellBounds_Invalid_Default()
    {
        OcclusionCulling.Clear();
        Assert.Equal(default, OcclusionCulling.GetCellBounds(0));
        Assert.Equal(default, OcclusionCulling.GetCellBounds(-1));
    }

    [Fact]
    public void ClosedPortal_BlocksIntersectingTarget()
    {
        var portalGo = new GameObject("portal");
        var portal = portalGo.AddComponent<OcclusionPortal>();
        portal.open = false;
        portal.center = Vector3.zero;
        portal.size = new Vector3(10, 10, 2);
        OcclusionCulling.RegisterPortal(portal);

        var target = new Bounds(Vector3.zero, new Vector3(2, 2, 2));
        Assert.False(OcclusionCulling.IsVisible(new Vector3(0, 0, -20), target));

        portal.open = true;
        Assert.True(OcclusionCulling.IsVisible(new Vector3(0, 0, -20), target));

        OcclusionCulling.UnregisterPortal(portal);
        UnityEngine.Object.DestroyImmediate(portalGo);
    }

    [Fact]
    public void RegisterArea_RebuildsCells()
    {
        var go = new GameObject("area");
        var area = go.AddComponent<OcclusionArea>();
        area.center = Vector3.zero;
        area.size = new Vector3(40, 10, 40);
        OcclusionCulling.RegisterArea(area);
        Assert.True(OcclusionCulling.cellCount > 0);
        OcclusionCulling.UnregisterArea(area);
        UnityEngine.Object.DestroyImmediate(go);
    }

    [Fact]
    public void QueryCount_Increments()
    {
        int before = OcclusionCulling.queryCount;
        OcclusionCulling.IsVisible(Vector3.zero, new Bounds(Vector3.zero, Vector3.one));
        Assert.Equal(before + 1, OcclusionCulling.queryCount);
    }

    [Fact]
    public void StaticOcclusionCulling_Compute_Bakes()
    {
        Assert.True(StaticOcclusionCulling.Compute());
        Assert.False(StaticOcclusionCulling.isRunning);
        Assert.True(StaticOcclusionCulling.umbraDataSize > 0);
        Assert.Contains("umbra", StaticOcclusionCulling.latestBakeLog);
    }

    [Fact]
    public void StaticOcclusionCulling_Cancel_And_Clear()
    {
        StaticOcclusionCulling.Compute();
        StaticOcclusionCulling.Cancel();
        Assert.Contains("cancel", StaticOcclusionCulling.latestBakeLog);
        StaticOcclusionCulling.Clear();
        Assert.Equal(0, OcclusionCulling.cellCount);
        Assert.Equal(0, StaticOcclusionCulling.umbraDataSize);
    }

    [Fact]
    public void CullingGroup_Query_SetsVisibility()
    {
        using var group = new CullingGroup();
        group.SetBoundingSpheres(new[]
        {
            new BoundingSphere(Vector3.zero, 1f),
            new BoundingSphere(new Vector3(0, 0, 1000), 1f)
        });
        group.SetBoundingDistances(new[] { 50f, 200f });
        group.Query(Vector3.zero);
        Assert.True(group.IsVisible(0));
        Assert.False(group.IsVisible(1)); // beyond last band
        Assert.Equal(0, group.GetDistance(0));
    }

    [Fact]
    public void CullingGroup_StateChanged_Fires()
    {
        using var group = new CullingGroup();
        int events = 0;
        group.onStateChanged = _ => events++;
        group.SetBoundingSpheres(new[] { new BoundingSphere(Vector3.zero, 2f) });
        group.Query(Vector3.zero);
        Assert.True(events >= 1);
    }

    [Fact]
    public void StaticBatchingUtility_Combine_MarksStatic()
    {
        var root = new GameObject("batch");
        var child = new GameObject("c");
        child.transform.SetParent(root.transform);
        StaticBatchingUtility.Combine(root);
        Assert.True(root.isStatic);
        Assert.True(child.isStatic);
        UnityEngine.Object.DestroyImmediate(root);
    }
}
