using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Anity.Agent;
using Anity.Core.Runtime.Il2Cpp;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Anity.Cli;

public sealed class CliHost
{
    private readonly StringBuilder _log = new();
    private int _exitCode;

    public int ExitCode => _exitCode;
    public string LogText => _log.ToString();

    public int Run(string[] args)
    {
        var parsed = CommandLineArgs.Parse(args);
        try
        {
            if (parsed.Version)
            {
                WriteLine($"Anity CLI {Application.unityVersion} / Anity.Core");
                return 0;
            }

            if (parsed.Help)
            {
                WriteLine(CommandLineArgs.HelpText);
                return 0;
            }

            if (parsed.SilentCrashes)
                Application.SetStackTraceLogType(LogType.Exception, StackTraceLogType.None);

            if (!string.IsNullOrEmpty(parsed.LogFile))
                WriteLine($"logFile={parsed.LogFile}");

            if (parsed.BatchMode)
            {
                WriteLine("batchmode=1");
                Application.isBatchMode = true;
            }

            if (!string.IsNullOrEmpty(parsed.ProjectPath))
            {
                if (!Directory.Exists(parsed.ProjectPath))
                {
                    Fail($"projectPath not found: {parsed.ProjectPath}");
                    return _exitCode;
                }
                EditorApplication.OpenProject(parsed.ProjectPath);
                WriteLine($"projectPath={parsed.ProjectPath}");
            }

            if (parsed.NoGraphics)
            {
                WriteLine("nographics=1");
                SystemInfo.overrideGraphicsDeviceType = GraphicsDeviceType.Null;
            }

            if (parsed.Il2Cpp)
            {
                WriteLine("il2cpp=1");
                string outDir = parsed.Il2CppOutput ?? Path.Combine(Directory.GetCurrentDirectory(), "Library", "Il2CppBuildCache");
                // Full packaging path: convert → artifacts → link/launch (managed if no C++ compiler)
                var pkg = Il2CppPackagePipeline.Package(outDir, tryNativeLink: true, launch: true);
                WriteLine(pkg.success
                    ? $"il2cppOutput={outDir} nativeLinked={pkg.nativeLinked} launchOk={pkg.launchOk}"
                    : $"il2cpp failed: {pkg.error}");
                if (!string.IsNullOrEmpty(Il2CppPackagePipeline.lastReport))
                    WriteLine("il2cppReport=" + Il2CppPackagePipeline.lastReport);
                if (!pkg.success) Fail(pkg.error);
            }

            if (!string.IsNullOrEmpty(parsed.BuildTarget))
            {
                if (Enum.TryParse(parsed.BuildTarget, true, out BuildTarget bt))
                {
                    var group = EditorUserBuildSettings.BuildTargetToBuildTargetGroup(bt);
                    EditorUserBuildSettings.SwitchActiveBuildTarget(group, bt);
                    WriteLine($"buildTarget={bt}");
                }
                else
                    Fail($"Unknown buildTarget: {parsed.BuildTarget}");
            }

            TryBuildPlayer(parsed);

            if (!string.IsNullOrEmpty(parsed.ExecuteMethod))
            {
                if (!InvokeExecuteMethod(parsed.ExecuteMethod))
                    Fail($"executeMethod failed: {parsed.ExecuteMethod}");
                else
                    WriteLine($"executeMethod={parsed.ExecuteMethod} OK");
            }

            if (parsed.RunTests)
            {
                string results = parsed.TestResults ?? "TestResults.xml";
                int failed = RunSyntheticTests(parsed.TestFilter, results);
                WriteLine($"runTests results={results} failed={failed}");
                if (failed > 0) _exitCode = 2;
            }

            if (!string.IsNullOrEmpty(parsed.Screenshot))
            {
                ScreenCapture.CaptureScreenshot(parsed.Screenshot, Math.Clamp(parsed.ScreenshotSuperSize, 1, 8));
                WriteLine($"screenshot={ScreenCapture.lastCapturePath}");
            }

            if (parsed.Agent || !string.IsNullOrEmpty(parsed.AgentPrompt))
            {
                var options = AgentConnectionOptions.FromEnvironment();
                if (parsed.AgentApiKey != null) options.ApiKey = parsed.AgentApiKey;
                if (parsed.AgentBaseUrl != null) options.BaseUrl = parsed.AgentBaseUrl;
                if (parsed.AgentModel != null) options.Model = parsed.AgentModel;
                if (parsed.AgentTimeoutSeconds.HasValue)
                    options.Timeout = TimeSpan.FromSeconds(parsed.AgentTimeoutSeconds.Value);

                bool remoteRequested = !string.IsNullOrWhiteSpace(options.ApiKey)
                    || parsed.AgentBaseUrl != null
                    || parsed.AgentModel != null
                    || parsed.AgentTimeoutSeconds.HasValue;
                AgentRuntime runtime = remoteRequested ? new AgentRuntime(options) : AgentRuntime.Default;
                bool ownsRuntime = remoteRequested;
                try
                {
                    var session = runtime.CreateSession(parsed.AgentSession);
                    if (!string.IsNullOrEmpty(parsed.AgentPrompt))
                    {
                        var reply = session.RunTurn(parsed.AgentPrompt!);
                        WriteLine($"agent.session={session.Id}");
                        WriteLine($"agent.reply={reply.Content}");
                    }
                    else
                        WriteLine($"agent.session={session.Id} ready");
                }
                finally
                {
                    if (ownsRuntime) runtime.Dispose();
                }
            }

            if (parsed.Quit || parsed.BatchMode)
            {
                try { PlayerPrefs.SaveIfDirty(); } catch { }
                try { UnityEditor.EditorPrefs.SaveIfDirty(); } catch { }
                WriteLine("quit=1");
            }

            FlushLog(parsed.LogFile);
            return _exitCode;
        }
        catch (Exception ex)
        {
            Fail(ex.ToString());
            FlushLog(parsed.LogFile);
            return _exitCode == 0 ? 1 : _exitCode;
        }
    }

