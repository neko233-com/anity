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

    /// <summary>Detect host C/C++ compiler (prefer GNU/Clang, then MSVC cl). Returns path or empty.</summary>
    public static string DetectCompiler()
    {
        // Prefer clang++/g++ before cl — "cl" basename must not confuse with clang
        foreach (var name in new[] { "clang++", "g++", "clang", "gcc", "cl" })
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
    /// True only for MSVC driver <c>cl.exe</c>. Must NOT match clang/clang++ (starts with "cl").
    /// </summary>
    public static bool IsMsvcCl(string compilerPath)
    {
        if (string.IsNullOrEmpty(compilerPath)) return false;
        string name = Path.GetFileNameWithoutExtension(compilerPath).ToLowerInvariant();
        return name == "cl";
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
        if (IsMsvcCl(compiler))
        {
            objOut = Path.Combine(il2cppOutputDir, "smoke.obj");
            args = $"/nologo /c /I\"{il2cppOutputDir}\" /Fo\"{objOut}\" \"{unit}\"";
        }
        else
        {
            args = $"-c -std=c++17 -I\"{il2cppOutputDir}\" -o \"{objOut}\" \"{unit}\"";
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

    public static string GetPlayerExecutablePath(string il2cppOutputDir)
    {
        bool win = Environment.OSVersion.Platform == PlatformID.Win32NT;
        return Path.Combine(il2cppOutputDir, win ? "AnityIl2CppPlayer.exe" : "AnityIl2CppPlayer");
    }

    /// <summary>
    /// Full native link of player: compile bootstrap + PlayerMain into executable when compiler available.
    /// Returns true only if native binary was produced.
    /// </summary>
    public static bool LinkPlayer(string il2cppOutputDir, TargetAbi abi = TargetAbi.WinX64)
    {
        lastCompileLog = string.Empty;
        if (string.IsNullOrEmpty(il2cppOutputDir) || !Directory.Exists(il2cppOutputDir))
            return false;

        // Ensure player main exists
        string playerMain = Path.Combine(il2cppOutputDir, "PlayerMain.cpp");
        string bootstrap = Path.Combine(il2cppOutputDir, "Il2CppBootstrap.cpp");
        if (!File.Exists(bootstrap))
        {
            File.WriteAllText(bootstrap,
                "#include \"il2cpp-config.h\"\nvoid anity_il2cpp_bootstrap() {}\n");
        }
        if (!File.Exists(playerMain))
        {
            File.WriteAllText(playerMain,
                "#include \"il2cpp-config.h\"\n#include <stdio.h>\nextern void anity_il2cpp_bootstrap();\n" +
                "int main(){ anity_il2cpp_bootstrap(); printf(\"AnityIl2CppPlayer\\n\"); return 0; }\n");
        }
        if (!File.Exists(Path.Combine(il2cppOutputDir, "il2cpp-config.h")))
            EmitToolchainFiles(il2cppOutputDir, abi);

        string compiler = DetectCompiler();
        if (string.IsNullOrEmpty(compiler))
        {
            lastCompileLog = "No C++ compiler — LinkPlayer skipped";
            File.WriteAllText(Path.Combine(il2cppOutputDir, "link.log"), lastCompileLog);
            return false;
        }

        string outExe = GetPlayerExecutablePath(il2cppOutputDir);
        string args;
        if (IsMsvcCl(compiler))
        {
            // MSVC cl.exe only
            args = $"/nologo /EHsc /I\"{il2cppOutputDir}\" /Fe\"{outExe}\" \"{bootstrap}\" \"{playerMain}\"";
        }
        else
        {
            // clang++ / g++ / gcc
            args = $"-std=c++17 -I\"{il2cppOutputDir}\" -o \"{outExe}\" \"{bootstrap}\" \"{playerMain}\"";
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
                lastCompileLog = "failed to start linker/compiler";
                return false;
            }
            string stdout = p.StandardOutput.ReadToEnd();
            string stderr = p.StandardError.ReadToEnd();
            p.WaitForExit(120_000);
            lastCompileLog = $"link compiler={compiler}\nargs={args}\nexit={p.ExitCode}\n{stdout}\n{stderr}";
            File.WriteAllText(Path.Combine(il2cppOutputDir, "link.log"), lastCompileLog);
            bool ok = p.ExitCode == 0 && File.Exists(outExe);
            if (ok)
                lastCompileLog += "\nplayer=" + outExe;
            return ok;
        }
        catch (Exception ex)
        {
            lastCompileLog = "LinkPlayer: " + ex.Message;
            try { File.WriteAllText(Path.Combine(il2cppOutputDir, "link.log"), lastCompileLog); } catch { }
            return false;
        }
    }

    /// <summary>Compile all .cpp units to objects (batch), returns count of object files produced.</summary>
    public static int CompileAllUnits(string il2cppOutputDir)
    {
        string compiler = DetectCompiler();
        if (string.IsNullOrEmpty(compiler) || !Directory.Exists(il2cppOutputDir))
            return 0;

        int count = 0;
        bool msvc = IsMsvcCl(compiler);
        foreach (var cpp in Directory.GetFiles(il2cppOutputDir, "*.cpp"))
        {
            string name = Path.GetFileNameWithoutExtension(cpp);
            string obj = Path.Combine(il2cppOutputDir, name + (msvc ? ".obj" : ".o"));
            string args = msvc
                ? $"/nologo /c /I\"{il2cppOutputDir}\" /Fo\"{obj}\" \"{cpp}\""
                : $"-c -std=c++17 -I\"{il2cppOutputDir}\" -o \"{obj}\" \"{cpp}\"";
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
                if (p == null) continue;
                p.WaitForExit(60_000);
                if (File.Exists(obj)) count++;
            }
            catch { }
        }
        return count;
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
        {
            if (string.Equals(f, "PlayerMain.cpp", StringComparison.OrdinalIgnoreCase)) continue;
            sb.AppendLine($"  {f}");
        }
        sb.AppendLine(")");
        sb.AppendLine("target_include_directories(anity_il2cpp PUBLIC ${CMAKE_CURRENT_SOURCE_DIR})");
        sb.AppendLine("if(EXISTS \"${CMAKE_CURRENT_SOURCE_DIR}/PlayerMain.cpp\")");
        sb.AppendLine("  add_executable(AnityIl2CppPlayer PlayerMain.cpp)");
        sb.AppendLine("  target_link_libraries(AnityIl2CppPlayer PRIVATE anity_il2cpp)");
        sb.AppendLine("endif()");
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
