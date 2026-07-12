using System;
using System.Collections.Generic;

namespace UnityEngine;

public static class Microphone
{
    private static readonly List<string> _devices = new() { "Microphone1" };
    private static readonly Dictionary<string, AudioClip> _recordings = new();
    private static readonly Dictionary<string, int> _positions = new();
    private static readonly Dictionary<string, bool> _isRecording = new();

    public static string[] devices => _devices.ToArray();

    public static AudioClip Start(string deviceName, bool loop, int lengthSec, int frequency)
    {
        if (string.IsNullOrEmpty(deviceName) && _devices.Count > 0)
            deviceName = _devices[0];

        var clip = AudioClip.Create($"Mic_{deviceName}", lengthSec * frequency, 1, frequency, false);
        _recordings[deviceName] = clip;
        _positions[deviceName] = 0;
        _isRecording[deviceName] = true;
        return clip;
    }

    public static void End(string deviceName)
    {
        if (string.IsNullOrEmpty(deviceName) && _devices.Count > 0)
            deviceName = _devices[0];
        _isRecording[deviceName] = false;
        _recordings.Remove(deviceName);
        _positions.Remove(deviceName);
    }

    public static int GetPosition(string deviceName)
    {
        if (string.IsNullOrEmpty(deviceName) && _devices.Count > 0)
            deviceName = _devices[0];
        return _positions.TryGetValue(deviceName, out int pos) ? pos : 0;
    }

    public static bool IsRecording(string deviceName)
    {
        if (string.IsNullOrEmpty(deviceName) && _devices.Count > 0)
            deviceName = _devices[0];
        return _isRecording.TryGetValue(deviceName, out bool rec) && rec;
    }

    public static void GetDeviceCaps(string deviceName, out int minFreq, out int maxFreq)
    {
        _ = deviceName;
        minFreq = 100;
        maxFreq = 48000;
    }

    internal static void RegisterDevice(string deviceName)
    {
        if (!_devices.Contains(deviceName))
            _devices.Add(deviceName);
    }
}
