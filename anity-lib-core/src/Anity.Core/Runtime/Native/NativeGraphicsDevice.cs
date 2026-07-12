using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Anity.Core.Runtime.Native;

/// <summary>
/// Managed wrapper over AnityGraphics_* — Unity player/editor graphics device bootstrap.
/// </summary>
public sealed class NativeGraphicsDevice : IDisposable
{
    private IntPtr _handle;
    private bool _disposed;

    public static NativeGraphicsDevice? Current { get; private set; }

    public IntPtr Handle => _handle;
    public bool IsValid => _handle != IntPtr.Zero;
    public GraphicsDeviceType DeviceType { get; private set; }
    public bool SupportsHDR { get; private set; }
    public int Width { get; private set; }
    public int Height { get; private set; }

    public static NativeGraphicsDevice Create(
        GraphicsDeviceType preferred,
        int width,
        int height,
        bool hdr,
        int msaa = 1,
        bool vsync = true,
        IntPtr nativeWindow = default)
    {
        var dev = new NativeGraphicsDevice();
        if (AnityNative.Available)
        {
            var desc = new AnityNative.GraphicsDeviceDesc
            {
                preferred = (int)Map(preferred),
                width = width,
                height = height,
                hdrEnabled = hdr ? 1 : 0,
                msaaSamples = msaa,
                vsync = vsync ? 1 : 0,
                nativeWindow = nativeWindow
            };
            try
            {
                if (AnityNative.Graphics_CreateDevice(ref desc, out var h) == AnityNative.Result.Ok && h != IntPtr.Zero)
                {
                    dev._handle = h;
                    dev.DeviceType = MapBack(AnityNative.Graphics_GetDeviceType(h));
                    dev.SupportsHDR = AnityNative.Graphics_SupportsHDR(h) != 0;
                    dev.Width = width;
                    dev.Height = height;
                    Current = dev;
                    SystemInfo.overrideGraphicsDeviceType = dev.DeviceType;
                    return dev;
                }
            }
            catch
            {
                AnityNative.MarkUnavailable();
            }
        }

        // Managed fallback device record
        dev.DeviceType = preferred;
        dev.SupportsHDR = hdr;
        dev.Width = width;
        dev.Height = height;
        SystemInfo.overrideGraphicsDeviceType = preferred;
        Current = dev;
        return dev;
    }

    public void BeginFrame()
    {
        if (_handle != IntPtr.Zero && AnityNative.Available)
            AnityNative.Graphics_BeginFrame(_handle);
    }

    public void EndFrame()
    {
        if (_handle != IntPtr.Zero && AnityNative.Available)
            AnityNative.Graphics_EndFrame(_handle);
    }

    public void Present()
    {
        if (_handle != IntPtr.Zero && AnityNative.Available)
            AnityNative.Graphics_Present(_handle);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_handle != IntPtr.Zero && AnityNative.Available)
        {
            AnityNative.Graphics_DestroyDevice(_handle);
            _handle = IntPtr.Zero;
        }
        if (Current == this) Current = null;
    }

    private static AnityNative.GraphicsDeviceTypeNative Map(GraphicsDeviceType t) => t switch
    {
        GraphicsDeviceType.Direct3D11 => AnityNative.GraphicsDeviceTypeNative.D3D11,
        GraphicsDeviceType.Direct3D12 => AnityNative.GraphicsDeviceTypeNative.D3D12,
        GraphicsDeviceType.Vulkan => AnityNative.GraphicsDeviceTypeNative.Vulkan,
        GraphicsDeviceType.Metal => AnityNative.GraphicsDeviceTypeNative.Metal,
        GraphicsDeviceType.OpenGLES3 => AnityNative.GraphicsDeviceTypeNative.OpenGLES3,
        GraphicsDeviceType.OpenGLES2 => AnityNative.GraphicsDeviceTypeNative.OpenGLES2,
        GraphicsDeviceType.OpenGLCore => AnityNative.GraphicsDeviceTypeNative.OpenGLCore,
        GraphicsDeviceType.WebGL2 => AnityNative.GraphicsDeviceTypeNative.WebGL2,
        _ => AnityNative.GraphicsDeviceTypeNative.Null
    };

    private static GraphicsDeviceType MapBack(int t) => t switch
    {
        2 => GraphicsDeviceType.Direct3D11,
        18 => GraphicsDeviceType.Direct3D12,
        21 => GraphicsDeviceType.Vulkan,
        16 => GraphicsDeviceType.Metal,
        11 => GraphicsDeviceType.OpenGLES3,
        8 => GraphicsDeviceType.OpenGLES2,
        17 => GraphicsDeviceType.OpenGLCore,
        28 => GraphicsDeviceType.WebGL2,
        _ => GraphicsDeviceType.Null
    };
}
