using System;
using System.Collections.Generic;

namespace UnityEngine;

public enum DeviceOrientation
{
    Unknown,
    Portrait,
    PortraitUpsideDown,
    LandscapeLeft,
    LandscapeRight,
    FaceUp,
    FaceDown
}

public enum IMECompositionMode
{
    Auto,
    On,
    Off
}

public struct AccelerationEvent
{
    public Vector3 acceleration;
    public float deltaTime;
}

public struct Touch
{
    public int fingerId;
    public Vector2 position;
    public Vector2 rawPosition;
    public Vector2 deltaPosition;
    public float deltaTime;
    public int tapCount;
    public TouchPhase phase;
    public float pressure;
    public float maximumPossiblePressure;
    public TouchType type;
    public float radius;
    public float radiusVariance;
    public float azimuthAngle;
    public float altitudeAngle;
}

public enum TouchPhase
{
    Began,
    Moved,
    Stationary,
    Ended,
    Canceled
}

public enum TouchType
{
    Direct,
    Indirect,
    Stylus
}

public class LocationService
{
    private bool _isEnabledByUser;
    private LocationServiceStatus _status = LocationServiceStatus.Stopped;
    private LocationInfo _lastData;
    private double _desiredAccuracyInMeters = 10;
    private double _updateDistanceInMeters = 10;

    public bool isEnabledByUser => _isEnabledByUser;
    public LocationServiceStatus status => _status;
    public LocationInfo lastData => _lastData;
    public double desiredAccuracyInMeters => _desiredAccuracyInMeters;
    public double updateDistanceInMeters => _updateDistanceInMeters;

    public void Start()
    {
        Start(10f, 10f);
    }

    public void Start(float desiredAccuracyInMeters)
    {
        Start(desiredAccuracyInMeters, 10f);
    }

    public void Start(float desiredAccuracyInMeters, float updateDistanceInMeters)
    {
        _desiredAccuracyInMeters = desiredAccuracyInMeters;
        _updateDistanceInMeters = updateDistanceInMeters;
        _isEnabledByUser = true;
        _status = LocationServiceStatus.Running;
        _lastData = default;
    }

    public void Stop()
    {
        _isEnabledByUser = false;
        _status = LocationServiceStatus.Stopped;
    }
}

public enum LocationServiceStatus
{
    Stopped,
    Initializing,
    Running,
    Failed
}

public struct LocationInfo
{
    public float latitude;
    public float longitude;
    public float altitude;
    public float horizontalAccuracy;
    public float verticalAccuracy;
    public double timestamp;
}

public class Compass
{
    public float magneticHeading { get; set; }
    public float trueHeading { get; set; }
    public float headingAccuracy { get; set; }
    public Vector3 rawVector { get; set; }
    public double timestamp { get; set; }
    public bool enabled { get; set; }

    public Compass()
    {
        magneticHeading = 0f;
        trueHeading = 0f;
        headingAccuracy = -1f;
        rawVector = Vector3.zero;
        timestamp = 0;
        enabled = false;
    }
}

public class Gyroscope
{
    public Vector3 rotationRate { get; set; }
    public Vector3 rotationRateUnbiased { get; set; }
    public Vector3 gravity { get; set; }
    public Vector3 userAcceleration { get; set; }
    public Quaternion attitude { get; set; }
    public bool enabled { get; set; }
    public float updateInterval { get; set; }

    public Gyroscope()
    {
        rotationRate = Vector3.zero;
        rotationRateUnbiased = Vector3.zero;
        gravity = Vector3.zero;
        userAcceleration = Vector3.zero;
        attitude = Quaternion.identity;
        enabled = false;
        updateInterval = 0.016f;
    }
}

public static class Input
{
    private static readonly HashSet<KeyCode> _keysHeld = new();
    private static readonly HashSet<KeyCode> _keysDown = new();
    private static readonly HashSet<KeyCode> _keysUp = new();
    private static readonly Dictionary<string, float> _axes = new();
    private static readonly Dictionary<string, float> _axesRaw = new();
    private static readonly Dictionary<string, bool> _buttonsHeld = new();
    private static readonly HashSet<string> _buttonsDown = new();
    private static readonly HashSet<string> _buttonsUp = new();
    private static readonly List<Touch> _touches = new();
    private static readonly List<AccelerationEvent> _accelerationEvents = new();
    private static bool _anyKey;
    private static bool _anyKeyDown;
    private static readonly LocationService _location = new();
    private static readonly Compass _compass = new();
    private static readonly Gyroscope _gyro = new();
    private static string _inputString = string.Empty;
    private static string _compositionString = string.Empty;

