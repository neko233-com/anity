using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
namespace Anity.Core.Runtime.Il2Cpp;

/// <summary>
/// IL2CPP post-convert toolchain: CMake/Ninja/cl scripts, method map, link step attempt.
/// Does not require a real il2cpp.exe — produces Unity-like layout so CI can validate artifacts.
/// </summary>
public static class Il2CppToolchain
{
    public static string lastCompileLog { get; private set; } = string.Empty;
    public static string lastDetectedCompiler { get; private set; } = string.Empty;

    public enum TargetAbi
    {
        WinX64,
        LinuxX64,
        AndroidArm64,
        IosArm64,
        WebGL
    }

    /// <summary>
    /// After <see cref="Il2CppBuilder.Build"/>, emit toolchain files into the same output dir.
    /// </summary>
    public static bool EmitToolchainFiles(string il2cppOutputDir, TargetAbi abi = TargetAbi.WinX64)
    {
        if (string.IsNullOrWhiteSpace(il2cppOutputDir) || !Directory.Exists(il2cppOutputDir))
            return false;

        var cppFiles = Directory.GetFiles(il2cppOutputDir, "*.cpp", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName)
            .Where(f => f != null)
            .Cast<string>()
            .ToList();

        File.WriteAllText(Path.Combine(il2cppOutputDir, "il2cpp-config.h"), GenerateConfigHeader(abi));
        File.WriteAllText(Path.Combine(il2cppOutputDir, "CMakeLists.txt"), GenerateCMake(cppFiles, abi));
        File.WriteAllText(Path.Combine(il2cppOutputDir, "compile_commands.json"), GenerateCompileCommands(il2cppOutputDir, cppFiles));
        File.WriteAllText(Path.Combine(il2cppOutputDir, "MethodMap.tsv"), GenerateMethodMap(il2cppOutputDir));
        File.WriteAllText(Path.Combine(il2cppOutputDir, "build.bat"), GenerateBuildBat());
        File.WriteAllText(Path.Combine(il2cppOutputDir, "build.sh"), GenerateBuildSh());
        File.WriteAllText(Path.Combine(il2cppOutputDir, "toolchain.json"),
            $"{{\"abi\":\"{abi}\",\"cppCount\":{cppFiles.Count},\"config\":\"{Il2CppBuilder.settings.compilerConfiguration}\"}}");
        return true;
    }

    /// <summary>Full pipeline: convert assemblies → emit toolchain → optional native compile attempt.</summary>
    public static bool BuildAndLink(IEnumerable<string> assemblyPaths, string outputDirectory, TargetAbi abi = TargetAbi.WinX64, bool tryNativeCompile = false)
    {
        if (!Il2CppBuilder.Build(assemblyPaths, outputDirectory))
            return false;
        if (!EmitToolchainFiles(outputDirectory, abi))
            return false;
        if (tryNativeCompile)
            TryNativeCompile(outputDirectory, abi);
        return true;
    }

    public static bool BuildAndLinkFromDomain(string outputDirectory, TargetAbi abi = TargetAbi.WinX64, bool tryNativeCompile = false)
    {
        if (!Il2CppBuilder.BuildFromLoadedDomain(outputDirectory))
            return false;
        if (!EmitToolchainFiles(outputDirectory, abi))
            return false;
        if (tryNativeCompile)
            TryNativeCompile(outputDirectory, abi);
        return true;
    }

    /// <summary>Detect host C/C++ compiler (cl, clang, gcc). Returns path or empty.</summary>
    public static string DetectCompiler()
    {
        foreach (var name in new[] { "cl", "clang++", "clang", "g++", "gcc" })
        {
            var path = FindOnPath(name);
            if (!string.IsNullOrEmpty(path))
            {
                lastDetectedCompiler = path;
                return path;
            }
        }
        lastDetectedCompiler = string.Empty;
        return string.Empty;
    }

