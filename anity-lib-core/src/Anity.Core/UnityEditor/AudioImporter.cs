using System.Collections.Generic;
using UnityEngine;

namespace UnityEditor;

public class AudioImporter : AssetImporter
{
  private readonly Dictionary<string, AudioImporterSampleSettings> _overrideSettings = new();
  private AudioImporterSampleSettings _defaultSampleSettings;

  public AudioImporter()
  {
    _defaultSampleSettings = new AudioImporterSampleSettings
    {
      compressionFormat = AudioCompressionFormat.Vorbis,
      quality = 1f,
      loadType = AudioClipLoadType.DecompressOnLoad,
      sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate,
      sampleRateOverride = 44100
    };
  }

  public bool loadInBackground { get; set; }
  public bool preloadAudioData { get; set; } = true;
  public bool ambisonic { get; set; }
  public bool forceToMono { get; set; }
  public bool normalize { get; set; }

  public AudioImporterSampleSettings defaultSampleSettings
  {
    get => _defaultSampleSettings;
    set => _defaultSampleSettings = value;
  }

  public AudioClipLoadType loadType
  {
    get => _defaultSampleSettings.loadType;
    set => _defaultSampleSettings.loadType = value;
  }

  public AudioCompressionFormat compressionFormat
  {
    get => _defaultSampleSettings.compressionFormat;
    set => _defaultSampleSettings.compressionFormat = value;
  }

  public float quality
  {
    get => _defaultSampleSettings.quality;
    set => _defaultSampleSettings.quality = value;
  }

  public AudioSampleRateSetting sampleRateSetting
  {
    get => _defaultSampleSettings.sampleRateSetting;
    set => _defaultSampleSettings.sampleRateSetting = value;
  }

  public uint sampleRateOverride
  {
    get => _defaultSampleSettings.sampleRateOverride;
    set => _defaultSampleSettings.sampleRateOverride = value;
  }

  public AudioClipLoadType loadInBackgroundType
  {
    get => _defaultSampleSettings.loadType;
    set => _defaultSampleSettings.loadType = value;
  }

  public AudioCompressionFormat defaultCompressionFormat
  {
    get => _defaultSampleSettings.compressionFormat;
    set => _defaultSampleSettings.compressionFormat = value;
  }

  public bool ContainsSampleSettingsOverride(string platform)
  {
    return _overrideSettings.ContainsKey(platform);
  }

  public AudioImporterSampleSettings GetOverrideSampleSettings(string platform)
  {
    if (_overrideSettings.TryGetValue(platform, out var settings))
      return settings;
    return _defaultSampleSettings;
  }

  public void SetOverrideSampleSettings(string platform, AudioImporterSampleSettings settings)
  {
    if (string.IsNullOrEmpty(platform)) return;
    _overrideSettings[platform] = settings;
  }

  public void ClearOverrideSampleSettings(string platform)
  {
    _overrideSettings.Remove(platform);
  }

  public static new AudioImporter GetAtPath(string path)
  {
    return new AudioImporter { assetPath = path };
  }
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

public enum AudioClipLoadType
{
  DecompressOnLoad,
  CompressedInMemory,
  Streaming
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
  public AudioSampleRateSetting sampleRateSetting;
  public uint sampleRateOverride;
}
