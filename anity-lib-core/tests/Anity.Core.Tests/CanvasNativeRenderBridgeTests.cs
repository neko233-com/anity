using System;
using System.Runtime.InteropServices;
using Anity.Core.Runtime.Native;
using UnityEngine;
using Xunit;
using GraphicsDeviceType = UnityEngine.Rendering.GraphicsDeviceType;

namespace Anity.Core.Tests;

[Collection(ComponentAttributeBehaviorCollection.Name)]
public sealed class CanvasNativeRenderBridgeTests : IDisposable
{
    private const int Width = 64;
    private const int Height = 64;
    private readonly GameObject _root;
    private readonly GameObject _child;
    private readonly CanvasRenderer _renderer;
    private readonly NativeGraphicsDevice _device;

    public CanvasNativeRenderBridgeTests()
    {
        CanvasNativeRenderBridge.ResetForTests();
        Screen.SetResolution(Width, Height, false);
        _root = new GameObject("native-bridge-canvas");
        _root.AddComponent<RectTransform>();
        _root.AddComponent<Canvas>();
        _child = new GameObject("native-bridge-renderer");
        _child.AddComponent<RectTransform>();
        _child.transform.SetParent(_root.transform, false);
        _renderer = _child.AddComponent<CanvasRenderer>();
        _renderer.SetMesh(QuadMesh());
        Canvas.ForceUpdateCanvases();
        _device = NativeGraphicsDevice.Create(GraphicsDeviceType.Null, Width, Height, false);
    }

    [Fact]
    public void OverlayVerticesBecomeTopLeftFramebufferPixels()
    {
        AssertBuild(out _, out var vertices, out _);
        Assert.Equal(8f, vertices[0].position.x, 3);
        Assert.Equal(56f, vertices[0].position.y, 3);
        Assert.Equal(56f, vertices[2].position.x, 3);
        Assert.Equal(8f, vertices[2].position.y, 3);
    }

    [Fact]
    public void RendererTintAndCanvasGroupAlphaAreBakedIntoVertexColor()
    {
        _renderer.SetColor(new Color(.5f, .25f, 1f, .5f));
        _root.AddComponent<CanvasGroup>().alpha = .5f;
        AssertBuild(out var desc, out var vertices, out _);
        Assert.Equal(128, vertices[0].color.r);
        Assert.Equal(64, vertices[0].color.g);
        Assert.Equal(255, vertices[0].color.b);
        Assert.Equal(64, vertices[0].color.a);
        Assert.Equal(.25f, desc.effectiveAlpha, 3);
    }

    [Fact]
    public void RectClipConvertsBottomLeftCoordinatesToTopLeftScissor()
    {
        _renderer.EnableRectClipping(new Rect(10, 20, 30, 20));
        _renderer.clippingSoftness = new Vector2(2, 3);
        AssertBuild(out var desc, out _, out _);
        Assert.True(desc.flags.HasFlag(AnityNative.UIRenderCommandFlags.RectClip));
        Assert.Equal(10, desc.clipXMin);
        Assert.Equal(24, desc.clipYMin);
        Assert.Equal(40, desc.clipXMax);
        Assert.Equal(44, desc.clipYMax);
        Assert.Equal(2, desc.softnessX);
        Assert.Equal(3, desc.softnessY);
    }

    [Fact]
    public void CullKeepsCommandButRemovesVisibleFlag()
    {
        _renderer.cull = true;
        AssertBuild(out var desc, out _, out _);
        Assert.False(desc.flags.HasFlag(AnityNative.UIRenderCommandFlags.Visible));
    }

    [Fact]
    public void MaskAndPopInstructionsSurviveCommandConversion()
    {
#pragma warning disable CS0618
        _renderer.isMask = true;
#pragma warning restore CS0618
        _renderer.hasPopInstruction = true;
        AssertBuild(out var desc, out _, out _);
        Assert.True(desc.flags.HasFlag(AnityNative.UIRenderCommandFlags.Mask));
        Assert.True(desc.flags.HasFlag(AnityNative.UIRenderCommandFlags.Pop));
    }

    [Fact]
    public void MaterialTextureAndAlphaTextureUseStableObjectIds()
    {
        Material material = Canvas.GetDefaultCanvasMaterial();
        var main = new Texture2D(1, 1);
        var alpha = new Texture2D(1, 1);
        _renderer.SetMaterial(material, main);
        _renderer.SetAlphaTexture(alpha);
        AssertBuild(out var desc, out _, out _);
        Assert.Equal(Id(material), desc.materialId);
        Assert.Equal(Id(main), desc.textureId);
        Assert.Equal(Id(alpha), desc.alphaTextureId);
    }

