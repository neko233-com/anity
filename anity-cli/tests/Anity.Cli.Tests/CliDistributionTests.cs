using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Xunit;

namespace Anity.Cli.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class CliDistributionCollection
{
    public const string Name = "self-contained CLI distribution";
}

[Collection(CliDistributionCollection.Name)]
public sealed class CliDistributionTests
{
    private static readonly string DistributionDirectory = RequireDistributionDirectory();
    private static readonly string Executable = Path.Combine(
        DistributionDirectory, OperatingSystem.IsWindows() ? "anity.exe" : "anity");

    [Fact]
    public void DirectExecutableRunsWithoutRegisteredDotNetRuntime()
    {
        using TempDirectory directory = new();
        ProcessResult result = Run(directory.Path, new[] { "-version" }, removeToolPath: true);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Anity CLI 2022.3.61f1", result.StandardOutput);
        Assert.DoesNotContain("install or update .NET", result.StandardError, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BatchmodeQuitUsesUnityCompatibleProcessContract()
    {
        using TempDirectory directory = new();
        ProcessResult result = Run(directory.Path,
            new[] { "-batchmode", "-quit", "-nographics", "-logFile", "-" });

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("logFile=-", result.StandardOutput);
        Assert.Contains("batchmode=1", result.StandardOutput);
        Assert.Contains("nographics=1", result.StandardOutput);
        Assert.Contains("quit=1", result.StandardOutput);
    }

    [Fact]
    public void StandardOutputLogSentinelNeverCreatesDashFile()
    {
        using TempDirectory directory = new();
        ProcessResult result = Run(directory.Path,
            new[] { "-batchmode", "-quit", "-logFile", "-" });

        Assert.Equal(0, result.ExitCode);
        Assert.False(File.Exists(Path.Combine(directory.Path, "-")));
    }

    [Fact]
    public void RealLogFileIsFlushedBeforeProcessExit()
    {
        using TempDirectory directory = new();
        string logPath = Path.Combine(directory.Path, "Logs", "Editor.log");
        ProcessResult result = Run(directory.Path,
            new[] { "-batchmode", "-quit", "-nographics", "-logFile", logPath });

        Assert.Equal(0, result.ExitCode);
        Assert.True(File.Exists(logPath));
        string log = File.ReadAllText(logPath);
        Assert.Contains("batchmode=1", log);
        Assert.Contains("quit=1", log);
    }

    [Fact]
    public void HelpRunsFromPublishedExecutable()
    {
        using TempDirectory directory = new();
        ProcessResult result = Run(directory.Path, new[] { "-help" });

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Anity CLI (Unity 2022.3 Pro compatible)", result.StandardOutput);
        Assert.Contains("-buildWindows64Player", result.StandardOutput);
    }

    [Fact]
    public void MissingProjectReturnsUnityStyleFailureExit()
    {
        using TempDirectory directory = new();
        string missing = Path.Combine(directory.Path, "missing-project");
        ProcessResult result = Run(directory.Path,
            new[] { "-batchmode", "-quit", "-projectPath", missing, "-logFile", "-" });

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("ERROR: projectPath not found:", result.StandardOutput);
    }

    [Fact]
    public void UnknownBuildTargetReturnsFailureExit()
    {
        using TempDirectory directory = new();
        ProcessResult result = Run(directory.Path,
            new[] { "-batchmode", "-quit", "-buildTarget", "DefinitelyNotUnity", "-logFile", "-" });

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("ERROR: Unknown buildTarget: DefinitelyNotUnity", result.StandardOutput);
    }

    [Fact]
    public void RunTestsWritesResultsFromPublishedProcess()
    {
        using TempDirectory directory = new();
        string resultsPath = Path.Combine(directory.Path, "TestResults.xml");
        ProcessResult result = Run(directory.Path, new[]
        {
            "-batchmode", "-quit", "-runTests", "-testFilter", "Vector3.Normalize",
            "-testResults", resultsPath, "-logFile", "-"
        });

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("failed=0", result.StandardOutput);
        Assert.Contains("name=\"Vector3.Normalize\" result=\"Passed\"", File.ReadAllText(resultsPath));
    }

    [Fact]
    public void ExecutableArchitectureMatchesCurrentHost()
    {
        Assert.Equal(RuntimeInformation.ProcessArchitecture, ReadBinaryArchitecture(Executable));
        if (!OperatingSystem.IsWindows())
            Assert.True((File.GetUnixFileMode(Executable) & UnixFileMode.UserExecute) != 0);
    }

    [Fact]
    public void NativeRuntimeIsPresentAndMatchesCurrentHost()
    {
        string nativeName = OperatingSystem.IsWindows()
            ? "anity_native.dll"
            : OperatingSystem.IsMacOS() ? "libanity_native.dylib" : "libanity_native.so";
        string nativePath = Path.Combine(DistributionDirectory, nativeName);

        Assert.True(File.Exists(nativePath));
        Assert.True(new FileInfo(nativePath).Length > 0);
        Assert.Equal(RuntimeInformation.ProcessArchitecture, ReadBinaryArchitecture(nativePath));
    }

    [Fact]
    public void RuntimeConfigDeclaresIncludedFramework()
    {
        string runtimeConfig = Path.Combine(DistributionDirectory, "anity.runtimeconfig.json");
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(runtimeConfig));
        JsonElement options = document.RootElement.GetProperty("runtimeOptions");

        Assert.False(options.TryGetProperty("framework", out _));
        JsonElement included = options.GetProperty("includedFrameworks");
        Assert.Contains(included.EnumerateArray(), entry =>
            entry.GetProperty("name").GetString() == "Microsoft.NETCore.App");
    }

    [Fact]
    public void AppLocalHostRuntimeIsBundled()
    {
        string hostFxr = OperatingSystem.IsWindows()
            ? "hostfxr.dll"
            : OperatingSystem.IsMacOS() ? "libhostfxr.dylib" : "libhostfxr.so";
        Assert.True(File.Exists(Path.Combine(DistributionDirectory, hostFxr)));
    }

    [Fact]
    public void DistributionDoesNotContainDebugSymbols()
    {
        Assert.Empty(Directory.EnumerateFiles(DistributionDirectory, "*.pdb", SearchOption.TopDirectoryOnly));
    }

    private static ProcessResult Run(string workingDirectory, IReadOnlyList<string> arguments,
        bool removeToolPath = false)
    {
        ProcessStartInfo start = new(Executable)
        {
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (string argument in arguments) start.ArgumentList.Add(argument);
        start.Environment.Remove("DOTNET_ROOT");
        start.Environment.Remove("DOTNET_ROOT_X64");
        start.Environment.Remove("DOTNET_ROOT_ARM64");
        start.Environment["DOTNET_ROOT"] = Path.Combine(workingDirectory, "missing-dotnet-root");
        start.Environment["DOTNET_MULTILEVEL_LOOKUP"] = "0";
        if (removeToolPath) start.Environment["PATH"] = Path.Combine(workingDirectory, "missing-path");

        using Process process = Process.Start(start) ?? throw new InvalidOperationException("Failed to start Anity CLI.");
        Task<string> standardOutput = process.StandardOutput.ReadToEndAsync();
        Task<string> standardError = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(30_000))
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit();
            throw new TimeoutException("Published Anity CLI did not exit within 30 seconds.");
        }
        return new ProcessResult(process.ExitCode,
            standardOutput.GetAwaiter().GetResult(), standardError.GetAwaiter().GetResult());
    }

