using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Xunit;

namespace Anity.Core.Tests;

[Collection(ComponentAttributeBehaviorCollection.Name)]
public sealed class CanvasRendererCanvasGroupTests
{
    [Fact]
    public void RootNamespaceTypesAndMetadataMatchUnitySurface()
    {
        Assert.Equal("UnityEngine.CanvasRenderer", typeof(CanvasRenderer).FullName);
        Assert.Equal("UnityEngine.CanvasGroup", typeof(CanvasGroup).FullName);
        Assert.Equal("UnityEngine.UIVertex", typeof(UIVertex).FullName);
        Assert.Equal("UnityEngine.ICanvasRaycastFilter", typeof(ICanvasRaycastFilter).FullName);
        Assert.True(typeof(CanvasRenderer).IsSealed);
        Assert.True(typeof(CanvasGroup).IsSealed);
        Assert.True(typeof(ICanvasRaycastFilter).IsAssignableFrom(typeof(CanvasGroup)));
        Assert.Null(typeof(CanvasRenderer).Assembly.GetType("UnityEngine.UI.CanvasRenderer"));
        Assert.Null(typeof(CanvasRenderer).Assembly.GetType("UnityEngine.UI.CanvasGroup"));
        Assert.Null(typeof(CanvasRenderer).Assembly.GetType("UnityEngine.UI.UIVertex"));
    }

    [Fact]
    public void RendererDefaultsMatchUnityProbe()
    {
        WithRenderer((_, renderer) =>
        {
            Assert.False(renderer.hasPopInstruction);
            Assert.Equal(0, renderer.materialCount);
            Assert.Equal(0, renderer.popMaterialCount);
            Assert.Equal(-1, renderer.absoluteDepth);
            Assert.Equal(-1, renderer.relativeDepth);
            Assert.True(renderer.hasMoved);
            Assert.True(renderer.cullTransparentMesh);
            Assert.False(renderer.hasRectClipping);
            Assert.False(renderer.cull);
            Assert.Equal(Vector2.zero, renderer.clippingSoftness);
            Assert.Equal(Color.white, renderer.GetColor());
            Assert.Equal(1f, renderer.GetAlpha());
            Assert.Null(renderer.GetMesh());
        });
    }

    [Fact]
    public void CanvasGroupDefaultsAndRaycastFilterMatchUnityProbe()
    {
        var gameObject = new GameObject("group", typeof(CanvasGroup));
        try
        {
            var group = gameObject.GetComponent<CanvasGroup>();
            Assert.Equal(1f, group.alpha);
            Assert.True(group.interactable);
            Assert.True(group.blocksRaycasts);
            Assert.False(group.ignoreParentGroups);
            Assert.True(group.IsRaycastLocationValid(Vector2.zero, null!));
            group.blocksRaycasts = false;
            Assert.False(group.IsRaycastLocationValid(new Vector2(10, 20), null!));
        }
        finally { UnityEngine.Object.DestroyImmediate(gameObject); }
    }

    [Fact]
    public void SetAlphaUpdatesColorAlphaLikeUnityProbe()
    {
        WithRenderer((_, renderer) =>
        {
            renderer.SetColor(new Color(.1f, .2f, .3f, .4f));
            renderer.SetAlpha(.25f);
            Assert.Equal(.25f, renderer.GetAlpha(), 3);
            Assert.Equal(new Color(.1f, .2f, .3f, .25f), renderer.GetColor());
        });
    }

    [Fact]
    public void InheritedAlphaMultipliesCanvasGroupsButNotRendererAlpha()
    {
        var parent = new GameObject("parent", typeof(CanvasGroup));
        var child = new GameObject("child", typeof(CanvasGroup), typeof(CanvasRenderer));
        child.transform.SetParent(parent.transform, false);
        try
        {
            parent.GetComponent<CanvasGroup>().alpha = .5f;
            child.GetComponent<CanvasGroup>().alpha = .4f;
            var renderer = child.GetComponent<CanvasRenderer>();
            renderer.SetAlpha(.8f);
            Assert.Equal(.2f, renderer.GetInheritedAlpha(), 3);
        }
        finally { UnityEngine.Object.DestroyImmediate(parent); }
    }

