namespace UnityEngine;

/// <summary>
/// Audio listener component.
/// </summary>
public class AudioListener : Behaviour
{
    public static float volume { get; set; } = 1f;
    public static bool pause { get; set; }
    public static AudioVelocityUpdateMode velocityUpdateMode { get; set; } = AudioVelocityUpdateMode.Dynamic;
}

public enum AudioVelocityUpdateMode
{
    Auto,
    Fixed,
    Dynamic
}
