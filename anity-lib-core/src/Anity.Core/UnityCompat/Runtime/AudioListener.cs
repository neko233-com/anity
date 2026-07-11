using System;

namespace UnityEngine;

public class AudioListener : Behaviour
{
    private static Vector3 _position = Vector3.zero;
    private static Vector3 _forward = Vector3.forward;
    private static Vector3 _up = Vector3.up;

    public static float volume { get; set; } = 1f;
    public static bool pause { get; set; }
    public static AudioVelocityUpdateMode velocityUpdateMode { get; set; } = AudioVelocityUpdateMode.Auto;

    public static Vector3 position
    {
        get => _position;
        set => _position = value;
    }

    public static Vector3 forward
    {
        get => _forward;
        set => _forward = value.normalized;
    }

    public static Vector3 up
    {
        get => _up;
        set => _up = value.normalized;
    }

    public static Matrix4x4 worldToLocalMatrix
    {
        get
        {
            return Matrix4x4.TRS(_position, Quaternion.LookRotation(_forward, _up), Vector3.one).inverse;
        }
    }

    public static Matrix4x4 localToWorldMatrix
    {
        get
        {
            return Matrix4x4.TRS(_position, Quaternion.LookRotation(_forward, _up), Vector3.one);
        }
    }

    public static AudioVelocityUpdateMode audioVelocityUpdateMode
    {
        get => velocityUpdateMode;
        set => velocityUpdateMode = value;
    }

    public static event Action? OnAudioConfigurationChanged;

    public static void GetOutputData(float[] samples, int channel)
    {
        _ = channel;
        if (samples == null) return;
        Array.Fill(samples, 0f);
    }

    public static void GetSpectrumData(float[] samples, int channel, FFTWindow window)
    {
        _ = channel;
        _ = window;
        if (samples == null) return;
        Array.Fill(samples, 0f);
    }

    internal static void TriggerAudioConfigurationChanged()
    {
        OnAudioConfigurationChanged?.Invoke();
    }
}

public enum AudioVelocityUpdateMode
{
    Auto,
    Fixed,
    Last
}