    [Fact]
    public void IgnoreParentGroupsStopsInheritedAlphaAtChild()
    {
        var parent = new GameObject("parent", typeof(CanvasGroup));
        var child = new GameObject("child", typeof(CanvasGroup), typeof(CanvasRenderer));
        child.transform.SetParent(parent.transform, false);
        try
        {
            parent.GetComponent<CanvasGroup>().alpha = .5f;
            var childGroup = child.GetComponent<CanvasGroup>();
            childGroup.alpha = .4f;
            childGroup.ignoreParentGroups = true;
            Assert.Equal(.4f, child.GetComponent<CanvasRenderer>().GetInheritedAlpha(), 3);
        }
        finally { UnityEngine.Object.DestroyImmediate(parent); }
    }

    [Fact]
    public void MaterialSlotsAndDefaultGetterMatchUnityProbe()
    {
        WithRenderer((_, renderer) =>
        {
            var first = new Material(Shader.Find("UI/Default"));
            var second = new Material(Shader.Find("UI/Default"));
            try
            {
                renderer.materialCount = 2;
                renderer.SetMaterial(first, 0);
                renderer.SetMaterial(second, 1);
                Assert.Same(first, renderer.GetMaterial());
                Assert.Same(first, renderer.GetMaterial(0));
                Assert.Same(second, renderer.GetMaterial(1));
                Assert.Null(renderer.GetMaterial(-1));
                Assert.Null(renderer.GetMaterial(2));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(first);
                UnityEngine.Object.DestroyImmediate(second);
            }
        });
    }

    [Fact]
    public void PopMaterialSlotsRoundTrip()
    {
        WithRenderer((_, renderer) =>
        {
            var material = new Material(Shader.Find("UI/Default"));
            try
            {
                renderer.popMaterialCount = 2;
                renderer.SetPopMaterial(material, 1);
                Assert.Equal(2, renderer.popMaterialCount);
                Assert.Same(material, renderer.GetPopMaterial(1));
                Assert.Null(renderer.GetPopMaterial(-1));
                Assert.Null(renderer.GetPopMaterial(2));
            }
            finally { UnityEngine.Object.DestroyImmediate(material); }
        });
    }

    [Fact]
    public void RectClippingAndSoftnessRoundTrip()
    {
        WithRenderer((_, renderer) =>
        {
            renderer.EnableRectClipping(new Rect(1, 2, 3, 4));
            renderer.clippingSoftness = new Vector2(5, 6);
            Assert.True(renderer.hasRectClipping);
            Assert.Equal(new Vector2(5, 6), renderer.clippingSoftness);
            renderer.DisableRectClipping();
            Assert.False(renderer.hasRectClipping);
        });
    }

    [Fact]
    public void SplitVertexStreamsMatchesUnityProbe()
    {
        List<UIVertex> vertices = CreateVertices();
        var positions = new List<Vector3>(); var colors = new List<Color32>();
        var uv0 = new List<Vector4>(); var uv1 = new List<Vector4>();
        var normals = new List<Vector3>(); var tangents = new List<Vector4>(); var indices = new List<int>();
        CanvasRenderer.SplitUIVertexStreams(vertices, positions, colors, uv0, uv1, normals, tangents, indices);
        Assert.Equal(4, positions.Count);
        Assert.Equal(new[] { 0, 1, 2, 3 }, indices);
        Assert.Equal(new Vector3(3, 4, 5), positions[2]);
        Assert.Equal(new Vector4(2.1f, 2.2f, 2.3f, 2.4f), uv0[2]);
    }

    [Fact]
    public void CreateVertexStreamUsesIndexOrder()
    {
        List<UIVertex> source = CreateVertices();
        var positions = source.Select(vertex => vertex.position).ToList();
        var colors = source.Select(vertex => vertex.color).ToList();
        var uv0 = source.Select(vertex => vertex.uv0).ToList();
        var uv1 = source.Select(vertex => vertex.uv1).ToList();
        var normals = source.Select(vertex => vertex.normal).ToList();
        var tangents = source.Select(vertex => vertex.tangent).ToList();
        var output = new List<UIVertex>();
        CanvasRenderer.CreateUIVertexStream(output, positions, colors, uv0, uv1, normals, tangents, new List<int> { 3, 1, 0 });
        Assert.Equal(3, output.Count);
        Assert.Equal(new Vector3(4, 5, 6), output[0].position);
        Assert.Equal(new Vector3(2, 3, 4), output[1].position);
    }

