using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Anity.Core.Runtime.Native;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.VFX;
using Xunit;

namespace Anity.Core.Tests;

[Collection(ComponentAttributeBehaviorCollection.Name)]
public sealed class NativeVFXPlanarOutputTests
{
    private const ulong EffectId = 0x701;
    private const string SystemName = "PlanarParticles";
    private const int Capacity = 2;
    private const int StrideWords = 28;

    [Fact]
    public void NativeAbiSizesMatchCContract()
    {
        Assert.Equal(144, Marshal.SizeOf<AnityNative.GraphicsVFXPlanarOutputDesc>());
        Assert.Equal(152, Marshal.SizeOf<AnityNative.GraphicsVFXPlanarCameraDesc>());
        Assert.Equal(48, Marshal.SizeOf<AnityNative.GraphicsVFXPlanarDrawInfo>());
    }

    [Fact]
    public void ValidOutputsInstallAndReplaceTransactionally()
        => WithNative(GraphicsDeviceType.Null, device =>
        {
            Assert.Equal(AnityNative.Result.Ok, Set(device, Desc(10), Desc(11)));
            Assert.True(device.TryGetVFXPlanarOutputCount(EffectId, out int first));
            Assert.Equal(2, first);
            Assert.Equal(AnityNative.Result.Ok, Set(device, Desc(12)));
            Assert.True(device.TryGetVFXPlanarOutputCount(EffectId, out int second));
            Assert.Equal(1, second);
        });

    [Fact]
    public void EmptyOutputSetIsAValidInstalledRegistry()
        => WithNative(GraphicsDeviceType.Null, device =>
        {
            Assert.Equal(AnityNative.Result.Ok, Set(device));
            Assert.True(device.TryGetVFXPlanarOutputCount(EffectId, out int count));
            Assert.Equal(0, count);
        });

    [Fact]
    public void DuplicateContextIsRejectedWithoutReplacingPreviousRegistry()
        => WithNative(GraphicsDeviceType.Null, device =>
        {
            Assert.Equal(AnityNative.Result.Ok, Set(device, Desc(10)));
            Assert.Equal(AnityNative.Result.InvalidArg, Set(device, Desc(20), Desc(20)));
            Assert.True(device.TryGetVFXPlanarOutputCount(EffectId, out int count));
            Assert.Equal(1, count);
        });

    [Fact]
    public void MisalignedRequiredOffsetIsRejected()
        => WithNative(GraphicsDeviceType.Null, device =>
        {
            AnityNative.GraphicsVFXPlanarOutputDesc desc = Desc(10);
            desc.positionOffsetBytes = 3;
            Assert.Equal(AnityNative.Result.InvalidArg, Set(device, desc));
        });

    [Fact]
    public void EffectIdentityMismatchIsRejected()
        => WithNative(GraphicsDeviceType.Null, device =>
        {
            AnityNative.GraphicsVFXPlanarOutputDesc desc = Desc(10);
            desc.effectId++;
            Assert.Equal(AnityNative.Result.InvalidArg, Set(device, desc));
        });

    [Fact]
    public void UnknownFeatureFlagIsRejected()
        => WithNative(GraphicsDeviceType.Null, device =>
        {
            AnityNative.GraphicsVFXPlanarOutputDesc desc = Desc(10);
            desc.flags |= 16;
            Assert.Equal(AnityNative.Result.InvalidArg, Set(device, desc));
        });

    [Fact]
    public void ClearEffectStateRemovesInstalledOutputs()
        => WithNative(GraphicsDeviceType.Null, device =>
        {
            Assert.Equal(AnityNative.Result.Ok, Set(device, Desc(10)));
            Assert.Equal(AnityNative.Result.Ok,
                AnityNative.Graphics_ClearVFXEffectState(device.Handle, EffectId));
            Assert.False(device.TryGetVFXPlanarOutputCount(EffectId, out _));
        });

    [Fact]
    public void NullBackendReportsRegisteredOutputAsSkipped()
        => WithNative(GraphicsDeviceType.Null, device =>
        {
            Assert.Equal(AnityNative.Result.Ok, Set(device, Desc(10)));
            AnityNative.GraphicsVFXPlanarCameraDesc camera = Camera(clear: true);
            Assert.Equal(AnityNative.Result.Ok,
                AnityNative.Graphics_DrawVFXPlanarOutputs(
                    device.Handle, EffectId, ref camera,
                    out AnityNative.GraphicsVFXPlanarDrawInfo info));
            Assert.Equal(1, info.outputCount);
            Assert.Equal(0, info.drawCount);
            Assert.Equal(1, info.skippedOutputCount);
            Assert.Equal(0, info.backendKind);
        });

