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
    private IntPtr _swapchain;
    private bool _disposed;
    private bool _managedSwapchain;

    public static NativeGraphicsDevice? Current { get; private set; }

    public IntPtr Handle => _handle;
    public IntPtr SwapchainHandle => _swapchain;
    public bool IsValid => _handle != IntPtr.Zero || _managedSwapchain;
    public bool HasSwapchain => _swapchain != IntPtr.Zero || _managedSwapchain;
    public GraphicsDeviceType DeviceType { get; private set; }
    public bool SupportsHDR { get; private set; }
    public int Width { get; private set; }
    public int Height { get; private set; }
    public int SwapchainImageCount { get; private set; }
    public bool SwapchainHeadless { get; private set; } = true;
    public bool SwapchainHasNativeSurface { get; private set; }
    /// <summary>0=software, 1=Vulkan, 2=Metal, 3=D3D</summary>
    public int SwapchainBackendKind { get; private set; }
    public int PresentCount { get; private set; }

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
        if (_swapchain != IntPtr.Zero && AnityNative.Available)
        {
            AnityNative.Graphics_PresentSwapchain(_swapchain);
            PresentCount++;
            return;
        }
        if (_handle != IntPtr.Zero && AnityNative.Available)
        {
            AnityNative.Graphics_Present(_handle);
            PresentCount++;
            return;
        }
        // managed headless present
        PresentCount++;
    }

    /// <summary>Create headless or windowed swapchain (Metal/Vulkan/D3D/null path).</summary>
    public bool CreateSwapchain(int width = 0, int height = 0, int imageCount = 2, bool vsync = true, bool hdr = false, IntPtr nativeWindow = default)
    {
        int w = width > 0 ? width : Width;
        int h = height > 0 ? height : Height;
        if (_handle != IntPtr.Zero && AnityNative.Available)
        {
            try
            {
                var desc = new AnityNative.SwapchainDesc
                {
                    width = w,
                    height = h,
                    imageCount = imageCount,
                    vsync = vsync ? 1 : 0,
                    hdr = hdr ? 1 : 0,
                    nativeWindow = nativeWindow
                };
                if (AnityNative.Graphics_CreateSwapchain(_handle, ref desc, out var sc) == AnityNative.Result.Ok && sc != IntPtr.Zero)
                {
                    _swapchain = sc;
                    SwapchainImageCount = AnityNative.Graphics_GetSwapchainImageCount(sc);
                    Width = AnityNative.Graphics_GetSwapchainWidth(sc);
                    Height = AnityNative.Graphics_GetSwapchainHeight(sc);
                    SwapchainHeadless = AnityNative.Graphics_IsSwapchainHeadless(sc) != 0;
                    try
                    {
                        SwapchainHasNativeSurface = AnityNative.Graphics_SwapchainHasNativeSurface(sc) != 0;
                        SwapchainBackendKind = AnityNative.Graphics_GetSwapchainBackendKind(sc);
                    }
                    catch
                    {
                        SwapchainHasNativeSurface = false;
                        SwapchainBackendKind = DeviceType == GraphicsDeviceType.Vulkan ? 1
                            : DeviceType == GraphicsDeviceType.Metal ? 2 : 0;
                    }
                    return true;
                }
            }
            catch
            {
                AnityNative.MarkUnavailable();
            }
        }

        // Managed headless swapchain (native lib missing or create failed)
        _managedSwapchain = true;
        Width = w > 0 ? w : 1280;
        Height = h > 0 ? h : 720;
        SwapchainImageCount = imageCount > 0 ? imageCount : 2;
        SwapchainHeadless = nativeWindow == IntPtr.Zero;
        SwapchainHasNativeSurface = false;
        SwapchainBackendKind = preferredKind(DeviceType);
        return true;
    }

    private static int preferredKind(GraphicsDeviceType t) => t switch
    {
        GraphicsDeviceType.Vulkan => 1,
        GraphicsDeviceType.Metal => 2,
        GraphicsDeviceType.Direct3D11 or GraphicsDeviceType.Direct3D12 => 3,
        _ => 0
    };

    public int AcquireNextImage()
    {
        if (_swapchain != IntPtr.Zero && AnityNative.Available)
        {
            if (AnityNative.Graphics_AcquireNextImage(_swapchain, out int idx) == AnityNative.Result.Ok)
                return idx;
        }
        return PresentCount % Math.Max(1, SwapchainImageCount);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_swapchain != IntPtr.Zero && AnityNative.Available)
        {
            try { AnityNative.Graphics_DestroySwapchain(_swapchain); } catch { }
            _swapchain = IntPtr.Zero;
        }
        if (_handle != IntPtr.Zero && AnityNative.Available)
        {
            AnityNative.Graphics_DestroyDevice(_handle);
            _handle = IntPtr.Zero;
        }
        _managedSwapchain = false;
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
