namespace UnityEngine;

public enum StackTraceLogType
{
  None = 0,
  ScriptOnly = 1,
  Full = 2
}

public enum LogType
{
  Error,
  Assert,
  Warning,
  Log,
  Exception,
  Fatal
}

public enum LogOption
{
  None,
  NoStacktrace
}