    [Fact]
    public void NonFiniteCameraMatrixIsRejected()
        => WithNative(GraphicsDeviceType.Null, device =>
        {
            Assert.Equal(AnityNative.Result.Ok, Set(device, Desc(10)));
            AnityNative.GraphicsVFXPlanarCameraDesc camera = Camera(clear: true);
            camera.worldToClip00 = float.NaN;
            Assert.Equal(AnityNative.Result.InvalidArg,
                AnityNative.Graphics_DrawVFXPlanarOutputs(
                    device.Handle, EffectId, ref camera, out _));
        });

    [Fact]
    public void MetalWithoutResidentParticlesSkipsOutputAndClearsTarget()
        => WithMetal(device =>
        {
            Assert.Equal(AnityNative.Result.Ok, Set(device, Desc(10)));
            AnityNative.GraphicsVFXPlanarDrawInfo info = Draw(device, clear: true);
            Assert.Equal(0, info.drawCount);
            Assert.Equal(1, info.skippedOutputCount);
            Assert.Equal(2, info.backendKind);
            AssertPixel(Read(device), 32, 32, 0, 0, 0, 0);
        });

    [Fact]
    public void ResidentQuadRasterizesParticleColor()
        => WithMetal(device =>
        {
            SpawnAndPublishResident(device, alive: true, red: 1f, alpha: 1f);
            Assert.Equal(AnityNative.Result.Ok, Set(device, Desc(10, primitiveType: 1)));
            AnityNative.GraphicsVFXPlanarDrawInfo info = Draw(device, clear: true);
            Assert.Equal(1, info.drawCount);
            Assert.Equal(1, info.particleCount);
            Assert.Equal(6, info.vertexCount);
            Assert.NotEqual(0ul, info.residentGeneration);
            AssertPixel(Read(device), 32, 32, 255, 0, 0, 255);
        });

    [Theory]
    [InlineData(true, 1f, 0f, 0f, 255, 0, 0)]
    [InlineData(true, 0f, 1f, 0f, 0, 255, 0)]
    [InlineData(true, 0f, 0f, 1f, 0, 0, 255)]
    [InlineData(true, 1f, 1f, 0f, 255, 255, 0)]
    [InlineData(true, 1f, 0f, 1f, 255, 0, 255)]
    [InlineData(false, 1f, 0f, 0f, 255, 0, 0)]
    [InlineData(false, 0f, 1f, 0f, 0, 255, 0)]
    [InlineData(false, 0f, 0f, 1f, 0, 0, 255)]
    [InlineData(false, 1f, 1f, 0f, 255, 255, 0)]
    [InlineData(false, 1f, 0f, 1f, 255, 0, 255)]
    public void PendingResidentInitialize_DrawsThroughQueueBeforeCpuRetirement(
        bool cancel, float red, float green, float blue,
        int expectedRed, int expectedGreen, int expectedBlue)
        => WithMetal(device =>
        {
            SpawnAndPublishResident(
                device, alive: true, red: 0f, alpha: 1f, positionX: 2f);
            Assert.Equal(AnityNative.Result.Ok,
                Set(device, Desc(10, primitiveType: 1)));
            Assert.True(BeginResidentSpawn(
                device, red, green, blue, out ulong ticket));
            Assert.True(device.TryGetVFXUpdateBackendStats(
                EffectId, Shader.PropertyToID(SystemName), out var queued));
            Assert.Equal(1ul, queued.pendingInitializeCount);
            Assert.Equal(1ul,
                queued.asynchronousInitializeResidentPublishCount);

            AnityNative.GraphicsVFXPlanarDrawInfo pendingDraw =
                Draw(device, clear: true);

            Assert.Equal(1, pendingDraw.drawCount);
            Assert.Equal(queued.residentGeneration,
                pendingDraw.residentGeneration);
            AssertPixel(Read(device), 32, 32,
                (byte)expectedRed, (byte)expectedGreen,
                (byte)expectedBlue, 255);
            Assert.True(device.TryGetVFXInitializeTicketInfo(ticket, out _));
            Assert.True(device.TryGetVFXUpdateBackendStats(
                EffectId, Shader.PropertyToID(SystemName), out var depended));
            Assert.Equal(1ul, depended.cameraDependencyCount);
            Assert.Equal(1ul, depended.pendingInitializeCount);

            Assert.True(cancel
                ? device.CancelVFXInitializeKernels(ticket)
                : device.CompleteVFXInitializeKernels(ticket));
            Assert.False(device.TryGetVFXInitializeTicketInfo(ticket, out _));
            Draw(device, clear: true);
            if (cancel)
                AssertPixel(Read(device), 32, 32, 0, 0, 0, 0);
            else
                AssertPixel(Read(device), 32, 32,
                    (byte)expectedRed, (byte)expectedGreen,
                    (byte)expectedBlue, 255);
        });

