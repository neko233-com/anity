using System;

namespace UnityEngine.Video;

public enum VideoSource
{
    VideoClip,
    Url
}

public enum VideoRenderMode
{
    CameraFarPlane = 0,
    CameraNearPlane = 1,
    RenderTexture = 2,
    MaterialOverride = 3,
    APIOnly = 4
}

public enum VideoAudioOutputMode
{
    None = 0,
    AudioSource = 1,
    Direct = 2
}

public enum VideoTimeReference
{
    Freerun = 0,
    InternalTime = 1,
    ExternalTime = 2
}

public enum Video3DLayout
{
    No3D = 0,
    SideBySide3D = 1,
    OverUnder3D = 2
}

public sealed class VideoPlayer : Behaviour
{
    private VideoSource _source = VideoSource.VideoClip;
    private VideoClip? _clip;
    private string _url = string.Empty;
    private bool _isPlaying;
    private bool _isPrepared;
    private bool _isPaused;
    private bool _isLooping;
    private bool _playOnAwake = true;
    private bool _waitForFirstFrame = true;
    private bool _skipOnDrop;
    private double _time;
    private double _clockTime;
    private long _frame;
    private ulong _frameCount;
    private float _playbackSpeed = 1f;
    private VideoRenderMode _renderMode = VideoRenderMode.APIOnly;
    private VideoAudioOutputMode _audioOutputMode = VideoAudioOutputMode.None;
    private RenderTexture? _targetTexture;
    private Camera? _targetCamera;
    private Renderer? _targetMaterialRenderer;
    private Material? _targetMaterial;
    private string _targetMaterialProperty = string.Empty;
    private float _targetCameraAspectRatio = 16f / 9f;
    private float _audioVolume = 1f;
    private bool _audioMuted;
    private ushort _controlledAudioTrackCount;
    private double _length;
    private uint _width;
    private uint _height;
    private double _frameRate = 30;
    private bool _isStopped = true;
    private float _prepareTime;
    private bool _sendFrameReadyEvents;
    private bool _prepareCompletedSent;
    private AudioSource? _audioSource;

    public VideoPlayer()
    {
        if (_playOnAwake)
        {
            SendPrepareCompletedEvent();
        }
    }

    public VideoSource source
    {
        get => _source;
        set => _source = value;
    }

    public VideoClip? clip
    {
        get => _clip;
        set
        {
            _clip = value;
            if (value != null)
            {
                _frameCount = value.frameCount;
                _length = value.length;
                _frameRate = value.frameRate;
                _width = value.width;
                _height = value.height;
                _source = VideoSource.VideoClip;
            }
        }
    }

    public string url
    {
        get => _url;
        set
        {
            _url = value ?? string.Empty;
            _source = VideoSource.Url;
        }
    }

    public bool playOnAwake
    {
        get => _playOnAwake;
        set => _playOnAwake = value;
    }

    public float playbackSpeed
    {
        get => _playbackSpeed;
        set => _playbackSpeed = value;
    }

    public bool isPlaying => _isPlaying;
    public bool isPaused => _isPaused;
    public bool isLooping
    {
        get => _isLooping;
        set => _isLooping = value;
    }

    public bool waitForFirstFrame
    {
        get => _waitForFirstFrame;
        set => _waitForFirstFrame = value;
    }

    public bool skipOnDrop
    {
        get => _skipOnDrop;
        set => _skipOnDrop = value;
    }

    public bool isPrepared => _isPrepared;
    public bool sendFrameReadyEvents
    {
        get => _sendFrameReadyEvents;
        set => _sendFrameReadyEvents = value;
    }

    public double time
    {
        get => _time;
        set
        {
            _time = Math.Max(0, value);
            _frame = (long)(_time * _frameRate);
            seekCompleted?.Invoke(this, _frame);
        }
    }

    public long frame
    {
        get => _frame;
        set
        {
            _frame = Math.Max(0, value);
            _time = _frame / _frameRate;
            seekCompleted?.Invoke(this, _frame);
        }
    }

    public ulong frameCount => _frameCount;
    public double length => _length;
    public double duration => _length;
    public double clockTime => _clockTime;

    public AudioSource? audioSource
    {
        get => _audioSource;
        set => _audioSource = value;
    }

    public VideoRenderMode renderMode
    {
        get => _renderMode;
        set => _renderMode = value;
    }

    public VideoAudioOutputMode audioOutputMode
    {
        get => _audioOutputMode;
        set => _audioOutputMode = value;
    }

    public RenderTexture? targetTexture
    {
        get => _targetTexture;
        set => _targetTexture = value;
    }

    public Camera? targetCamera
    {
        get => _targetCamera;
        set => _targetCamera = value;
    }

    public Renderer? targetMaterialRenderer
    {
        get => _targetMaterialRenderer;
        set => _targetMaterialRenderer = value;
    }

    public Material? targetMaterial
    {
        get => _targetMaterial;
        set => _targetMaterial = value;
    }

    public string targetMaterialProperty
    {
        get => _targetMaterialProperty;
        set => _targetMaterialProperty = value ?? string.Empty;
    }

    public float targetCameraAspectRatio
    {
        get => _targetCameraAspectRatio;
        set => _targetCameraAspectRatio = value;
    }

    public ushort controlledAudioTrackCount
    {
        get => _controlledAudioTrackCount;
        set => _controlledAudioTrackCount = value;
    }

    public ushort audioTrackCount { get; private set; } = 1;

    public float audioVolume
    {
        get => _audioVolume;
        set => _audioVolume = Math.Clamp(value, 0f, 1f);
    }