    public static event Action<DeviceOrientation>? onDeviceOrientationChange;

    public static LocationService location => _location;
    public static Compass compass => _compass;
    public static Gyroscope gyro => _gyro;
    public static bool simulateMouseWithTouches { get; set; }
    public static bool anyKey => _anyKey;
    public static bool anyKeyDown => _anyKeyDown;
    public static Vector3 mousePosition { get; set; }
    public static Vector2 mouseScrollDelta { get; set; }
    public static bool backButtonLeavesApp { get; set; }
    public static bool compensateSensors { get; set; }
    public static bool multiTouchEnabled { get; set; } = true;
    public static Vector3 acceleration { get; set; }
    private static DeviceOrientation _deviceOrientation = DeviceOrientation.Portrait;
    public static DeviceOrientation deviceOrientation
    {
        get => _deviceOrientation;
        set
        {
            if (_deviceOrientation != value)
            {
                _deviceOrientation = value;
                onDeviceOrientationChange?.Invoke(value);
            }
        }
    }
    public static Touch[] touches => _touches.ToArray();
    public static int touchCount => _touches.Count;
    public static IMECompositionMode imeCompositionMode { get; set; } = IMECompositionMode.Auto;
    public static string compositionString
    {
        get => _compositionString;
        set => _compositionString = value ?? string.Empty;
    }
    public static bool imeIsSelected { get; set; }
    public static Vector2 compositionCursorPos { get; set; }
    public static string inputString
    {
        get => _inputString;
        set => _inputString = value ?? string.Empty;
    }
    public static bool mousePresent => true;
    public static bool touchSupported => true;
    public static bool touchPressureSupported => true;
    public static bool stylusTouchSupported => false;
    public static int accelerationEventCount => _accelerationEvents.Count;

    static Input()
    {
        ResetAxes();
    }

    public static void ResetInputAxes()
    {
        ResetAxes();
        foreach (var key in _axes.Keys)
        {
            _axes[key] = 0f;
            _axesRaw[key] = 0f;
        }
        _buttonsDown.Clear();
        _buttonsUp.Clear();
        foreach (var button in _buttonsHeld.Keys)
        {
            _buttonsHeld[button] = false;
        }
        _keysDown.Clear();
        _keysUp.Clear();
        mouseScrollDelta = Vector2.zero;
        _inputString = string.Empty;
    }

    private static void ResetAxes()
    {
        _axes["Horizontal"] = 0f;
        _axes["Vertical"] = 0f;
        _axes["Mouse X"] = 0f;
        _axes["Mouse Y"] = 0f;
        _axes["Mouse ScrollWheel"] = 0f;
        _axesRaw["Horizontal"] = 0f;
        _axesRaw["Vertical"] = 0f;
        _axesRaw["Mouse X"] = 0f;
        _axesRaw["Mouse Y"] = 0f;
        _axesRaw["Mouse ScrollWheel"] = 0f;
        _buttonsHeld["Fire1"] = false;
        _buttonsHeld["Fire2"] = false;
        _buttonsHeld["Fire3"] = false;
        _buttonsHeld["Jump"] = false;
        _buttonsHeld["Submit"] = false;
        _buttonsHeld["Cancel"] = false;
    }

    public static void SimulateKeyDown(KeyCode key)
    {
        if (!_keysHeld.Contains(key))
        {
            _keysDown.Add(key);
            _anyKeyDown = true;
        }
        _keysHeld.Add(key);
        _anyKey = true;
    }

    public static void SimulateKeyUp(KeyCode key)
    {
        if (_keysHeld.Remove(key))
        {
            _keysUp.Add(key);
        }
        if (_keysHeld.Count == 0)
        {
            _anyKey = false;
        }
    }

    public static void SimulateAxis(string axisName, float value)
    {
        _axes[axisName] = value;
        _axesRaw[axisName] = Math.Sign(value);
    }