    [Fact]
    public void FirstInitialize_DrawRetiresBootstrapAndSkipsUntilResidentUpload()
        => WithMetal(device =>
        {
            Assert.Equal(AnityNative.Result.Ok,
                Set(device, Desc(10, primitiveType: 1)));
            Assert.True(BeginResidentSpawn(
                device, 1f, 0f, 0f, out ulong ticket));
            Assert.True(device.TryGetVFXInitializeTicketInfo(ticket, out _));

            AnityNative.GraphicsVFXPlanarDrawInfo draw =
                Draw(device, clear: true);

            Assert.Equal(0, draw.drawCount);
            Assert.Equal(1, draw.skippedOutputCount);
            Assert.False(device.TryGetVFXInitializeTicketInfo(ticket, out _));
            Assert.True(device.TryGetVFXParticleSystemInfo(
                EffectId, Shader.PropertyToID(SystemName), out var state));
            Assert.Equal(1, state.aliveCount);
            Assert.NotEqual(0ul, state.generation);
            AssertPixel(Read(device), 32, 32, 0, 0, 0, 0);
        });

    [Fact]
    public void PendingResidentInitializeAndUpdate_DrawsFinalGenerationBeforeCpuRetirement()
        => WithMetal(device =>
        {
            SpawnAndPublishResident(
                device, alive: true, red: 1f, alpha: 1f, positionX: 2f);
            Assert.Equal(AnityNative.Result.Ok,
                Set(device, Desc(10, primitiveType: 1)));
            Assert.True(device.BeginVFXFrame(out uint frame));
            Assert.True(device.PrepareVFXEffectManualFrame(
                EffectId, frame, 0.1f, out _));
            Assert.True(BeginResidentSpawn(
                device, 0f, 1f, 0f, out ulong initializeTicket));
            var setBlue = new VFXRuntimeUpdateOperationData(
                VFXRuntimeUpdateOperationKind.SetAttribute,
                4, -1, -1, -1, -1,
                VFXRuntimeValueType.Float3,
                VFXRuntimeInitializeComposition.Overwrite,
                VFXRuntimeInitializeRandomMode.Off,
                false,
                new[] { F(0), F(0), F(1) },
                Array.Empty<uint>(), F(1));
            var update = new VFXRuntimeUpdateKernelData(
                52, SystemName, Capacity, StrideWords, true, false, 0, 27,
                new[] { setBlue });
            Assert.True(device.BeginVFXUpdateKernels(
                EffectId, new[] { update }, 0.1f, 23,
                out ulong updateTicket));
            Assert.True(device.CommitVFXEffectFrame(EffectId, frame, out _));
            Assert.True(device.TryGetVFXUpdateBackendStats(
                EffectId, Shader.PropertyToID(SystemName), out var published));
            Assert.Equal(1ul, published.pendingInitializeCount);
            Assert.Equal(1ul, published.pendingUpdateCount);
            Assert.Equal(0ul,
                published.asynchronousInitializeResidentCompletionCount);

            AnityNative.GraphicsVFXPlanarDrawInfo draw =
                Draw(device, clear: true);

            Assert.Equal(1, draw.drawCount);
            Assert.Equal(1, draw.particleCount);
            Assert.Equal(published.residentGeneration, draw.residentGeneration);
            AssertPixel(Read(device), 32, 32, 0, 0, 255, 255);
            Assert.True(device.TryGetVFXInitializeTicketInfo(
                initializeTicket, out _));
            Assert.True(device.TryGetVFXUpdateTicketInfo(updateTicket, out _));
            Assert.True(device.CompleteVFXUpdateKernels(updateTicket));
            Assert.False(device.TryGetVFXInitializeTicketInfo(
                initializeTicket, out _));
            Assert.False(device.TryGetVFXUpdateTicketInfo(updateTicket, out _));
        });