    [Fact]
    public void AddVertexStreamReplacesOutputListsLikeUnityProbe()
    {
        List<UIVertex> vertices = CreateVertices();
        var positions = new List<Vector3> { new(99, 99, 99) };
        CanvasRenderer.AddUIVertexStream(vertices, positions, new List<Color32>(), new List<Vector4>(),
            new List<Vector4>(), new List<Vector3>(), new List<Vector4>());
        Assert.Equal(4, positions.Count);
        Assert.Equal(new Vector3(1, 2, 3), positions[0]);
        Assert.Equal(new Vector3(4, 5, 6), positions[3]);
    }

    [Fact]
    public void LegacySetVerticesBuildsQuadMesh()
    {
        WithRenderer((_, renderer) =>
        {
#pragma warning disable CS0618
            renderer.SetVertices(CreateVertices());
#pragma warning restore CS0618
            Assert.NotNull(renderer.GetMesh());
            Assert.Equal(4, renderer.GetMesh()!.vertexCount);
        });
    }

    [Fact]
    public void ClearReleasesGeometryAndMaterialSlotsLikeUnityProbe()
    {
        WithRenderer((_, renderer) =>
        {
            var material = new Material(Shader.Find("UI/Default"));
            try
            {
                renderer.materialCount = 1;
                renderer.SetMaterial(material, 0);
                renderer.SetMesh(new Mesh());
                renderer.Clear();
                Assert.Null(renderer.GetMesh());
                Assert.Null(renderer.GetMaterial(0));
                Assert.Equal(0, renderer.materialCount);
            }
            finally { UnityEngine.Object.DestroyImmediate(material); }
        });
    }

    [Fact]
    public void UIVertexSimpleVertMatchesUnityDefaults()
    {
        UIVertex vertex = UIVertex.simpleVert;
        Assert.Equal(Vector3.zero, vertex.position);
        Assert.Equal(new Vector3(0, 0, -1), vertex.normal);
        Assert.Equal(new Vector4(1, 0, 0, -1), vertex.tangent);
        Assert.Equal(new Color32(255, 255, 255, 255), vertex.color);
        Assert.Equal(Vector4.zero, vertex.uv0);
        Assert.Equal(Vector4.zero, vertex.uv3);
    }

    [Fact]
    public void RebuildEventHasOfficialNestedDelegateType()
    {
        EventInfo rebuild = typeof(CanvasRenderer).GetEvent(nameof(CanvasRenderer.onRequestRebuild))!;
        Assert.Equal(typeof(CanvasRenderer.OnRequestRebuild), rebuild.EventHandlerType);
        CanvasRenderer.OnRequestRebuild handler = () => { };
        CanvasRenderer.onRequestRebuild += handler;
        CanvasRenderer.onRequestRebuild -= handler;
    }

    private static List<UIVertex> CreateVertices()
    {
        var vertices = new List<UIVertex>();
        for (int i = 0; i < 4; i++)
        {
            UIVertex vertex = UIVertex.simpleVert;
            vertex.position = new Vector3(i + 1, i + 2, i + 3);
            vertex.color = new Color32((byte)(10 + i), (byte)(20 + i), (byte)(30 + i), (byte)(40 + i));
            vertex.uv0 = new Vector4(i + .1f, i + .2f, i + .3f, i + .4f);
            vertex.uv1 = new Vector4(i + 1.1f, i + 1.2f, i + 1.3f, i + 1.4f);
            vertices.Add(vertex);
        }
        return vertices;
    }

    private static void WithRenderer(Action<GameObject, CanvasRenderer> action)
    {
        var gameObject = new GameObject("renderer", typeof(CanvasRenderer));
        try { action(gameObject, gameObject.GetComponent<CanvasRenderer>()); }
        finally { UnityEngine.Object.DestroyImmediate(gameObject); }
    }
}
