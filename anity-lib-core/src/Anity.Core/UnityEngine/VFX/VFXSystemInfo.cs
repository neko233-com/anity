using System.Runtime.InteropServices;

namespace UnityEngine.VFX;

public enum VFXSpawnerLoopState
{
    Finished = 0,
    DelayingBeforeLoop = 1,
    Looping = 2,
    DelayingAfterLoop = 3
}

[StructLayout(LayoutKind.Sequential)]
[Bindings.NativeType(Header = "Modules/VFX/Public/VFXSpawnerState.h")]
[Scripting.RequiredByNativeCode]
public sealed class VFXSpawnerState : IDisposable
{
    private readonly VFXEventAttribute _eventAttribute;
    private bool _disposed;
    private VFXSpawnerLoopState _loopState;
    private bool _newLoop;
    private float _spawnCount;
    private float _deltaTime;
    private float _totalTime;
    private float _delayBeforeLoop;
    private float _loopDuration;
    private float _delayAfterLoop;
    private int _loopIndex;
    private int _loopCount;

    public VFXSpawnerState()
        : this((VisualEffectAsset?)null)
    {
    }

    internal VFXSpawnerState(VisualEffectAsset? asset)
    {
        _eventAttribute = new VFXEventAttribute(asset);
    }

    internal VFXSpawnerState(VFXSpawnerState source) : this()
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        CopyFrom(source);
    }

    public bool playing
    {
        get => loopState == VFXSpawnerLoopState.Looping;
        set => loopState = value ? VFXSpawnerLoopState.Looping : VFXSpawnerLoopState.Finished;
    }

    public bool newLoop
    {
        get { ThrowIfDisposed(); return _newLoop; }
    }

    public VFXSpawnerLoopState loopState
    {
        get { ThrowIfDisposed(); return _loopState; }
        set { ThrowIfDisposed(); _loopState = value; }
    }

    public float spawnCount
    {
        get { ThrowIfDisposed(); return _spawnCount; }
        set { ThrowIfDisposed(); _spawnCount = value; }
    }

    public float deltaTime
    {
        get { ThrowIfDisposed(); return _deltaTime; }
        set { ThrowIfDisposed(); _deltaTime = value; }
    }

    public float totalTime
    {
        get { ThrowIfDisposed(); return _totalTime; }
        set { ThrowIfDisposed(); _totalTime = value; }
    }

    public float delayBeforeLoop
    {
        get { ThrowIfDisposed(); return _delayBeforeLoop; }
        set { ThrowIfDisposed(); _delayBeforeLoop = value; }
    }

    public float loopDuration
    {
        get { ThrowIfDisposed(); return _loopDuration; }
        set { ThrowIfDisposed(); _loopDuration = value; }
    }

    public float delayAfterLoop
    {
        get { ThrowIfDisposed(); return _delayAfterLoop; }
        set { ThrowIfDisposed(); _delayAfterLoop = value; }
    }

    public int loopIndex
    {
        get { ThrowIfDisposed(); return _loopIndex; }
        set
        {
            ThrowIfDisposed();
            _newLoop = value != _loopIndex;
            _loopIndex = value;
        }
    }

    public int loopCount
    {
        get { ThrowIfDisposed(); return _loopCount; }
        set { ThrowIfDisposed(); _loopCount = value; }
    }

    public VFXEventAttribute vfxEventAttribute
    {
        get { ThrowIfDisposed(); return _eventAttribute; }
    }

    internal void CopyFrom(VFXSpawnerState source)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        ThrowIfDisposed();
        source.ThrowIfDisposed();
        _loopState = source._loopState;
        _newLoop = source._newLoop;
        _spawnCount = source._spawnCount;
        _deltaTime = source._deltaTime;
        _totalTime = source._totalTime;
        _delayBeforeLoop = source._delayBeforeLoop;
        _loopDuration = source._loopDuration;
        _delayAfterLoop = source._delayAfterLoop;
        _loopIndex = source._loopIndex;
        _loopCount = source._loopCount;
        _eventAttribute.CopyValuesFrom(source._eventAttribute);
    }

    internal void SetNewLoop(bool value)
    {
        ThrowIfDisposed();
        _newLoop = value;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _eventAttribute.Dispose();
        GC.SuppressFinalize(this);
    }

    ~VFXSpawnerState()
    {
        if (!_disposed)
        {
            _disposed = true;
            _eventAttribute.Dispose();
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(VFXSpawnerState));
    }
}

[Bindings.NativeHeader("Modules/VFX/Public/Systems/VFXParticleSystem.h")]
[Scripting.UsedByNativeCode]
public struct VFXParticleSystemInfo
{
    public uint aliveCount;
    public uint capacity;
    public bool sleeping;
    public Bounds bounds;

    public VFXParticleSystemInfo(uint aliveCount, uint capacity, bool sleeping, Bounds bounds)
    {
        this.aliveCount = aliveCount;
        this.capacity = capacity;
        this.sleeping = sleeping;
        this.bounds = bounds;
    }
}
