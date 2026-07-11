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
  private static float _time;
  private static float _unscaledTime;
  private static float _fixedTime;
  private static float _timeSinceLevelLoad;
  private static int _frameCount;
  private static int _renderedFrameCount;
  private static float _smoothDeltaTime = 1f / 60f;
  private static float _lastFrameRealtime;

  static Time()
  {
    _lastFrameRealtime = (float)_stopwatch.Elapsed.TotalSeconds;
  }

  public static int captureFramerate
  {
    get => _captureFramerate;
    set => _captureFramerate = value;
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
  public static float smoothDeltaTime => _smoothDeltaTime;
  public static float unscaledDeltaTime { get; private set; }
  public static float fixedUnscaledDeltaTime => fixedDeltaTime;

  public static float time => _time;
  public static float realtimeSinceStartup => (float)_stopwatch.Elapsed.TotalSeconds;
  public static float unscaledTime => _unscaledTime;
  public static float timeSinceLevelLoad => _timeSinceLevelLoad;
  public static float fixedTime => _fixedTime;
  public static float fixedUnscaledTime => _fixedTime;
  public static int frameCount => _frameCount;
  public static int renderedFrameCount => _renderedFrameCount;
  public static bool inFixedTimeStep { get; private set; }

  public static void Tick(float? customDeltaTime = null)
  {
    float currentRealtime = (float)_stopwatch.Elapsed.TotalSeconds;
    float unscaledDt = customDeltaTime ?? (currentRealtime - _lastFrameRealtime);
    unscaledDt = MathF.Min(unscaledDt, maximumDeltaTime);
    _lastFrameRealtime = currentRealtime;

    unscaledDeltaTime = unscaledDt;
    _deltaTime = unscaledDt * _timeScale;
    _smoothDeltaTime = _smoothDeltaTime * 0.8f + _deltaTime * 0.2f;
    _unscaledTime += unscaledDt;
    _time += _deltaTime;
    _timeSinceLevelLoad += _deltaTime;
    _frameCount++;
    _renderedFrameCount++;

    Object.TickDestroyQueue();
  }

  public static void FixedTick()
  {
    inFixedTimeStep = true;
    _fixedTime += _fixedDeltaTime;
    inFixedTimeStep = false;
  }

  public static void ResetLevelLoadTime()
  {
    _timeSinceLevelLoad = 0f;
  }
}