    [Fact]
    public void DeadResidentParticleDoesNotRasterize()
        => WithMetal(device =>
        {
            SpawnAndPublishResident(device, alive: false, red: 1f, alpha: 1f);
            Assert.Equal(AnityNative.Result.Ok, Set(device, Desc(10, primitiveType: 1)));
            AnityNative.GraphicsVFXPlanarDrawInfo info = Draw(device, clear: true);
            Assert.Equal(0, info.drawCount);
            Assert.Equal(1, info.skippedOutputCount);
            AssertPixel(Read(device), 32, 32, 0, 0, 0, 0);
        });

    [Fact]
    public void TriangleAndOctagonTopologiesSubmitExpectedVertexCounts()
        => WithMetal(device =>
        {
            SpawnAndPublishResident(device, alive: true, red: 0f, alpha: 1f);
            foreach ((int primitive, int vertices) in new[] { (0, 3), (2, 18) })
            {
                Assert.Equal(AnityNative.Result.Ok, Set(device, Desc(10 + primitive, primitive)));
                AnityNative.GraphicsVFXPlanarDrawInfo info = Draw(device, clear: true);
                Assert.Equal(1, info.drawCount);
                Assert.Equal(vertices, info.vertexCount);
            }
        });

    [Fact]
    public void SortingRequirementUsesGpuSortedIndexPath()
        => WithMetal(device =>
        {
            SpawnAndPublishResident(device, alive: true, red: 1f, alpha: 1f);
            Assert.Equal(AnityNative.Result.Ok, Set(device, Desc(10, flags: 1u | 4u)));
            AnityNative.GraphicsVFXPlanarDrawInfo info = Draw(device, clear: true);
            Assert.Equal(1, info.drawCount);
            Assert.Equal(0, info.skippedOutputCount);
            AssertPixel(Read(device), 32, 32, 255, 0, 0, 255);
        });

    private static void SpawnAndPublishResident(
        NativeGraphicsDevice device, bool alive, float red, float alpha,
        float positionX = 0f)
    {
        VFXRuntimeInitializeAttributeData[] attributes =
        {
            Attribute("alive", VFXRuntimeValueType.Boolean, 0, alive ? 1u : 0u),
            Attribute("position", VFXRuntimeValueType.Float3, 1,
                F(positionX), F(0), F(0)),
            Attribute("color", VFXRuntimeValueType.Float3, 4, F(red), F(0), F(0)),
            Attribute("alpha", VFXRuntimeValueType.Float, 7, F(alpha)),
            Attribute("axisX", VFXRuntimeValueType.Float3, 8, F(1), F(0), F(0)),
            Attribute("axisY", VFXRuntimeValueType.Float3, 11, F(0), F(1), F(0)),
            Attribute("axisZ", VFXRuntimeValueType.Float3, 14, F(0), F(0), F(1)),
            Attribute("angleX", VFXRuntimeValueType.Float, 17, F(0)),
            Attribute("angleY", VFXRuntimeValueType.Float, 18, F(0)),
            Attribute("angleZ", VFXRuntimeValueType.Float, 19, F(0)),
            Attribute("pivotX", VFXRuntimeValueType.Float, 20, F(0)),
            Attribute("pivotY", VFXRuntimeValueType.Float, 21, F(0)),
            Attribute("pivotZ", VFXRuntimeValueType.Float, 22, F(0)),
            Attribute("size", VFXRuntimeValueType.Float, 23, F(1)),
            Attribute("scaleX", VFXRuntimeValueType.Float, 24, F(1)),
            Attribute("scaleY", VFXRuntimeValueType.Float, 25, F(1)),
            Attribute("scaleZ", VFXRuntimeValueType.Float, 26, F(1)),
            Attribute("seed", VFXRuntimeValueType.UInt32, 27, 123u)
        };
        var kernel = new VFXRuntimeInitializeKernelData(
            41, Capacity, StrideWords, 1, true, attributes,
            Array.Empty<VFXRuntimeInitializeOperationData>());
        var dispatch = new AnityNative.GraphicsVFXInitializeDispatchDesc
        {
            effectId = EffectId,
            sequence = 1,
            initializeContextId = 41,
            sourceSpawnerContextId = 31,
            eventNameId = 12,
            particleSystemId = Shader.PropertyToID(SystemName),
            spawnSystemId = 13,
            recordCount = 1,
            strideBytes = sizeof(uint)
        };
        Assert.True(device.SubmitVFXInitializeKernels(
            new[] { dispatch }, new VFXRuntimeInitializeKernelData?[] { kernel },
            new byte[sizeof(uint)], 17));
        var operation = new VFXRuntimeUpdateOperationData(
            VFXRuntimeUpdateOperationKind.SetAttribute,
            alive ? 23 : 0, -1, -1, -1, -1,
            alive ? VFXRuntimeValueType.Float : VFXRuntimeValueType.Boolean,
            VFXRuntimeInitializeComposition.Overwrite,
            VFXRuntimeInitializeRandomMode.Off,
            false,
            new[] { alive ? F(1) : 0u },
            Array.Empty<uint>(), F(1));
        var update = new VFXRuntimeUpdateKernelData(
            51, SystemName, Capacity, StrideWords, true, false, 0, 27,
            new[] { operation });
        Assert.True(device.DispatchVFXUpdateKernels(EffectId, new[] { update }, 0f, 17));
    }

