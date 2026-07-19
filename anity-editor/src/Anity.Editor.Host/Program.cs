using System.Text.Json;
using System.Linq;
using Anity.Cli;
using Anity.Editor.Host.Services;

// The app bundle is also a Unity-compatible editor executable.  Keep the
// interactive host verbs below, but forward Unity-style switches to the
// single CLI implementation so `Anity.app/.../Anity.Editor.Host -batchmode`
// has the same behavior as the standalone `anity` executable.
if (args.Length > 0 && args[0] is not "--help" and not "-h" && args[0].StartsWith("-", StringComparison.Ordinal))
{
  Environment.ExitCode = new CliHost().Run(args);
  return;
}

if (args.Length == 0 || args[0] == "--help" || args[0] == "-h")
{
  Console.WriteLine("Anity Editor Host");
  Console.WriteLine("commands:");
  Console.WriteLine("  start --project-path <path>");
  Console.WriteLine("  status");
  Console.WriteLine("  stop [sessionId]");
  Console.WriteLine("  menu list");
  Console.WriteLine("  menu run <menu-path>");
  Console.WriteLine("  window list");
  Console.WriteLine("  window open <alias>");
  Console.WriteLine("  sample [ticks] [fps]");
  return;
}

var host = new EditorHost();
var command = args[0];
var tail = args.Skip(1).ToArray();

switch (command)
{
  case "start":
    {
      var path = args.Length > 2 && args[1] == "--project-path" ? args[2] : "SampleProject";
      var session = await host.StartSessionAsync(path);
      var json = JsonSerializer.Serialize(session);
      Console.WriteLine(json);
      break;
    }

  case "status":
    {
      Console.WriteLine(host.GetStatus());
      break;
    }

  case "stop":
    {
      var sessionId = tail.Length > 0 ? tail[0] : null;
      var session = await host.StopAsync(sessionId);
      Console.WriteLine(session is null ? "no session" : JsonSerializer.Serialize(session));
      break;
    }

  case "menu":
    {
      if (tail.Length == 0)
      {
        PrintUsage("menu");
        break;
      }

      if (tail[0] == "list")
      {
        foreach (var item in host.GetMenus())
        {
          Console.WriteLine(item);
        }

        break;
      }

      if (tail[0] == "run" && tail.Length >= 2)
      {
        var menuPath = string.Join(" ", tail.Skip(1));
        var ok = host.ExecuteMenu(menuPath);
        Console.WriteLine(ok ? "executed" : "not-found");
        break;
      }

      PrintUsage("menu");
      break;
    }

  case "window":
    {
      if (tail.Length == 0)
      {
        PrintUsage("window");
        break;
      }

      if (tail[0] == "list")
      {
        foreach (var window in host.GetWindowCatalog())
        {
          Console.WriteLine(window);
        }
        break;
      }

      if (tail[0] == "open" && tail.Length >= 2)
      {
        var ok = host.OpenWindow(string.Join(" ", tail.Skip(1)));
        Console.WriteLine(ok ? "opened" : "not-found");
        break;
      }

      PrintUsage("window");
      break;
    }

  case "sample":
    {
      var ticks = args.Length > 1 && int.TryParse(args[1], out var t) ? t : 60;
      var fps = args.Length > 2 && int.TryParse(args[2], out var f) ? f : 30;
      await host.RunCompatibilityDemoAsync(ticks, fps);
      break;
    }

  default:
    Console.WriteLine("unknown command");
    break;
}

void PrintUsage(string command)
{
  Console.WriteLine($"usage: {command}");
}
