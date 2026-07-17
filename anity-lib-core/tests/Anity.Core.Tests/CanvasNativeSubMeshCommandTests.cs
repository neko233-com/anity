using System;
using Anity.Core.Runtime.Native;
using UnityEngine;
using Xunit;
using GraphicsDeviceType = UnityEngine.Rendering.GraphicsDeviceType;

namespace Anity.Core.Tests;

[Collection(ComponentAttributeBehaviorCollection.Name)]
public sealed class CanvasNativeSubMeshCommandTests : IDisposable
{
    private readonly GameObject _root;
    private readonly GameObject _child;
    private readonly CanvasRenderer _renderer;
    private readonly NativeGraphicsDevice _device;

    public CanvasNativeSubMeshCommandTests()
    {
        CanvasNativeRenderBridge.ResetForTests();
        _root = new GameObject("submesh-command-canvas");
        _root.AddComponent<RectTransform>();
        _root.AddComponent<Canvas>();
        _child = new GameObject("submesh-command-renderer");
        _child.AddComponent<RectTransform>();
        _child.transform.SetParent(_root.transform, false);
        _renderer = _child.AddComponent<CanvasRenderer>();
        _renderer.SetMesh(TwoSubMeshQuad());
        _device = NativeGraphicsDevice.Create(GraphicsDeviceType.Null, 64, 64, false);
    }

    [Fact]
    public void SingleSubMeshKeepsRendererObjectId()
    {
        _renderer.SetMesh(SingleSubMeshQuad());
        AssertCommands(out var commands);
        Assert.Single(commands);
        Assert.Equal(Id(_renderer), commands[0].Desc.rendererId);
    }

    [Fact]
    public void EachNonEmptySubMeshBecomesAnIndependentCommand()
    {
        AssertCommands(out var commands);
        Assert.Equal(2, commands.Length);
        Assert.Equal(new uint[] { 0, 1, 2 }, commands[0].Indices);
        Assert.Equal(new uint[] { 2, 3, 0 }, commands[1].Indices);
    }

    [Fact]
    public void MultiSubMeshCommandIdsAreDistinctAndStable()
    {
        AssertCommands(out var first);
        AssertCommands(out var second);
        Assert.NotEqual(first[0].Desc.rendererId, first[1].Desc.rendererId);
        Assert.Equal(first[0].Desc.rendererId, second[0].Desc.rendererId);
        Assert.Equal(first[1].Desc.rendererId, second[1].Desc.rendererId);
    }

    [Fact]
    public void MaterialSlotMatchesOriginalSubMeshIndex()
    {
        var first = new Material();
        var second = new Material();
        _renderer.materialCount = 2;
        _renderer.SetMaterial(first, 0);
        _renderer.SetMaterial(second, 1);
        AssertCommands(out var commands);
        Assert.Equal(Id(first), commands[0].Desc.materialId);
        Assert.Equal(Id(second), commands[1].Desc.materialId);
    }

    [Fact]
    public void MissingMaterialSlotFallsBackToSlotZero()
    {
        var first = new Material();
        _renderer.materialCount = 1;
        _renderer.SetMaterial(first, 0);
        AssertCommands(out var commands);
        Assert.All(commands, command => Assert.Equal(Id(first), command.Desc.materialId));
    }

    [Fact]
    public void ExplicitCanvasTextureOverridesMaterialTextures()
    {
        var first = new Material { mainTexture = new Texture2D(1, 1) };
        var second = new Material { mainTexture = new Texture2D(1, 1) };
        var canvasTexture = new Texture2D(1, 1);
        _renderer.materialCount = 2;
        _renderer.SetMaterial(first, 0);
        _renderer.SetMaterial(second, 1);
        _renderer.SetTexture(canvasTexture);
        AssertCommands(out var commands);
        Assert.All(commands, command => Assert.Equal(Id(canvasTexture), command.Desc.textureId));
    }

    [Fact]
    public void MaterialMainTextureIsUsedWhenCanvasTextureIsUnset()
    {
        var firstTexture = new Texture2D(1, 1);
        var secondTexture = new Texture2D(1, 1);
        var first = new Material { mainTexture = firstTexture };
        var second = new Material { mainTexture = secondTexture };
        _renderer.materialCount = 2;
        _renderer.SetMaterial(first, 0);
        _renderer.SetMaterial(second, 1);
        AssertCommands(out var commands);
        Assert.Equal(Id(firstTexture), commands[0].Desc.textureId);
        Assert.Equal(Id(secondTexture), commands[1].Desc.textureId);
    }

    [Fact]
    public void AlphaTextureIsCarriedByEverySubMeshCommand()
    {
        var alpha = new Texture2D(1, 1);
        _renderer.SetAlphaTexture(alpha);
        AssertCommands(out var commands);
        Assert.All(commands, command => Assert.Equal(Id(alpha), command.Desc.alphaTextureId));
    }