    public static void SimulateButtonDown(string buttonName)
    {
        if (!_buttonsHeld.TryGetValue(buttonName, out bool held) || !held)
        {
            _buttonsDown.Add(buttonName);
        }
        _buttonsHeld[buttonName] = true;
    }

    public static void SimulateButtonUp(string buttonName)
    {
        if (_buttonsHeld.TryGetValue(buttonName, out bool held) && held)
        {
            _buttonsUp.Add(buttonName);
        }
        _buttonsHeld[buttonName] = false;
    }

    public static void SimulateTouch(int fingerId, Vector2 position, TouchPhase phase)
    {
        var touch = new Touch
        {
            fingerId = fingerId,
            position = position,
            rawPosition = position,
            phase = phase,
            tapCount = 1,
            pressure = 1f,
            maximumPossiblePressure = 1f,
            type = TouchType.Direct,
            deltaTime = Time.deltaTime
        };

        for (int i = 0; i < _touches.Count; i++)
        {
            if (_touches[i].fingerId == fingerId)
            {
                var prev = _touches[i];
                touch.deltaPosition = position - prev.position;
                if (phase == TouchPhase.Ended || phase == TouchPhase.Canceled)
                {
                    _touches.RemoveAt(i);
                }
                else
                {
                    _touches[i] = touch;
                }
                return;
            }
        }

        if (phase != TouchPhase.Ended && phase != TouchPhase.Canceled)
        {
            _touches.Add(touch);
        }
    }

    public static AccelerationEvent GetAccelerationEvent(int index)
    {
        if (index < 0 || index >= _accelerationEvents.Count)
            return default;
        return _accelerationEvents[index];
    }

    internal static void UpdatePerFrame()
    {
        _keysDown.Clear();
        _keysUp.Clear();
        _buttonsDown.Clear();
        _buttonsUp.Clear();
        _anyKeyDown = false;
        mouseScrollDelta = Vector2.zero;
        _inputString = string.Empty;
        _accelerationEvents.Clear();
        for (int i = _touches.Count - 1; i >= 0; i--)
        {
            var t = _touches[i];
            if (t.phase == TouchPhase.Began)
            {
                t.phase = TouchPhase.Stationary;
                _touches[i] = t;
            }
            else if (t.phase == TouchPhase.Moved)
            {
                t.phase = TouchPhase.Stationary;
                _touches[i] = t;
            }
            else if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled)
            {
                _touches.RemoveAt(i);
            }
        }
    }

    public static bool GetKey(KeyCode key) => _keysHeld.Contains(key);
    public static bool GetKey(string name) => GetKey((KeyCode)Enum.Parse(typeof(KeyCode), name, true));
    public static bool GetKeyDown(KeyCode key) => _keysDown.Contains(key);
    public static bool GetKeyDown(string name) => GetKeyDown((KeyCode)Enum.Parse(typeof(KeyCode), name, true));
    public static bool GetKeyUp(KeyCode key) => _keysUp.Contains(key);
    public static bool GetKeyUp(string name) => GetKeyUp((KeyCode)Enum.Parse(typeof(KeyCode), name, true));

    public static bool GetMouseButton(int button) => _keysHeld.Contains((KeyCode)((int)KeyCode.Mouse0 + button));
    public static bool GetMouseButtonDown(int button) => _keysDown.Contains((KeyCode)((int)KeyCode.Mouse0 + button));
    public static bool GetMouseButtonUp(int button) => _keysUp.Contains((KeyCode)((int)KeyCode.Mouse0 + button));

    public static float GetAxis(string axisName) => _axes.TryGetValue(axisName, out float v) ? v : 0f;
    public static float GetAxisRaw(string axisName) => _axesRaw.TryGetValue(axisName, out float v) ? v : 0f;
    public static bool GetButton(string buttonName) => _buttonsHeld.TryGetValue(buttonName, out bool v) && v;
    public static bool GetButtonDown(string buttonName) => _buttonsDown.Contains(buttonName);
    public static bool GetButtonUp(string buttonName) => _buttonsUp.Contains(buttonName);

    public static Touch GetTouch(int index)
    {
        if (index < 0 || index >= _touches.Count)
            return default;
        return _touches[index];
    }

    public static bool GetKeyUp(KeyCode key, bool useAutoRepeat) => GetKeyUp(key);

    public static bool GetTouchSupported() => true;
    public static bool IsTouchSupported() => true;
}