    public bool audioMuted
    {
        get => _audioMuted;
        set => _audioMuted = value;
    }

    public bool canSetTime => true;
    public bool canSetSkipOnDrop => true;
    public bool canSetPlaybackSpeed => true;
    public bool canStep => true;

    public event Action<VideoPlayer, string>? errorReceived;
    public event Action<VideoPlayer, long>? frameReady;
    public event Action<VideoPlayer>? loopPointReached;
    public event Action<VideoPlayer>? prepareCompleted;
    public event Action<VideoPlayer, long>? seekCompleted;
    public event Action<VideoPlayer>? started;
    public event Action<VideoPlayer, double>? clockResync;

    public void Prepare()
    {
        _isPrepared = true;
        _prepareCompletedSent = true;
        _prepareTime = 0f;
        SendPrepareCompletedEvent();
    }

    public void Play()
    {
        if (!_isPrepared)
        {
            Prepare();
        }
        _isPlaying = true;
        _isPaused = false;
        _isStopped = false;
        _time = 0;
        _frame = 0;
        started?.Invoke(this);
        SendFrameReadyEvents();
    }

    public void Pause()
    {
        _isPaused = true;
        _isPlaying = false;
    }

    public void Stop()
    {
        _isPlaying = false;
        _isPaused = false;
        _isStopped = true;
        _time = 0;
        _frame = 0;
    }

    public void StepForward()
    {
        if (_isPrepared)
        {
            _frame++;
            _time = _frame / _frameRate;
            SendFrameReadyEvents();
        }
    }

    public void Rewind()
    {
        _time = 0;
        _frame = 0;
    }

    internal void SendFrameReadyEvents()
    {
        if (_sendFrameReadyEvents)
        {
            frameReady?.Invoke(this, _frame);
        }
    }

    internal void SendPrepareCompletedEvent()
    {
        if (!_prepareCompletedSent)
        {
            _prepareCompletedSent = true;
            prepareCompleted?.Invoke(this);
        }
    }

    internal void UpdatePlayer(float deltaTime)
    {
        if (!_isPlaying || _isPaused) return;

        _time += deltaTime * _playbackSpeed;
        _clockTime = _time;
        _frame = (long)(_time * _frameRate);

        if (_sendFrameReadyEvents)
        {
            frameReady?.Invoke(this, _frame);
        }

        if (_length > 0 && _time >= _length)
        {
            loopPointReached?.Invoke(this);
            if (_isLooping)
            {
                _time = 0;
                _frame = 0;
            }
            else
            {
                _isPlaying = false;
                _isStopped = true;
            }
        }
    }

    public void SetDirectAudioVolume(ushort trackIndex, float volume)
    {
        _ = trackIndex;
        _audioVolume = Math.Clamp(volume, 0f, 1f);
    }

    public float GetDirectAudioVolume(ushort trackIndex)
    {
        _ = trackIndex;
        return _audioVolume;
    }

    public void SetDirectAudioMute(ushort trackIndex, bool mute)
    {
        _ = trackIndex;
        _audioMuted = mute;
    }

    public bool GetDirectAudioMute(ushort trackIndex)
    {
        _ = trackIndex;
        return _audioMuted;
    }

    public ushort GetAudioChannelCount(ushort trackIndex)
    {
        _ = trackIndex;
        return 2;
    }

    public uint GetAudioSampleRate(ushort trackIndex)
    {
        _ = trackIndex;
        return 44100;
    }

    public void EnableAudioTrack(ushort trackIndex, bool enabled)
    {
        _ = trackIndex;
        _ = enabled;
    }

    public bool IsAudioTrackEnabled(ushort trackIndex)
    {
        _ = trackIndex;
        return !_audioMuted;
    }

    public string GetAudioLanguageCode(ushort trackIndex)
    {
        _ = trackIndex;
        return "und";
    }
}

public sealed class VideoClip : Object
{
    private string _originalPath = string.Empty;
    private string _importerInfo = string.Empty;

    public VideoClip()
    {
        frameRate = 30;
        width = 1920;
        height = 1080;
        length = 0;
        frameCount = 0;
        audioTrackCount = 0;
        pixelAspectRatioNumerator = 1;
        pixelAspectRatioDenominator = 1;
    }

    public VideoClip(string clipName, double clipLength, double clipFrameRate, uint clipWidth, uint clipHeight)
    {
        name = clipName;
        length = clipLength;
        frameRate = clipFrameRate;
        width = clipWidth;
        height = clipHeight;
        frameCount = (ulong)(length * frameRate);
        audioTrackCount = 1;
        pixelAspectRatioNumerator = 1;
        pixelAspectRatioDenominator = 1;
    }

    public string originalPath
    {
        get => _originalPath;
        set => _originalPath = value ?? string.Empty;
    }

    public string importerInfo
    {
        get => _importerInfo;
        set => _importerInfo = value ?? string.Empty;
    }

    public ulong frameCount { get; set; }
    public double frameRate { get; set; }
    public double length { get; set; }
    public uint width { get; set; }
    public uint height { get; set; }
    public uint pixelAspectRatioNumerator { get; set; }
    public uint pixelAspectRatioDenominator { get; set; }
    public ushort audioTrackCount { get; set; }
    public bool alphaChannel { get; set; }
    public bool hasAudio => audioTrackCount > 0;
    public double spt => frameRate > 0 ? 1.0 / frameRate : 0;

    public float pixelAspectRatio => pixelAspectRatioDenominator > 0
        ? (float)pixelAspectRatioNumerator / pixelAspectRatioDenominator
        : 1f;
}
