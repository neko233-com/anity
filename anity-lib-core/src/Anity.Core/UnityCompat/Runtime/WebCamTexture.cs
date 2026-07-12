using System;
using System.Collections.Generic;

namespace UnityEngine;

public enum WebCamKind
{
    WideAngle,
    Telephoto,
    ColorAndDepth,
    DepthOnly
}

public struct WebCamDevice
{
    public string name;
    public bool isFrontFacing;
    public WebCamKind kind;
    public Resolution[] availableResolutions;

    internal WebCamDevice(string name, bool isFrontFacing, WebCamKind kind)
    {
        this.name = name ?? string.Empty;
        this.isFrontFacing = isFrontFacing;
        this.kind = kind;
        availableResolutions = Array.Empty<Resolution>();
    }
}

public class WebCamTexture : Texture
{
    private static readonly List<WebCamDevice> _devices = new();
    private bool _isPlaying;
    private bool _didUpdateThisFrame;
    private string _deviceName;
    private int _requestedWidth;
    private int _requestedHeight;
    private int _requestedFPS;

    public static WebCamDevice[] devices => _devices.ToArray();
    public string deviceName => _deviceName;
    public int requestedWidth => _requestedWidth;
    public int requestedHeight => _requestedHeight;
    public int requestedFPS => _requestedFPS;
    public bool isPlaying => _isPlaying;
    public bool didUpdateThisFrame => _didUpdateThisFrame;
    public bool isFrontFacing { get; private set; }
    public WebCamKind kind { get; private set; }
    public float videoRotationAngle { get; set; }
    public bool videoVerticallyMirrored { get; set; }
    public IntPtr nativeTexturePtr => IntPtr.Zero;

    static WebCamTexture()
    {
        _devices.Add(new WebCamDevice("WebCam1", false, WebCamKind.WideAngle));
        _devices.Add(new WebCamDevice("Front Camera", true, WebCamKind.WideAngle));
    }

    public WebCamTexture() : this("", 640, 480, 30)
    {
    }

    public WebCamTexture(int requestedWidth, int requestedHeight) : this("", requestedWidth, requestedHeight, 30)
    {
    }

    public WebCamTexture(int requestedWidth, int requestedHeight, int requestedFPS) : this("", requestedWidth, requestedHeight, requestedFPS)
    {
    }

    public WebCamTexture(string deviceName) : this(deviceName, 640, 480, 30)
    {
    }

    public WebCamTexture(string deviceName, int requestedWidth, int requestedHeight) : this(deviceName, requestedWidth, requestedHeight, 30)
    {
    }

    public WebCamTexture(string deviceName, int requestedWidth, int requestedHeight, int requestedFPS)
    {
        _deviceName = string.IsNullOrEmpty(deviceName) ? "WebCam1" : deviceName;
        _requestedWidth = Math.Max(1, requestedWidth);
        _requestedHeight = Math.Max(1, requestedHeight);
        _requestedFPS = Math.Max(1, requestedFPS);
        _isPlaying = false;
        _didUpdateThisFrame = false;
        isFrontFacing = false;
        kind = WebCamKind.WideAngle;
        width = 1;
        height = 1;
        dimension = TextureDimension.Tex2D;
    }

    public void Play()
    {
        _isPlaying = true;
        _didUpdateThisFrame = true;
        width = _requestedWidth;
        height = _requestedHeight;
        foreach (var device in _devices)
        {
            if (device.name == _deviceName)
            {
                isFrontFacing = device.isFrontFacing;
                kind = device.kind;
                break;
            }
        }
    }

    public void Pause()
    {
        _isPlaying = false;
        _didUpdateThisFrame = false;
    }

    public void Stop()
    {
        _isPlaying = false;
        _didUpdateThisFrame = false;
        width = 1;
        height = 1;
    }

    public Color32[] GetPixels32(Color32[] colors = null)
    {
        int pixelCount = width * height;
        if (colors == null || colors.Length != pixelCount)
            colors = new Color32[pixelCount];
        for (int i = 0; i < pixelCount; i++)
            colors[i] = new Color32(128, 128, 128, 255);
        return colors;
    }

    internal static void RegisterDevice(WebCamDevice device)
    {
        _devices.Add(device);
    }
}