    private static bool BeginResidentSpawn(
        NativeGraphicsDevice device,
        float red,
        float green,
        float blue,
        out ulong ticket)
    {
        VFXRuntimeInitializeAttributeData[] attributes =
        {
            Attribute("alive", VFXRuntimeValueType.Boolean, 0, 1u),
            Attribute("position", VFXRuntimeValueType.Float3, 1, F(0), F(0), F(0)),
            Attribute("color", VFXRuntimeValueType.Float3, 4, F(red), F(green), F(blue)),
            Attribute("alpha", VFXRuntimeValueType.Float, 7, F(1)),
            Attribute("axisX", VFXRuntimeValueType.Float3, 8, F(1), F(0), F(0)),
            Attribute("axisY", VFXRuntimeValueType.Float3, 11, F(0), F(1), F(0)),
            Attribute("axisZ", VFXRuntimeValueType.Float3, 14, F(0), F(0), F(1)),
            Attribute("angleX", VFXRuntimeValueType.Float, 17, F(0)),
            Attribute("angleY", VFXRuntimeValueType.Float, 18, F(0)),
            Attribute("angleZ", VFXRuntimeValueType.Float, 19, F(0)),
            Attribute("pivotX", VFXRuntimeValueType.Float, 20, F(0)),
            Attribute("pivotY", VFXRuntimeValueType.Float, 21, F(0)),
            Attribute("pivotZ", VFXRuntimeValueType.Float, 22, F(0)),
            Attribute("size", VFXRuntimeValueType.Float, 23, F(1)),
            Attribute("scaleX", VFXRuntimeValueType.Float, 24, F(1)),
            Attribute("scaleY", VFXRuntimeValueType.Float, 25, F(1)),
            Attribute("scaleZ", VFXRuntimeValueType.Float, 26, F(1)),
            Attribute("seed", VFXRuntimeValueType.UInt32, 27, 456u)
        };
        var kernel = new VFXRuntimeInitializeKernelData(
            42, Capacity, StrideWords, 1, true, attributes,
            Array.Empty<VFXRuntimeInitializeOperationData>());
        var dispatch = new AnityNative.GraphicsVFXInitializeDispatchDesc
        {
            effectId = EffectId,
            sequence = 2,
            initializeContextId = 42,
            sourceSpawnerContextId = 31,
            eventNameId = 12,
            particleSystemId = Shader.PropertyToID(SystemName),
            spawnSystemId = 13,
            recordCount = 1,
            strideBytes = sizeof(uint)
        };
        return device.BeginVFXInitializeKernels(
            new[] { dispatch },
            new VFXRuntimeInitializeKernelData?[] { kernel },
            new byte[sizeof(uint)], 19, out ticket);
    }

