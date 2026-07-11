using System;

namespace UnityEngine;

public interface ILogger
{
  ILogHandler logHandler { get; set; }
  bool logEnabled { get; set; }
  LogType filterLogType { get; set; }
  bool IsLogTypeAllowed(LogType logType);
  void Log(LogType logType, object message);
  void Log(LogType logType, object message, Object context);
  void Log(LogType logType, string tag, object message);
  void Log(LogType logType, string tag, object message, Object context);
  void Log(object message);
  void Log(string tag, object message);
  void Log(LogType logType, object message, Object context, bool logOnMainThread);
  void LogWarning(object message);
  void LogWarning(string tag, object message);
  void LogWarning(object message, Object context);
  void LogError(object message);
  void LogError(string tag, object message);
  void LogError(object message, Object context);
  void LogException(Exception exception);
  void LogException(Exception exception, Object context);
  void LogAssertion(object message);
  void LogAssertion(object message, Object context);
  void LogFormat(LogType logType, string format, params object[] args);
  void LogFormat(string format, params object[] args);
  void LogWarningFormat(string format, params object[] args);
  void LogErrorFormat(string format, params object[] args);
  void LogFormat(LogType logType, Object context, string format, params object[] args);
}

public interface ILogHandler
{
  void LogFormat(LogType logType, Object context, string format, params object[] args);
  void LogException(Exception exception, Object context);
}

public class Logger : ILogger
{
  public ILogHandler logHandler { get; set; }
  public bool logEnabled { get; set; } = true;
  public LogType filterLogType { get; set; } = LogType.Log;

  public Logger(ILogHandler logHandler)
  {
    this.logHandler = logHandler;
  }

  public bool IsLogTypeAllowed(LogType logType)
  {
    if (!logEnabled) return false;
    if (logType == LogType.Exception || logType == LogType.Error || logType == LogType.Assert) return true;
    return (int)logType <= (int)filterLogType;
  }

  public void Log(LogType logType, object message) => Log(logType, message, null);
  public void Log(LogType logType, object message, Object context)
  {
    if (!IsLogTypeAllowed(logType)) return;
    logHandler.LogFormat(logType, context, message?.ToString() ?? "null");
  }

  public void Log(LogType logType, string tag, object message) => Log(logType, tag, message, null);
  public void Log(LogType logType, string tag, object message, Object context)
  {
    if (!IsLogTypeAllowed(logType)) return;
    logHandler.LogFormat(logType, context, $"[{tag}] {message}");
  }

  public void Log(object message) => Log(LogType.Log, message, null);
  public void Log(string tag, object message) => Log(LogType.Log, tag, message, null);
  public void Log(LogType logType, object message, Object context, bool logOnMainThread)
  {
    _ = logOnMainThread;
    Log(logType, message, context);
  }

  public void LogWarning(object message) => Log(LogType.Warning, message, null);
  public void LogWarning(string tag, object message) => Log(LogType.Warning, tag, message, null);
  public void LogWarning(object message, Object context) => Log(LogType.Warning, message, context);

  public void LogError(object message) => Log(LogType.Error, message, null);
  public void LogError(string tag, object message) => Log(LogType.Error, tag, message, null);
  public void LogError(object message, Object context) => Log(LogType.Error, message, context);

  public void LogException(Exception exception) => LogException(exception, null);
  public void LogException(Exception exception, Object context)
  {
    logHandler.LogException(exception, context);
  }

  public void LogAssertion(object message) => Log(LogType.Assert, message, null);
  public void LogAssertion(object message, Object context) => Log(LogType.Assert, message, context);

  public void LogFormat(LogType logType, string format, params object[] args) => LogFormat(logType, null, format, args);
  public void LogFormat(string format, params object[] args) => LogFormat(LogType.Log, null, format, args);
  public void LogWarningFormat(string format, params object[] args) => LogFormat(LogType.Warning, null, format, args);
  public void LogErrorFormat(string format, params object[] args) => LogFormat(LogType.Error, null, format, args);
  public void LogFormat(LogType logType, Object context, string format, params object[] args)
  {
    if (!IsLogTypeAllowed(logType)) return;
    logHandler.LogFormat(logType, context, format, args);
  }
}

