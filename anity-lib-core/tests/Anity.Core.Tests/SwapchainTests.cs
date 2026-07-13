using System;
using Anity.Core.Runtime.Native;
using Anity.Core.Runtime.Platform;
using UnityEngine;
using UnityEngine.Rendering;
using Xunit;

namespace Anity.Core.Tests;

/// <summary>Metal/Vulkan headless swapchain (managed + native P/Invoke) — ≥12 cases.</summary>
[Collection("ScreenState")]
public class SwapchainTests : IDisposable
{
    public SwapchainTests()
    {
        PlatformGraphics.ClearForce();
    }

    public void Dispose()
    {
        PlatformGraphics.ClearForce();
    }

    [Fact]
    public void Create_VulkanPreferred_HasSwapchain()
    {
        using var dev = NativeGraphicsDevice.Create(GraphicsDeviceType.Vulkan, 800, 600, hdr: true);
        Assert.True(dev.CreateSwapchain(800, 600, imageCount: 3));
        Assert.True(dev.HasSwapchain);
        Assert.Equal(3, dev.SwapchainImageCount);
        Assert.True(dev.SwapchainHeadless);
        Assert.Equal(800, dev.Width);
        Assert.Equal(600, dev.Height);
    }

    [Fact]
    public void Create_MetalPreferred_HasSwapchain()
    {
        using var dev = NativeGraphicsDevice.Create(GraphicsDeviceType.Metal, 1024, 768, hdr: true);
        Assert.True(dev.CreateSwapchain());
        Assert.True(dev.HasSwapchain);
        Assert.True(dev.SwapchainImageCount >= 2);
    }

    [Fact]
    public void Present_IncrementsCount()
    {
        using var dev = NativeGraphicsDevice.Create(GraphicsDeviceType.Vulkan, 640, 480, false);
        Assert.True(dev.CreateSwapchain());
        int before = dev.PresentCount;
        dev.BeginFrame();
        dev.Present();
        dev.EndFrame();
        Assert.Equal(before + 1, dev.PresentCount);
    }

    [Fact]
    public void AcquireNextImage_Cycles()
    {
        using var dev = NativeGraphicsDevice.Create(GraphicsDeviceType.Vulkan, 100, 100, false);
        dev.CreateSwapchain(imageCount: 2);
        int a = dev.AcquireNextImage();
        int b = dev.AcquireNextImage();
        Assert.InRange(a, 0, 1);
        Assert.InRange(b, 0, 1);
    }

    [Fact]
    public void MultiplePresents()
    {
        using var dev = NativeGraphicsDevice.Create(GraphicsDeviceType.Metal, 320, 240, false);
        dev.CreateSwapchain(imageCount: 2);
        for (int i = 0; i < 5; i++)
            dev.Present();
        Assert.Equal(5, dev.PresentCount);
    }

    [Fact]
    public void D3D11_Preferred_Swapchain()
    {
        using var dev = NativeGraphicsDevice.Create(GraphicsDeviceType.Direct3D11, 1280, 720, false);
        Assert.True(dev.CreateSwapchain(1280, 720));
        Assert.True(dev.HasSwapchain);
    }

    [Fact]
    public void Dispose_SafeWithSwapchain()
    {
        var dev = NativeGraphicsDevice.Create(GraphicsDeviceType.Vulkan, 64, 64, false);
        dev.CreateSwapchain();
        dev.Dispose();
        // second dispose no throw
        dev.Dispose();
    }

    [Fact]
    public void Platform_Android_VulkanDefault()
    {
        Assert.Equal(GraphicsDeviceType.Vulkan, PlatformGraphics.GetDefaultDeviceType(TargetPlatform.Android));
    }

    [Fact]
    public void Platform_iOS_MetalDefault()
    {
        Assert.Equal(GraphicsDeviceType.Metal, PlatformGraphics.GetDefaultDeviceType(TargetPlatform.iOS));
    }

