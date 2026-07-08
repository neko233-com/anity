namespace UnityEngine;

public class AudioClip : Object
{
  public string name { get; set; } = string.Empty;
  public float length { get; set; }
  public int samples { get; set; }
  public int channels { get; set; }
  public int frequency { get; set; }
  public bool ambisonic { get; set; }
  public bool preloadAudioData { get; set; }
  public bool loadInBackground { get; set; }

  public bool GetData(float[] data, int offsetSamples)
  {
    _ = data;
    _ = offsetSamples;
    return false;
  }

  public bool SetData(float[] data, int offsetSamples)
  {
    _ = data;
    _ = offsetSamples;
    return false;
  }

  public static AudioClip Create(string name, int lengthSamples, int channels, int frequency, bool stream)
  {
    _ = name;
    _ = lengthSamples;
    _ = channels;
    _ = frequency;
    _ = stream;
    return null;
  }
}