internal class ConsoleLogHandler : ILogHandler
{
  public void LogFormat(LogType logType, Object context, string format, params object[] args)
  {
    string message = args.Length > 0 ? string.Format(format, args) : format;
    string prefix = logType switch
    {
      LogType.Error => "[ERROR]",
      LogType.Assert => "[ASSERT]",
      LogType.Warning => "[WARN]",
      LogType.Exception => "[EXCEPTION]",
      LogType.Fatal => "[FATAL]",
      _ => "[LOG]"
    };
    string contextStr = context is not null ? $" ({context.name})" : "";
    string output = $"{prefix}{contextStr} {message}";
    if (logType == LogType.Error || logType == LogType.Exception || logType == LogType.Assert || logType == LogType.Fatal)
    {
      Console.Error.WriteLine(output);
    }
    else
    {
      Console.WriteLine(output);
    }
  }

  public void LogException(Exception exception, Object context)
  {
    string contextStr = context is not null ? $" ({context.name})" : "";
    Console.Error.WriteLine($"[EXCEPTION]{contextStr} {exception}");
  }
}

public static class Debug
{
  private static readonly ILogger _unityLogger = new Logger(new ConsoleLogHandler());

  public static ILogger unityLogger => _unityLogger;
  public static ILogger logger => _unityLogger;
  public static bool isDebugBuild => true;
  public static bool developerConsoleEnabled => true;
  public static bool isDebugBuildEnabled => true;

  public static void Log(object? message) => unityLogger.Log(LogType.Log, message);
  public static void Log(object? message, Object? context) => unityLogger.Log(LogType.Log, message, context);
  public static void Log(string tag, object? message) => unityLogger.Log(tag, message);

  public static void LogWarning(object? message) => unityLogger.LogWarning(message);
  public static void LogWarning(object? message, Object? context) => unityLogger.LogWarning(message, context);
  public static void LogWarning(string tag, object? message) => unityLogger.LogWarning(tag, message);

  public static void LogError(object? message) => unityLogger.LogError(message);
  public static void LogError(object? message, Object? context) => unityLogger.LogError(message, context);
  public static void LogError(string tag, object? message) => unityLogger.LogError(tag, message);

  public static void LogException(Exception e) => unityLogger.LogException(e);
  public static void LogException(Exception e, Object? context) => unityLogger.LogException(e, context);

  public static void LogAssert(object? message) => unityLogger.LogAssertion(message);
  public static void LogAssert(object? message, Object? context) => unityLogger.LogAssertion(message, context);

  public static void Assert(bool condition)
  {
    if (!condition) unityLogger.Log(LogType.Assert, "Assertion failed");
  }
  public static void Assert(bool condition, object? message)
  {
    if (!condition) unityLogger.Log(LogType.Assert, message);
  }
  public static void Assert(bool condition, object? message, Object? context)
  {
    if (!condition) unityLogger.Log(LogType.Assert, message, context);
  }

  public static void LogFormat(string format, params object[] args) => unityLogger.LogFormat(format, args);
  public static void LogFormat(LogType logType, string format, params object[] args) => unityLogger.LogFormat(logType, format, args);
  public static void LogWarningFormat(string format, params object[] args) => unityLogger.LogWarningFormat(format, args);
  public static void LogErrorFormat(string format, params object[] args) => unityLogger.LogErrorFormat(format, args);
  public static void LogFormat(LogType logType, Object? context, string format, params object[] args) => unityLogger.LogFormat(logType, context, format, args);

  public static void DrawLine(Vector3 start, Vector3 end) {}
  public static void DrawLine(Vector3 start, Vector3 end, Color color) {}
  public static void DrawLine(Vector3 start, Vector3 end, Color color, float duration) {}
  public static void DrawLine(Vector3 start, Vector3 end, Color color, float duration, bool depthTest) {}
  public static void DrawRay(Vector3 start, Vector3 dir) {}
  public static void DrawRay(Vector3 start, Vector3 dir, Color color) {}
  public static void DrawRay(Vector3 start, Vector3 dir, Color color, float duration) {}
  public static void DrawRay(Vector3 start, Vector3 dir, Color color, float duration, bool depthTest) {}

  public static void Break() {}
  public static void DebugBreak() => Break();
  public static void LogAssertionFormat(string format, params object[] args) => unityLogger.LogFormat(LogType.Assert, format, args);
}
