using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Anity.Core.Runtime.Native;
using UnityEngine;
using Xunit;

namespace Anity.Core.Tests;

[Collection(ComponentAttributeBehaviorCollection.Name)]
public sealed class CanvasRendererNativeTests
{
    [Fact]
    public void NativeUIStructLayoutsMatchCAbi()
    {
        Assert.Equal(12, Marshal.SizeOf<AnityNative.UIVector3>());
        Assert.Equal(16, Marshal.SizeOf<AnityNative.UIVector4>());
        Assert.Equal(4, Marshal.SizeOf<AnityNative.UIColor32>());
        Assert.Equal(108, Marshal.SizeOf<AnityNative.UIVertexNative>());
        Assert.Equal(108, Marshal.SizeOf<AnityNative.UIPackedVertex>());
        Assert.Equal(24, Marshal.SizeOf<AnityNative.UIBounds>());
        Assert.Equal(68, Marshal.SizeOf<AnityNative.UIRenderState>());
        Assert.Equal(36, Marshal.SizeOf<AnityNative.UIVisibility>());
    }

    [Fact]
    public void NativeVertexPackingPreservesPositionColorNormalAndTangent()
    {
        AnityNative.UIVertexNative[] source = CreateNativeVertices(4);
        bool resolved = AnityNative.TryPackUIVertices(source, source.Length, out var packed, out _);
        AssertNativeResolved(resolved);
        if (!resolved) return;
        Assert.Equal(source.Length, packed.Length);
        AssertVector(source[2].position, packed[2].position);
        AssertVector(source[2].normal, packed[2].normal);
        AssertVector(source[2].tangent, packed[2].tangent);
        Assert.Equal(source[2].color.r, packed[2].color.r);
        Assert.Equal(source[2].color.a, packed[2].color.a);
    }

    [Fact]
    public void NativeVertexPackingPreservesAllFourUvStreams()
    {
        AnityNative.UIVertexNative[] source = CreateNativeVertices(4);
        bool resolved = AnityNative.TryPackUIVertices(source, source.Length, out var packed, out _);
        AssertNativeResolved(resolved);
        if (!resolved) return;
        AssertVector(source[3].uv0, packed[3].uv0);
        AssertVector(source[3].uv1, packed[3].uv1);
        AssertVector(source[3].uv2, packed[3].uv2);
        AssertVector(source[3].uv3, packed[3].uv3);
    }

    [Fact]
    public void NativeVertexPackingCalculatesThreeDimensionalBounds()
    {
        AnityNative.UIVertexNative[] source = CreateNativeVertices(4);
        source[0].position = new AnityNative.UIVector3(-4, 8, 2);
        source[1].position = new AnityNative.UIVector3(3, -5, 7);
        bool resolved = AnityNative.TryPackUIVertices(source, source.Length, out _, out var bounds);
        AssertNativeResolved(resolved);
        if (!resolved) return;
        AssertVector(new AnityNative.UIVector3(-4, -5, 2), bounds.min);
        AssertVector(new AnityNative.UIVector3(4, 8, 7), bounds.max);
    }

    [Fact]
    public void NativeVertexPackingAcceptsEmptyGeometry()
    {
        bool resolved = AnityNative.TryPackUIVertices(Array.Empty<AnityNative.UIVertexNative>(), 0, out var packed, out var bounds);
        AssertNativeResolved(resolved);
        if (!resolved) return;
        Assert.Empty(packed);
        AssertVector(default, bounds.min);
        AssertVector(default, bounds.max);
    }

    [Fact]
    public void NativeVertexPackingRejectsCountPastInput()
    {
        Assert.False(AnityNative.TryPackUIVertices(CreateNativeVertices(4), 5, out var packed, out _));
        Assert.Empty(packed);
    }

    [Fact]
    public void NativeVertexPackingRejectsNonFinitePosition()
    {
        AnityNative.UIVertexNative[] source = CreateNativeVertices(4);
        source[2].position.x = float.NaN;
        bool resolved = AnityNative.TryPackUIVertices(source, source.Length, out var packed, out _);
        Assert.False(resolved);
        Assert.Empty(packed);
    }