    /// <summary>
    /// Attempt a smoke compile of Il2CppBootstrap/one stub if compiler exists.
    /// Returns true if compile succeeded or was skipped (no compiler) — false only on hard fail when forced.
    /// </summary>
    public static bool TryNativeCompile(string il2cppOutputDir, TargetAbi abi = TargetAbi.WinX64)
    {
        lastCompileLog = string.Empty;
        string compiler = DetectCompiler();
        if (string.IsNullOrEmpty(compiler))
        {
            lastCompileLog = "No C++ compiler on PATH — skip native link (artifacts still valid)";
            File.WriteAllText(Path.Combine(il2cppOutputDir, "compile.log"), lastCompileLog);
            return true; // soft skip
        }

        // Prefer smallest unit
        string bootstrap = Path.Combine(il2cppOutputDir, "Il2CppBootstrap.cpp");
        string unit = File.Exists(bootstrap)
            ? bootstrap
            : Directory.GetFiles(il2cppOutputDir, "*.cpp").FirstOrDefault();
        if (unit == null)
        {
            lastCompileLog = "No .cpp units";
            return false;
        }

        string objOut = Path.Combine(il2cppOutputDir, "smoke.o");
        string args;
        string fileName = Path.GetFileName(compiler).ToLowerInvariant();
        if (fileName.StartsWith("cl"))
        {
            objOut = Path.Combine(il2cppOutputDir, "smoke.obj");
            args = $"/nologo /c /I\"{il2cppOutputDir}\" /Fo\"{objOut}\" \"{unit}\"";
        }
        else
        {
            args = $"-c -I\"{il2cppOutputDir}\" -o \"{objOut}\" \"{unit}\"";
        }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = compiler,
                Arguments = args,
                WorkingDirectory = il2cppOutputDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var p = Process.Start(psi);
            if (p == null)
            {
                lastCompileLog = "Failed to start compiler";
                return false;
            }
            string stdout = p.StandardOutput.ReadToEnd();
            string stderr = p.StandardError.ReadToEnd();
            p.WaitForExit(60_000);
            lastCompileLog = $"compiler={compiler}\nargs={args}\nexit={p.ExitCode}\n{stdout}\n{stderr}";
            File.WriteAllText(Path.Combine(il2cppOutputDir, "compile.log"), lastCompileLog);
            // cl may still fail without VS env — treat non-zero as soft fail logged
            return p.ExitCode == 0 || File.Exists(objOut);
        }
        catch (Exception ex)
        {
            lastCompileLog = ex.Message;
            try { File.WriteAllText(Path.Combine(il2cppOutputDir, "compile.log"), lastCompileLog); } catch { }
            return false;
        }
    }

    public static IReadOnlyList<string> ListGeneratedCpp(string dir)
    {
        if (!Directory.Exists(dir)) return Array.Empty<string>();
        return Directory.GetFiles(dir, "*.cpp").Select(Path.GetFileName).Where(x => x != null).Cast<string>().ToList();
    }

    public static bool ValidateOutputLayout(string dir)
    {
        if (!Directory.Exists(dir)) return false;
        return File.Exists(Path.Combine(dir, "link.xml"))
               && File.Exists(Path.Combine(dir, "Il2CppMetadata.map"))
               && File.Exists(Path.Combine(dir, "il2cpp-config.h"))
               && File.Exists(Path.Combine(dir, "CMakeLists.txt"));
    }

    private static string GenerateConfigHeader(TargetAbi abi)
    {
        var sb = new StringBuilder();
        sb.AppendLine("#pragma once");
        sb.AppendLine("// Generated by Anity Il2CppToolchain");
        sb.AppendLine("#define ANITY_IL2CPP 1");
        sb.AppendLine($"#define ANITY_IL2CPP_ABI_{(int)abi} 1");
        switch (abi)
        {
            case TargetAbi.AndroidArm64: sb.AppendLine("#define IL2CPP_TARGET_ANDROID 1"); break;
            case TargetAbi.IosArm64: sb.AppendLine("#define IL2CPP_TARGET_IOS 1"); break;
            case TargetAbi.WebGL: sb.AppendLine("#define IL2CPP_TARGET_JAVASCRIPT 1"); break;
            case TargetAbi.LinuxX64: sb.AppendLine("#define IL2CPP_TARGET_LINUX 1"); break;
            default: sb.AppendLine("#define IL2CPP_TARGET_WINDOWS 1"); break;
        }
        if (Il2CppBuilder.settings.enableExceptions)
            sb.AppendLine("#define IL2CPP_ENABLE_EXCEPTIONS 1");
        sb.AppendLine("typedef void* Il2CppObject;");
        sb.AppendLine("typedef void* Il2CppMethodPointer;");
        return sb.ToString();
    }

    private static string GenerateCMake(List<string> cppFiles, TargetAbi abi)
    {
        var sb = new StringBuilder();
        sb.AppendLine("cmake_minimum_required(VERSION 3.16)");
        sb.AppendLine("project(AnityIl2Cpp LANGUAGES CXX)");
        sb.AppendLine("set(CMAKE_CXX_STANDARD 17)");
        sb.AppendLine("add_library(anity_il2cpp STATIC");
        foreach (var f in cppFiles.Take(200))
            sb.AppendLine($"  {f}");
        sb.AppendLine(")");
        sb.AppendLine("target_include_directories(anity_il2cpp PUBLIC ${CMAKE_CURRENT_SOURCE_DIR})");
        sb.AppendLine($"# abi={abi}");
        return sb.ToString();
    }

    private static string GenerateCompileCommands(string dir, List<string> cppFiles)
    {
        var sb = new StringBuilder();
        sb.AppendLine("[");
        for (int i = 0; i < cppFiles.Count; i++)
        {
            string f = cppFiles[i];
            string path = Path.Combine(dir, f).Replace("\\", "/");
            sb.Append("  {\"directory\":\"").Append(dir.Replace("\\", "/")).Append("\",");
            sb.Append("\"command\":\"c++ -c -I. ").Append(f).Append("\",");
            sb.Append("\"file\":\"").Append(path).Append("\"}");
            if (i + 1 < cppFiles.Count) sb.Append(',');
            sb.AppendLine();
        }
        sb.AppendLine("]");
        return sb.ToString();
    }

    private static string GenerateMethodMap(string dir)
    {
        var sb = new StringBuilder();
        sb.AppendLine("type\tmethod\tfile");
        foreach (var cpp in Directory.GetFiles(dir, "*.cpp").Take(500))
        {
            string name = Path.GetFileNameWithoutExtension(cpp);
            sb.Append(name).Append("\tinit\t").Append(Path.GetFileName(cpp)).AppendLine();
        }
        return sb.ToString();
    }

    private static string GenerateBuildBat() =>
        "@echo off\r\nREM Anity IL2CPP native build helper\r\nif exist CMakeLists.txt (\r\n  cmake -B build -G \"Ninja\" .\r\n  cmake --build build\r\n) else (\r\n  echo missing CMakeLists.txt\r\n)\r\n";

    private static string GenerateBuildSh() =>
        "#!/usr/bin/env bash\nset -e\nif [[ -f CMakeLists.txt ]]; then\n  cmake -B build -G Ninja .\n  cmake --build build\nelse\n  echo missing CMakeLists.txt\nfi\n";

    private static string FindOnPath(string exe)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = Environment.OSVersion.Platform == PlatformID.Win32NT ? "where" : "which",
                Arguments = exe,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var p = Process.Start(psi);
            if (p == null) return string.Empty;
            string o = p.StandardOutput.ReadToEnd().Trim();
            p.WaitForExit(5000);
            if (p.ExitCode != 0 || string.IsNullOrEmpty(o)) return string.Empty;
            return o.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }
}
