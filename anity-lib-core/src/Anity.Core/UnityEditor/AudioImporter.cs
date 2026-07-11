using UnityEngine;

namespace UnityEditor;

public class AudioImporter : AssetImporter
{
  public AudioImporterLoadType loadType { get; set; } = AudioImporterLoadType.DecompressOnLoad;
  public AudioCompressionFormat compressionFormat { get; set; } = AudioCompressionFormat.Vorbis;
  public float quality { get; set; } = 1f;
  public bool loadInBackground { get; set; }
  public bool preloadAudioData { get; set; } = true;
  public bool ambisonic { get; set; }
  public AudioSampleRateSetting sampleRateSetting { get; set; } = AudioSampleRateSetting.PreserveSampleRate;
  public uint sampleRateOverride { get; set; } = 44100;
  public bool forceToMono { get; set; }
  public bool normalize { get; set; }
  public AudioClipLoadType loadInBackgroundType { get; set; } = AudioClipLoadType.DecompressOnLoad;
  public AudioCompressionFormat defaultCompressionFormat { get; set; } = AudioCompressionFormat.PCM;

  public void GetOverrideSampleSettings(string platform, out AudioImporterSampleSettings settings)
  {
    settings = new AudioImporterSampleSettings();
  }

  public void SetOverrideSampleSettings(string platform, AudioImporterSampleSettings settings)
  {
    _ = platform;
    _ = settings;
  }

  public void ClearOverrideSampleSettings(string platform)
  {
    _ = platform;
  }

  public bool ContainsSampleSettingsOverride(string platform)
  {
    _ = platform;
    return false;
  }

  public static new AudioImporter GetAtPath(string path)
  {
    return new AudioImporter { assetPath = path };
  }
}

public enum AudioImporterLoadType
{
  DecompressOnLoad,
  CompressedInMemory,
  Streaming
}

public enum AudioClipLoadType
{
  DecompressOnLoad,
  CompressedInMemory,
  Streaming
}

public enum AudioCompressionFormat
{
  PCM,
  Vorbis,
  ADPCM,
  MP3,
  VAG,
  HEVAG,
  XMA,
  AAC,
  GCADPCM,
  ATRAC9
}

public enum AudioSampleRateSetting
{
  PreserveSampleRate,
  OptimizeSampleRate,
  OverrideSampleRate
}

public struct AudioImporterSampleSettings
{
  public AudioCompressionFormat compressionFormat;
  public float quality;
  public AudioClipLoadType loadType;
  public uint sampleRateSetting;
  public uint sampleRateOverride;
}
