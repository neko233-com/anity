using System;
using System.Collections.Generic;
using System.Threading;

namespace UnityEditor.Compilation;

public sealed class AssemblyBuilder
{
  public event Action<string, CompilerMessage[]>? buildStarted;
  public event Action<string, CompilerMessage[]>? buildFinished;
  public event Action<string, CompilerMessage[]>? compilationFinished;

  public string? targetAssemblyPath { get; private set; }
  public string? buildTarget { get; set; }
  public bool builtInCompilerFlags { get; set; }
  public string? additionalDefines { get; set; }
  public string[]? noWarn { get; set; }
  public string[]? warningsAsErrors { get; set; }
  public string[]? references { get; set; }
  public string[]? sourceFiles { get; set; }
  public string[]? defines { get; set; }

  public string[] References
  {
    get => references ?? Array.Empty<string>();
    set => references = value;
  }

  public string[] DefineConstraints
  {
    get => _defineConstraints;
    set => _defineConstraints = value;
  }

  public PlatformArchitecture PlatformArchitecture { get; set; }

  public string OutputPath
  {
    get => targetAssemblyPath ?? string.Empty;
    set => targetAssemblyPath = value;
  }

  private bool _isDone;
  private readonly ManualResetEventSlim _waitHandle = new(false);
  private readonly List<CompilerMessage> _messages = new();
  private string[] _defineConstraints = Array.Empty<string>();

  public AssemblyBuilder(string assemblyName, string outputFolder)
  {
    _ = assemblyName;
    targetAssemblyPath = outputFolder;
  }

  public bool Build()
  {
    _isDone = true;
    var messages = Array.Empty<CompilerMessage>();
    buildStarted?.Invoke(targetAssemblyPath ?? string.Empty, messages);
    buildFinished?.Invoke(targetAssemblyPath ?? string.Empty, messages);
    compilationFinished?.Invoke(targetAssemblyPath ?? string.Empty, messages);
    _waitHandle.Set();
    return true;
  }

  public void BuildAsync()
  {
    _ = ThreadPool.QueueUserWorkItem(_ => Build());
  }

  public void AddCompilerMessage(CompilerMessage message)
  {
    _messages.Add(message);
  }

  public CompilerMessage[] GetCompilerMessages()
  {
    return _messages.ToArray();
  }

  public bool Build(string[] sourceFiles, string outputFolder)
  {
    this.sourceFiles = sourceFiles;
    targetAssemblyPath = outputFolder;
    return Build();
  }

  public void SetProjectReference(string assemblyName, string? reference)
  {
    _ = assemblyName;
    _ = reference;
  }

  public void AddDependency(string filePath, string? assemblyName)
  {
    _ = filePath;
    _ = assemblyName;
  }

  public bool isDone => _isDone;

  public void Done()
  {
    _isDone = true;
    _waitHandle.Set();
  }

  public void WaitForCompletion()
  {
    _waitHandle.Wait();
  }
}

public enum PlatformArchitecture
{
  Any = -1,
  x86 = 0,
  x86_64 = 1,
  ARM = 2,
  ARM64 = 3
}