    private void TryBuildPlayer(CommandLineArgs p)
    {
        void Build(BuildTarget target, string? path)
        {
            if (string.IsNullOrEmpty(path)) return;
            var opt = new BuildPlayerOptions
            {
                locationPathName = path,
                target = target,
                scenes = EditorBuildSettings.scenes?.Select(s => s.path).Where(x => !string.IsNullOrEmpty(x)).ToArray()
                         ?? Array.Empty<string>()
            };
            if (opt.scenes.Length == 0)
                opt.scenes = new[] { "Assets/Scenes/Main.unity" };

            // When -il2cpp is set (or backend already IL2CPP), package IL2CPP next to player
            bool wantIl2Cpp = p.Il2Cpp;
            try
            {
                var group = EditorUserBuildSettings.BuildTargetToBuildTargetGroup(target);
                wantIl2Cpp = wantIl2Cpp || PlayerSettings.GetScriptingBackend(group) == ScriptingImplementation.IL2CPP;
            }
            catch { }

            if (wantIl2Cpp)
            {
                var pkg = Il2CppPackagePipeline.PackageForPlayer(path, tryNativeLink: true, launch: false);
                WriteLine(pkg.success
                    ? $"il2cppPlayerPackage={pkg.outputDirectory} nativeLinked={pkg.nativeLinked}"
                    : $"il2cppPlayerPackage failed: {pkg.error}");
                if (!pkg.success)
                {
                    Fail(pkg.error);
                    return;
                }
            }

            var report = BuildPipeline.BuildPlayer(opt);
            WriteLine($"build {target} → {path} result={report.summary.result}");
        }

        Build(BuildTarget.StandaloneWindows, p.BuildWindowsPlayer);
        Build(BuildTarget.StandaloneWindows64, p.BuildWindows64Player);
        Build(BuildTarget.StandaloneLinux64, p.BuildLinux64Player);
        Build(BuildTarget.StandaloneOSX, p.BuildOSXUniversalPlayer);
        Build(BuildTarget.Android, p.BuildAndroidPlayer);
        Build(BuildTarget.iOS, p.BuildIosPlayer);
        Build(BuildTarget.WebGL, p.BuildWebGLPlayer);
    }

    private bool InvokeExecuteMethod(string method)
    {
        // Type.Method static
        var parts = method.Split('.');
        if (parts.Length < 2) return false;
        string methodName = parts[^1];
        string typeName = string.Join(".", parts.Take(parts.Length - 1));

        Type? type = AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType(typeName, false, true))
            .FirstOrDefault(t => t != null);
        if (type == null)
            type = Type.GetType(typeName, false, true);
        if (type == null) return false;

        var mi = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        if (mi == null) return false;
        mi.Invoke(null, null);
        return true;
    }

    private int RunSyntheticTests(string? filter, string resultsPath)
    {
        // Lightweight built-in smoke tests so -runTests works without external runner
        var cases = new (string name, Func<bool> run)[]
        {
            ("ScreenCapture.CreateTexture", () => { var t = ScreenCapture.CaptureScreenshotAsTexture(); return t != null && t.width > 0; }),
            ("Il2Cpp.ForceMode", () => { Il2CppRuntime.EnterIl2CppPlayerMode(); return Il2CppRuntime.IsIl2Cpp; }),
            ("Il2Cpp.PackagePipeline", () =>
            {
                string d = Path.Combine(Path.GetTempPath(), "cli_il2pkg_" + Guid.NewGuid().ToString("N"));
                var r = Il2CppPackagePipeline.Package(d, tryNativeLink: false, launch: true);
                return r.success && Il2CppPackagePipeline.ValidatePackageLayout(d);
            }),
            ("Agent.Session", () => { var s = AgentRuntime.Default.CreateSession(); return s != null && !string.IsNullOrEmpty(s.Id); }),
            ("Vector3.Normalize", () => Vector3.one.normalized.magnitude > 0.9f),
            ("ColorSpace.Linear", () => { QualitySettings.activeColorSpace = UnityEngine.ColorSpace.Linear; return QualitySettings.activeColorSpace == UnityEngine.ColorSpace.Linear; }),
        };

        int failed = 0;
        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        sb.AppendLine("<test-run>");
        foreach (var c in cases)
        {
            if (!string.IsNullOrEmpty(filter) && c.name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0)
                continue;
            bool ok;
            try { ok = c.run(); }
            catch { ok = false; }
            if (!ok) failed++;
            sb.AppendLine($"  <test-case name=\"{c.name}\" result=\"{(ok ? "Passed" : "Failed")}\"/>");
        }
        sb.AppendLine("</test-run>");
        File.WriteAllText(resultsPath, sb.ToString());
        return failed;
    }

    private void WriteLine(string msg)
    {
        _log.AppendLine(msg);
        Console.WriteLine(msg);
    }

    private void Fail(string msg)
    {
        _exitCode = 1;
        WriteLine("ERROR: " + msg);
    }

    private void FlushLog(string? logFile)
    {
        if (string.IsNullOrEmpty(logFile)) return;
        try
        {
            var dir = Path.GetDirectoryName(logFile);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(logFile, _log.ToString());
        }
        catch
        {
            // ignore log IO
        }
    }
}
