using System.Text.Json;

namespace Anity.Hub;

internal static class Program
{
  private static async Task<int> Main(string[] args)
  {
    if (args.Length > 0 && (args[0] is "--help" or "-h"))
    {
      Console.WriteLine("Anity Hub launcher");
      Console.WriteLine("commands: manifest <path>");
      return 0;
    }

    if (args.Length == 0 || args[0] != "manifest")
    {
      Console.WriteLine("usage: anity-hub manifest <path>");
      return 1;
    }

    var path = args.ElementAtOrDefault(1) ?? "manifest.json";
    if (!File.Exists(path))
    {
      Console.Error.WriteLine($"manifest not found: {path}");
      return 2;
    }

    await using var fs = File.OpenRead(path);
    var model = await JsonSerializer.DeserializeAsync<ManifestModel>(fs, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    if (model is null)
    {
      Console.Error.WriteLine("invalid manifest");
      return 3;
    }

    Console.WriteLine($"name={model.Name}");
    Console.WriteLine($"version={model.Version}");
    if (!string.IsNullOrWhiteSpace(model.EditorBinary))
    {
      var platform = ResolvePlatform();
      Console.WriteLine($"platform={platform}");
      Console.WriteLine($"binary={model.EditorBinary}");
    }
    return 0;
  }

  private static string ResolvePlatform()
  {
    return Environment.OSVersion.Platform switch
    {
      PlatformID.Win32NT => "windows",
      PlatformID.MacOSX => "macos",
      _ => "linux"
    };
  }
}

public sealed class ManifestModel
{
  public required string Name { get; set; }
  public required string Version { get; set; }
  public string EditorBinary { get; set; } = "bin/Anity.Editor";
}
