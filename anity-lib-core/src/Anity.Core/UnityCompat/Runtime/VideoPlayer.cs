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

public sealed class VideoPlayer : Behaviour
{
    private VideoSource _source = VideoSource.VideoClip;
    private VideoClip? _clip;
    private string _url = string.Empty;
    private bool _isPlaying;
    private bool _isPrepared;
    private bool _isLooping;
    private double _time;
    private long _frame;
    private ulong _frameCount;
    private float _playbackSpeed = 1f;
    private VideoRenderMode _renderMode = VideoRenderMode.APIOnly;
    private VideoAudioOutputMode _audioOutputMode = VideoAudioOutputMode.None;
    private RenderTexture? _targetTexture;
    private Camera? _targetCamera;
    private Material? _targetMaterial;
    private string _targetMaterialProperty = string.Empty;
    private double _length;
    private int _width;
    private int _height;
    private float _audioVolume = 1f;
    private bool _audioMuted;

    public VideoSource source { get => _source; set => _source = value; }
    public VideoClip? clip { get => _clip; set => _clip = value; }
    public string url { get => _url; set => _url = value ?? string.Empty; }
    public bool isPlaying => _isPlaying;
    public bool isPaused => _isPrepared && !_isPlaying;
    public bool isLooping { get => _isLooping; set => _isLooping = value; }
    public bool isPrepared => _isPrepared;
    public double time { get => _time; set => _time = value; }
    public long frame { get => _frame; set => _frame = value; }
    public ulong frameCount => _frameCount;
    public float playbackSpeed { get => _playbackSpeed; set => _playbackSpeed = value; }
    public double length => _length;
    public int width => _width;
    public int height => _height;
    public VideoRenderMode renderMode { get => _renderMode; set => _renderMode = value; }
    public VideoAudioOutputMode audioOutputMode { get => _audioOutputMode; set => _audioOutputMode = value; }
    public RenderTexture? targetTexture { get => _targetTexture; set => _targetTexture = value; }
    public Camera? targetCamera { get => _targetCamera; set => _targetCamera = value; }
    public Material? targetMaterial { get => _targetMaterial; set => _targetMaterial = value; }
    public string targetMaterialProperty { get => _targetMaterialProperty; set => _targetMaterialProperty = value; }
    public float audioVolume { get => _audioVolume; set => _audioVolume = value; }
    public bool audioMuted { get => _audioMuted; set => _audioMuted = value; }

    public event Action<VideoPlayer>? prepareCompleted;
    public event Action<VideoPlayer>? loopPointReached;
    public event Action<VideoPlayer>? started;
    public event Action<VideoPlayer>? errorReceived;

    public void Prepare() { _isPrepared = true; prepareCompleted?.Invoke(this); }
    public void Play() { _isPlaying = true; _isPrepared = true; started?.Invoke(this); }
    public void Pause() => _isPlaying = false;
    public void Stop() { _isPlaying = false; _time = 0; _frame = 0; }
    public void StepForward() => _frame++;

    public void SetDirectAudioVolume(ushort trackIndex, float volume) { _ = trackIndex; _audioVolume = volume; }
    public float GetDirectAudioVolume(ushort trackIndex) { _ = trackIndex; return _audioVolume; }
    public void SetDirectAudioMute(ushort trackIndex, bool mute) { _ = trackIndex; _audioMuted = mute; }
    public bool GetDirectAudioMute(ushort trackIndex) { _ = trackIndex; return _audioMuted; }
}

public sealed class VideoClip : UnityEngine.Object
{
    public string originalPath => string.Empty;
    public ulong frameCount => 0;
    public double frameRate => 30;
    public double length => 0;
    public uint width => 0;
    public uint height => 0;
    public uint pixelAspectRatioNumerator => 1;
    public uint pixelAspectRatioDenominator => 1;
    public double spt => 0;
}