    [Fact]
    public void EmptySubMeshIsSkippedWithoutChangingLaterMaterialSlot()
    {
        Mesh mesh = TwoSubMeshQuad();
        mesh.subMeshCount = 3;
        mesh.SetIndices(Array.Empty<int>(), MeshTopology.Triangles, 1);
        mesh.SetIndices(new[] { 2, 3, 0 }, MeshTopology.Triangles, 2);
        var first = new Material();
        var third = new Material();
        _renderer.materialCount = 3;
        _renderer.SetMaterial(first, 0);
        _renderer.SetMaterial(third, 2);
        _renderer.SetMesh(mesh);
        AssertCommands(out var commands);
        Assert.Equal(2, commands.Length);
        Assert.Equal(Id(first), commands[0].Desc.materialId);
        Assert.Equal(Id(third), commands[1].Desc.materialId);
    }

    [Fact]
    public void NonTriangleSubMeshIsRejectedBeforeQueueMutation()
    {
        Mesh mesh = SingleSubMeshQuad();
        mesh.SetIndices(new[] { 0, 1, 1, 2, 2, 3 }, MeshTopology.Lines, 0);
        _renderer.SetMesh(mesh);
        Assert.False(CanvasNativeRenderBridge.TryBuildCommands(
            _renderer, _device, 4, out _));
    }

    [Fact]
    public void InvalidIndexInLaterSubMeshRejectsWholeRenderer()
    {
        Mesh mesh = TwoSubMeshQuad();
        mesh.SetIndices(new[] { 2, 3, 99 }, MeshTopology.Triangles, 1);
        _renderer.SetMesh(mesh);
        Assert.False(CanvasNativeRenderBridge.TryBuildCommands(
            _renderer, _device, 4, out _));
    }

    [Fact]
    public void RendererStateAndSortDepthAreSharedAcrossSubMeshCommands()
    {
        _renderer.EnableRectClipping(new Rect(1, 2, 20, 30));
        _renderer.clippingSoftness = new Vector2(3, 4);
        Assert.True(CanvasNativeRenderBridge.TryBuildCommands(
            _renderer, _device, 17, out var commands));
        Assert.All(commands, command =>
        {
            Assert.Equal(17, command.Desc.sortDepth);
            Assert.True(command.Desc.flags.HasFlag(AnityNative.UIRenderCommandFlags.RectClip));
            Assert.Equal(3, command.Desc.softnessX);
            Assert.Equal(4, command.Desc.softnessY);
        });
    }

    [Fact]
    public void FrameFlushSubmitsTwoMaterialBatchesToNativeCanvas()
    {
        if (!AnityNative.Available) return;
        var first = new Material();
        var second = new Material();
        _renderer.materialCount = 2;
        _renderer.SetMaterial(first, 0);
        _renderer.SetMaterial(second, 1);
        Assert.True(_device.CreateSwapchain(64, 64));

        _device.BeginFrame();
        _device.EndFrame();

        NativeUICanvas canvas = Assert.IsType<NativeUICanvas>(CanvasNativeRenderBridge.CurrentCanvas);
        AnityNative.UICanvasStats stats = canvas.GetStats();
        Assert.Equal(2, stats.commandCount);
        Assert.Equal(2, stats.visibleCommandCount);
        Assert.Equal(2, stats.batchCount);
        Assert.Equal(6, stats.indexCount);
        Assert.Equal(2, _device.LastUIUploadStats.batchCount);
    }

    public void Dispose()
    {
        _device.Dispose();
        CanvasNativeRenderBridge.ResetForTests();
        UnityEngine.Object.DestroyImmediate(_root);
    }

    private void AssertCommands(out CanvasNativeRenderBridge.RenderCommand[] commands)
        => Assert.True(CanvasNativeRenderBridge.TryBuildCommands(
            _renderer, _device, 7, out commands));

    private static Mesh SingleSubMeshQuad()
    {
        Mesh mesh = QuadVertices();
        mesh.SetIndices(new[] { 0, 1, 2, 2, 3, 0 }, MeshTopology.Triangles, 0);
        return mesh;
    }

    private static Mesh TwoSubMeshQuad()
    {
        Mesh mesh = QuadVertices();
        mesh.subMeshCount = 2;
        mesh.SetIndices(new[] { 0, 1, 2 }, MeshTopology.Triangles, 0);
        mesh.SetIndices(new[] { 2, 3, 0 }, MeshTopology.Triangles, 1);
        return mesh;
    }

    private static Mesh QuadVertices()
        => new()
        {
            vertices = new[]
            {
                new Vector3(0, 0), new Vector3(32, 0),
                new Vector3(32, 32), new Vector3(0, 32)
            },
            colors32 = new[]
            {
                new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 255),
                new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 255)
            }
        };

    private static ulong Id(UnityEngine.Object value)
        => unchecked((ulong)(uint)value.GetInstanceID());
}
