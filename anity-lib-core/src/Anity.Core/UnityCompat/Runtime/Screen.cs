namespace UnityEngine;

public static class Screen
{
    private static int _width = 1920;
    private static int _height = 1080;
    private static int _refreshRate = 60;
    private static float _dpi = 96f;
    private static bool _fullScreen;
    private static FullScreenMode _fullScreenMode = FullScreenMode.Windowed;
    private static int _sleepTimeout = SleepTimeout.NeverSleep;
    private static ScreenOrientation _orientation = ScreenOrientation.AutoRotation;
    private static bool _orientationInitialized;
    private static bool _hasNotch;
    private static float _safeAreaInsetTop = 24f;
    private static float _safeAreaInsetBottom;

    private static void InitializeOrientation()
    {
        if (_orientationInitialized) return;
        _orientationInitialized = true;
        if (Application.isMobilePlatform)
        {
            _orientation = (ScreenOrientation)UnityEditor.PlayerSettings.defaultScreenOrientation;
            autorotateToPortrait = UnityEditor.PlayerSettings.allowedAutorotateToPortrait;
            autorotateToPortraitUpsideDown = UnityEditor.PlayerSettings.allowedAutorotateToPortraitUpsideDown;
            autorotateToLandscapeLeft = UnityEditor.PlayerSettings.allowedAutorotateToLandscapeLeft;
            autorotateToLandscapeRight = UnityEditor.PlayerSettings.allowedAutorotateToLandscapeRight;
            _hasNotch = true;
            _safeAreaInsetTop = 88f;
            _safeAreaInsetBottom = 34f;
        }
    }

    public static int width
    {
        get => _width;
        set => _width = value;
    }

    public static int height
    {
        get => _height;
        set => _height = value;
    }

    public static Resolution currentResolution => new(_width, _height, _refreshRate);
    public static Resolution[] resolutions => new[]
    {
        new Resolution(800, 600, 60),
        new Resolution(1024, 768, 60),
        new Resolution(1280, 720, 60),
        new Resolution(1366, 768, 60),
        new Resolution(1600, 900, 60),
        new Resolution(1920, 1080, 60),
        new Resolution(2560, 1440, 60),
        new Resolution(3840, 2160, 60),
        currentResolution
    };

    public static float dpi
    {
        get => _dpi;
        set => _dpi = value;
    }

    public static bool fullScreen
    {
        get => _fullScreen;
        set
        {
            _fullScreen = value;
            _fullScreenMode = value ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
        }
    }

    public static FullScreenMode fullScreenMode
    {
        get => _fullScreenMode;
        set
        {
            _fullScreenMode = value;
            _fullScreen = value != FullScreenMode.Windowed;
        }
    }

    public static int sleepTimeout
    {
        get => _sleepTimeout;
        set => _sleepTimeout = value;
    }

    public static Rect safeArea
    {
        get
        {
            InitializeOrientation();
            return new Rect(0, _safeAreaInsetBottom, _width, _height - _safeAreaInsetTop - _safeAreaInsetBottom);
        }
    }

    public static Rect[] cutouts
    {
        get
        {
            InitializeOrientation();
            if (!_hasNotch) return Array.Empty<Rect>();
            float notchWidth = _width * 0.2f;
            float notchX = (_width - notchWidth) * 0.5f;
            return new[] { new Rect(notchX, _height - 30f, notchWidth, 30f) };
        }
    }

    public static float brightness { get; set; } = 1f;
    public static bool autorotateToPortrait { get; set; } = true;
    public static bool autorotateToPortraitUpsideDown { get; set; }
    public static bool autorotateToLandscapeLeft { get; set; } = true;
    public static bool autorotateToLandscapeRight { get; set; } = true;

    public static ScreenOrientation orientation
    {
        get
        {
            InitializeOrientation();
            return _orientation;
        }
        set
        {
            InitializeOrientation();
            _orientation = value;
        }
    }

    public static void SetResolution(int width, int height, bool fullscreen)
    {
        _width = width;
        _height = height;
        fullScreen = fullscreen;
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
        fullScreenMode = fullscreenMode;
    }

    public static void SetResolution(int width, int height, FullScreenMode fullscreenMode, int preferredRefreshRate)
    {
        SetResolution(width, height, fullscreenMode);
        _refreshRate = preferredRefreshRate;
    }

    public static void SetResolution(int width, int height, FullScreenMode fullscreenMode, RefreshRate preferredRefreshRate)
    {
        SetResolution(width, height, fullscreenMode);
        _refreshRate = (int)preferredRefreshRate.value;
    }
}

public struct Resolution
{
    public int width;
    public int height;
    public RefreshRate refreshRateRatio;

    public int refreshRate
    {
        get => (int)refreshRateRatio.value;
        set => refreshRateRatio = new RefreshRate { numerator = (uint)value, denominator = 1 };
    }

    public Resolution(int width, int height, int refreshRate)
    {
        this.width = width;
        this.height = height;
        refreshRateRatio = new RefreshRate { numerator = (uint)refreshRate, denominator = 1 };
    }

    public override string ToString() => $"{width} x {height} @{refreshRate}Hz";
}

public struct RefreshRate
{
    public uint numerator;
    public uint denominator;
    public float value => denominator == 0 ? 0f : (float)numerator / denominator;

    public override string ToString() => $"{value:F0}Hz";
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
    Portrait = 0,
    PortraitUpsideDown = 1,
    LandscapeLeft = 2,
    LandscapeRight = 3,
    AutoRotation = 4,
    Landscape = 5
}

public enum UIOrientation
{
    Portrait = 0,
    PortraitUpsideDown = 1,
    LandscapeLeft = 2,
    LandscapeRight = 3,
    AutoRotation = 4
}

public static class SleepTimeout
{
    public const int NeverSleep = -1;
    public const int SystemSetting = -2;
}
