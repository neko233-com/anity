using System;
using System.Collections.Generic;

namespace UnityEditor;

/// <summary>
/// Unity Assembly Definition asset for defining assembly dependencies and compilation settings.
/// </summary>
[Serializable]
public class AssemblyDefinition
{
    public string name = string.Empty;
    public string rootNamespace = string.Empty;
    public string[] references = Array.Empty<string>();
    public string[] includePlatforms = Array.Empty<string>();
    public string[] excludePlatforms = Array.Empty<string>();
    public bool allowUnsafeCode = false;
    public bool overrideReferences = false;
    public string[] precompiledReferences = Array.Empty<string>();
    public bool autoReferenced = true;
    public int defineConstraints = 0;
    public string[] versionDefines = Array.Empty<string>();
    public bool noEngineReferences = false;

    public AssemblyDefinition()
    {
    }

    public AssemblyDefinition(string name)
    {
        this.name = name;
    }

    public AssemblyDefinition(string name, string[] references)
    {
        this.name = name;
        this.references = references;
    }
}

/// <summary>
/// Assembly Definition Reference asset for referencing other assemblies.
/// </summary>
[Serializable]
public class AssemblyDefinitionReference
{
    public string reference = string.Empty;

    public AssemblyDefinitionReference()
    {
    }

    public AssemblyDefinitionReference(string reference)
    {
        this.reference = reference;
    }
}

/// <summary>
/// Compilation Pipeline for managing script compilation.
/// </summary>
public static class CompilationPipeline
{
    public static string[] GetAssemblies(AssembliesType assembliesType = AssembliesType.Editor)
    {
        return Array.Empty<string>();
    }

    public static string[] GetReferencedAssemblies(string assemblyName, AssembliesType assembliesType = AssembliesType.Editor)
    {
        return Array.Empty<string>();
    }

    public static string GetAssemblyDefinitionFilePathFromAssemblyName(string assemblyName)
    {
        return string.Empty;
    }

    public static string GetAssemblyNameFromAssemblyDefinitionFilePath(string asmdefPath)
    {
        return string.Empty;
    }

    public static string[] GetAssemblyDefinitionFilesFromAssemblyName(string assemblyName, AssembliesType assembliesType = AssembliesType.Editor)
    {
        return Array.Empty<string>();
    }

    public static CompilationResult CompileAssemblyDefinitions(string[] assemblyDefinitionFiles, bool editorScripts, string[] extraScriptingDefines)
    {
        return new CompilationResult();
    }

    public static CompilationResult CompileAssemblyDefinitions(string[] assemblyDefinitionFiles, bool editorScripts, string[] extraScriptingDefines, bool shouldIncludePlatformAssemblyReferences)
    {
        return new CompilationResult();
    }

    public static event Action<string, CompilationResult> compilationFinished;

    public static void RequestScriptCompilation(RequestScriptCompilationOptions options = RequestScriptCompilationOptions.CleanBuildCache)
    {
    }
}

/// <summary>
/// Assemblies type for compilation pipeline.
/// </summary>
public enum AssembliesType
{
    Editor,
    Runtime
}

/// <summary>
/// Request script compilation options.
/// </summary>
[Flags]
public enum RequestScriptCompilationOptions
{
    None = 0,
    CleanBuildCache = 1,
    ForceAnalysis = 2
}

/// <summary>
/// Compilation result.
/// </summary>
public class CompilationResult
{
    public string[] assemblyNames = Array.Empty<string>();
    public CompilerMessage[] messages = Array.Empty<CompilerMessage>();
    public bool hadCompilerError { get; set; }
}

/// <summary>
/// Compiler message.
/// </summary>
public struct CompilerMessage
{
    public CompilerMessageType type;
    public string message;
    public string file;
    public int line;
    public int column;
}

/// <summary>
/// Compiler message type.
/// </summary>
public enum CompilerMessageType
{
    Error,
    Warning,
    Info
}