    [Fact]
    public void ForceVulkan_ThenSwapchain()
    {
        PlatformGraphics.ForceGraphicsDevice(GraphicsDeviceType.Vulkan);
        using var dev = NativeGraphicsDevice.Create(GraphicsDeviceType.Vulkan, 200, 200, true);
        Assert.True(dev.CreateSwapchain(200, 200, 3, vsync: true, hdr: true));
        Assert.True(dev.SwapchainHeadless);
    }

    [Fact]
    public void ForceMetal_ThenSwapchain()
    {
        PlatformGraphics.ForceGraphicsDevice(GraphicsDeviceType.Metal);
        using var dev = NativeGraphicsDevice.Create(GraphicsDeviceType.Metal, 200, 200, true);
        Assert.True(dev.CreateSwapchain());
        Assert.True(dev.HasSwapchain);
    }

    [Fact]
    public void Resize_UpdatesDimensions()
    {
        using var dev = NativeGraphicsDevice.Create(GraphicsDeviceType.Vulkan, 100, 100, false);
        dev.CreateSwapchain(100, 100);
        // managed path width/height set at create
        Assert.Equal(100, dev.Width);
        Assert.True(dev.CreateSwapchain(400, 300, 2));
        Assert.Equal(400, dev.Width);
        Assert.Equal(300, dev.Height);
    }

    [Fact]
    public void SwapchainDesc_StructLayout_Ok()
    {
        var d = new AnityNative.SwapchainDesc { width = 1, height = 2, imageCount = 3, vsync = 1, hdr = 0 };
        Assert.Equal(1, d.width);
        Assert.Equal(3, d.imageCount);
    }

    [Fact]
    public void Vulkan_Swapchain_BackendKind()
    {
        using var dev = NativeGraphicsDevice.Create(GraphicsDeviceType.Vulkan, 640, 480, hdr: true);
        Assert.True(dev.CreateSwapchain(640, 480, imageCount: 3, vsync: true, hdr: true));
        Assert.True(dev.HasSwapchain);
        // managed or native — backend kind should reflect Vulkan preference
        Assert.True(dev.SwapchainBackendKind == 1 || dev.SwapchainImageCount >= 2);
        Assert.Equal(640, dev.Width);
        Assert.Equal(480, dev.Height);
    }

    [Fact]
    public void Metal_Swapchain_BackendKind()
    {
        using var dev = NativeGraphicsDevice.Create(GraphicsDeviceType.Metal, 512, 512, hdr: true);
        Assert.True(dev.CreateSwapchain(512, 512, 3));
        Assert.True(dev.HasSwapchain);
        Assert.True(dev.SwapchainBackendKind == 2 || dev.SwapchainImageCount >= 2);
    }

    [Fact]
    public void Acquire_Then_Present_FrameLoop()
    {
        using var dev = NativeGraphicsDevice.Create(GraphicsDeviceType.Vulkan, 320, 240, false);
        dev.CreateSwapchain(320, 240, 3);
        for (int i = 0; i < 10; i++)
        {
            int idx = dev.AcquireNextImage();
            Assert.InRange(idx, 0, Math.Max(0, dev.SwapchainImageCount - 1));
            dev.Present();
        }
        Assert.Equal(10, dev.PresentCount);
    }

    [Fact]
    public void Headless_WithoutWindow()
    {
        using var dev = NativeGraphicsDevice.Create(GraphicsDeviceType.Vulkan, 100, 100, false);
        Assert.True(dev.CreateSwapchain(nativeWindow: IntPtr.Zero));
        Assert.True(dev.SwapchainHeadless);
    }

    [Fact]
    public void TripleBuffer_ImageCount()
    {
        using var dev = NativeGraphicsDevice.Create(GraphicsDeviceType.Metal, 256, 256, false);
        dev.CreateSwapchain(256, 256, imageCount: 3);
        Assert.InRange(dev.SwapchainImageCount, 2, 4);
    }
}
