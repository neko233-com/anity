namespace UnityEngine;

/// <summary>
/// Screen resolution and display information.
/// </summary>
public static class Screen
{
    private static int _width = 1920;
    private static int _height = 1080;
    private static int _refreshRate = 60;
    private static float _dpi = 96f;
    private static bool _fullScreen;
    private static FullScreenMode _fullScreenMode = FullScreenMode.Windowed;
    private static int _sleepTimeout;

    public static int width => _width;
    public static int height => _height;
    public static Resolution currentResolution => new Resolution(_width, _height, _refreshRate);
    public static Resolution[] resolutions => new[] { currentResolution };
    public static float dpi => _dpi;
    public static bool fullScreen => _fullScreen;
    public static FullScreenMode fullScreenMode => _fullScreenMode;
    public static int sleepTimeout
    {
        get => _sleepTimeout;
        set => _sleepTimeout = value;
    }
    public static int currentResolutionRefreshRate => _refreshRate;
    public static Rect safeArea => new Rect(0, 0, _width, _height);
    public static Rect[] cutouts => Array.Empty<Rect>();
    public static int brightness { get; set; } = 100;
    public static bool autorotateToPortrait { get; set; } = true;
    public static bool autorotateToPortraitUpsideDown { get; set; } = true;
    public static bool autorotateToLandscapeLeft { get; set; } = true;
    public static bool autorotateToLandscapeRight { get; set; } = true;
    public static ScreenOrientation orientation { get; set; } = ScreenOrientation.AutoRotation;

    public static void SetResolution(int width, int height, bool fullscreen)
    {
        _width = width;
        _height = height;
        _fullScreen = fullscreen;
    }

    public static void SetResolution(int width, int height, bool fullscreen, int preferredRefreshRate)
    {
        SetResolution(width, height, fullscreen);
        _refreshRate = preferredRefreshRate;
    }

    public static void SetResolution(int width, int height, FullScreenMode fullscreenMode)
    {
        _width = width;
        _height = height;
        _fullScreenMode = fullscreenMode;
        _fullScreen = fullscreenMode != FullScreenMode.Windowed;
    }

    public static void SetResolution(int width, int height, FullScreenMode fullscreenMode, int preferredRefreshRate)
    {
        SetResolution(width, height, fullscreenMode);
        _refreshRate = preferredRefreshRate;
    }
}

public struct Resolution
{
    public int width;
    public int height;
    public int refreshRateRatio;

    public Resolution(int width, int height, int refreshRate)
    {
        this.width = width;
        this.height = height;
        refreshRateRatio = refreshRate;
    }

    public override string ToString() => $"{width} x {height} @{refreshRateRatio}Hz";
}

public enum FullScreenMode
{
    ExclusiveFullScreen,
    FullScreenWindow,
    MaximizedWindow,
    Windowed
}

public enum ScreenOrientation
{
    Portrait = 1,
    PortraitUpsideDown = 2,
    LandscapeLeft = 3,
    LandscapeRight = 4,
    AutoRotation = 5,
    Landscape = 6
}
