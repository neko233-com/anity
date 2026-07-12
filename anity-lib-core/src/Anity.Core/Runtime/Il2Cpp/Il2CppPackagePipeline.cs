using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Anity.Core.Runtime.Il2Cpp;

/// <summary>
/// End-to-end IL2CPP packaging path used by CLI, BuildPipeline, and tests.
/// convert → toolchain artifacts → optional native link → player launch readiness.
/// </summary>
public static class Il2CppPackagePipeline
{
    public static string lastError { get; private set; } = string.Empty;
    public static string lastOutputDirectory { get; private set; } = string.Empty;
    public static string lastReport { get; private set; } = string.Empty;
    public static bool lastLaunchOk { get; private set; }
    public static bool lastNativeLinked { get; private set; }

    public sealed class PackageResult
    {
        public bool success;
        public string outputDirectory = string.Empty;
        public bool hasLinkXml;
        public bool hasMetadata;
        public bool hasConfigHeader;
        public bool hasCMake;
        public bool hasPlayerMain;
        public bool hasManagedMarker;
        public bool nativeLinked;
        public bool launchOk;
        public int exitCode = -1;
        public List<string> artifacts = new();
        public string log = string.Empty;
        public string error = string.Empty;
    }

    /// <summary>
    /// Full packaging: set IL2CPP backend → convert domain → emit toolchain → build player → launch.
    /// </summary>
    public static PackageResult Package(
        string outputDirectory,
        Il2CppToolchain.TargetAbi abi = Il2CppToolchain.TargetAbi.WinX64,
        bool tryNativeLink = true,
        bool launch = true)
    {
        lastError = string.Empty;
        lastLaunchOk = false;
        lastNativeLinked = false;
        var result = new PackageResult { outputDirectory = outputDirectory ?? string.Empty };

        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            result.error = lastError = "outputDirectory required";
            lastReport = result.error;
            return result;
        }

        try
        {
            Directory.CreateDirectory(outputDirectory);
            lastOutputDirectory = outputDirectory;

            // Align with Unity PlayerSettings scripting backend
            try
            {
                PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone, ScriptingImplementation.IL2CPP);
            }
            catch
            {
                // PlayerSettings may be partial in test hosts
            }

            Il2CppRuntime.EnterIl2CppPlayerMode();
            Il2CppRuntime.ForcePlatform(Platform.IL2CPP);

            bool playerOk = Il2CppPlayerHost.BuildPlayer(outputDirectory, abi, tryNativeLink);
            if (!playerOk)
            {
                result.error = lastError = string.IsNullOrEmpty(Il2CppBuilder.lastError)
                    ? "Il2CppPlayerHost.BuildPlayer failed"
                    : Il2CppBuilder.lastError;
                lastReport = result.error;
                return result;
            }

            // Detect native link
            string nativeExe = Il2CppToolchain.GetPlayerExecutablePath(outputDirectory);
            result.nativeLinked = File.Exists(nativeExe);
            lastNativeLinked = result.nativeLinked;

            CollectArtifacts(outputDirectory, result);
            result.hasLinkXml = File.Exists(Path.Combine(outputDirectory, "link.xml"));
            result.hasMetadata = File.Exists(Path.Combine(outputDirectory, "Il2CppMetadata.map"));
            result.hasConfigHeader = File.Exists(Path.Combine(outputDirectory, "il2cpp-config.h"));
            result.hasCMake = File.Exists(Path.Combine(outputDirectory, "CMakeLists.txt"));
            result.hasPlayerMain = File.Exists(Path.Combine(outputDirectory, "PlayerMain.cpp"));
            result.hasManagedMarker = File.Exists(Path.Combine(outputDirectory, "player.managed"));

            if (!result.hasLinkXml || !result.hasMetadata || !result.hasConfigHeader || !result.hasPlayerMain)
            {
                result.error = lastError = "IL2CPP packaging incomplete: missing required artifacts";
                lastReport = Summarize(result);
                return result;
            }

            if (!Il2CppPlayerHost.IsPlayerReady(outputDirectory))
            {
                result.error = lastError = "player not ready after package";
                lastReport = Summarize(result);
                return result;
            }

            if (launch)
            {
                result.launchOk = Il2CppPlayerHost.Launch(outputDirectory);
                result.exitCode = Il2CppPlayerHost.lastExitCode;
                lastLaunchOk = result.launchOk;
                if (!result.launchOk)
                {
                    result.error = lastError = "player launch failed: " + Il2CppPlayerHost.lastLaunchLog;
                    lastReport = Summarize(result);
                    return result;
                }
                if (!Il2CppRuntime.IsIl2Cpp)
                {
                    result.error = lastError = "IL2CPP player mode not active after launch";
                    lastReport = Summarize(result);
                    return result;
                }
            }

