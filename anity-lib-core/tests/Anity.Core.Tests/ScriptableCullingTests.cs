using System;
using System.Collections;
using System.Reflection;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Anity.Core.Runtime.Native;
using Xunit;
using Object = UnityEngine.Object;

namespace Anity.Core.Tests;

[Collection(ComponentAttributeBehaviorCollection.Name)]
public sealed class ScriptableCullingTests
{
    private const int IsolatedLayer = 31;

    [Fact]
    public void Cull_ReportsActiveVisibleRenderer()
    {
        using var fixture = new RendererFixture(Vector3.zero);
        Assert.Equal(1, Cull().visibleRenderers.length);
    }

    [Fact]
    public void Cull_ExcludesDisabledRenderer()
    {
        using var fixture = new RendererFixture(Vector3.zero); fixture.renderer.enabled = false;
        Assert.Equal(0, Cull().visibleRenderers.length);
    }

    [Fact]
    public void Cull_ExcludesRendererMarkedNotVisible()
    {
        using var fixture = new RendererFixture(Vector3.zero); fixture.renderer.isVisible = false;
        Assert.Equal(0, Cull().visibleRenderers.length);
    }

    [Fact]
    public void Cull_RespectsLayerMask()
    {
        using var fixture = new RendererFixture(Vector3.zero);
        Assert.Equal(0, Cull(mask: 1 << 30).visibleRenderers.length);
    }

    [Fact]
    public void Cull_RespectsPerLayerDistance()
    {
        using var fixture = new RendererFixture(new Vector3(8f, 0f, 0f));
        var distances = new float[32]; distances[IsolatedLayer] = 2f;
        Assert.Equal(0, Cull(layerDistances: distances).visibleRenderers.length);
    }

    [Fact]
    public void Cull_DoesNotCullWhenLayerDistanceIsZero()
    {
        using var fixture = new RendererFixture(new Vector3(800f, 0f, 0f));
        var distances = new float[32];
        Assert.Equal(1, Cull(layerDistances: distances).visibleRenderers.length);
    }

    [Fact]
    public void Cull_UsesWorldTransformedMeshBoundsForFrustum()
    {
        using var fixture = new RendererFixture(new Vector3(5f, 0f, 0f));
        var camera = new GameObject("Culling Camera");
        try
        {
            var parameters = new ScriptableCullingParameters(camera.AddComponent<Camera>())
            {
                cullingMask = 1 << IsolatedLayer,
                cullingMatrix = Matrix4x4.identity
            };
            var context = new ScriptableRenderContext(); context.Cull(ref parameters, out var results);
            Assert.Equal(0, results.visibleRenderers.length);
        }
        finally { Object.DestroyImmediate(camera); }
    }

    [Theory]
    [InlineData(-1.4f, false)]
    [InlineData(-1.2f, false)]
    [InlineData(-.9f, true)]
    [InlineData(-.2f, true)]
    [InlineData(0f, true)]
    [InlineData(.2f, true)]
    [InlineData(.9f, true)]
    [InlineData(1.2f, false)]
    [InlineData(1.4f, false)]
    [InlineData(2f, false)]
    public void Cull_UsesSkinnedRendererLocalBoundsForFrustum(float localBoundsCenterX, bool visible)
    {
        var gameObject = new GameObject("Skinned culling renderer") { layer = IsolatedLayer };
        var mesh = new Mesh
        {
            vertices = new[] { Vector3.zero, Vector3.right, Vector3.up },
            triangles = new[] { 0, 1, 2 }
        };
        try
        {
            var skinned = gameObject.AddComponent<SkinnedMeshRenderer>();
            skinned.sharedMesh = mesh;
            skinned.localBounds = new Bounds(new Vector3(localBoundsCenterX, 0f, 0f), new Vector3(.1f, .1f, .1f));
            var camera = new GameObject("Skinned culling camera");
            try
            {
                var parameters = new ScriptableCullingParameters(camera.AddComponent<Camera>())
                {
                    cullingMask = 1 << IsolatedLayer,
                    cullingMatrix = Matrix4x4.identity
                };
                var context = new ScriptableRenderContext(); context.Cull(ref parameters, out var results);
                Assert.Equal(visible ? 1 : 0, results.visibleRenderers.length);
            }
            finally { Object.DestroyImmediate(camera); }
        }
        finally { Object.DestroyImmediate(mesh); Object.DestroyImmediate(gameObject); }
    }

    [Theory]
    [InlineData(-1.4f, false)]
    [InlineData(-1.1f, false)]
    [InlineData(-.8f, true)]
    [InlineData(-.2f, true)]
    [InlineData(0f, true)]
    [InlineData(.2f, true)]
    [InlineData(.8f, true)]
    [InlineData(1.1f, false)]
    [InlineData(1.4f, false)]
    [InlineData(2f, false)]
    public void Cull_UsesCurrentNativeSkinnedDeformationBounds(float boneX, bool visible)
    {
        if (!AnityNative.Available) return;
        var rendererObject = new GameObject("Dynamic skinned culling renderer") { layer = IsolatedLayer };
        var boneObject = new GameObject("Dynamic skinned culling bone");
        var mesh = new Mesh
        {
            vertices = new[] { new Vector3(-.05f, -.05f, 0f), new Vector3(.05f, -.05f, 0f), new Vector3(0f, .05f, 0f) },
            triangles = new[] { 0, 1, 2 }, bindposes = new[] { Matrix4x4.identity },
            boneWeights = new[] { new BoneWeight { boneIndex0 = 0, weight0 = 1f }, new BoneWeight { boneIndex0 = 0, weight0 = 1f }, new BoneWeight { boneIndex0 = 0, weight0 = 1f } }
        };
        try
        {
            boneObject.transform.position = new Vector3(boneX, 0f, 0f);
            var skinned = rendererObject.AddComponent<SkinnedMeshRenderer>(); skinned.sharedMesh = mesh; skinned.bones = new[] { boneObject.transform };
            skinned.localBounds = new Bounds(Vector3.zero, new Vector3(.1f, .1f, .1f));
            var camera = new GameObject("Dynamic skinned culling camera");
            try
            {
                var parameters = new ScriptableCullingParameters(camera.AddComponent<Camera>()) { cullingMask = 1 << IsolatedLayer, cullingMatrix = Matrix4x4.identity };
                var context = new ScriptableRenderContext(); context.Cull(ref parameters, out var results);
                Assert.Equal(visible ? 1 : 0, results.visibleRenderers.length);
            }
            finally { Object.DestroyImmediate(camera); }
        }
        finally { Object.DestroyImmediate(mesh); Object.DestroyImmediate(boneObject); Object.DestroyImmediate(rendererObject); }
    }

    [Fact]
    public void CullingParameters_CameraConstructorCarriesCameraMaskAndMatrices()
    {
        var gameObject = new GameObject("Culling Camera");
        try
        {
            var camera = gameObject.AddComponent<Camera>();
            camera.cullingMask = 1 << IsolatedLayer;
            gameObject.transform.position = new Vector3(2f, 3f, 4f);
            var parameters = new ScriptableCullingParameters(camera);
            Assert.Equal(camera.cullingMask, parameters.cullingMask);
            Assert.Equal(gameObject.transform.position, parameters.worldOrigin);
            Assert.Equal(camera.projectionMatrix * camera.worldToCameraMatrix, parameters.cullingMatrix);
        }
        finally { Object.DestroyImmediate(gameObject); }
    }

    [Fact]
    public void Cull_SetsVelocityRasterizationForObjectMotion()
    {
        using var fixture = new RendererFixture(Vector3.zero);
        fixture.renderer.motionVectorGenerationMode = MotionVectorGenerationMode.Object;
        Assert.True(Cull().velocityNeedsRasterization);
    }

    [Fact]
    public void Cull_DoesNotRequestObjectVelocityForForceNoMotion()
    {
        using var fixture = new RendererFixture(Vector3.zero);
        fixture.renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        Assert.False(Cull().velocityNeedsRasterization);
    }

    [Fact]
    public void DrawRenderers_RecordsOneCommandPerVisibleMaterial()
    {
        using var fixture = new RendererFixture(Vector3.zero);
        fixture.renderer.sharedMaterial = fixture.material;
        var results = Cull();
        var drawing = new DrawingSettings(new ShaderTagId("UniversalForward"), new SortingSettings(null));
        var filtering = new FilteringSettings(RenderQueueRange.opaque, 1u << IsolatedLayer);
        var context = new ScriptableRenderContext(); context.DrawRenderers(results, ref drawing, ref filtering);
        var field = typeof(ScriptableRenderContext).GetField("_drawCommands", BindingFlags.Instance | BindingFlags.NonPublic);
        var commands = Assert.IsAssignableFrom<IList>(field!.GetValue(context));
        Assert.Single(commands);
    }

