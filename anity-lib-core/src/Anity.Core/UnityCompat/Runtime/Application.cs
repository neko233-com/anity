using System.Runtime.InteropServices;
using System;
using System.IO;

namespace UnityEngine;

public static class Application
{
  public static string dataPath => AppContext.BaseDirectory;
  public static string temporaryCachePath => Path.GetTempPath();
  public static RuntimePlatform platform => RuntimePlatformFromOS();
  public static bool isPlaying => true;
  public static bool isEditor => false;

  private static RuntimePlatform RuntimePlatformFromOS()
  {
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return RuntimePlatform.WindowsPlayer;
    if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return RuntimePlatform.OSXPlayer;
    return RuntimePlatform.LinuxPlayer;
  }
}

public enum RuntimePlatform
{
  WindowsPlayer,
  OSXPlayer,
  LinuxPlayer
}