    [Fact]
    public void AllMeshSubmeshesAreIncludedInNativeIndexStream()
    {
        Mesh mesh = QuadMesh();
        mesh.subMeshCount = 2;
        mesh.SetIndices(new[] { 0, 1, 2 }, MeshTopology.Triangles, 0);
        mesh.SetIndices(new[] { 2, 3, 0 }, MeshTopology.Triangles, 1);
        _renderer.SetMesh(mesh);
        AssertBuild(out _, out _, out var indices);
        Assert.Equal(new uint[] { 0, 1, 2, 2, 3, 0 }, indices);
    }

    [Fact]
    public void InvalidMeshIndexIsRejectedBeforeNativeSubmission()
    {
        Mesh mesh = QuadMesh();
        mesh.SetIndices(new[] { 0, 1, 9 }, MeshTopology.Triangles, 0);
        _renderer.SetMesh(mesh);
        Assert.False(CanvasNativeRenderBridge.TryBuild(
            _renderer, _device, 0, out _, out _, out _));
    }

    [Fact]
    public void EmptyGeometryIsRejectedBeforeNativeSubmission()
    {
        _renderer.SetMesh(new Mesh());
        Assert.False(CanvasNativeRenderBridge.TryBuild(
            _renderer, _device, 0, out _, out _, out _));
    }

    [Fact]
    public void UvNormalAndTangentChannelsArePreserved()
    {
        Mesh mesh = QuadMesh();
        mesh.uv = new[] { new Vector2(.1f, .2f), Vector2.zero, Vector2.zero, Vector2.zero };
        mesh.uv2 = new[] { new Vector2(.3f, .4f), Vector2.zero, Vector2.zero, Vector2.zero };
        mesh.normals = new[] { Vector3.forward, Vector3.forward, Vector3.forward, Vector3.forward };
        mesh.tangents = new[] { new Vector4(1, 0, 0, -1), Vector4.zero, Vector4.zero, Vector4.zero };
        _renderer.SetMesh(mesh);
        AssertBuild(out _, out var vertices, out _);
        Assert.Equal(.1f, vertices[0].uv0.x, 3);
        Assert.Equal(.4f, vertices[0].uv1.y, 3);
        Assert.Equal(1f, vertices[0].normal.z, 3);
        Assert.Equal(-1f, vertices[0].tangent.w, 3);
    }

    [Fact]
    public void MetalFrameRendersCanvasRendererWithoutManualNativeCanvas()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return;
        _device.Dispose();
        using NativeGraphicsDevice metal = NativeGraphicsDevice.Create(
            GraphicsDeviceType.Metal, Width, Height, false);
        bool resolved = metal.Handle != IntPtr.Zero &&
            metal.CreateSwapchain(Width, Height, imageCount: 3, hdr: false);
        if (Environment.GetEnvironmentVariable("ANITY_REQUIRE_NATIVE") == "1")
            Assert.True(resolved);
        if (!resolved) return;

        _renderer.SetColor(new Color(0, 1, 0, 1));
        metal.BeginFrame();
        metal.EndFrame();

        Assert.True(metal.HasAttachedUICanvas);
        Assert.Equal(1, metal.LastUIUploadStats.drawCount);
        Assert.True(metal.TryReadbackSwapchainRGBA8(out byte[] pixels));
        int offset = (32 * Width + 32) * 4;
        Assert.Equal((byte)0, pixels[offset]);
        Assert.Equal((byte)255, pixels[offset + 1]);
        Assert.Equal((byte)0, pixels[offset + 2]);
        Assert.Equal((byte)255, pixels[offset + 3]);
    }

    public void Dispose()
    {
        _device.Dispose();
        CanvasNativeRenderBridge.ResetForTests();
        UnityEngine.Object.DestroyImmediate(_root);
    }

    private void AssertBuild(
        out AnityNative.UIRenderCommandDesc desc,
        out AnityNative.UIPackedVertex[] vertices,
        out uint[] indices)
        => Assert.True(CanvasNativeRenderBridge.TryBuild(
            _renderer, _device, 7, out desc, out vertices, out indices));

    private static Mesh QuadMesh()
    {
        var mesh = new Mesh
        {
            vertices = new[]
            {
                new Vector3(-24, -24, 0), new Vector3(24, -24, 0),
                new Vector3(24, 24, 0), new Vector3(-24, 24, 0)
            },
            colors32 = new[]
            {
                new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 255),
                new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 255)
            }
        };
        mesh.SetIndices(new[] { 0, 1, 2, 2, 3, 0 }, MeshTopology.Triangles, 0);
        return mesh;
    }

    private static ulong Id(UnityEngine.Object value)
        => unchecked((ulong)(uint)value.GetInstanceID());
}
