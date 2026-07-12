using System;

namespace UnityEngine;

[AddComponentMenu("Audio/Audio Low Pass Filter")]
public class AudioLowPassFilter : Behaviour
{
    public float cutoffFrequency { get; set; } = 5000f;
    public float lowpassResonanceQ { get; set; } = 1f;
}

[AddComponentMenu("Audio/Audio High Pass Filter")]
public class AudioHighPassFilter : Behaviour
{
    public float cutoffFrequency { get; set; } = 5000f;
    public float highpassResonanceQ { get; set; } = 1f;
}

[AddComponentMenu("Audio/Audio Reverb Filter")]
public class AudioReverbFilter : Behaviour
{
    public AudioReverbPreset reverbPreset { get; set; } = AudioReverbPreset.Generic;
    public float dryLevel { get; set; } = 0f;
    public float room { get; set; } = -1000f;
    public float roomHF { get; set; } = -100f;
    public float roomLF { get; set; } = 0f;
    public float decayTime { get; set; } = 1.49f;
    public float decayHFRatio { get; set; } = 0.83f;
    public float reflectionsLevel { get; set; } = -2602f;
    public float reflectionsDelay { get; set; } = 0.007f;
    public float reverbLevel { get; set; } = 200f;
    public float reverbDelay { get; set; } = 0.011f;
    public float hfReference { get; set; } = 5000f;
    public float lfReference { get; set; } = 250f;
    public float diffusion { get; set; } = 100f;
    public float density { get; set; } = 100f;
}

[AddComponentMenu("Audio/Audio Echo Filter")]
public class AudioEchoFilter : Behaviour
{
    public float delay { get; set; } = 500f;
    public float decayRatio { get; set; } = 0.5f;
    public float wetMix { get; set; } = 1f;
    public float dryMix { get; set; } = 1f;
}

[AddComponentMenu("Audio/Audio Distortion Filter")]
public class AudioDistortionFilter : Behaviour
{
    public float distortionLevel { get; set; } = 0.5f;
}

[AddComponentMenu("Audio/Audio Chorus Filter")]
public class AudioChorusFilter : Behaviour
{
    public float dryMix { get; set; } = 0.5f;
    public float wetMix1 { get; set; } = 0.5f;
    public float wetMix2 { get; set; } = 0.5f;
    public float wetMix3 { get; set; } = 0.5f;
    public float delay { get; set; } = 40f;
    public float rate { get; set; } = 0.8f;
    public float depth { get; set; } = 0.03f;
}

public enum AudioReverbPreset
{
    Off,
    Generic,
    PaddedCell,
    Room,
    Bathroom,
    Livingroom,
    Stoneroom,
    Auditorium,
    Concerthall,
    Cave,
    Arena,
    Hangar,
    CarpetedHallway,
    Hallway,
    StoneCorridor,
    Alley,
    Forest,
    City,
    Mountains,
    Quarry,
    Plain,
    ParkingLot,
    SewerPipe,
    Underwater,
    Drugged,
    Dizzy,
    Psychotic,
    User
}
