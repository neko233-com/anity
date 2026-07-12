using Anity.Core.Runtime.Platform;
using UnityEngine;
using UnityEngine.Rendering;
using Xunit;

namespace Anity.Core.Tests;

/// <summary>Metal / Vulkan / platform graphics matrix — ≥10 cases.</summary>
[Collection("ScreenState")]
public class PlatformGraphicsTests
{
    public PlatformGraphicsTests()
    {
        PlatformGraphics.ClearForce();
    }

    [Fact]
    public void iOS_Default_IsMetal()
    {
        Assert.Equal(GraphicsDeviceType.Metal, PlatformGraphics.GetDefaultDeviceType(TargetPlatform.iOS));
    }

    [Fact]
    public void Android_Default_IsVulkan()
    {
        Assert.Equal(GraphicsDeviceType.Vulkan, PlatformGraphics.GetDefaultDeviceType(TargetPlatform.Android));
    }

    [Fact]
    public void MacOS_Default_IsMetal()
    {
        Assert.Equal(GraphicsDeviceType.Metal, PlatformGraphics.GetDefaultDeviceType(TargetPlatform.MacOS));
    }

    [Fact]
    public void Linux_Default_IsVulkan()
    {
        Assert.Equal(GraphicsDeviceType.Vulkan, PlatformGraphics.GetDefaultDeviceType(TargetPlatform.Linux));
    }

    [Fact]
    public void Windows_Default_IsD3D11()
    {
        Assert.Equal(GraphicsDeviceType.Direct3D11, PlatformGraphics.GetDefaultDeviceType(TargetPlatform.Windows));
    }

    [Fact]
    public void ForceMetal_SetsFlags()
    {
        try
        {
            PlatformGraphics.ForceGraphicsDevice(GraphicsDeviceType.Metal);
            Assert.True(PlatformGraphics.IsMetal);
            Assert.False(PlatformGraphics.IsVulkan);
            Assert.Equal(GraphicsDeviceType.Metal, PlatformGraphics.ActiveDeviceType);
        }
        finally { PlatformGraphics.ClearForce(); }
    }

    [Fact]
    public void ForceVulkan_SetsFlags()
    {
        try
        {
            PlatformGraphics.ForceGraphicsDevice(GraphicsDeviceType.Vulkan);
            Assert.True(PlatformGraphics.IsVulkan);
            Assert.False(PlatformGraphics.IsMetal);
        }
        finally { PlatformGraphics.ClearForce(); }
    }

    [Fact]
    public void PreferredApis_iOS_ContainsMetal()
    {
        var apis = PlatformGraphics.GetPreferredApis(TargetPlatform.iOS);
        Assert.Contains(GraphicsDeviceType.Metal, apis);
    }

    [Fact]
    public void PreferredApis_Android_VulkanFirst()
    {
        var apis = PlatformGraphics.GetPreferredApis(TargetPlatform.Android);
        Assert.NotEmpty(apis);
        Assert.Equal(GraphicsDeviceType.Vulkan, apis[0]);
    }

    [Fact]
    public void ClearForce_ClearsForce()
    {
        PlatformGraphics.ForceGraphicsDevice(GraphicsDeviceType.Metal);
        PlatformGraphics.ClearForce();
        // after clear, ActiveDeviceType falls back to platform default
        Assert.Equal(PlatformGraphics.GetDefaultDeviceType(PlatformGraphics.ActivePlatform),
            PlatformGraphics.ActiveDeviceType);
    }

    [Fact]
    public void WebGL_Default_IsWebGL2()
    {
        Assert.Equal(GraphicsDeviceType.WebGL2, PlatformGraphics.GetDefaultDeviceType(TargetPlatform.WebGL));
    }
}
