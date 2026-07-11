using System;
using System.Collections.Generic;

namespace UnityEngine;

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
    public float latitude => 0;
    public float longitude => 0;
    public float altitude => 0;
    public float horizontalAccuracy => 0;
    public float verticalAccuracy => 0;
    public double timestamp => 0;
}

public class Compass
{
    public float magneticHeading => 0;
    public float trueHeading => 0;
    public float headingAccuracy => 0;
    public Vector3 rawVector => Vector3.zero;
    public bool enabled { get; set; }
}

public class Gyroscope
{
    public Vector3 rotationRate => Vector3.zero;
    public Vector3 rotationRateUnbiased => Vector3.zero;
    public Vector3 gravity => Vector3.zero;
    public Vector3 userAcceleration => Vector3.zero;
    public Quaternion attitude => Quaternion.identity;
    public bool enabled { get; set; }
    public float updateInterval => 0.016f;
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
    private static readonly Touch[] _emptyTouches = Array.Empty<Touch>();
    private static bool _anyKey;
    private static bool _anyKeyDown;
    private static readonly LocationService _location = new();
    private static readonly Compass _compass = new();
    private static readonly Gyroscope _gyro = new();

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
    public static bool multiTouchEnabled { get; set; }
    public static Vector3 acceleration { get; set; }
    public static Touch[] touches => _emptyTouches;
    public static int touchCount => touches.Length;
    public static int imeCompositionMode { get; set; }
    public static string compositionString { get; private set; } = string.Empty;
    public static bool imeIsSelected => false;
    public static Vector2 compositionCursorPos { get; set; }
    public static bool mousePresent => true;
    public static bool touchSupported => false;
    public static bool GetTouchSupported() => false;

    static Input()
    {
        ResetAxes();
    }

    private static void ResetAxes()
    {
        _axes["Horizontal"] = 0f;
        _axes["Vertical"] = 0f;
        _axesRaw["Horizontal"] = 0f;
        _axesRaw["Vertical"] = 0f;
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
        if (!_buttonsHeld[buttonName])
        {
            _buttonsDown.Add(buttonName);
        }
        _buttonsHeld[buttonName] = true;
    }

    public static void SimulateButtonUp(string buttonName)
    {
        if (_buttonsHeld[buttonName])
        {
            _buttonsUp.Add(buttonName);
        }
        _buttonsHeld[buttonName] = false;
    }

    internal static void UpdatePerFrame()
    {
        _keysDown.Clear();
        _keysUp.Clear();
        _buttonsDown.Clear();
        _buttonsUp.Clear();
        _anyKeyDown = false;
        mouseScrollDelta = Vector2.zero;
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

    public static Touch GetTouch(int index) => touches[index];
    public static bool GetKeyUp(KeyCode key, bool useAutoRepeat) => GetKeyUp(key);
}
