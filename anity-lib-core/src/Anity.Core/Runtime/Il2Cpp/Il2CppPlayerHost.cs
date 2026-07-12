using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEngine;

namespace Anity.Core.Runtime.Il2Cpp;

/// <summary>
/// IL2CPP player link + launch host (Unity player-style entry after AOT conversion).
/// Full pipeline: convert → emit → compile/link player → Launch (native exe or managed fallback).
/// </summary>
public static class Il2CppPlayerHost
{
    public static string lastLaunchLog { get; private set; } = string.Empty;
    public static int lastExitCode { get; private set; } = -1;
    public static string lastPlayerPath { get; private set; } = string.Empty;

    /// <summary>
    /// Build player into outputDirectory: IL2CPP convert + player main + link attempt.
    /// Returns true if artifacts ready (native link optional).
    /// </summary>
    public static bool BuildPlayer(string outputDirectory, Il2CppToolchain.TargetAbi abi = Il2CppToolchain.TargetAbi.WinX64, bool tryNativeLink = true)
    {
        if (string.IsNullOrWhiteSpace(outputDirectory))
            throw new ArgumentException("outputDirectory required");

        Directory.CreateDirectory(outputDirectory);
        if (!Il2CppBuilder.BuildFromLoadedDomain(outputDirectory))
            return false;
        if (!Il2CppToolchain.EmitToolchainFiles(outputDirectory, abi))
            return false;

        EmitPlayerMain(outputDirectory, abi);
        EmitPlayerManifest(outputDirectory, abi);

        bool linked = false;
        if (tryNativeLink)
            linked = Il2CppToolchain.LinkPlayer(outputDirectory, abi);

        // Always write managed launcher marker so Launch works without native toolchain
        string marker = Path.Combine(outputDirectory, "player.managed");
        File.WriteAllText(marker, "AnityIl2CppManagedPlayer\nready=1\n");

        lastPlayerPath = linked
            ? Il2CppToolchain.GetPlayerExecutablePath(outputDirectory)
            : marker;
        return true;
    }

    /// <summary>
    /// Launch player process. Prefer native exe; else managed IL2CPP runtime entry.
    /// </summary>
    public static bool Launch(string playerDir, int timeoutMs = 30_000)
    {
        lastLaunchLog = string.Empty;
        lastExitCode = -1;
        if (string.IsNullOrEmpty(playerDir) || !Directory.Exists(playerDir))
        {
            lastLaunchLog = "player dir missing";
            return false;
        }

        string native = Il2CppToolchain.GetPlayerExecutablePath(playerDir);
        if (File.Exists(native))
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = native,
                    WorkingDirectory = playerDir,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var p = Process.Start(psi);
                if (p == null)
                {
                    lastLaunchLog = "failed to start native player";
                    return false;
                }
                if (!p.WaitForExit(timeoutMs))
                {
                    try { p.Kill(); } catch { }
                    lastLaunchLog = "native player timeout";
                    lastExitCode = -2;
                    return false;
                }
                lastExitCode = p.ExitCode;
                lastLaunchLog = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
                File.WriteAllText(Path.Combine(playerDir, "launch.log"), lastLaunchLog);
                return lastExitCode == 0;
            }
            catch (Exception ex)
            {
                lastLaunchLog = "native launch: " + ex.Message;
            }
        }

        // Managed fallback player — initialize IL2CPP runtime and exit 0
        return LaunchManaged(playerDir);
    }

    public static bool LaunchManaged(string playerDir)
    {
        try
        {
            Il2CppRuntime.EnterIl2CppPlayerMode();
            Il2CppRuntime.Initialize();
            Il2CppApi.InitializeRuntime();
            lastExitCode = 0;
            lastLaunchLog = "managed player: IsIl2Cpp=" + Il2CppRuntime.IsIl2Cpp + " platform=" + Il2CppRuntime.CurrentPlatform;
            lastPlayerPath = Path.Combine(playerDir ?? "", "player.managed");
            try
            {
                if (!string.IsNullOrEmpty(playerDir))
                    File.WriteAllText(Path.Combine(playerDir, "launch.log"), lastLaunchLog);
            }
            catch { }
            return true;
        }
        catch (Exception ex)
        {
            lastExitCode = 1;
            lastLaunchLog = "managed player failed: " + ex.Message;
            return false;
        }
    }

    public static bool IsPlayerReady(string playerDir)
    {
        if (string.IsNullOrEmpty(playerDir) || !Directory.Exists(playerDir)) return false;
        if (File.Exists(Il2CppToolchain.GetPlayerExecutablePath(playerDir))) return true;
        return File.Exists(Path.Combine(playerDir, "player.managed"))
               && File.Exists(Path.Combine(playerDir, "Il2CppMetadata.map"));
    }

    private static void EmitPlayerMain(string dir, Il2CppToolchain.TargetAbi abi)
    {
        var sb = new StringBuilder();
        sb.AppendLine("#include \"il2cpp-config.h\"");
        sb.AppendLine("#include <stdio.h>");
        sb.AppendLine("extern void anity_il2cpp_bootstrap();");
        sb.AppendLine("int main(int argc, char** argv) {");
        sb.AppendLine("  (void)argc; (void)argv;");
        sb.AppendLine("  anity_il2cpp_bootstrap();");
        sb.AppendLine($"  printf(\"AnityIl2CppPlayer abi={(int)abi}\\n\");");
        sb.AppendLine("  return 0;");
        sb.AppendLine("}");
        File.WriteAllText(Path.Combine(dir, "PlayerMain.cpp"), sb.ToString());
    }

    private static void EmitPlayerManifest(string dir, Il2CppToolchain.TargetAbi abi)
    {
        File.WriteAllText(Path.Combine(dir, "player.manifest.json"),
            $"{{\"name\":\"AnityIl2CppPlayer\",\"abi\":\"{abi}\",\"scripting\":\"IL2CPP\"}}");
    }
}