    [Fact]
    public void NativeQuadIndicesMatchUnityLegacyWinding()
    {
        bool resolved = AnityNative.TryBuildUIQuadIndices(4, out int[] indices);
        AssertNativeResolved(resolved);
        if (!resolved) return;
        Assert.Equal(new[] { 0, 1, 2, 2, 3, 0 }, indices);
    }

    [Fact]
    public void NativeQuadIndicesOffsetEachAdditionalQuad()
    {
        bool resolved = AnityNative.TryBuildUIQuadIndices(8, out int[] indices);
        AssertNativeResolved(resolved);
        if (!resolved) return;
        Assert.Equal(new[] { 0, 1, 2, 2, 3, 0, 4, 5, 6, 6, 7, 4 }, indices);
    }

    [Fact]
    public void NativeQuadIndicesHandleEmptyAndRejectPartialQuad()
    {
        bool emptyResolved = AnityNative.TryBuildUIQuadIndices(0, out int[] empty);
        AssertNativeResolved(emptyResolved);
        if (emptyResolved) Assert.Empty(empty);
        Assert.False(AnityNative.TryBuildUIQuadIndices(6, out int[] invalid));
        Assert.Empty(invalid);
    }

    [Fact]
    public void NativeVisibilityMultipliesAlphaAndCullsTransparentMesh()
    {
        AnityNative.UIRenderState state = VisibleState();
        state.colorAlpha = .5f;
        state.inheritedAlpha = .4f;
        bool resolved = AnityNative.TryEvaluateUIVisibility(state, out var visibility);
        AssertNativeResolved(resolved);
        if (!resolved) return;
        Assert.Equal(.2f, visibility.effectiveAlpha, 4);
        Assert.Equal(1, visibility.visible);

        state.colorAlpha = 0;
        Assert.True(AnityNative.TryEvaluateUIVisibility(state, out visibility));
        Assert.Equal(1, visibility.culledByAlpha);
        Assert.Equal(0, visibility.visible);
    }

    [Fact]
    public void NativeVisibilityCullsBoundsOutsideRect()
    {
        AnityNative.UIRenderState state = VisibleState();
        state.bounds.min = new AnityNative.UIVector3(20, 20, 0);
        state.bounds.max = new AnityNative.UIVector3(30, 30, 0);
        bool resolved = AnityNative.TryEvaluateUIVisibility(state, out var visibility);
        AssertNativeResolved(resolved);
        if (!resolved) return;
        Assert.Equal(1, visibility.culledByClip);
        Assert.Equal(0, visibility.visible);
    }

    [Fact]
    public void NativeVisibilityMarksPartialIntersectionAsClipped()
    {
        AnityNative.UIRenderState state = VisibleState();
        state.bounds.min = new AnityNative.UIVector3(-2, 2, 0);
        state.bounds.max = new AnityNative.UIVector3(5, 12, 0);
        bool resolved = AnityNative.TryEvaluateUIVisibility(state, out var visibility);
        AssertNativeResolved(resolved);
        if (!resolved) return;
        Assert.Equal(1, visibility.visible);
        Assert.Equal(1, visibility.clipped);
        Assert.Equal(0, visibility.culledByClip);
    }

    [Fact]
    public void NativeVisibilityClampsNegativeSoftnessAndComputesInnerClip()
    {
        AnityNative.UIRenderState state = VisibleState();
        state.softnessX = -2;
        state.softnessY = 3;
        bool resolved = AnityNative.TryEvaluateUIVisibility(state, out var visibility);
        AssertNativeResolved(resolved);
        if (!resolved) return;
        Assert.Equal(0, visibility.innerClipXMin);
        Assert.Equal(10, visibility.innerClipXMax);
        Assert.Equal(3, visibility.innerClipYMin);
        Assert.Equal(7, visibility.innerClipYMax);
    }

