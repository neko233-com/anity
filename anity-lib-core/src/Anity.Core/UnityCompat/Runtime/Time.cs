using System;

namespace UnityEngine;

public static class Time
{
  private static readonly DateTime StartTime = DateTime.UtcNow;
  private static float _timeScale = 1f;
  private static float _deltaTime = 1f / 60f;
  private static float _fixedDeltaTime = 0.02f;
  private static int _captureFramerate;

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
  public static float smoothDeltaTime => deltaTime;
  public static float unscaledDeltaTime => deltaTime;
  public static float fixedUnscaledDeltaTime => fixedDeltaTime;

  public static float time => unscaledTime * timeScale;
  public static float realtimeSinceStartup => (float)(DateTime.UtcNow - StartTime).TotalSeconds;
  public static float unscaledTime => realtimeSinceStartup;
  public static float timeSinceLevelLoad => time;
  public static float fixedTime => fixedDeltaTime > 0f ? unscaledTime / fixedDeltaTime * fixedDeltaTime : 0f;
  public static float fixedUnscaledTime => fixedTime;
  public static int frameCount => Math.Max(0, (int)(unscaledTime / Math.Max(_deltaTime, 1e-6f)));
  public static bool inFixedTimeStep => fixedTime >= 0f && frameCount % Math.Max(1, (int)MathF.Round(1f / MathF.Max(fixedDeltaTime, 1e-6f))) == 0;
  public static int renderedFrameCount => frameCount;
}

