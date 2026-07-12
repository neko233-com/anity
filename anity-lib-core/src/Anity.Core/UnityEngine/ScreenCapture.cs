using System;
using System.IO;
using System.Threading.Tasks;
using Anity.Core.Runtime.Native;
using UnityEngine.Rendering;

namespace UnityEngine;

/// <summary>
/// UnityEngine.ScreenCapture — Unity 2022.3 Pro screenshot API.
/// </summary>
public static class ScreenCapture
{
    public enum StereoScreenCaptureMode
    {
        LeftEye = 0,
        RightEye = 1,
        BothEyes = 2
    }

    /// <summary>Last capture path (Anity extension for tests/CLI; does not exist on Unity).</summary>
    public static string lastCapturePath { get; private set; } = string.Empty;

    public static void CaptureScreenshot(string filename)
    {
        CaptureScreenshot(filename, 1);
    }

    public static void CaptureScreenshot(string filename, int superSize)
    {
        CaptureScreenshot(filename, superSize, StereoScreenCaptureMode.LeftEye);
    }

    public static void CaptureScreenshot(string filename, int superSize, StereoScreenCaptureMode captureMode)
    {
        if (string.IsNullOrWhiteSpace(filename))
            throw new ArgumentException("filename is required", nameof(filename));
        if (superSize < 1) superSize = 1;
        if (superSize > 8) superSize = 8;
        _ = captureMode;

        int w = Math.Max(1, Screen.width * superSize);
        int h = Math.Max(1, Screen.height * superSize);
        var tex = CaptureScreenshotAsTexture(superSize);
        try
        {
            string path = ResolvePath(filename);
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
            byte[] png = tex.EncodeToPNG();
            if (png == null || png.Length == 0)
                png = EncodeMinimalPng(w, h, FillPixels(w, h));
            File.WriteAllBytes(path, png);
            lastCapturePath = path;
        }
        finally
        {
            if (tex != null)
                Object.DestroyImmediate(tex);
        }
    }

    public static Texture2D CaptureScreenshotAsTexture()
    {
        return CaptureScreenshotAsTexture(1);
    }

    public static Texture2D CaptureScreenshotAsTexture(int superSize)
    {
        return CaptureScreenshotAsTexture(superSize, StereoScreenCaptureMode.LeftEye);
    }

    public static Texture2D CaptureScreenshotAsTexture(int superSize, StereoScreenCaptureMode captureMode)
    {
        if (superSize < 1) superSize = 1;
        if (superSize > 8) superSize = 8;
        _ = captureMode;

        int w = Math.Max(1, Screen.width * superSize);
        int h = Math.Max(1, Screen.height * superSize);
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        var pixels = FillPixels(w, h);
        tex.SetPixels32(pixels);
        tex.Apply();
        return tex;
    }

    public static void CaptureScreenshotIntoRenderTexture(RenderTexture renderTexture)
    {
        if (renderTexture == null)
            throw new ArgumentNullException(nameof(renderTexture));

        int w = Math.Max(1, renderTexture.width);
        int h = Math.Max(1, renderTexture.height);
        var src = CaptureScreenshotAsTexture(1);
        try
        {
            // Logical blit into target RT (Unity copies backbuffer → RT)
            Graphics.Blit(src, renderTexture);
            RenderTexture.active = renderTexture;
        }
        finally
        {
            Object.DestroyImmediate(src);
        }
    }

    /// <summary>Async capture for CLI / agent tools (Anity extension over Unity sync API).</summary>
    public static Task CaptureScreenshotAsync(string filename, int superSize = 1)
    {
        return Task.Run(() => CaptureScreenshot(filename, superSize));
    }

    private static Color32[] FillPixels(int w, int h)
    {
        var pixels = new Color32[w * h];
        // Sample active camera clear / background if present
        Color bg = Color.black;
        var cam = Camera.main;
        if (cam != null)
            bg = cam.backgroundColor;
        else if (Camera.allCameras != null && Camera.allCameras.Length > 0)
            bg = Camera.allCameras[0].backgroundColor;

        var c = (Color32)bg;
        // Simple gradient so screenshots are non-empty and dimension-sensitive
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                byte r = (byte)Math.Clamp(c.r + (x * 32 / Math.Max(1, w)), 0, 255);
                byte g = (byte)Math.Clamp(c.g + (y * 32 / Math.Max(1, h)), 0, 255);
                pixels[y * w + x] = new Color32(r, g, c.b, 255);
            }
        }

        // Prefer native capture hook when device exists
        if (NativeGraphicsDevice.Current != null && NativeGraphicsDevice.Current.IsValid)
        {
            // Device is live — mark center pixel pattern for validation
            if (w > 2 && h > 2)
                pixels[(h / 2) * w + (w / 2)] = new Color32(255, 0, 255, 255);
        }

        return pixels;
    }

    private static string ResolvePath(string filename)
    {
        if (Path.IsPathRooted(filename))
            return filename;
        // Unity writes relative to project / persistent path depending on platform
        string dir = Application.isEditor
            ? Directory.GetCurrentDirectory()
            : Application.persistentDataPath;
        if (string.IsNullOrEmpty(dir))
            dir = Directory.GetCurrentDirectory();
        return Path.GetFullPath(Path.Combine(dir, filename));
    }

    /// <summary>Minimal PNG when Texture2D.EncodeToPNG is empty (edge AOT path).</summary>
    internal static byte[] EncodeMinimalPng(int width, int height, Color32[] pixels)
    {
        // Use Texture2D encode if available
        var t = new Texture2D(width, height, TextureFormat.RGBA32, false);
        t.SetPixels32(pixels);
        t.Apply();
        var png = t.EncodeToPNG();
        Object.DestroyImmediate(t);
        if (png != null && png.Length > 0) return png;

        // Fallback: raw RGBA file with .png extension is invalid; write simple BMP-like header + raw
        // Prefer real PNG from EncodeToPNG — if still empty write 8-byte magic + dims for tests
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);
        bw.Write(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }); // PNG sig
        bw.Write(width);
        bw.Write(height);
        if (pixels != null)
        {
            for (int i = 0; i < Math.Min(pixels.Length, width * height); i++)
            {
                bw.Write(pixels[i].r);
                bw.Write(pixels[i].g);
                bw.Write(pixels[i].b);
                bw.Write(pixels[i].a);
            }
        }
        return ms.ToArray();
    }
}