    [Fact]
    public void CanvasRendererLegacyMeshUsesNativePackedDataAndBounds()
    {
        var gameObject = new GameObject("native-canvas-renderer", typeof(CanvasRenderer));
        try
        {
            var vertices = CreateUnityVertices();
#pragma warning disable CS0618
            gameObject.GetComponent<CanvasRenderer>().SetVertices(vertices);
#pragma warning restore CS0618
            Mesh mesh = gameObject.GetComponent<CanvasRenderer>().GetMesh()!;
            Assert.Equal(4, mesh.vertexCount);
            Assert.Equal(new[] { 0, 1, 2, 2, 3, 0 }, mesh.GetIndices(0));
            Assert.Equal(vertices[2].position, mesh.vertices[2]);
            Assert.Equal(vertices[2].color, mesh.colors32[2]);
            Assert.Equal(new Vector3(2.5f, 3.5f, 4.5f), mesh.bounds.center);
            Assert.Equal(new Vector3(3, 3, 3), mesh.bounds.size);
        }
        finally { UnityEngine.Object.DestroyImmediate(gameObject); }
    }

    private static AnityNative.UIRenderState VisibleState()
        => new()
        {
            bounds = new AnityNative.UIBounds
            {
                min = new AnityNative.UIVector3(2, 2, 0),
                max = new AnityNative.UIVector3(8, 8, 0)
            },
            clipXMin = 0,
            clipYMin = 0,
            clipXMax = 10,
            clipYMax = 10,
            colorAlpha = 1,
            inheritedAlpha = 1,
            hasGeometry = 1,
            rectClipping = 1,
            cullTransparentMesh = 1
        };

    private static AnityNative.UIVertexNative[] CreateNativeVertices(int count)
    {
        var result = new AnityNative.UIVertexNative[count];
        for (int index = 0; index < count; index++)
        {
            result[index] = new AnityNative.UIVertexNative
            {
                position = new AnityNative.UIVector3(index + 1, index + 2, index + 3),
                normal = new AnityNative.UIVector3(index + .1f, index + .2f, index + .3f),
                tangent = new AnityNative.UIVector4(index + 1.1f, index + 1.2f, index + 1.3f, index + 1.4f),
                color = new AnityNative.UIColor32((byte)(10 + index), (byte)(20 + index), (byte)(30 + index), (byte)(40 + index)),
                uv0 = Uv(index, 0),
                uv1 = Uv(index, 10),
                uv2 = Uv(index, 20),
                uv3 = Uv(index, 30)
            };
        }
        return result;
    }

    private static AnityNative.UIVector4 Uv(int index, int stream)
        => new(stream + index + .1f, stream + index + .2f, stream + index + .3f, stream + index + .4f);

    private static List<UIVertex> CreateUnityVertices()
    {
        var result = new List<UIVertex>();
        for (int index = 0; index < 4; index++)
        {
            UIVertex vertex = UIVertex.simpleVert;
            vertex.position = new Vector3(index + 1, index + 2, index + 3);
            vertex.color = new Color32((byte)(10 + index), (byte)(20 + index), (byte)(30 + index), (byte)(40 + index));
            vertex.uv0 = new Vector4(index + .1f, index + .2f, index + .3f, index + .4f);
            vertex.uv1 = new Vector4(index + 1.1f, index + 1.2f, index + 1.3f, index + 1.4f);
            result.Add(vertex);
        }
        return result;
    }

    private static void AssertNativeResolved(bool resolved)
    {
        if (Environment.GetEnvironmentVariable("ANITY_REQUIRE_NATIVE") == "1")
            Assert.True(resolved);
    }

    private static void AssertVector(AnityNative.UIVector3 expected, AnityNative.UIVector3 actual)
    {
        Assert.Equal(expected.x, actual.x, 5);
        Assert.Equal(expected.y, actual.y, 5);
        Assert.Equal(expected.z, actual.z, 5);
    }

    private static void AssertVector(AnityNative.UIVector4 expected, AnityNative.UIVector4 actual)
    {
        Assert.Equal(expected.x, actual.x, 5);
        Assert.Equal(expected.y, actual.y, 5);
        Assert.Equal(expected.z, actual.z, 5);
        Assert.Equal(expected.w, actual.w, 5);
    }
}
