using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Anity.Cli;

/// <summary>
/// Unity Editor/Player CLI argument surface (Unity 2022.3 Pro compatible names).
/// </summary>
public sealed class CommandLineArgs
{
    public bool BatchMode { get; set; }
    public bool Quit { get; set; }
    public bool NoGraphics { get; set; }
    public bool SilentCrashes { get; set; }
    public bool AcceptApiUpdate { get; set; }
    public string? ProjectPath { get; set; }
    public string? LogFile { get; set; }
    public string? ExecuteMethod { get; set; }
    public string? BuildTarget { get; set; }
    public string? BuildWindowsPlayer { get; set; }
    public string? BuildWindows64Player { get; set; }
    public string? BuildLinux64Player { get; set; }
    public string? BuildOSXUniversalPlayer { get; set; }
    public string? BuildAndroidPlayer { get; set; }
    public string? BuildIosPlayer { get; set; }
    public string? BuildWebGLPlayer { get; set; }
    public bool RunTests { get; set; }
    public string? TestPlatform { get; set; }
    public string? TestResults { get; set; }
    public string? TestFilter { get; set; }
    public bool Il2Cpp { get; set; }
    public string? Il2CppOutput { get; set; }
    public string? Screenshot { get; set; }
    public int ScreenshotSuperSize { get; set; } = 1;
    public bool Agent { get; set; }
    public string? AgentPrompt { get; set; }
    public string? AgentSession { get; set; }
    public bool Help { get; set; }
    public bool Version { get; set; }
    public List<string> CustomArgs { get; } = new();
    public Dictionary<string, string> UnknownNamed { get; } = new(StringComparer.OrdinalIgnoreCase);

    public static CommandLineArgs Parse(string[] args)
    {
        var r = new CommandLineArgs();
        if (args == null || args.Length == 0)
        {
            r.Help = true;
            return r;
        }

        for (int i = 0; i < args.Length; i++)
        {
            string a = args[i];
            if (string.IsNullOrEmpty(a)) continue;

            string key = a.StartsWith("-") ? a.TrimStart('-') : a;
            string? Next() => i + 1 < args.Length ? args[++i] : null;

            switch (key.ToLowerInvariant())
            {
                case "batchmode": r.BatchMode = true; break;
                case "quit": r.Quit = true; break;
                case "nographics": r.NoGraphics = true; break;
                case "silent-crashes": r.SilentCrashes = true; break;
                case "accept-apiupdate": r.AcceptApiUpdate = true; break;
                case "projectpath": r.ProjectPath = Next(); break;
                case "logfile": r.LogFile = Next(); break;
                case "executemethod": r.ExecuteMethod = Next(); break;
                case "buildtarget": r.BuildTarget = Next(); break;
                case "buildwindowsplayer": r.BuildWindowsPlayer = Next(); break;
                case "buildwindows64player": r.BuildWindows64Player = Next(); break;
                case "buildlinux64player": r.BuildLinux64Player = Next(); break;
                case "buildosxuniversalplayer": r.BuildOSXUniversalPlayer = Next(); break;
                case "buildandroidplayer": r.BuildAndroidPlayer = Next(); break;
                case "buildiosplayer":
                case "buildiPhoneplayer":
                case "buildiphoneplayer": r.BuildIosPlayer = Next(); break;
                case "buildwebglplayer": r.BuildWebGLPlayer = Next(); break;
                case "runtests": r.RunTests = true; break;
                case "testplatform": r.TestPlatform = Next(); break;
                case "testresults": r.TestResults = Next(); break;
                case "testfilter":
                case "editorTestsFilter":
                case "editortestsfilter": r.TestFilter = Next(); break;
                case "il2cpp": r.Il2Cpp = true; break;
                case "il2cppoutput": r.Il2CppOutput = Next(); break;
                case "screenshot": r.Screenshot = Next(); break;
                case "screenshotsupersize":
                    if (int.TryParse(Next(), out int ss)) r.ScreenshotSuperSize = ss;
                    break;
                case "agent": r.Agent = true; break;
                case "agentprompt": r.AgentPrompt = Next(); break;
                case "agentsession": r.AgentSession = Next(); break;
                case "h":
                case "help":
                case "?": r.Help = true; break;
                case "version":
                case "v": r.Version = true; break;
                default:
                    if (a.StartsWith("-"))
                    {
                        var val = Next();
                        r.UnknownNamed[key] = val ?? "true";
                    }
                    else
                        r.CustomArgs.Add(a);
                    break;
            }
        }

        if (!string.IsNullOrEmpty(r.ProjectPath))
            r.ProjectPath = Path.GetFullPath(r.ProjectPath);

        return r;
    }

    public static string HelpText =>
@"Anity CLI (Unity 2022.3 Pro compatible)

Usage:
  anity [options]

Unity-compatible:
  -batchmode                 Run without interactive UI
  -quit                      Exit after other commands
  -nographics                No graphics device
  -projectPath <path>        Open project
  -executeMethod <T.M>       Invoke static method
  -logFile <path>            Write log
  -buildTarget <name>        Set active build target
  -buildWindows64Player <path>
  -buildAndroidPlayer <path>
  -buildiOSPlayer <path>
  -buildWebGLPlayer <path>
  -runTests                  Run editmode/playmode tests
  -testResults <path>        NUnit/XML results path
  -testFilter <name>         Filter tests
  -silent-crashes            Suppress crash dialogs

Anity extensions:
  -il2cpp                    Full IL2CPP package: convert → artifacts → link → launch
  -il2cppOutput <dir>        IL2CPP output directory (default Library/Il2CppBuildCache)
                             With -build*Player also emits Il2CppOutputProject next to player
  -screenshot <file.png>     Capture screenshot
  -screenshotSuperSize <n>   Super-size (1-8)
  -agent                     Enable Anity.Agent extension session
  -agentPrompt <text>        Run one agent turn
  -agentSession <id>         Resume agent session

  -help                      Show this help
  -version                   Print version
";
}