    private static VFXRuntimeInitializeAttributeData Attribute(
        string name, VFXRuntimeValueType type, int offset, params uint[] defaults)
        => new(new VFXRuntimeAttributeData(name, type, offset, defaults.Length), defaults);

    private static uint F(float value) => unchecked((uint)BitConverter.SingleToInt32Bits(value));

    private static AnityNative.Result Set(
        NativeGraphicsDevice device, params AnityNative.GraphicsVFXPlanarOutputDesc[] outputs)
        => AnityNative.Graphics_SetVFXPlanarOutputs(
            device.Handle, EffectId, outputs, outputs.Length);

    private static AnityNative.GraphicsVFXPlanarOutputDesc Desc(
        long contextId, int primitiveType = 1, uint flags = 1)
        => new()
        {
            version = 1,
            flags = flags,
            effectId = EffectId,
            contextId = contextId,
            particleSystemId = Shader.PropertyToID(SystemName),
            primitiveType = primitiveType,
            particleCapacity = Capacity,
            attributeStrideBytes = StrideWords * sizeof(uint),
            aliveOffsetBytes = 0,
            positionOffsetBytes = 1 * sizeof(uint),
            colorOffsetBytes = 4 * sizeof(uint),
            alphaOffsetBytes = 7 * sizeof(uint),
            axisXOffsetBytes = 8 * sizeof(uint),
            axisYOffsetBytes = 11 * sizeof(uint),
            axisZOffsetBytes = 14 * sizeof(uint),
            angleXOffsetBytes = 17 * sizeof(uint),
            angleYOffsetBytes = 18 * sizeof(uint),
            angleZOffsetBytes = 19 * sizeof(uint),
            pivotXOffsetBytes = 20 * sizeof(uint),
            pivotYOffsetBytes = 21 * sizeof(uint),
            pivotZOffsetBytes = 22 * sizeof(uint),
            sizeOffsetBytes = 23 * sizeof(uint),
            scaleXOffsetBytes = 24 * sizeof(uint),
            scaleYOffsetBytes = 25 * sizeof(uint),
            scaleZOffsetBytes = 26 * sizeof(uint),
            uvMode = 0,
            blendMode = 3,
            cullMode = 0,
            zTest = 6,
            renderQueue = 3000
        };

    private static AnityNative.GraphicsVFXPlanarCameraDesc Camera(bool clear)
        => new()
        {
            cameraId = 1,
            localToWorld00 = 1, localToWorld11 = 1,
            localToWorld22 = 1, localToWorld33 = 1,
            worldToClip00 = 1, worldToClip11 = 1,
            worldToClip22 = 1, worldToClip33 = 1,
            cullingMask = -1,
            flags = clear ? 1 : 0
        };

    private static AnityNative.GraphicsVFXPlanarDrawInfo Draw(
        NativeGraphicsDevice device, bool clear)
    {
        AnityNative.GraphicsVFXPlanarCameraDesc camera = Camera(clear);
        Assert.Equal(AnityNative.Result.Ok,
            AnityNative.Graphics_DrawVFXPlanarOutputs(
                device.Handle, EffectId, ref camera,
                out AnityNative.GraphicsVFXPlanarDrawInfo info));
        return info;
    }

    private static byte[] Read(NativeGraphicsDevice device)
    {
        Assert.True(device.TryReadbackSwapchainRGBA8(out byte[] pixels));
        return pixels;
    }

    private static void AssertPixel(
        byte[] pixels, int x, int y, byte r, byte g, byte b, byte a)
    {
        int offset = (y * 64 + x) * 4;
        Assert.Equal((r, g, b, a),
            (pixels[offset], pixels[offset + 1], pixels[offset + 2], pixels[offset + 3]));
    }

    private static void WithNative(GraphicsDeviceType type, Action<NativeGraphicsDevice> action)
    {
        using NativeGraphicsDevice device = NativeGraphicsDevice.Create(type, 64, 64, false);
        bool available = device.Handle != IntPtr.Zero;
        if (Environment.GetEnvironmentVariable("ANITY_REQUIRE_NATIVE") == "1") Assert.True(available);
        if (available) action(device);
    }

    private static void WithMetal(Action<NativeGraphicsDevice> action)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return;
        WithNative(GraphicsDeviceType.Metal, device =>
        {
            Assert.True(device.CreateSwapchain(64, 64, imageCount: 3, hdr: false));
            action(device);
        });
    }
}
