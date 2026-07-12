using System;
using System.IO;
using UnityEngine;
using Xunit;

namespace Anity.Core.Tests;

/// <summary>ScreenCapture — ≥10 cases including edge conditions (Unity 2022 Pro parity).</summary>
public class ScreenCaptureTests
{
    public ScreenCaptureTests()
    {
        Screen.width = 640;
        Screen.height = 360;
    }

    [Fact]
    public void CaptureScreenshot_WritesFile_RelativePath()
    {
        string name = $"sc_{Guid.NewGuid():N}.png";
        ScreenCapture.CaptureScreenshot(name);
        Assert.True(File.Exists(ScreenCapture.lastCapturePath));
        Assert.True(new FileInfo(ScreenCapture.lastCapturePath).Length > 8);
        File.Delete(ScreenCapture.lastCapturePath);
    }

    [Fact]
    public void CaptureScreenshot_SuperSize2_LargerThanBase()
    {
        var t1 = ScreenCapture.CaptureScreenshotAsTexture(1);
        var t2 = ScreenCapture.CaptureScreenshotAsTexture(2);
        Assert.Equal(Screen.width, t1.width);
        Assert.Equal(Screen.width * 2, t2.width);
        Assert.Equal(Screen.height * 2, t2.height);
    }

    [Fact]
    public void CaptureScreenshot_SuperSizeClampedMin()
    {
        var t = ScreenCapture.CaptureScreenshotAsTexture(0);
        Assert.Equal(Screen.width, t.width);
    }

    [Fact]
    public void CaptureScreenshot_SuperSizeClampedMax()
    {
        var t = ScreenCapture.CaptureScreenshotAsTexture(99);
        Assert.Equal(Screen.width * 8, t.width);
    }

    [Fact]
    public void CaptureScreenshot_NullFilename_Throws()
    {
        Assert.ThrowsAny<ArgumentException>(() => ScreenCapture.CaptureScreenshot(null!));
    }

    [Fact]
    public void CaptureScreenshot_EmptyFilename_Throws()
    {
        Assert.ThrowsAny<ArgumentException>(() => ScreenCapture.CaptureScreenshot("  "));
    }

    [Fact]
    public void CaptureScreenshotAsTexture_NonEmptyPixels()
    {
        var t = ScreenCapture.CaptureScreenshotAsTexture();
        var px = t.GetPixels32();
        Assert.True(px.Length == t.width * t.height);
        Assert.Contains(px, c => c.a == 255);
    }

    [Fact]
    public void CaptureScreenshotIntoRenderTexture_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => ScreenCapture.CaptureScreenshotIntoRenderTexture(null!));
    }

    [Fact]
    public void CaptureScreenshotIntoRenderTexture_Valid()
    {
        var rt = new RenderTexture(128, 72, 0);
        ScreenCapture.CaptureScreenshotIntoRenderTexture(rt);
        Assert.Equal(128, rt.width);
    }

    [Fact]
    public void CaptureScreenshot_AbsolutePath()
    {
        string path = Path.Combine(Path.GetTempPath(), $"sc_abs_{Guid.NewGuid():N}.png");
        ScreenCapture.CaptureScreenshot(path, 1);
        Assert.True(File.Exists(path));
        Assert.True(File.ReadAllBytes(path).Length > 16);
        File.Delete(path);
    }

    [Fact]
    public void CaptureScreenshot_StereoMode_BothEyes_DoesNotThrow()
    {
        var t = ScreenCapture.CaptureScreenshotAsTexture(1, ScreenCapture.StereoScreenCaptureMode.BothEyes);
        Assert.NotNull(t);
        Assert.True(t.width > 0);
    }

    [Fact]
    public void EncodeToPNG_ProducesPngSignature()
    {
        var t = ScreenCapture.CaptureScreenshotAsTexture(1);
        var png = t.EncodeToPNG();
        Assert.True(png.Length > 8);
        Assert.Equal(0x89, png[0]);
        Assert.Equal((byte)'P', png[1]);
        Assert.Equal((byte)'N', png[2]);
        Assert.Equal((byte)'G', png[3]);
    }
}
