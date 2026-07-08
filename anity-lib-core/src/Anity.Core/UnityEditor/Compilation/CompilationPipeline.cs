using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace UnityEditor.Compilation;

public static class CompilationPipeline
{
  private static bool _isCompiling;
  private static readonly Dictionary<string, Assembly> _cachedAssemblies = new(StringComparer.OrdinalIgnoreCase);
  private static readonly List<AssemblyDefinition> _assemblyDefinitions = new();

  public static bool isCompiling => _isCompiling;

  public static event Action<string, CompilerMessage[]>? compilationFinished;
  public static event Action<CompilationResult>? compilationResultFinished;
  public static event Action<string, CompilerMessage[]>? compilationStarted;
  public static event Action<object>? compilationObjectStarted;

  public static Assembly[] GetAssemblies()
  {
    var list = new List<Assembly>();
    foreach (var asm in _cachedAssemblies.Values)
    {
      list.Add(asm);
    }

    return list.ToArray();
  }

  public static string[] GetAssemblyNames()
  {
    return _cachedAssemblies.Keys.ToArray();
  }

  public static string GetAssembliesPath()
  {
    return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Library", "ScriptAssemblies");
  }

  public static string[] GetAssemblyReferences(string assemblyPath)
  {
    _ = assemblyPath;
    return Array.Empty<string>();
  }

  public static string[] GetScriptAssemblies()
  {
    return Array.Empty<string>();
  }

  public static bool IsCompilationQueued()
  {
    return false;
  }

  public static void RequestScriptCompilation()
  {
    _isCompiling = true;
    compilationStarted?.Invoke("scripts", Array.Empty<CompilerMessage>());
    compilationObjectStarted?.Invoke("scripts");
  }

  public static void RequestScriptCompilation(RequestScriptCompilationOptions options)
  {
    _ = options;
    RequestScriptCompilation();
  }

  public static void TriggerFinished(string assemblyPath, IEnumerable<CompilerMessage> messages)
  {
    _isCompiling = false;
    var msgArray = messages as CompilerMessage[] ?? Array.Empty<CompilerMessage>();
    compilationFinished?.Invoke(assemblyPath, msgArray);
  }

  public static void TriggerFinished(CompilationResult result)
  {
    _isCompiling = false;
    compilationResultFinished?.Invoke(result);
  }

  public static string[] GetDefinesForAssembly(string assemblyPath)
  {
    _ = assemblyPath;
    return Array.Empty<string>();
  }

  public static AssemblyDefinition[] GetAllAssemblyDefinitions()
  {
    return _assemblyDefinitions.ToArray();
  }

  public static string[] GetAssemblyDefinitionReferences(string assemblyName)
  {
    var def = _assemblyDefinitions.FirstOrDefault(d => string.Equals(d.name, assemblyName, StringComparison.OrdinalIgnoreCase));
    return def.references;
  }

  public static int GetCurrentAssemblyDefinitionIndex()
  {
    return _assemblyDefinitions.Count;
  }
}

public static class BuildUtility
{
  public static bool IsManagedAssembly(string path) => string.Equals(Path.GetExtension(path), ".dll", StringComparison.OrdinalIgnoreCase);
}

public readonly struct Assembly
{
  public readonly string name;
  public readonly string assemblyPath;
  public readonly string outputPath;
  public readonly AssemblyFlags flags;
  public readonly string[] references;
  public readonly string[] defineConstraints;

  public Assembly(string name, string assemblyPath, string outputPath = "", AssemblyFlags flags = AssemblyFlags.None, string[]? references = null, string[]? defineConstraints = null)
  {
    this.name = name;
    this.assemblyPath = assemblyPath;
    this.outputPath = outputPath;
    this.flags = flags;
    this.references = references ?? Array.Empty<string>();
    this.defineConstraints = defineConstraints ?? Array.Empty<string>();
  }
}

public readonly struct CompilerMessage
{
  public readonly string file;
  public readonly string message;
  public readonly int line;
  public readonly int column;
  public readonly CompilerMessageType type;

  public CompilerMessage(string file, string message, int line = 0, int column = 0, CompilerMessageType type = CompilerMessageType.Error)
  {
    this.file = file;
    this.message = message;
    this.line = line;
    this.column = column;
    this.type = type;
  }
}

public class CompilationResult
{
  public Assembly[] assemblies;
  public CompilerMessage[] errors;
  public CompilerMessage[] warnings;
  public string output;

  public CompilationResult()
  {
    assemblies = Array.Empty<Assembly>();
    errors = Array.Empty<CompilerMessage>();
    warnings = Array.Empty<CompilerMessage>();
    output = string.Empty;
  }

  public CompilationResult(Assembly[] assemblies, CompilerMessage[] errors, CompilerMessage[] warnings, string output)
  {
    this.assemblies = assemblies;
    this.errors = errors;
    this.warnings = warnings;
    this.output = output;
  }
}

public class AssemblyDefinition
{
  public string name;
  public string rootNamespace;
  public string[] references;
  public string[] includePlatforms;
  public string[] excludePlatforms;
  public bool allowUnsafeCode;
  public bool overrideReferences;
  public string[] precompiledReferences;
  public bool autoReferenced;
  public string[] defineConstraints;
  public string[] versionDefines;
  public bool noEngineReferences;

  public AssemblyDefinition()
  {
    name = string.Empty;
    rootNamespace = string.Empty;
    references = Array.Empty<string>();
    includePlatforms = Array.Empty<string>();
    excludePlatforms = Array.Empty<string>();
    allowUnsafeCode = false;
    overrideReferences = false;
    precompiledReferences = Array.Empty<string>();
    autoReferenced = true;
    defineConstraints = Array.Empty<string>();
    versionDefines = Array.Empty<string>();
    noEngineReferences = false;
  }
}

public enum CompilerMessageType
{
  Info,
  Warning,
  Error
}

public enum AssemblyFlags
{
  None = 0,
  EditorAssembly = 1,
  PlayerAssembly = 2,
  UnityModule = 4
}

[Flags]
public enum RequestScriptCompilationOptions
{
  None = 0,
  CompileScripts = 1,
  RecompileAfterUndoReversion = 2,
  RecompileOnFocusChange = 4
}