    private static Architecture ReadBinaryArchitecture(string path)
    {
        using FileStream stream = File.OpenRead(path);
        using BinaryReader reader = new(stream);
        uint magic = reader.ReadUInt32();
        if (magic == 0xfeedfacf)
        {
            uint cpuType = reader.ReadUInt32();
            return cpuType switch
            {
                0x0100000c => Architecture.Arm64,
                0x01000007 => Architecture.X64,
                _ => throw new InvalidDataException($"Unsupported Mach-O CPU type 0x{cpuType:x8}.")
            };
        }

        if ((magic & 0xffff) == 0x5a4d)
        {
            stream.Position = 0x3c;
            int peOffset = reader.ReadInt32();
            stream.Position = peOffset;
            Assert.Equal(0x00004550u, reader.ReadUInt32());
            return reader.ReadUInt16() switch
            {
                0xaa64 => Architecture.Arm64,
                0x8664 => Architecture.X64,
                _ => throw new InvalidDataException("Unsupported PE machine type.")
            };
        }

        byte[] magicBytes = BitConverter.GetBytes(magic);
        if (magicBytes is [0x7f, (byte)'E', (byte)'L', (byte)'F'])
        {
            stream.Position = 18;
            return reader.ReadUInt16() switch
            {
                0xb7 => Architecture.Arm64,
                0x3e => Architecture.X64,
                _ => throw new InvalidDataException("Unsupported ELF machine type.")
            };
        }

        throw new InvalidDataException("Unknown CLI binary format.");
    }

    private static string RequireDistributionDirectory()
    {
        string? directory = Environment.GetEnvironmentVariable("ANITY_CLI_DISTRIBUTION_DIR");
        Assert.False(string.IsNullOrWhiteSpace(directory),
            "ANITY_CLI_DISTRIBUTION_DIR is required; run tests through _scripts/run-tests.*.");
        Assert.True(Directory.Exists(directory), $"CLI distribution is missing: {directory}");
        return Path.GetFullPath(directory);
    }

    private sealed class TempDirectory : IDisposable
    {
        internal TempDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                "anity-cli-distribution-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        internal string Path { get; }

        public void Dispose()
        {
            try { Directory.Delete(Path, recursive: true); }
            catch { }
        }
    }

    private readonly record struct ProcessResult(int ExitCode, string StandardOutput, string StandardError);
}
