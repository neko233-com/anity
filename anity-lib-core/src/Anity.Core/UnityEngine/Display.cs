using System;
using Anity.Core.Runtime.Native;

namespace UnityEngine;

/// <summary>
/// UnityEngine.Display — multi-monitor + HDR display query (Unity 2022.3).
/// </summary>
public class Display
{
    private static Display[] _displays = Array.Empty<Display>();
    private static Display _main;

    private readonly int _index;
    private int _systemWidth = 1920;
    private int _systemHeight = 1080;
    private int _renderingWidth = 1920;
    private int _renderingHeight = 1080;
    private bool _requiresBlitToBackbuffer;
    private bool _requiresSrgbBlitToBackbuffer;
    private bool _active = true;

    static Display()
    {
        _main = new Display(0);
        _displays = new[] { _main };
        if (AnityNative.TryQueryHDR(out var hdr) && hdr.available != 0)
        {
            // multi-display soft expand when HDR panel reported
            _displays = new[] { _main, new Display(1) { _systemWidth = 2560, _systemHeight = 1440 } };
        }
    }

    public Display(int index)
    {
        _index = index;
        _systemWidth = Screen.width;
        _systemHeight = Screen.height;
        _renderingWidth = Screen.width;
        _renderingHeight = Screen.height;
    }

    public static Display main => _main;
    public static Display[] displays => _displays;
    public static int displayCount => _displays.Length;

    public int systemWidth => _systemWidth;
    public int systemHeight => _systemHeight;
    public int renderingWidth => _renderingWidth;
    public int renderingHeight => _renderingHeight;
    public bool active => _active;
    public bool requiresBlitToBackbuffer => _requiresBlitToBackbuffer;
    public bool requiresSrgbBlitToBackbuffer => _requiresSrgbBlitToBackbuffer;

    public RenderBuffer colorBuffer => default;
    public RenderBuffer depthBuffer => default;

    public void Activate() => _active = true;
    public void Activate(int width, int height, int refreshRate)
    {
        _renderingWidth = width;
        _renderingHeight = height;
        _active = true;
        Screen.SetResolution(width, height, Screen.fullScreenMode, refreshRate);
    }

    public void SetParams(int width, int height, int x, int y)
    {
        _renderingWidth = width;
        _renderingHeight = height;
        _ = x; _ = y;
    }

    public void SetRenderingResolution(int w, int h)
    {
        _renderingWidth = w;
        _renderingHeight = h;
    }

    public static Vector3 RelativeMouseAt(Vector3 inputMouseCoordinates)
    {
        // Map mouse into main display space
        return new Vector3(
            Mathf.Clamp(inputMouseCoordinates.x, 0, Screen.width),
            Mathf.Clamp(inputMouseCoordinates.y, 0, Screen.height),
            0f);
    }
}
