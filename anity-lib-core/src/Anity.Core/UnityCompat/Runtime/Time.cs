using System;
using System.Diagnostics;

namespace UnityEngine;

public static class Time
{
  private static readonly Stopwatch _stopwatch = Stopwatch.StartNew();
  private static float _timeScale = 1f;
  private static float _deltaTime = 1f / 60f;
  private static float _fixedDeltaTime = 0.02f;
  private static int _captureFramerate;
  private static float _captureDeltaTime;
  private static float _time;
  private static float _unscaledTime;
  private static float _fixedTime;
  private static float _fixedUnscaledTime;
  private static float _timeSinceLevelLoad;
  private static double _timeAsDouble;
  private static double _unscaledTimeAsDouble;
  private static double _fixedTimeAsDouble;
  private static double _fixedUnscaledTimeAsDouble;
  private static double _timeSinceLevelLoadAsDouble;
  private static int _frameCount;
  private static int _renderedFrameCount;
  private static float _smoothDeltaTime = 1f / 60f;
  private static double _lastFrameRealtime;

  static Time()
  {
    _lastFrameRealtime = _stopwatch.Elapsed.TotalSeconds;
  }

  public static int captureFramerate
  {
    get => _captureFramerate;
    set => _captureFramerate = value;
  }

  public static float captureDeltaTime
  {
    get => _captureDeltaTime;
    set => _captureDeltaTime = MathF.Max(0f, value);
  }

  public static float timeScale
  {
    get => _timeScale;
    set => _timeScale = MathF.Max(0f, value);
  }

  public static float deltaTime
  {
    get => _deltaTime;
    set => _deltaTime = MathF.Max(0f, value);
  }

  public static float fixedDeltaTime
  {
    get => _fixedDeltaTime;
    set => _fixedDeltaTime = MathF.Max(1e-6f, value);
  }

  public static float maximumDeltaTime { get; set; } = 0.333f;
  public static float maximumParticleDeltaTime { get; set; } = 0.03f;
  public static float smoothDeltaTime => _smoothDeltaTime;
  public static float unscaledDeltaTime { get; private set; }
  public static float fixedUnscaledDeltaTime => fixedDeltaTime;

  public static float time => _time;
  public static float realtimeSinceStartup => (float)_stopwatch.Elapsed.TotalSeconds;
  public static float unscaledTime => _unscaledTime;
  public static float timeSinceLevelLoad => _timeSinceLevelLoad;
  public static float fixedTime => _fixedTime;
  public static float fixedUnscaledTime => _fixedUnscaledTime;
  public static int frameCount => _frameCount;
  public static int renderedFrameCount => _renderedFrameCount;
  public static bool inFixedTimeStep { get; private set; }

  public static double timeAsDouble => _timeAsDouble;
  public static double realtimeSinceStartupAsDouble => _stopwatch.Elapsed.TotalSeconds;
  public static double unscaledTimeAsDouble => _unscaledTimeAsDouble;
  public static double timeSinceLevelLoadAsDouble => _timeSinceLevelLoadAsDouble;
  public static double fixedTimeAsDouble => _fixedTimeAsDouble;
  public static double fixedUnscaledTimeAsDouble => _fixedUnscaledTimeAsDouble;

  public static void Tick(float? customDeltaTime = null)
  {
    double currentRealtimeDouble = _stopwatch.Elapsed.TotalSeconds;
    float currentRealtime = (float)currentRealtimeDouble;
    double dtFromRealtime = currentRealtimeDouble - _lastFrameRealtime;
    float unscaledDt = customDeltaTime ?? (float)dtFromRealtime;
    unscaledDt = MathF.Min(unscaledDt, maximumDeltaTime);
    double unscaledDtDouble = customDeltaTime.HasValue ? (double)customDeltaTime.Value : dtFromRealtime;
    unscaledDtDouble = Math.Min(unscaledDtDouble, maximumDeltaTime);
    _lastFrameRealtime = currentRealtimeDouble;

    unscaledDeltaTime = unscaledDt;
    _deltaTime = unscaledDt * _timeScale;
    double deltaTimeDouble = unscaledDtDouble * _timeScale;
    _smoothDeltaTime = _smoothDeltaTime * 0.8f + _deltaTime * 0.2f;
    _unscaledTime += unscaledDt;
    _unscaledTimeAsDouble += unscaledDtDouble;
    _time += _deltaTime;
    _timeAsDouble += deltaTimeDouble;
    _timeSinceLevelLoad += _deltaTime;
    _timeSinceLevelLoadAsDouble += deltaTimeDouble;
    _frameCount++;
    _renderedFrameCount++;
  }

  public static void FixedTick()
  {
    inFixedTimeStep = true;
    _fixedTime += _fixedDeltaTime;
    _fixedTimeAsDouble += _fixedDeltaTime;
    _fixedUnscaledTime += _fixedDeltaTime;
    _fixedUnscaledTimeAsDouble += _fixedDeltaTime;
    inFixedTimeStep = false;
  }

  public static void ResetLevelLoadTime()
  {
    _timeSinceLevelLoad = 0f;
    _timeSinceLevelLoadAsDouble = 0.0;
  }
}