            result.success = true;
            result.log = BuildLog(result);
            lastReport = Summarize(result);
            File.WriteAllText(Path.Combine(outputDirectory, "package.report.txt"), result.log);
            return result;
        }
        catch (Exception ex)
        {
            result.error = lastError = ex.Message;
            result.log = ex.ToString();
            lastReport = result.error;
            return result;
        }
    }

    /// <summary>Package into a BuildPlayer location (CLI -build*Player with IL2CPP backend).</summary>
    public static PackageResult PackageForPlayer(string playerLocationPath, bool tryNativeLink = true, bool launch = false)
    {
        if (string.IsNullOrWhiteSpace(playerLocationPath))
        {
            lastError = "playerLocationPath required";
            return new PackageResult { error = lastError };
        }

        string dir = Path.GetDirectoryName(Path.GetFullPath(playerLocationPath)) ?? ".";
        string il2cppDir = Path.Combine(dir, "Il2CppOutputProject");
        var r = Package(il2cppDir, tryNativeLink: tryNativeLink, launch: launch);
        if (r.success)
        {
            // Unity-like: place a marker next to player path
            try
            {
                Directory.CreateDirectory(dir);
                File.WriteAllText(playerLocationPath + ".il2cpp.json",
                    $"{{\"il2cppOutput\":\"{il2cppDir.Replace("\\", "/")}\",\"nativeLinked\":{(r.nativeLinked ? "true" : "false")}}}");
            }
            catch { }
        }
        return r;
    }

    public static bool ValidatePackageLayout(string outputDirectory)
    {
        if (string.IsNullOrEmpty(outputDirectory) || !Directory.Exists(outputDirectory))
            return false;
        return File.Exists(Path.Combine(outputDirectory, "link.xml"))
               && File.Exists(Path.Combine(outputDirectory, "Il2CppMetadata.map"))
               && File.Exists(Path.Combine(outputDirectory, "il2cpp-config.h"))
               && File.Exists(Path.Combine(outputDirectory, "PlayerMain.cpp"))
               && (File.Exists(Path.Combine(outputDirectory, "player.managed"))
                   || File.Exists(Il2CppToolchain.GetPlayerExecutablePath(outputDirectory)));
    }

    private static void CollectArtifacts(string dir, PackageResult result)
    {
        foreach (var f in Directory.EnumerateFiles(dir, "*", SearchOption.TopDirectoryOnly))
            result.artifacts.Add(Path.GetFileName(f));
    }

    private static string Summarize(PackageResult r) =>
        $"success={r.success} native={r.nativeLinked} launch={r.launchOk} exit={r.exitCode} artifacts={r.artifacts.Count} err={r.error}";

    private static string BuildLog(PackageResult r)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Anity IL2CPP Package Report");
        sb.AppendLine("output=" + r.outputDirectory);
        sb.AppendLine("success=" + r.success);
        sb.AppendLine("nativeLinked=" + r.nativeLinked);
        sb.AppendLine("launchOk=" + r.launchOk);
        sb.AppendLine("exitCode=" + r.exitCode);
        sb.AppendLine("IsIl2Cpp=" + Il2CppRuntime.IsIl2Cpp);
        sb.AppendLine("platform=" + Il2CppRuntime.CurrentPlatform);
        sb.AppendLine("link.xml=" + r.hasLinkXml);
        sb.AppendLine("metadata=" + r.hasMetadata);
        sb.AppendLine("config=" + r.hasConfigHeader);
        sb.AppendLine("cmake=" + r.hasCMake);
        sb.AppendLine("PlayerMain=" + r.hasPlayerMain);
        sb.AppendLine("managed=" + r.hasManagedMarker);
        sb.AppendLine("artifacts:");
        foreach (var a in r.artifacts.OrderBy(x => x))
            sb.AppendLine("  " + a);
        if (!string.IsNullOrEmpty(Il2CppPlayerHost.lastLaunchLog))
            sb.AppendLine("launchLog=" + Il2CppPlayerHost.lastLaunchLog);
        if (!string.IsNullOrEmpty(Il2CppToolchain.lastCompileLog))
            sb.AppendLine("compileLog=" + Il2CppToolchain.lastCompileLog.Split('\n').FirstOrDefault());
        return sb.ToString();
    }
}