    [Fact]
    public void DrawRenderers_UsesShaderQueueWhenMaterialQueueIsInherited()
    {
        using var fixture = new RendererFixture(Vector3.zero);
        fixture.material.shader = Shader.Find("Anity/CullingQueue")!;
        fixture.material.shader.renderQueue = 2000;
        fixture.material.renderQueue = -1;
        var drawing = new DrawingSettings(new ShaderTagId("UniversalForward"), new SortingSettings(null));
        var filtering = new FilteringSettings(RenderQueueRange.opaque, 1u << IsolatedLayer);
        var context = new ScriptableRenderContext(); context.DrawRenderers(Cull(), ref drawing, ref filtering);
        var field = typeof(ScriptableRenderContext).GetField("_drawCommands", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.Single(Assert.IsAssignableFrom<IList>(field!.GetValue(context)));
    }

    [Fact]
    public void NativeCameraMeshRaster_DrawsIndexedTriangleIntoMetalTarget()
    {
        if (!AnityNative.Available) return;
        using var device = NativeGraphicsDevice.Create(GraphicsDeviceType.Metal, 8, 8, false);
        var target = new RenderTexture(8, 8, 24, RenderTextureFormat.ARGB32);
        try
        {
            Assert.True(device.EnsureCameraRenderTarget(target));
            Assert.True(device.TryRecordCameraPass((ulong)(uint)target.GetInstanceID(), 8, 8,
                new Rect(0, 0, 8, 8), Color.black, true, true, 1, false, false, false));
            Assert.True(device.TryDrawCameraMesh(target,
                new[] { new Vector3(-.8f, -.8f, .5f), new Vector3(.8f, -.8f, .5f), new Vector3(0f, .8f, .5f) },
                new[] { Vector3.forward, Vector3.forward, Vector3.forward },
                new[] { Color.red, Color.red, Color.red }, new[] { 0, 1, 2 }, Matrix4x4.identity));
            Assert.True(device.TryReadbackCameraRenderTargetRGBA8(target, out var pixels));
            int center = ((4 * 8) + 4) * 4;
            Assert.True(pixels[center] > 200); Assert.True(pixels[center + 1] < 40); Assert.True(pixels[center + 2] < 40);
        }
        finally { device.ReleaseCameraRenderTarget(target); target.Release(); }
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    [InlineData(2, 1)]
    [InlineData(3, 1)]
    [InlineData(4, 1)]
    [InlineData(0, 2)]
    [InlineData(1, 2)]
    [InlineData(2, 2)]
    [InlineData(3, 2)]
    [InlineData(4, 2)]
    public void NativeCameraMeshRaster_Tex2DArrayKeepsEyeSlicesIndependent(int caseId, int samples)
    {
        if (!AnityNative.Available) return;
        var descriptor = new RenderTextureDescriptor(8, 8, RenderTextureFormat.ARGB32, 24)
        {
            dimension = UnityEngine.TextureDimension.Tex2DArray,
            volumeDepth = 2,
            msaaSamples = samples,
            vrUsage = UnityEngine.VRTextureUsage.TwoEyes
        };
        var normalsDescriptor = descriptor;
        normalsDescriptor.depthBufferBits = 0;
        normalsDescriptor.colorFormat = RenderTextureFormat.ARGB32;
        normalsDescriptor.graphicsFormat = GraphicsFormat.R8G8B8A8_SNorm;
        var motionDescriptor = descriptor;
        motionDescriptor.depthBufferBits = 0;
        motionDescriptor.colorFormat = RenderTextureFormat.RGHalf;
        motionDescriptor.graphicsFormat = GraphicsFormat.R16G16_SFloat;
        var depthDescriptor = descriptor;
        depthDescriptor.depthBufferBits = 0;
        depthDescriptor.msaaSamples = 1;
        depthDescriptor.colorFormat = RenderTextureFormat.ARGB32;
        depthDescriptor.enableRandomWrite = true;
        using var device = NativeGraphicsDevice.Create(GraphicsDeviceType.Metal, 8, 8, false, msaa: samples);
        var target = new RenderTexture(descriptor);
        var opaqueCapture = new RenderTexture(descriptor);
        var normalsCapture = new RenderTexture(normalsDescriptor);
        var motionCapture = new RenderTexture(motionDescriptor);
        var depthCapture = new RenderTexture(depthDescriptor);
        var opaquePass = new CameraOpaqueTexturePass();
        var normalsPass = new CameraNormalsTexturePass();
        var motionPass = new CameraMotionVectorsTexturePass();
        var depthPass = new CameraDepthTexturePass();
        Color leftColor = new(.2f + caseId * .1f, 0f, 0f, 1f);
        Color rightColor = new(0f, .2f + caseId * .1f, 0f, 1f);
        try
        {
            Assert.True(device.EnsureCameraRenderTarget(target));
            Assert.True(device.EnsureCameraRenderTarget(opaqueCapture));
            Assert.True(device.EnsureCameraRenderTarget(normalsCapture));
            Assert.True(device.EnsureCameraRenderTarget(motionCapture));
            Assert.True(device.EnsureCameraRenderTarget(depthCapture));
            for (int slice = 0; slice < 2; slice++)
            {
                Color color = slice == 0 ? leftColor : rightColor;
                Vector3 normal = slice == 0 ? Vector3.forward : Vector3.right;
                var positions = new[]
                {
                    new Vector3(-.8f, -.8f, slice == 0 ? .25f : .75f),
                    new Vector3(.8f, -.8f, slice == 0 ? .25f : .75f),
                    new Vector3(0f, .8f, slice == 0 ? .25f : .75f)
                };
                Assert.True(device.TryRecordCameraPass((ulong)(uint)target.GetInstanceID(), 8, 8,
                    new Rect(0, 0, 8, 8), Color.black, true, true, samples, false, false, false,
                    depthSlice: slice));
                Assert.True(device.TryDrawCameraMesh(target, positions,
                    new[] { normal, normal, normal }, FilledColors(3, color), TriangleIndices(),
                    Matrix4x4.identity, null,
                    slice == 0 ? Matrix4x4.identity : Matrix4x4.Translate(new Vector3(-.5f, 0f, 0f)),
                    depthSlice: slice));
            }
            Assert.True(device.TryReadbackCameraRenderTargetRGBA8(target, out var left, 0));
            Assert.True(device.TryReadbackCameraRenderTargetRGBA8(target, out var right, 1));
            int center = (4 * 8 + 4) * 4;
            Assert.InRange(left[center], 35 + caseId * 25, 65 + caseId * 25);
            Assert.True(left[center + 1] < 20);
            Assert.InRange(right[center + 1], 35 + caseId * 25, 65 + caseId * 25);
            Assert.True(right[center] < 20);

            // URP's opaque texture must preserve the eye's source and destination layer.
            Assert.True(device.TryCopyCameraRenderTargetColor(target, false, opaqueCapture, sourceSlice: 1, destinationSlice: 1));
            Assert.True(device.TryReadbackCameraRenderTargetRGBA8(opaqueCapture, out var opaquePixels, 1));
            Assert.InRange(opaquePixels[center + 1], 35 + caseId * 25, 65 + caseId * 25);
            Assert.True(opaquePixels[center] < 20);

            Assert.True(device.TryCopyCameraRenderTargetNormalsToColor(target, false, normalsCapture,
                sourceSlice: 1, destinationSlice: 1));
            Assert.True(device.TryReadbackCameraRenderTargetRGBA8(normalsCapture, out var normalsPixels, 1));
            Assert.True(normalsPixels[center] > 240);
            Assert.InRange(normalsPixels[center + 1], 120, 136);
            Assert.InRange(normalsPixels[center + 2], 120, 136);

            Assert.True(device.TryCopyCameraRenderTargetMotionToColor(target, false, motionCapture,
                sourceSlice: 1, destinationSlice: 1));
            Assert.True(device.TryReadbackCameraRenderTargetRGBA8(motionCapture, out var motionPixels, 1));
            Assert.InRange(motionPixels[center], 153, 165);
            Assert.InRange(motionPixels[center + 1], 120, 136);

            Assert.True(device.TryCopyCameraRenderTargetDepthToColor(target, false, depthCapture,
                sourceSlice: 1, destinationSlice: 1));
            Assert.True(device.TryReadbackCameraRenderTargetRGBA8(depthCapture, out var depthPixels, 1));
            Assert.InRange(depthPixels[center], 180, 200);

            var normalsData = new RenderingData
            {
                cameraData = new CameraData
                {
                    nativeTargetTexture = target,
                    requiresNormalsTexture = true,
                    xrDepthSlice = 1,
                    cameraTargetDescriptor = descriptor
                }
            };
            normalsPass.Execute(default, ref normalsData);
            Assert.NotNull(normalsData.cameraData.normalsTexture);
            Assert.True(device.TryReadbackCameraRenderTargetRGBA8(
                normalsData.cameraData.normalsTexture, out var passNormalsPixels, 1));
            Assert.True(passNormalsPixels[center] > 240);

            var motionData = new RenderingData
            {
                cameraData = new CameraData
                {
                    nativeTargetTexture = target,
                    requiresMotionVectors = true,
                    xrDepthSlice = 1,
                    cameraTargetDescriptor = descriptor
                }
            };
            motionPass.Execute(default, ref motionData);
            Assert.NotNull(motionData.cameraData.motionVectorTexture);
            Assert.True(device.TryReadbackCameraRenderTargetRGBA8(
                motionData.cameraData.motionVectorTexture, out var passMotionPixels, 1));
            Assert.InRange(passMotionPixels[center], 153, 165);

            var depthData = new RenderingData
            {
                cameraData = new CameraData
                {
                    nativeTargetTexture = target,
                    requiresDepthTexture = true,
                    xrDepthSlice = 1,
                    cameraTargetDescriptor = descriptor
                }
            };
            depthPass.Execute(default, ref depthData);
            Assert.NotNull(depthData.cameraData.depthTexture);
            Assert.True(device.TryReadbackCameraRenderTargetRGBA8(
                depthData.cameraData.depthTexture, out var passDepthPixels, 1));
            Assert.InRange(passDepthPixels[center], 180, 200);

            var renderingData = new RenderingData
            {
                cameraData = new CameraData
                {
                    nativeTargetTexture = target,
                    requiresOpaqueTexture = true,
                    xrDepthSlice = 1,
                    cameraTargetDescriptor = descriptor
                }
            };
            opaquePass.Execute(default, ref renderingData);
            Assert.NotNull(renderingData.cameraData.opaqueTexture);
            Assert.True(device.TryReadbackCameraRenderTargetRGBA8(
                renderingData.cameraData.opaqueTexture, out var passOpaquePixels, 1));
            Assert.InRange(passOpaquePixels[center + 1], 35 + caseId * 25, 65 + caseId * 25);
            Assert.True(passOpaquePixels[center] < 20);
        }
        finally
        {
            opaquePass.OnCameraCleanup(null);
            normalsPass.OnCameraCleanup(null);
            motionPass.OnCameraCleanup(null);
            depthPass.OnCameraCleanup(null);
            device.ReleaseCameraRenderTarget(depthCapture); device.ReleaseCameraRenderTarget(motionCapture); device.ReleaseCameraRenderTarget(normalsCapture);
            device.ReleaseCameraRenderTarget(opaqueCapture); device.ReleaseCameraRenderTarget(target);
            depthCapture.Release(); motionCapture.Release(); normalsCapture.Release(); opaqueCapture.Release(); target.Release();
        }
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    [InlineData(2, 1)]
    [InlineData(3, 1)]
    [InlineData(4, 1)]
    [InlineData(0, 2)]
    [InlineData(1, 2)]
    [InlineData(2, 2)]
    [InlineData(3, 2)]
    [InlineData(4, 2)]
    public void NativeCameraMeshRaster_SinglePassInstancedWritesBothEyeLayers(int caseId, int samples)
    {
        if (!AnityNative.Available) return;
        var descriptor = new RenderTextureDescriptor(8, 8, RenderTextureFormat.ARGB32, 24)
        {
            dimension = UnityEngine.TextureDimension.Tex2DArray,
            volumeDepth = 2,
            msaaSamples = samples,
            vrUsage = UnityEngine.VRTextureUsage.TwoEyes
        };
        using var device = NativeGraphicsDevice.Create(GraphicsDeviceType.Metal, 8, 8, false, msaa: samples);
        var target = new RenderTexture(descriptor);
        try
        {
            Assert.True(device.EnsureCameraRenderTarget(target));
            Assert.True(device.TryRecordCameraPass((ulong)(uint)target.GetInstanceID(), 8, 8,
                new Rect(0, 0, 8, 8), Color.black, true, true, samples, false, false, false,
                depthSlice: 0, depthSliceCount: 2));
            Color color = new(.2f + caseId * .1f, .1f, .8f, 1f);
            Assert.True(device.TryDrawCameraMesh(target, TrianglePositions(), TriangleNormals(), FilledColors(3, color),
                TriangleIndices(), Matrix4x4.Translate(new Vector3(-.45f, 0f, 0f)),
                stereoRightObjectToClip: Matrix4x4.Translate(new Vector3(.45f, 0f, 0f))));
            Assert.True(device.TryReadbackCameraRenderTargetRGBA8(target, out var left, 0));
            Assert.True(device.TryReadbackCameraRenderTargetRGBA8(target, out var right, 1));
            int leftEyePixel = ((4 * 8) + 2) * 4;
            int rightEyePixel = ((4 * 8) + 6) * 4;
            Assert.InRange(left[leftEyePixel], 35 + caseId * 25, 65 + caseId * 25);
            Assert.InRange(right[rightEyePixel], 35 + caseId * 25, 65 + caseId * 25);
            Assert.True(left[rightEyePixel] < 20);
            Assert.True(right[leftEyePixel] < 20);
        }
        finally { device.ReleaseCameraRenderTarget(target); target.Release(); }
    }

    [Fact]
    public void NativeCameraMeshRaster_WritesEncodedNormalsIntoMetalTarget()
    {
        if (!AnityNative.Available) return;
        using var device = NativeGraphicsDevice.Create(GraphicsDeviceType.Metal, 8, 8, false);
        var source = new RenderTexture(8, 8, 24, RenderTextureFormat.ARGB32);
        var normals = NewNormalTexture(8, 8);
        try
        {
            Assert.True(device.EnsureCameraRenderTarget(source));
            Assert.True(device.EnsureCameraRenderTarget(normals));
            Assert.True(device.TryRecordCameraPass((ulong)(uint)source.GetInstanceID(), 8, 8,
                new Rect(0, 0, 8, 8), Color.black, true, true, 1, false, false, false));
            Assert.True(device.TryDrawCameraMesh(source,
                new[] { new Vector3(-.8f, -.8f, .5f), new Vector3(.8f, -.8f, .5f), new Vector3(0f, .8f, .5f) },
                new[] { Vector3.forward, Vector3.forward, Vector3.forward },
                new[] { Color.white, Color.white, Color.white }, new[] { 0, 1, 2 }, Matrix4x4.identity));
            Assert.True(device.TryCopyCameraRenderTargetNormalsToColor(source, false, normals));
            Assert.True(device.TryReadbackCameraRenderTargetRGBA8(normals, out var pixels));
            int center = ((4 * 8) + 4) * 4;
            Assert.InRange(pixels[center], 120, 136);
            Assert.InRange(pixels[center + 1], 120, 136);
            Assert.True(pixels[center + 2] > 240);
            Assert.InRange(pixels[center + 3], 120, 136);
        }
        finally
        {
            device.ReleaseCameraRenderTarget(normals); device.ReleaseCameraRenderTarget(source);
            normals.Release(); source.Release();
        }
    }

    [Fact]
    public void NativeCameraMeshRaster_UsesUnityMatrixFieldOrderForMetalTransform()
    {
        if (!AnityNative.Available) return;
        using var device = NativeGraphicsDevice.Create(GraphicsDeviceType.Metal, 8, 8, false);
        var target = new RenderTexture(8, 8, 24, RenderTextureFormat.ARGB32);
        try
        {
            Assert.True(device.EnsureCameraRenderTarget(target));
            Assert.True(device.TryRecordCameraPass((ulong)(uint)target.GetInstanceID(), 8, 8,
                new Rect(0, 0, 8, 8), Color.black, true, true, 1, false, false, false));
            var translateRight = Matrix4x4.Translate(new Vector3(.5f, 0f, 0f));
            Assert.True(device.TryDrawCameraMesh(target,
                new[] { new Vector3(-.4f, -.4f, .5f), new Vector3(.4f, -.4f, .5f), new Vector3(0f, .4f, .5f) },
                new[] { Vector3.forward, Vector3.forward, Vector3.forward },
                new[] { Color.red, Color.red, Color.red }, new[] { 0, 1, 2 }, translateRight));
            Assert.True(device.TryReadbackCameraRenderTargetRGBA8(target, out var pixels));
            int left = ((4 * 8) + 2) * 4;
            int right = ((4 * 8) + 6) * 4;
            Assert.True(pixels[left] < 40);
            Assert.True(pixels[right] > 200);
        }
        finally { device.ReleaseCameraRenderTarget(target); target.Release(); }
    }

    [Theory]
    [InlineData(0f, 0f, 1f, 128, 128, 255)]
    [InlineData(1f, 0f, 0f, 255, 128, 128)]
    [InlineData(0f, 1f, 0f, 128, 255, 128)]
    [InlineData(0f, 0f, -1f, 128, 128, 0)]
    public void NativeCameraMeshRaster_EncodesUnitVertexNormals(float x, float y, float z,
        int expectedR, int expectedG, int expectedB)
    {
        if (!AnityNative.Available) return;
        var pixel = RasterAndCopyNormal(new Vector3(x, y, z));
        Assert.InRange(pixel.r, expectedR - 12, expectedR + 12);
        Assert.InRange(pixel.g, expectedG - 12, expectedG + 12);
        Assert.InRange(pixel.b, expectedB - 12, expectedB + 12);
        // URP's SNorm normal target writes alpha 0, which the diagnostic
        // readback remaps to midpoint 128 along with signed RGB channels.
        Assert.InRange(pixel.a, 120, 136);
    }

    [Fact]
    public void NativeCameraMeshRaster_NormalsAttachmentClearsToTransparentBeforeGeometry()
    {
        if (!AnityNative.Available) return;
        using var device = NativeGraphicsDevice.Create(GraphicsDeviceType.Metal, 4, 4, false);
        var source = new RenderTexture(4, 4, 24, RenderTextureFormat.ARGB32);
        var normals = NewNormalTexture(4, 4);
        try
        {
            Assert.True(device.EnsureCameraRenderTarget(source)); Assert.True(device.EnsureCameraRenderTarget(normals));
            Assert.True(device.TryRecordCameraPass((ulong)(uint)source.GetInstanceID(), 4, 4,
                new Rect(0, 0, 4, 4), Color.black, true, true, 1, false, false, false));
            Assert.True(device.TryCopyCameraRenderTargetNormalsToColor(source, false, normals));
            Assert.True(device.TryReadbackCameraRenderTargetRGBA8(normals, out var pixels));
            // The raw SNorm clear is zero in every channel; the readback API
            // maps signed values to diagnostic UNorm bytes, hence midpoint.
            Assert.All(pixels, value => Assert.InRange(value, (byte)120, (byte)136));
        }
        finally { device.ReleaseCameraRenderTarget(normals); device.ReleaseCameraRenderTarget(source); normals.Release(); source.Release(); }
    }

    [Fact]
    public void NativeCameraMeshRaster_UsesInverseTransposeForNonUniformScaleNormals()
    {
        if (!AnityNative.Available) return;
        var localNormal = new Vector3(1f, 1f, 0f).normalized;
        var normalMatrix = Matrix4x4.Scale(new Vector3(2f, 1f, 1f)).inverse.transpose;
        var pixel = RasterAndCopyNormal(localNormal, normalMatrix);
        // inverse-transpose(2,1,1) transforms (1,1,0) to normalized(.5,1,0).
        Assert.InRange(pixel.r, 177, 191);
        Assert.InRange(pixel.g, 235, 249);
        Assert.InRange(pixel.b, 120, 136);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void NativeCameraMeshRaster_NormalsCopyRejectsInvalidSourceForms(int form)
    {
        if (!AnityNative.Available) return;
        using var device = NativeGraphicsDevice.Create(GraphicsDeviceType.Metal, 4, 4, false);
        var target = new RenderTexture(4, 4, 0, RenderTextureFormat.ARGB32);
        var source = new RenderTexture(4, 4, 0, RenderTextureFormat.ARGB32);
        try
        {
            Assert.True(device.EnsureCameraRenderTarget(target));
            bool copied = form switch
            {
                0 => device.TryCopyCameraRenderTargetNormalsToColor(null, false, target),
                1 => device.TryCopyCameraRenderTargetNormalsToColor(target, false, target),
                _ => device.TryCopyCameraRenderTargetNormalsToColor(source, false, target)
            };
            Assert.False(copied);
        }
        finally { device.ReleaseCameraRenderTarget(target); target.Release(); source.Release(); }
    }

    private static (byte r, byte g, byte b, byte a) RasterAndCopyNormal(Vector3 normal,
        Matrix4x4? normalObjectToWorld = null)
    {
        using var device = NativeGraphicsDevice.Create(GraphicsDeviceType.Metal, 8, 8, false);
        var source = new RenderTexture(8, 8, 24, RenderTextureFormat.ARGB32);
        var destination = NewNormalTexture(8, 8);
        try
        {
            Assert.True(device.EnsureCameraRenderTarget(source)); Assert.True(device.EnsureCameraRenderTarget(destination));
            Assert.True(device.TryRecordCameraPass((ulong)(uint)source.GetInstanceID(), 8, 8,
                new Rect(0, 0, 8, 8), Color.black, true, true, 1, false, false, false));
            Assert.True(device.TryDrawCameraMesh(source,
                new[] { new Vector3(-.8f, -.8f, .5f), new Vector3(.8f, -.8f, .5f), new Vector3(0f, .8f, .5f) },
                new[] { normal, normal, normal }, new[] { Color.white, Color.white, Color.white },
                new[] { 0, 1, 2 }, Matrix4x4.identity, normalObjectToWorld));
            Assert.True(device.TryCopyCameraRenderTargetNormalsToColor(source, false, destination));
            Assert.True(device.TryReadbackCameraRenderTargetRGBA8(destination, out var pixels));
            int center = ((4 * 8) + 4) * 4;
            return (pixels[center], pixels[center + 1], pixels[center + 2], pixels[center + 3]);
        }
        finally
        {
            device.ReleaseCameraRenderTarget(destination); device.ReleaseCameraRenderTarget(source);
            destination.Release(); source.Release();
        }
    }

    private static RenderTexture NewNormalTexture(int width, int height)
    {
        var descriptor = new RenderTextureDescriptor(width, height, RenderTextureFormat.ARGB32, 0)
        {
            graphicsFormat = GraphicsFormat.R8G8B8A8_SNorm
        };
        return new RenderTexture(descriptor);
    }

    [Theory]
    [InlineData(-0.5f, 0f, 159, 128)]
    [InlineData(0.5f, 0f, 96, 128)]
    [InlineData(0f, -0.5f, 128, 159)]
    [InlineData(0f, 0.5f, 128, 96)]
    public void NativeCameraMeshRaster_EncodesPreviousClipMotion(float previousX, float previousY,
        int expectedR, int expectedG)
    {
        if (!AnityNative.Available) return;
        var pixel = RasterAndCopyMotion(Matrix4x4.Translate(new Vector3(previousX, previousY, 0f)));
        Assert.InRange(pixel.r, expectedR - 6, expectedR + 6);
        Assert.InRange(pixel.g, expectedG - 6, expectedG + 6);
    }

    [Theory]
    [InlineData(-.8f, 179)]
    [InlineData(-.6f, 166)]
    [InlineData(-.4f, 153)]
    [InlineData(-.2f, 140)]
    [InlineData(0f, 128)]
    [InlineData(.2f, 115)]
    [InlineData(.4f, 102)]
    [InlineData(.6f, 89)]
    [InlineData(.8f, 77)]
    [InlineData(.5f, 96)]
    public void NativeCameraMeshRaster_EncodesPreviousSkinnedVertexMotion(float previousVertexX,
        int expectedR)
    {
        if (!AnityNative.Available) return;
        Vector3[] previousVertices = TrianglePositions();
        for (int i = 0; i < previousVertices.Length; i++) previousVertices[i].x += previousVertexX;
        var pixel = RasterAndCopyMotion(Matrix4x4.identity, previousVertices);
        Assert.InRange(pixel.r, expectedR - 8, expectedR + 8);
        Assert.InRange(pixel.g, 120, 136);
    }

    [Theory]
    [InlineData(-.8f)]
    [InlineData(-.6f)]
    [InlineData(-.4f)]
    [InlineData(-.2f)]
    [InlineData(-.1f)]
    [InlineData(.1f)]
    [InlineData(.2f)]
    [InlineData(.4f)]
    [InlineData(.6f)]
    [InlineData(.8f)]
    public void NativeCameraMeshRaster_NonJitteredMotionIgnoresRasterProjectionJitter(float jitterX)
    {
        if (!AnityNative.Available) return;
        var pixel = RasterAndCopyMotion(Matrix4x4.identity, motion: Matrix4x4.identity,
            raster: Matrix4x4.Translate(new Vector3(jitterX, 0f, 0f)));
        Assert.InRange(pixel.r, 120, 136); Assert.InRange(pixel.g, 120, 136);
    }

    [Theory]
    [InlineData(-.9f)]
    [InlineData(-.7f)]
    [InlineData(-.5f)]
    [InlineData(-.3f)]
    [InlineData(-.1f)]
    [InlineData(.1f)]
    [InlineData(.3f)]
    [InlineData(.5f)]
    [InlineData(.7f)]
    [InlineData(.9f)]
    public void NativeCameraMeshRaster_TransparentDrawPreservesOpaqueMotion(float transparentPreviousX)
    {
        if (!AnityNative.Available) return;
        using var device = NativeGraphicsDevice.Create(GraphicsDeviceType.Metal, 8, 8, false);
        var source = new RenderTexture(8, 8, 24, RenderTextureFormat.ARGB32);
        var destination = NewMotionTexture(8, 8);
        try
        {
            Assert.True(device.EnsureCameraRenderTarget(source));
            Assert.True(device.EnsureCameraRenderTarget(destination));
            Assert.True(device.TryRecordCameraPass((ulong)(uint)source.GetInstanceID(), 8, 8,
                new Rect(0, 0, 8, 8), Color.black, true, true, 1, false, false, false));
            Assert.True(device.TryDrawCameraMesh(source, TrianglePositions(), TriangleNormals(),
                FilledColors(3, Color.white), TriangleIndices(), Matrix4x4.identity, null,
                Matrix4x4.Translate(new Vector3(-.5f, 0f, 0f))));
            Assert.True(device.TryCopyCameraRenderTargetMotionToColor(source, false, destination));
            Assert.True(device.TryReadbackCameraRenderTargetRGBA8(destination, out var before));
            var transparent = TrianglePositions();
            for (int i = 0; i < transparent.Length; i++) transparent[i].z = .4f;
            Assert.True(device.TryDrawCameraMesh(source, transparent, TriangleNormals(),
                FilledColors(3, new Color(1f, 0f, 0f, .5f)), TriangleIndices(), Matrix4x4.identity,
                null, Matrix4x4.Translate(new Vector3(transparentPreviousX, 0f, 0f)), blendMode: 1,
                depthWriteEnabled: false, writeMotionVectors: false));
            Assert.True(device.TryCopyCameraRenderTargetMotionToColor(source, false, destination));
            Assert.True(device.TryReadbackCameraRenderTargetRGBA8(destination, out var after));
            int center = (4 * 8 + 4) * 4;
            Assert.InRange(before[center], (byte)145, (byte)175);
            Assert.InRange(after[center], before[center] - 3, before[center] + 3);
            Assert.InRange(after[center + 1], before[center + 1] - 3, before[center + 1] + 3);
        }
        finally
        {
            device.ReleaseCameraRenderTarget(destination); device.ReleaseCameraRenderTarget(source);
            destination.Release(); source.Release();
        }
    }

    [Theory]
    [InlineData(-.9f)]
    [InlineData(-.7f)]
    [InlineData(-.5f)]
    [InlineData(-.3f)]
    [InlineData(-.1f)]
    [InlineData(.1f)]
    [InlineData(.3f)]
    [InlineData(.5f)]
    [InlineData(.7f)]
    [InlineData(.9f)]
    public void Camera_NonJitteredProjectionTracksOverrideAndReset(float jitterX)
    {
        var cameraObject = new GameObject("Non-jittered projection camera");
        try
        {
            var camera = cameraObject.AddComponent<Camera>();
            Matrix4x4 raster = Matrix4x4.Translate(new Vector3(jitterX, 0f, 0f));
            Matrix4x4 nonJittered = Matrix4x4.Translate(new Vector3(-jitterX, 0f, 0f));
            camera.projectionMatrix = raster;
            Assert.Equal(raster, camera.nonJitteredProjectionMatrix);
            camera.nonJitteredProjectionMatrix = nonJittered;
            Assert.Equal(nonJittered, camera.nonJitteredProjectionMatrix);
            camera.ResetProjectionMatrix();
            Assert.Equal(camera.projectionMatrix, camera.nonJitteredProjectionMatrix);
        }
        finally { Object.DestroyImmediate(cameraObject); }
    }

    [Theory]
    [InlineData(-.9f)]
    [InlineData(-.7f)]
    [InlineData(-.5f)]
    [InlineData(-.3f)]
    [InlineData(-.1f)]
    [InlineData(.1f)]
    [InlineData(.3f)]
    [InlineData(.5f)]
    [InlineData(.7f)]
    [InlineData(.9f)]
    public void Camera_TransparentRasterUsesConfiguredJitterProjection(float jitterX)
    {
        var cameraObject = new GameObject("Transparent jitter camera");
        try
        {
            var camera = cameraObject.AddComponent<Camera>();
            Matrix4x4 jittered = Matrix4x4.Translate(new Vector3(jitterX, 0f, 0f));
            Matrix4x4 nonJittered = Matrix4x4.Translate(new Vector3(-jitterX, 0f, 0f));
            camera.projectionMatrix = jittered;
            camera.nonJitteredProjectionMatrix = nonJittered;
            var context = new ScriptableRenderContext();
            var field = typeof(ScriptableRenderContext).GetField("_nativeTransparentViewProjection", BindingFlags.Instance | BindingFlags.NonPublic)!;
            Assert.True(camera.useJitteredProjectionMatrixForTransparentRendering);
            context.SetNativeCameraDrawState(null, null, jittered, camera, nonJittered);
            Assert.Equal(jittered, (Matrix4x4)field.GetValue(context)!);
            camera.useJitteredProjectionMatrixForTransparentRendering = false;
            context.SetNativeCameraDrawState(null, null, jittered, camera, nonJittered);
            Assert.Equal(nonJittered, (Matrix4x4)field.GetValue(context)!);
            var copy = new GameObject("Transparent jitter copy").AddComponent<Camera>();
            try
            {
                copy.CopyFrom(camera);
                Assert.False(copy.useJitteredProjectionMatrixForTransparentRendering);
            }
            finally { Object.DestroyImmediate(copy.gameObject); }
        }
        finally { Object.DestroyImmediate(cameraObject); }
    }

    [Theory]
    [InlineData(.002f)]
    [InlineData(.004f)]
    [InlineData(.006f)]
    [InlineData(.008f)]
    [InlineData(.010f)]
    [InlineData(.012f)]
    [InlineData(.014f)]
    [InlineData(.016f)]
    [InlineData(.018f)]
    [InlineData(.020f)]
    public void Camera_StereoMatricesSupportEyeOffsetsOverridesAndReset(float separation)
    {
        var cameraObject = new GameObject("Stereo matrix camera");
        try
        {
            var camera = cameraObject.AddComponent<Camera>();
            camera.stereoSeparation = separation;
            camera.stereoConvergence = 2f;
            camera.projectionMatrix = Matrix4x4.identity;
            camera.nonJitteredProjectionMatrix = Matrix4x4.Translate(new Vector3(.25f, 0f, 0f));
            Matrix4x4 left = camera.GetStereoProjectionMatrix(Camera.StereoscopicEye.Left);
            Matrix4x4 right = camera.GetStereoProjectionMatrix(Camera.StereoscopicEye.Right);
            Assert.True(left.m02 > right.m02);
            Assert.Equal(camera.nonJitteredProjectionMatrix.m02 + separation * .25f,
                camera.GetStereoNonJitteredProjectionMatrix(Camera.StereoscopicEye.Left).m02, 5);
            Assert.NotEqual(camera.GetStereoViewMatrix(Camera.StereoscopicEye.Left), camera.GetStereoViewMatrix(Camera.StereoscopicEye.Right));
            Matrix4x4 leftProjection = Matrix4x4.Translate(new Vector3(-.4f, 0f, 0f));
            Matrix4x4 rightView = Matrix4x4.Translate(new Vector3(.4f, 0f, 0f));
            camera.SetStereoProjectionMatrix(Camera.StereoscopicEye.Left, leftProjection);
            camera.SetStereoViewMatrix(Camera.StereoscopicEye.Right, rightView);
            Assert.Equal(leftProjection, camera.GetStereoProjectionMatrix(Camera.StereoscopicEye.Left));
            Assert.Equal(rightView, camera.GetStereoViewMatrix(Camera.StereoscopicEye.Right));
            camera.ResetStereoProjectionMatrices(); camera.ResetStereoViewMatrices();
            Assert.NotEqual(leftProjection, camera.GetStereoProjectionMatrix(Camera.StereoscopicEye.Left));
            Assert.NotEqual(rightView, camera.GetStereoViewMatrix(Camera.StereoscopicEye.Right));
        }
        finally { Object.DestroyImmediate(cameraObject); }
    }

    [Theory]
    [InlineData(4096)]
    [InlineData(4097)]
    [InlineData(4098)]
    [InlineData(4099)]
    [InlineData(4100)]
    [InlineData(4101)]
    [InlineData(4102)]
    [InlineData(4103)]
    [InlineData(4104)]
    [InlineData(4105)]
    public void MotionHistory_IsBoundedForCamerasAndRenderers(int entryCount)
    {
        const int maximumEntries = 4096;
        var contextType = typeof(ScriptableRenderContext);
        var historyLock = contextType.GetField("s_MotionHistoryLock", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        var cameraHistory = (IDictionary)contextType.GetField("s_PreviousCameraViewProjections", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        var rendererHistory = (IDictionary)contextType.GetField("s_PreviousRendererLocalToWorld", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        var skinnedHistory = (IDictionary)contextType.GetField("s_PreviousSkinnedRendererPositions", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        var trimCamera = contextType.GetMethod("TrimCameraMotionHistory", BindingFlags.Static | BindingFlags.NonPublic)!;
        var trimRenderer = contextType.GetMethod("TrimRendererMotionHistory", BindingFlags.Static | BindingFlags.NonPublic)!;
        lock (historyLock)
        {
            cameraHistory.Clear(); rendererHistory.Clear(); skinnedHistory.Clear();
            try
            {
                for (int id = 0; id < entryCount; id++)
                {
                    cameraHistory[(long)id] = Matrix4x4.Translate(new Vector3(id, 0f, 0f));
                    rendererHistory[id] = Matrix4x4.Translate(new Vector3(0f, id, 0f));
                    skinnedHistory[id] = new[] { new Vector3(0f, 0f, id) };
                }
                trimCamera.Invoke(null, null); trimRenderer.Invoke(null, null);
                int expected = Math.Min(entryCount, maximumEntries);
                Assert.Equal(expected, cameraHistory.Count);
                Assert.Equal(expected, rendererHistory.Count);
                Assert.Equal(expected, skinnedHistory.Count);
            }
            finally { cameraHistory.Clear(); rendererHistory.Clear(); skinnedHistory.Clear(); }
        }
    }

    [Theory]
    [InlineData(.002f)]
    [InlineData(.004f)]
    [InlineData(.006f)]
    [InlineData(.008f)]
    [InlineData(.010f)]
    [InlineData(.012f)]
    [InlineData(.014f)]
    [InlineData(.016f)]
    [InlineData(.018f)]
    [InlineData(.020f)]
    public void MotionHistory_KeepsPreviousViewProjectionPerStereoEye(float separation)
    {
        var cameraObject = new GameObject("Stereo history camera");
        try
        {
            var camera = cameraObject.AddComponent<Camera>();
            camera.stereoSeparation = separation;
            var context = new ScriptableRenderContext();
            Matrix4x4 leftPrevious = Matrix4x4.Translate(new Vector3(-separation, 0f, 0f));
            Matrix4x4 rightPrevious = Matrix4x4.Translate(new Vector3(separation, 0f, 0f));
            context.SetNativeCameraDrawState(null, null, leftPrevious, camera, leftPrevious, 0);
            context.SetNativeCameraDrawState(null, null, Matrix4x4.identity);
            context.SetNativeCameraDrawState(null, null, rightPrevious, camera, rightPrevious, 1);
            context.SetNativeCameraDrawState(null, null, Matrix4x4.identity);

            var previousField = typeof(ScriptableRenderContext).GetField(
                "_nativePreviousMotionViewProjection", BindingFlags.Instance | BindingFlags.NonPublic)!;
            Matrix4x4 leftCurrent = Matrix4x4.Translate(new Vector3(-2f * separation, 0f, 0f));
            context.SetNativeCameraDrawState(null, null, leftCurrent, camera, leftCurrent, 0);
            Assert.Equal(leftPrevious, (Matrix4x4)previousField.GetValue(context)!);
            context.SetNativeCameraDrawState(null, null, Matrix4x4.identity);
            Matrix4x4 rightCurrent = Matrix4x4.Translate(new Vector3(2f * separation, 0f, 0f));
            context.SetNativeCameraDrawState(null, null, rightCurrent, camera, rightCurrent, 1);
            Assert.Equal(rightPrevious, (Matrix4x4)previousField.GetValue(context)!);
            context.SetNativeCameraDrawState(null, null, Matrix4x4.identity);
        }
        finally { Object.DestroyImmediate(cameraObject); }
    }

    [Fact]
    public void NativeCameraMeshRaster_FirstFrameMotionIsZero()
    {
        if (!AnityNative.Available) return;
        var pixel = RasterAndCopyMotion(null);
        Assert.InRange(pixel.r, 120, 136); Assert.InRange(pixel.g, 120, 136);
    }

    [Fact]
    public void NativeCameraMeshRaster_MotionAttachmentClearsToZero()
    {
        if (!AnityNative.Available) return;
        using var device = NativeGraphicsDevice.Create(GraphicsDeviceType.Metal, 4, 4, false);
        var source = new RenderTexture(4, 4, 24, RenderTextureFormat.ARGB32); var motion = NewMotionTexture(4, 4);
        try
        {
            Assert.True(device.EnsureCameraRenderTarget(source)); Assert.True(device.EnsureCameraRenderTarget(motion));
            Assert.True(device.TryRecordCameraPass((ulong)(uint)source.GetInstanceID(), 4, 4, new Rect(0, 0, 4, 4), Color.black, true, true, 1, false, false, false));
            Assert.True(device.TryCopyCameraRenderTargetMotionToColor(source, false, motion));
            Assert.True(device.TryReadbackCameraRenderTargetRGBA8(motion, out var pixels));
            for (int i = 0; i < pixels.Length; i += 4) { Assert.InRange(pixels[i], (byte)120, (byte)136); Assert.InRange(pixels[i + 1], (byte)120, (byte)136); }
        }
        finally { device.ReleaseCameraRenderTarget(motion); device.ReleaseCameraRenderTarget(source); motion.Release(); source.Release(); }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void NativeCameraMeshRaster_MotionCopyRejectsInvalidSourceForms(int form)
    {
        if (!AnityNative.Available) return;
        using var device = NativeGraphicsDevice.Create(GraphicsDeviceType.Metal, 4, 4, false);
        var target = NewMotionTexture(4, 4); var source = new RenderTexture(4, 4, 0, RenderTextureFormat.ARGB32);
        try
        {
            Assert.True(device.EnsureCameraRenderTarget(target));
            bool copied = form switch { 0 => device.TryCopyCameraRenderTargetMotionToColor(null, false, target), 1 => device.TryCopyCameraRenderTargetMotionToColor(target, false, target), _ => device.TryCopyCameraRenderTargetMotionToColor(source, false, target) };
            Assert.False(copied);
        }
        finally { device.ReleaseCameraRenderTarget(target); target.Release(); source.Release(); }
    }

    private static (byte r, byte g) RasterAndCopyMotion(Matrix4x4? previous, Vector3[]? previousPositions = null,
        Matrix4x4? motion = null, Matrix4x4? raster = null)
    {
        using var device = NativeGraphicsDevice.Create(GraphicsDeviceType.Metal, 8, 8, false);
        var source = new RenderTexture(8, 8, 24, RenderTextureFormat.ARGB32); var destination = NewMotionTexture(8, 8);
        try
        {
            Assert.True(device.EnsureCameraRenderTarget(source)); Assert.True(device.EnsureCameraRenderTarget(destination));
            Assert.True(device.TryRecordCameraPass((ulong)(uint)source.GetInstanceID(), 8, 8, new Rect(0, 0, 8, 8), Color.black, true, true, 1, false, false, false));
            Assert.True(device.TryDrawCameraMesh(source, TrianglePositions(), TriangleNormals(),
                FilledColors(3, Color.white), TriangleIndices(), raster ?? Matrix4x4.identity, null, previous,
                motionObjectToClip: motion,
                previousPositions: previousPositions));
            Assert.True(device.TryCopyCameraRenderTargetMotionToColor(source, false, destination)); Assert.True(device.TryReadbackCameraRenderTargetRGBA8(destination, out var pixels));
            int center = ((4 * 8) + 4) * 4; return (pixels[center], pixels[center + 1]);
        }
        finally { device.ReleaseCameraRenderTarget(destination); device.ReleaseCameraRenderTarget(source); destination.Release(); source.Release(); }
    }

    private static RenderTexture NewMotionTexture(int width, int height)
    {
        var descriptor = new RenderTextureDescriptor(width, height, RenderTextureFormat.RGHalf, 0) { graphicsFormat = GraphicsFormat.R16G16_SFloat };
        return new RenderTexture(descriptor);
    }

    // Camera.targetTexture is optional in Unity. These cases exercise the
    // presentation CameraTarget rather than the RenderTexture path above.
    [Theory]
    [InlineData(1f, 0f, 0f, 0)]
    [InlineData(0f, 1f, 0f, 1)]
    [InlineData(0f, 0f, 1f, 2)]
    public void NativeCameraTargetMeshRaster_WritesTriangleColor(float r, float g, float b, int channel)
    {
        if (!AnityNative.Available) return;
        using var device = NewMetalCameraTargetDevice();
        RecordCameraTarget(device);
        Assert.True(device.TryDrawCameraMesh(null, TrianglePositions(), TriangleNormals(),
            FilledColors(3, new Color(r, g, b, 1f)), TriangleIndices(), Matrix4x4.identity,
            targetIsCameraTarget: true));
        Assert.True(device.TryReadbackSwapchainRGBA8(out var pixels));
        int center = ((4 * 8) + 4) * 4;
        Assert.True(pixels[center + channel] > 220);
    }

    [Theory]
    [InlineData(0f, 0f, 1f, 128, 128, 255)]
    [InlineData(1f, 0f, 0f, 255, 128, 128)]
    [InlineData(0f, 1f, 0f, 128, 255, 128)]
    [InlineData(0f, 0f, -1f, 128, 128, 0)]
    public void NativeCameraTargetMeshRaster_CopiesWorldNormals(float x, float y, float z,
        int expectedR, int expectedG, int expectedB)
    {
        if (!AnityNative.Available) return;
        var pixel = RasterCameraTargetNormal(new Vector3(x, y, z));
        Assert.InRange(pixel.r, expectedR - 12, expectedR + 12);
        Assert.InRange(pixel.g, expectedG - 12, expectedG + 12);
        Assert.InRange(pixel.b, expectedB - 12, expectedB + 12);
    }

    [Theory]
    [InlineData(-0.5f, 159)]
    [InlineData(0.5f, 96)]
    public void NativeCameraTargetMeshRaster_CopiesObjectMotion(float previousX, int expectedR)
    {
        if (!AnityNative.Available) return;
        var pixel = RasterCameraTargetMotion(Matrix4x4.Translate(new Vector3(previousX, 0f, 0f)));
        Assert.InRange(pixel.r, expectedR - 6, expectedR + 6);
        Assert.InRange(pixel.g, 120, 136);
    }

    [Fact]
    public void NativeCameraTargetMeshRaster_ForceNoMotionWritesZeroVelocity()
    {
        if (!AnityNative.Available) return;
        var pixel = RasterCameraTargetMotion(Matrix4x4.identity);
        Assert.InRange(pixel.r, 120, 136);
        Assert.InRange(pixel.g, 120, 136);
    }

    private static (byte r, byte g, byte b) RasterCameraTargetNormal(Vector3 normal)
    {
        using var device = NewMetalCameraTargetDevice();
        var destination = NewNormalTexture(8, 8);
        try
        {
            Assert.True(device.EnsureCameraRenderTarget(destination));
            RecordCameraTarget(device);
            Assert.True(device.TryDrawCameraMesh(null, TrianglePositions(),
                new[] { normal, normal, normal }, FilledColors(3, Color.white), TriangleIndices(),
                Matrix4x4.identity, targetIsCameraTarget: true));
            Assert.True(device.TryCopyCameraRenderTargetNormalsToColor(null, true, destination));
            Assert.True(device.TryReadbackCameraRenderTargetRGBA8(destination, out var pixels));
            int center = ((4 * 8) + 4) * 4;
            return (pixels[center], pixels[center + 1], pixels[center + 2]);
        }
        finally { device.ReleaseCameraRenderTarget(destination); destination.Release(); }
    }

    private static (byte r, byte g) RasterCameraTargetMotion(Matrix4x4 previousObjectToClip)
    {
        using var device = NewMetalCameraTargetDevice();
        var destination = NewMotionTexture(8, 8);
        try
        {
            Assert.True(device.EnsureCameraRenderTarget(destination));
            RecordCameraTarget(device);
            Assert.True(device.TryDrawCameraMesh(null, TrianglePositions(), TriangleNormals(),
                FilledColors(3, Color.white), TriangleIndices(), Matrix4x4.identity, null,
                previousObjectToClip, targetIsCameraTarget: true));
            Assert.True(device.TryCopyCameraRenderTargetMotionToColor(null, true, destination));
            Assert.True(device.TryReadbackCameraRenderTargetRGBA8(destination, out var pixels));
            int center = ((4 * 8) + 4) * 4;
            return (pixels[center], pixels[center + 1]);
        }
        finally { device.ReleaseCameraRenderTarget(destination); destination.Release(); }
    }

    private static NativeGraphicsDevice NewMetalCameraTargetDevice(int samples = 1)
    {
        var device = NativeGraphicsDevice.Create(GraphicsDeviceType.Metal, 8, 8, false, msaa: samples);
        Assert.True(device.CreateSwapchain(8, 8, imageCount: 2));
        return device;
    }

    private static void RecordCameraTarget(NativeGraphicsDevice device, int samples = 1) =>
        Assert.True(device.TryRecordCameraPass(0, 8, 8, new Rect(0, 0, 8, 8),
            Color.black, true, true, samples, false, false, isCameraTarget: true));

    private static Vector3[] TrianglePositions() => new[]
    {
        new Vector3(-.8f, -.8f, .5f), new Vector3(.8f, -.8f, .5f), new Vector3(0f, .8f, .5f)
    };

    private static Vector3[] TriangleNormals() => new[] { Vector3.forward, Vector3.forward, Vector3.forward };
    private static int[] TriangleIndices() => new[] { 0, 1, 2 };
    private static Color[] FilledColors(int count, Color color)
    {
        var result = new Color[count]; Array.Fill(result, color); return result;
    }

    [Theory]
    [InlineData(0, 1f, 0.5f, 245, 10)]
    [InlineData(1, 1f, 0.5f, 120, 120)]
    [InlineData(2, 0.5f, 0.5f, 120, 120)]
    [InlineData(3, 1f, 0.5f, 120, 245)]
    [InlineData(4, 1f, 1f, 0, 10)]
    public void NativeCameraMeshRaster_ExecutesUnityBlendModes(int blendMode,
        float sourceRed, float sourceAlpha, int expectedRed, int expectedBlue)
    {
        if (!AnityNative.Available) return;
        using var device = NativeGraphicsDevice.Create(GraphicsDeviceType.Metal, 8, 8, false);
        var target = new RenderTexture(8, 8, 24, RenderTextureFormat.ARGB32);
        try
        {
            Assert.True(device.EnsureCameraRenderTarget(target));
            Assert.True(device.TryRecordCameraPass((ulong)(uint)target.GetInstanceID(), 8, 8,
                new Rect(0, 0, 8, 8), Color.blue, true, true, 1, false, false, false));
            Assert.True(device.TryDrawCameraMesh(target, TrianglePositions(), TriangleNormals(),
                FilledColors(3, new Color(sourceRed, 0f, 0f, sourceAlpha)), TriangleIndices(),
                Matrix4x4.identity, blendMode: blendMode, depthWriteEnabled: false));
            Assert.True(device.TryReadbackCameraRenderTargetRGBA8(target, out var pixels));
            int center = ((4 * 8) + 4) * 4;
            Assert.InRange(pixels[center], expectedRed - 12, expectedRed + 12);
            Assert.InRange(pixels[center + 2], expectedBlue - 12, expectedBlue + 12);
        }
        finally { device.ReleaseCameraRenderTarget(target); target.Release(); }
    }

    [Theory]
    [InlineData(0.49f, 0.5f, false)]
    [InlineData(0.5f, 0.5f, true)]
    [InlineData(1f, 0f, true)]
    public void NativeCameraMeshRaster_AlphaClipUsesUnityCutoffBoundary(float alpha, float cutoff, bool visible)
    {
        if (!AnityNative.Available) return;
        using var device = NativeGraphicsDevice.Create(GraphicsDeviceType.Metal, 8, 8, false);
        var target = new RenderTexture(8, 8, 24, RenderTextureFormat.ARGB32);
        try
        {
            Assert.True(device.EnsureCameraRenderTarget(target));
            Assert.True(device.TryRecordCameraPass((ulong)(uint)target.GetInstanceID(), 8, 8,
                new Rect(0, 0, 8, 8), Color.black, true, true, 1, false, false, false));
            Assert.True(device.TryDrawCameraMesh(target, TrianglePositions(), TriangleNormals(),
                FilledColors(3, new Color(1f, 0f, 0f, alpha)), TriangleIndices(), Matrix4x4.identity,
                alphaClipEnabled: true, alphaClipThreshold: cutoff));
            Assert.True(device.TryReadbackCameraRenderTargetRGBA8(target, out var pixels));
            int center = ((4 * 8) + 4) * 4;
            Assert.Equal(visible, pixels[center] > 220);
        }
        finally { device.ReleaseCameraRenderTarget(target); target.Release(); }
    }

    [Theory]
    [InlineData(-1, 0.5f)]
    [InlineData(5, float.NaN)]
    public void NativeCameraMeshRaster_RejectsInvalidBlendContract(int blendMode, float cutoff)
    {
        if (!AnityNative.Available) return;
        using var device = NativeGraphicsDevice.Create(GraphicsDeviceType.Metal, 4, 4, false);
        var target = new RenderTexture(4, 4, 24, RenderTextureFormat.ARGB32);
        try
        {
            Assert.True(device.EnsureCameraRenderTarget(target));
            Assert.False(device.TryDrawCameraMesh(target,
                new[] { new Vector3(-.8f, -.8f, .5f), new Vector3(.8f, -.8f, .5f), new Vector3(0f, .8f, .5f) },
                TriangleNormals(), FilledColors(3, Color.white), TriangleIndices(), Matrix4x4.identity,
                blendMode: blendMode, alphaClipThreshold: cutoff));
        }
        finally { device.ReleaseCameraRenderTarget(target); target.Release(); }
    }

    [Theory]
    [InlineData(1f, 0f, 0f, 0)]
    [InlineData(0f, 1f, 0f, 1)]
    [InlineData(0f, 0f, 1f, 2)]
    [InlineData(1f, 1f, 1f, 0)]
    public void NativeCameraMeshRaster_BaseMapModulatesVertexColor(float r, float g, float b, int channel)
    {
        if (!AnityNative.Available) return;
        using var device = NativeGraphicsDevice.Create(GraphicsDeviceType.Metal, 8, 8, false);
        var target = new RenderTexture(8, 8, 24, RenderTextureFormat.ARGB32);
        var texture = SolidTexture(new Color(r, g, b, 1f));
        try
        {
            Assert.True(device.EnsureCameraRenderTarget(target));
            Assert.True(device.TryRecordCameraPass((ulong)(uint)target.GetInstanceID(), 8, 8, new Rect(0, 0, 8, 8), Color.black, true, true, 1, false, false, false));
            Assert.True(device.TryDrawCameraMesh(target, TrianglePositions(), TriangleNormals(), FilledColors(3, Color.white), TriangleIndices(), Matrix4x4.identity, uvs: TriangleUvs(Vector2.zero), baseTexture: texture));
            Assert.True(device.TryReadbackCameraRenderTargetRGBA8(target, out var pixels));
            Assert.True(pixels[((4 * 8 + 4) * 4) + channel] > 220);
        }
        finally { device.ReleaseCameraRenderTarget(target); target.Release(); Object.DestroyImmediate(texture); }
    }

    [Theory]
    [InlineData(0f, 255)]
    [InlineData(0.99f, 0)]
    [InlineData(1.2f, 255)]
    public void NativeCameraMeshRaster_BaseMapUsesPointRepeatSampler(float u, int expectedRed)
    {
        if (!AnityNative.Available) return;
        using var device = NativeGraphicsDevice.Create(GraphicsDeviceType.Metal, 8, 8, false);
        var target = new RenderTexture(8, 8, 24, RenderTextureFormat.ARGB32);
        var texture = new Texture2D(2, 1); texture.filterMode = FilterMode.Point; texture.wrapMode = UnityEngine.TextureWrapMode.Repeat;
        texture.SetPixel(0, 0, Color.red); texture.SetPixel(1, 0, Color.black); texture.Apply();
        try
        {
            Assert.True(device.EnsureCameraRenderTarget(target)); Assert.True(device.TryRecordCameraPass((ulong)(uint)target.GetInstanceID(), 8, 8, new Rect(0, 0, 8, 8), Color.black, true, true, 1, false, false, false));
            Assert.True(device.TryDrawCameraMesh(target, TrianglePositions(), TriangleNormals(), FilledColors(3, Color.white), TriangleIndices(), Matrix4x4.identity, uvs: TriangleUvs(new Vector2(u, 0f)), baseTexture: texture));
            Assert.True(device.TryReadbackCameraRenderTargetRGBA8(target, out var pixels)); Assert.InRange(pixels[(4 * 8 + 4) * 4], expectedRed - 12, expectedRed + 12);
        }
        finally { device.ReleaseCameraRenderTarget(target); target.Release(); Object.DestroyImmediate(texture); }
    }

    [Theory]
    [InlineData(0.49f, false)]
    [InlineData(0.5f, true)]
    [InlineData(1f, true)]
    public void NativeCameraMeshRaster_BaseMapAlphaParticipatesInAlphaClip(float alpha, bool visible)
    {
        if (!AnityNative.Available) return;
        using var device = NativeGraphicsDevice.Create(GraphicsDeviceType.Metal, 8, 8, false);
        var target = new RenderTexture(8, 8, 24, RenderTextureFormat.ARGB32); var texture = SolidTexture(new Color(1f, 0f, 0f, alpha));
        try
        {
            Assert.True(device.EnsureCameraRenderTarget(target)); Assert.True(device.TryRecordCameraPass((ulong)(uint)target.GetInstanceID(), 8, 8, new Rect(0, 0, 8, 8), Color.black, true, true, 1, false, false, false));
            Assert.True(device.TryDrawCameraMesh(target, TrianglePositions(), TriangleNormals(), FilledColors(3, Color.white), TriangleIndices(), Matrix4x4.identity, alphaClipEnabled: true, alphaClipThreshold: .5f, uvs: TriangleUvs(Vector2.zero), baseTexture: texture));
            Assert.True(device.TryReadbackCameraRenderTargetRGBA8(target, out var pixels)); Assert.Equal(visible, pixels[(4 * 8 + 4) * 4] > 220);
        }
        finally { device.ReleaseCameraRenderTarget(target); target.Release(); Object.DestroyImmediate(texture); }
    }

    [Theory]
    [InlineData(1f, 0f, 255)]
    [InlineData(1f, .99f, 0)]
    [InlineData(1f, 1.2f, 255)]
    [InlineData(2f, 0f, 255)]
    [InlineData(2f, .5f, 0)]
    [InlineData(2f, 1f, 255)]
    [InlineData(.5f, 0f, 255)]
    [InlineData(.5f, .5f, 0)]
    [InlineData(.5f, .99f, 0)]
    [InlineData(3f, .99f, 0)]
    public void NativeCameraMeshRaster_BaseMapSTTransformsUvBeforeSampling(float scaleX, float offsetX, int expectedRed)
    {
        if (!AnityNative.Available) return;
        using var device = NativeGraphicsDevice.Create(GraphicsDeviceType.Metal, 8, 8, false);
        var target = new RenderTexture(8, 8, 24, RenderTextureFormat.ARGB32);
        var texture = new Texture2D(2, 1) { filterMode = FilterMode.Point, wrapMode = UnityEngine.TextureWrapMode.Repeat };
        texture.SetPixel(0, 0, Color.red); texture.SetPixel(1, 0, Color.black); texture.Apply();
        try
        {
            Assert.True(device.EnsureCameraRenderTarget(target)); Assert.True(device.TryRecordCameraPass((ulong)(uint)target.GetInstanceID(), 8, 8, new Rect(0, 0, 8, 8), Color.black, true, true, 1, false, false, false));
            Assert.True(device.TryDrawCameraMesh(target, TrianglePositions(), TriangleNormals(), FilledColors(3, Color.white), TriangleIndices(), Matrix4x4.identity, uvs: TriangleUvs(Vector2.zero), baseTexture: texture, baseMapScale: new Vector2(scaleX, 1f), baseMapOffset: new Vector2(offsetX, 0f)));
            Assert.True(device.TryReadbackCameraRenderTargetRGBA8(target, out var pixels)); Assert.InRange(pixels[(4 * 8 + 4) * 4], expectedRed - 12, expectedRed + 12);
        }
        finally { device.ReleaseCameraRenderTarget(target); target.Release(); Object.DestroyImmediate(texture); }
    }

    [Theory]
    [InlineData(UnityEngine.TextureWrapMode.Clamp, -0.5f, 0)]
    [InlineData(UnityEngine.TextureWrapMode.Clamp, 0.2f, 255)]
    [InlineData(UnityEngine.TextureWrapMode.Clamp, 1.2f, 255)]
    [InlineData(UnityEngine.TextureWrapMode.Repeat, -0.2f, 0)]
    [InlineData(UnityEngine.TextureWrapMode.Repeat, 1.2f, 255)]
    [InlineData(UnityEngine.TextureWrapMode.Mirror, -0.2f, 0)]
    [InlineData(UnityEngine.TextureWrapMode.Mirror, -0.99f, 255)]
    [InlineData(UnityEngine.TextureWrapMode.Mirror, 1.2f, 255)]
    [InlineData(UnityEngine.TextureWrapMode.MirrorOnce, -0.2f, 0)]
    [InlineData(UnityEngine.TextureWrapMode.MirrorOnce, 1.2f, 255)]
    public void NativeCameraMeshRaster_BaseMapUsesRegistryWrapMode(UnityEngine.TextureWrapMode wrapMode, float u, int expectedRed)
    {
        if (!AnityNative.Available) return;
        using var device = NativeGraphicsDevice.Create(GraphicsDeviceType.Metal, 8, 8, false);
        var target = new RenderTexture(8, 8, 24, RenderTextureFormat.ARGB32);
        var texture = new Texture2D(2, 1) { filterMode = FilterMode.Point, wrapMode = wrapMode };
        texture.SetPixel(0, 0, Color.red); texture.SetPixel(1, 0, Color.black); texture.Apply();
        try
        {
            Assert.True(device.EnsureCameraRenderTarget(target)); Assert.True(device.TryRecordCameraPass((ulong)(uint)target.GetInstanceID(), 8, 8, new Rect(0, 0, 8, 8), Color.black, true, true, 1, false, false, false));
            Assert.True(device.TryDrawCameraMesh(target, TrianglePositions(), TriangleNormals(), FilledColors(3, Color.white), TriangleIndices(), Matrix4x4.identity, uvs: TriangleUvs(new Vector2(u, 0f)), baseTexture: texture));
            Assert.True(device.TryReadbackCameraRenderTargetRGBA8(target, out var pixels)); Assert.InRange(pixels[(4 * 8 + 4) * 4], expectedRed - 12, expectedRed + 12);
        }
        finally { device.ReleaseCameraRenderTarget(target); target.Release(); Object.DestroyImmediate(texture); }
    }

    [Theory]
    [InlineData(FilterMode.Bilinear, 0f, 128)]
    [InlineData(FilterMode.Bilinear, .25f, 255)]
    [InlineData(FilterMode.Bilinear, .5f, 128)]
    [InlineData(FilterMode.Bilinear, .75f, 0)]
    [InlineData(FilterMode.Bilinear, 1f, 128)]
    [InlineData(FilterMode.Trilinear, 0f, 128)]
    [InlineData(FilterMode.Trilinear, .25f, 255)]
    [InlineData(FilterMode.Trilinear, .5f, 128)]
    [InlineData(FilterMode.Trilinear, .75f, 0)]
    [InlineData(FilterMode.Trilinear, 1f, 128)]
    public void NativeCameraMeshRaster_BaseMapInterpolatesBilinearAndTrilinear(FilterMode filterMode, float u, int expectedRed)
    {
        if (!AnityNative.Available) return;
        using var device = NativeGraphicsDevice.Create(GraphicsDeviceType.Metal, 8, 8, false);
        var target = new RenderTexture(8, 8, 24, RenderTextureFormat.ARGB32);
        var texture = new Texture2D(2, 1) { filterMode = filterMode, wrapMode = UnityEngine.TextureWrapMode.Clamp };
        texture.SetPixel(0, 0, Color.red); texture.SetPixel(1, 0, Color.black); texture.Apply();
        try
        {
            Assert.True(device.EnsureCameraRenderTarget(target)); Assert.True(device.TryRecordCameraPass((ulong)(uint)target.GetInstanceID(), 8, 8, new Rect(0, 0, 8, 8), Color.black, true, true, 1, false, false, false));
            Assert.True(device.TryDrawCameraMesh(target, TrianglePositions(), TriangleNormals(), FilledColors(3, Color.white), TriangleIndices(), Matrix4x4.identity, uvs: TriangleUvs(new Vector2(u, 0f)), baseTexture: texture));
            Assert.True(device.TryReadbackCameraRenderTargetRGBA8(target, out var pixels)); Assert.InRange(pixels[(4 * 8 + 4) * 4], expectedRed - 12, expectedRed + 12);
        }
        finally { device.ReleaseCameraRenderTarget(target); target.Release(); Object.DestroyImmediate(texture); }
    }

    [Theory]
    [InlineData(.5f, .5f, 1f, 1f, 128, 128, 255)]
    [InlineData(.5f, .5f, 0f, 1f, 128, 128, 0)]
    [InlineData(.5f, .5f, 0f, -1f, 128, 128, 0)]
    [InlineData(.5f, 1f, .5f, 1f, 128, 255, 128)]
    [InlineData(.5f, 0f, .5f, 1f, 128, 0, 128)]
    [InlineData(.5f, 1f, .5f, -1f, 128, 0, 128)]
    [InlineData(.5f, 0f, .5f, -1f, 128, 255, 128)]
    [InlineData(1f, .5f, .5f, 1f, 255, 128, 128)]
    [InlineData(0f, .5f, .5f, 1f, 0, 128, 128)]
    [InlineData(.5f, .5f, 1f, -1f, 128, 128, 255)]
    public void NativeCameraMeshRaster_BumpMapWritesWorldSpaceNormals(float r, float g, float b, float tangentW,
        int expectedR, int expectedG, int expectedB)
    {
        if (!AnityNative.Available) return;
        using var device = NativeGraphicsDevice.Create(GraphicsDeviceType.Metal, 8, 8, false);
        var source = new RenderTexture(8, 8, 24, RenderTextureFormat.ARGB32); var destination = NewNormalTexture(8, 8);
        var normalMap = NormalMapTexture(new Color(r, g, b, 1f));
        try
        {
            Assert.True(device.EnsureCameraRenderTarget(source)); Assert.True(device.EnsureCameraRenderTarget(destination));
            Assert.True(device.TryRecordCameraPass((ulong)(uint)source.GetInstanceID(), 8, 8, new Rect(0, 0, 8, 8), Color.black, true, true, 1, false, false, false));
            Assert.True(device.TryDrawCameraMesh(source, TrianglePositions(), TriangleNormals(), FilledColors(3, Color.white), TriangleIndices(), Matrix4x4.identity,
                uvs: TriangleUvs(Vector2.zero), tangents: new[] { new Vector4(1f, 0f, 0f, tangentW), new Vector4(1f, 0f, 0f, tangentW), new Vector4(1f, 0f, 0f, tangentW) }, normalMap: normalMap));
            Assert.True(device.TryCopyCameraRenderTargetNormalsToColor(source, false, destination)); Assert.True(device.TryReadbackCameraRenderTargetRGBA8(destination, out var pixels));
            int center = (4 * 8 + 4) * 4; Assert.InRange(pixels[center], expectedR - 14, expectedR + 14); Assert.InRange(pixels[center + 1], expectedG - 14, expectedG + 14); Assert.InRange(pixels[center + 2], expectedB - 14, expectedB + 14);
        }
        finally { device.ReleaseCameraRenderTarget(destination); device.ReleaseCameraRenderTarget(source); destination.Release(); source.Release(); Object.DestroyImmediate(normalMap); }
    }

    [Theory]
    [InlineData(1, .5f, .5f, 1f, 1f, 128, 128, 255)]
    [InlineData(2, .5f, .5f, 1f, 1f, 128, 128, 255)]
    [InlineData(4, .5f, .5f, 1f, 1f, 128, 128, 255)]
    [InlineData(1, 1f, .5f, .5f, 1f, 255, 128, 128)]
    [InlineData(2, 1f, .5f, .5f, 1f, 255, 128, 128)]
    [InlineData(4, 0f, .5f, .5f, 1f, 0, 128, 128)]
    [InlineData(1, .5f, 1f, .5f, 1f, 128, 255, 128)]
    [InlineData(2, .5f, 1f, .5f, -1f, 128, 0, 128)]
    [InlineData(4, .5f, .5f, 0f, 1f, 128, 128, 0)]
    [InlineData(4, .5f, .5f, 0f, -1f, 128, 128, 0)]
    public void NativeCameraTargetMeshRaster_BumpMapWritesResolvedWorldNormals(int samples,
        float r, float g, float b, float tangentW, int expectedR, int expectedG, int expectedB)
    {
        if (!AnityNative.Available) return;
        using var device = NewMetalCameraTargetDevice(samples);
        var destination = NewNormalTexture(8, 8); var normalMap = NormalMapTexture(new Color(r, g, b, 1f));
        try
        {
            Assert.True(device.EnsureCameraRenderTarget(destination)); RecordCameraTarget(device, samples);
            Assert.True(device.TryDrawCameraMesh(null, TrianglePositions(), TriangleNormals(), FilledColors(3, Color.white), TriangleIndices(), Matrix4x4.identity,
                targetIsCameraTarget: true, uvs: TriangleUvs(Vector2.zero), tangents: new[] { new Vector4(1f, 0f, 0f, tangentW), new Vector4(1f, 0f, 0f, tangentW), new Vector4(1f, 0f, 0f, tangentW) }, normalMap: normalMap));
            Assert.True(device.TryCopyCameraRenderTargetNormalsToColor(null, true, destination)); Assert.True(device.TryReadbackCameraRenderTargetRGBA8(destination, out var pixels));
            int center = (4 * 8 + 4) * 4; Assert.InRange(pixels[center], expectedR - 14, expectedR + 14); Assert.InRange(pixels[center + 1], expectedG - 14, expectedG + 14); Assert.InRange(pixels[center + 2], expectedB - 14, expectedB + 14);
        }
        finally { device.ReleaseCameraRenderTarget(destination); destination.Release(); Object.DestroyImmediate(normalMap); }
    }

    [Theory]
    [InlineData(0f, 2f, 1f, 0f, 0f)]
    [InlineData(0f, 2f, 0f, 1f, 2f)]
    [InlineData(0f, 2f, .5f, .5f, 1f)]
    [InlineData(-2f, 2f, .5f, .5f, 0f)]
    [InlineData(3f, 7f, .25f, .75f, 6f)]
    [InlineData(-1f, 1f, .25f, .75f, .5f)]
    [InlineData(0f, 4f, .25f, .25f, 2f)]
    [InlineData(2f, 6f, 2f, 1f, 10f / 3f)]
    [InlineData(-3f, 3f, 1f, 2f, 1f)]
    [InlineData(1f, 5f, .8f, .2f, 1.8f)]
    public void NativeSkinning_BlendWeightsDeformVerticesInRendererLocalSpace(
        float bone0X, float bone1X, float weight0, float weight1, float expectedX)
    {
        if (!AnityNative.Available) return;
        var rendererObject = new GameObject("Skinned renderer"); var bone0Object = new GameObject("Bone 0"); var bone1Object = new GameObject("Bone 1");
        var mesh = new Mesh { vertices = new[] { Vector3.zero, Vector3.right, Vector3.up }, normals = new[] { Vector3.forward, Vector3.forward, Vector3.forward }, tangents = new[] { new Vector4(1f, 0f, 0f, 1f), new Vector4(1f, 0f, 0f, 1f), new Vector4(1f, 0f, 0f, 1f) } };
        try
        {
            bone0Object.transform.position = new Vector3(bone0X, 0f, 0f); bone1Object.transform.position = new Vector3(bone1X, 0f, 0f);
            mesh.bindposes = new[] { Matrix4x4.identity, Matrix4x4.identity };
            mesh.boneWeights = new[]
            {
                new BoneWeight { boneIndex0 = 0, boneIndex1 = 1, weight0 = weight0, weight1 = weight1 },
                new BoneWeight { boneIndex0 = 0, boneIndex1 = 1, weight0 = weight0, weight1 = weight1 },
                new BoneWeight { boneIndex0 = 0, boneIndex1 = 1, weight0 = weight0, weight1 = weight1 }
            };
            var renderer = rendererObject.AddComponent<SkinnedMeshRenderer>(); renderer.sharedMesh = mesh; renderer.bones = new[] { bone0Object.transform, bone1Object.transform };
            Assert.True(NativeGraphicsDevice.TrySkinMeshVertices(mesh, renderer, out var positions, out var normals, out var tangents));
            Assert.InRange(positions[0].x, expectedX - .0001f, expectedX + .0001f); Assert.InRange(positions[0].y, -.0001f, .0001f); Assert.InRange(positions[0].z, -.0001f, .0001f);
            Assert.InRange(normals[0].z, .999f, 1.001f); Assert.InRange(tangents[0].x, .999f, 1.001f); Assert.Equal(1f, tangents[0].w);
            var baked = new Mesh(); renderer.BakeMesh(baked, false);
            Assert.InRange(baked.vertices[0].x, expectedX - .0001f, expectedX + .0001f); Assert.Equal(mesh.triangles, baked.triangles); Object.DestroyImmediate(baked);
        }
        finally { Object.DestroyImmediate(mesh); Object.DestroyImmediate(bone1Object); Object.DestroyImmediate(bone0Object); Object.DestroyImmediate(rendererObject); }
    }

    [Theory]
    [InlineData(-50f, 0f, -.5f, 0f)]
    [InlineData(0f, 0f, 0f, 0f)]
    [InlineData(25f, 0f, .25f, 0f)]
    [InlineData(50f, 0f, .5f, 0f)]
    [InlineData(75f, 0f, .75f, 0f)]
    [InlineData(100f, 0f, 1f, 0f)]
    [InlineData(150f, 0f, 1.5f, 0f)]
    [InlineData(25f, 100f, .25f, .25f)]
    [InlineData(75f, 50f, .75f, .125f)]
    [InlineData(100f, -100f, 1f, -.25f)]
    public void NativeBlendShapes_EvaluateFramesAndComposeBeforeSkinning(float moveWeight,
        float liftWeight, float expectedX, float expectedY)
    {
        if (!AnityNative.Available) return;
        var rendererObject = new GameObject("Blendshape renderer"); var boneObject = new GameObject("Blendshape bone");
        var mesh = new Mesh { vertices = new[] { Vector3.zero, Vector3.right, Vector3.up }, normals = new[] { Vector3.forward, Vector3.forward, Vector3.forward }, tangents = new[] { new Vector4(1f, 0f, 0f, 1f), new Vector4(1f, 0f, 0f, 1f), new Vector4(1f, 0f, 0f, 1f) }, triangles = new[] { 0, 1, 2 } };
        try
        {
            Vector3[] zeros = { Vector3.zero, Vector3.zero, Vector3.zero };
            Vector3[] moveHalf = { new Vector3(.5f, 0f, 0f), new Vector3(.5f, 0f, 0f), new Vector3(.5f, 0f, 0f) };
            Vector3[] moveFull = { new Vector3(1f, 0f, 0f), new Vector3(1f, 0f, 0f), new Vector3(1f, 0f, 0f) };
            Vector3[] lift = { new Vector3(0f, .25f, 0f), new Vector3(0f, .25f, 0f), new Vector3(0f, .25f, 0f) };
            mesh.AddBlendShapeFrame("Move", 50f, moveHalf, zeros, zeros); mesh.AddBlendShapeFrame("Move", 100f, moveFull, zeros, zeros);
            mesh.AddBlendShapeFrame("Lift", 100f, lift, zeros, zeros);
            Assert.Equal(2, mesh.blendShapeCount); Assert.Equal("Move", mesh.GetBlendShapeName(0)); Assert.Equal(2, mesh.GetBlendShapeFrameCount(0)); Assert.Equal(50f, mesh.GetBlendShapeFrameWeight(0, 0));
            var renderer = rendererObject.AddComponent<SkinnedMeshRenderer>(); renderer.sharedMesh = mesh; renderer.bones = new[] { boneObject.transform };
            renderer.SetBlendShapeWeight(0, moveWeight); renderer.SetBlendShapeWeight(1, liftWeight);
            mesh.bindposes = new[] { Matrix4x4.identity }; mesh.boneWeights = new[] { new BoneWeight { boneIndex0 = 0, weight0 = 1f }, new BoneWeight { boneIndex0 = 0, weight0 = 1f }, new BoneWeight { boneIndex0 = 0, weight0 = 1f } };
            Assert.True(NativeGraphicsDevice.TrySkinMeshVertices(mesh, renderer, out var positions, out var normals, out var tangents));
            Assert.InRange(positions[0].x, expectedX - .0001f, expectedX + .0001f); Assert.InRange(positions[0].y, expectedY - .0001f, expectedY + .0001f);
            Assert.InRange(normals[0].z, .999f, 1.001f); Assert.Equal(1f, tangents[0].w);
            var baked = new Mesh(); renderer.BakeMesh(baked, false); Assert.InRange(baked.vertices[0].x, expectedX - .0001f, expectedX + .0001f); Assert.InRange(baked.vertices[0].y, expectedY - .0001f, expectedY + .0001f); Object.DestroyImmediate(baked);
        }
        finally { Object.DestroyImmediate(mesh); Object.DestroyImmediate(boneObject); Object.DestroyImmediate(rendererObject); }
    }

    [Fact]
    public void BlendShapeApi_CopiesFramesAndRejectsInvalidShapeContracts()
    {
        var mesh = new Mesh { vertices = new[] { Vector3.zero } };
        var objectWithRenderer = new GameObject("Blendshape API renderer");
        try
        {
            Vector3[] one = { Vector3.right };
            Assert.Throws<ArgumentNullException>(() => mesh.AddBlendShapeFrame(null!, 100f, one, one, one));
            Assert.Throws<ArgumentException>(() => mesh.AddBlendShapeFrame("Bad", 100f, Array.Empty<Vector3>(), one, one));
            mesh.AddBlendShapeFrame("Shape", 50f, one, new[] { Vector3.forward }, new[] { Vector3.up });
            Assert.Throws<ArgumentException>(() => mesh.AddBlendShapeFrame("Shape", 50f, one, one, one));
            Assert.Throws<IndexOutOfRangeException>(() => mesh.GetBlendShapeName(1));
            var vertices = new Vector3[1]; var normals = new Vector3[1]; var tangents = new Vector3[1];
            mesh.GetBlendShapeFrameVertices(0, 0, vertices, normals, tangents);
            Assert.Equal(Vector3.right, vertices[0]); Assert.Equal(Vector3.forward, normals[0]); Assert.Equal(Vector3.up, tangents[0]);
            Assert.Throws<ArgumentException>(() => mesh.GetBlendShapeFrameVertices(0, 0, Array.Empty<Vector3>(), normals, tangents));
            var renderer = objectWithRenderer.AddComponent<SkinnedMeshRenderer>(); renderer.sharedMesh = mesh;
            Assert.Throws<ArgumentOutOfRangeException>(() => renderer.SetBlendShapeWeight(0, float.NaN));
            Assert.Throws<IndexOutOfRangeException>(() => renderer.GetBlendShapeWeight(1));
            mesh.ClearBlendShapes(); Assert.Equal(0, mesh.blendShapeCount);
        }
        finally { Object.DestroyImmediate(mesh); Object.DestroyImmediate(objectWithRenderer); }
    }

    [Theory]
    [InlineData(1, 1, 0f)]
    [InlineData(2, 1, .33333334f)]
    [InlineData(4, 1, .9411765f)]
    [InlineData(8, 1, 1.59f)]
    [InlineData(1, 2, 0f)]
    [InlineData(2, 2, .33333334f)]
    [InlineData(4, 2, .9411765f)]
    [InlineData(8, 2, 1.59f)]
    [InlineData(4, 3, .9411765f)]
    [InlineData(8, 3, 1.59f)]
    public void NativeVariableSkinning_RespectsUnityInfluenceQuality(int influenceLimit, int caseId, float expectedX)
    {
        if (!AnityNative.Available) return;
        _ = caseId; SkinWeights previousQuality = QualitySettings.skinWeights;
        var rendererObject = new GameObject("Variable skin renderer"); var mesh = new Mesh { vertices = new[] { Vector3.zero, Vector3.right, Vector3.up }, normals = new[] { Vector3.forward, Vector3.forward, Vector3.forward }, tangents = new[] { new Vector4(1f, 0f, 0f, 1f), new Vector4(1f, 0f, 0f, 1f), new Vector4(1f, 0f, 0f, 1f) }, bindposes = new[] { Matrix4x4.identity, Matrix4x4.identity, Matrix4x4.identity, Matrix4x4.identity, Matrix4x4.identity, Matrix4x4.identity, Matrix4x4.identity, Matrix4x4.identity } };
        var bones = new GameObject[8];
        try
        {
            for (int i = 0; i < bones.Length; i++) { bones[i] = new GameObject($"Variable skin bone {i}"); bones[i].transform.position = new Vector3(i, 0f, 0f); }
            using var counts = new NativeArray<byte>(new byte[] { 8, 8, 8 }, Allocator.Temp);
            var values = new BoneWeight1[24]; float[] weights = { .4f, .2f, .15f, .1f, .05f, .04f, .03f, .03f };
            for (int vertex = 0; vertex < 3; vertex++) for (int influence = 0; influence < 8; influence++) values[vertex * 8 + influence] = new BoneWeight1 { boneIndex = influence, weight = weights[influence] };
            using var allWeights = new NativeArray<BoneWeight1>(values, Allocator.Temp); mesh.SetBoneWeights(counts, allWeights);
            var renderer = rendererObject.AddComponent<SkinnedMeshRenderer>(); renderer.sharedMesh = mesh; renderer.bones = Array.ConvertAll(bones, bone => bone.transform);
            renderer.skinWeight = influenceLimit switch { 1 => SkinQuality.Bone1, 2 => SkinQuality.Bone2, 4 => SkinQuality.Bone4, _ => SkinQuality.Auto };
            QualitySettings.skinWeights = influenceLimit == 8 ? SkinWeights.Unlimited : SkinWeights.FourBones;
            Assert.True(NativeGraphicsDevice.TrySkinMeshVertices(mesh, renderer, out var positions, out _, out _)); Assert.InRange(positions[0].x, expectedX - .001f, expectedX + .001f);
        }
        finally { QualitySettings.skinWeights = previousQuality; Object.DestroyImmediate(mesh); for (int i = 0; i < bones.Length; i++) Object.DestroyImmediate(bones[i]); Object.DestroyImmediate(rendererObject); }
    }

    [Theory]
    [InlineData(2f, 1f, 3f, .5f, .5f, 1f, 1f)]
    [InlineData(2f, 1f, 3f, 1f, .5f, .5f, 1f)]
    [InlineData(2f, 1f, 3f, .5f, 1f, .5f, 1f)]
    [InlineData(2f, 1f, 3f, .5f, 0f, .5f, -1f)]
    [InlineData(-2f, 1f, 3f, .5f, 1f, .5f, 1f)]
    [InlineData(-2f, 1f, 3f, .5f, 1f, .5f, -1f)]
    [InlineData(1f, 3f, 2f, .5f, .5f, 1f, 1f)]
    [InlineData(1f, 3f, 2f, 1f, .5f, .5f, -1f)]
    [InlineData(3f, 2f, 1f, .5f, 1f, .5f, 1f)]
    [InlineData(3f, 2f, 1f, .5f, 0f, .5f, -1f)]
    public void NativeCameraMeshRaster_BumpMapUsesObjectDirectionTangentsUnderNonUniformScale(
        float scaleX, float scaleY, float scaleZ, float r, float g, float b, float tangentW)
    {
        if (!AnityNative.Available) return;
        using var device = NativeGraphicsDevice.Create(GraphicsDeviceType.Metal, 8, 8, false);
        var source = new RenderTexture(8, 8, 24, RenderTextureFormat.ARGB32); var destination = NewNormalTexture(8, 8);
        var normalMap = NormalMapTexture(new Color(r, g, b, 1f));
        Vector3 localNormal = new Vector3(0f, 1f, 1f).normalized;
        Vector3 localTangent = new Vector3(0f, 1f, -1f).normalized;
        Matrix4x4 model = Matrix4x4.Scale(new Vector3(scaleX, scaleY, scaleZ));
        try
        {
            Assert.True(device.EnsureCameraRenderTarget(source)); Assert.True(device.EnsureCameraRenderTarget(destination));
            Assert.True(device.TryRecordCameraPass((ulong)(uint)source.GetInstanceID(), 8, 8, new Rect(0, 0, 8, 8), Color.black, true, true, 1, false, false, false));
            Assert.True(device.TryDrawCameraMesh(source, TrianglePositions(), new[] { localNormal, localNormal, localNormal }, FilledColors(3, Color.white), TriangleIndices(), Matrix4x4.identity,
                normalObjectToWorld: model.inverse.transpose, tangentObjectToWorld: model,
                uvs: TriangleUvs(Vector2.zero), tangents: new[] { new Vector4(localTangent.x, localTangent.y, localTangent.z, tangentW), new Vector4(localTangent.x, localTangent.y, localTangent.z, tangentW), new Vector4(localTangent.x, localTangent.y, localTangent.z, tangentW) }, normalMap: normalMap));
            Assert.True(device.TryCopyCameraRenderTargetNormalsToColor(source, false, destination)); Assert.True(device.TryReadbackCameraRenderTargetRGBA8(destination, out var pixels));
            Vector3 normal = model.inverse.transpose.MultiplyVector(localNormal).normalized;
            Vector3 tangent = model.MultiplyVector(localTangent); tangent = (tangent - normal * Vector3.Dot(normal, tangent)).normalized;
            Vector3 bitangent = Vector3.Cross(normal, tangent) * (tangentW * (model.determinant < 0f ? -1f : 1f));
            Vector3 map = new Vector3(MathF.Round(r * 255f) / 255f, MathF.Round(g * 255f) / 255f, MathF.Round(b * 255f) / 255f) * 2f - Vector3.one;
            Vector3 expected = (tangent * map.x + bitangent * map.y + normal * map.z).normalized;
            int center = (4 * 8 + 4) * 4;
            Assert.InRange(pixels[center], (int)MathF.Round((expected.x * .5f + .5f) * 255f) - 14, (int)MathF.Round((expected.x * .5f + .5f) * 255f) + 14);
            Assert.InRange(pixels[center + 1], (int)MathF.Round((expected.y * .5f + .5f) * 255f) - 14, (int)MathF.Round((expected.y * .5f + .5f) * 255f) + 14);
            Assert.InRange(pixels[center + 2], (int)MathF.Round((expected.z * .5f + .5f) * 255f) - 14, (int)MathF.Round((expected.z * .5f + .5f) * 255f) + 14);
        }
        finally { device.ReleaseCameraRenderTarget(destination); device.ReleaseCameraRenderTarget(source); destination.Release(); source.Release(); Object.DestroyImmediate(normalMap); }
    }

    private static Texture2D SolidTexture(Color color)
    {
        var texture = new Texture2D(1, 1); texture.SetPixel(0, 0, color); texture.Apply(); return texture;
    }
    private static Texture2D NormalMapTexture(Color color)
    {
        var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false, true);
        texture.SetPixel(0, 0, color); texture.Apply(); return texture;
    }
    private static Vector2[] TriangleUvs(Vector2 uv) => new[] { uv, uv, uv };

    private static CullingResults Cull(int? mask = null, float[]? layerDistances = null)
    {
        var parameters = new ScriptableCullingParameters(null)
        {
            cullingMask = mask ?? (1 << IsolatedLayer),
            layerCullDistances = layerDistances,
            worldOrigin = Vector3.zero
        };
        var context = new ScriptableRenderContext(); context.Cull(ref parameters, out var results);
        return results;
    }

    private sealed class RendererFixture : IDisposable
    {
        internal readonly GameObject gameObject;
        internal readonly MeshRenderer renderer;
        internal readonly Mesh mesh;
        internal readonly Material material;

        internal RendererFixture(Vector3 position)
        {
            gameObject = new GameObject("Culling Renderer") { layer = IsolatedLayer };
            gameObject.transform.position = position;
            var filter = gameObject.AddComponent<MeshFilter>();
            renderer = gameObject.AddComponent<MeshRenderer>();
            mesh = new Mesh { vertices = new[] { new Vector3(-.5f, -.5f, 0f), new Vector3(.5f, -.5f, 0f), new Vector3(0f, .5f, 0f) }, triangles = new[] { 0, 1, 2 } };
            filter.sharedMesh = mesh;
            material = new Material { renderQueue = 2000 };
            renderer.sharedMaterial = material;
        }

        public void Dispose()
        {
            Object.DestroyImmediate(gameObject);
            Object.DestroyImmediate(mesh);
            Object.DestroyImmediate(material);
        }
    }
}
