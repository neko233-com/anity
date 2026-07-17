using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Anity.Agent;
using Anity.Editor.Host.Services.Agent;
using UnityEditor;
using UnityEngine;

namespace Anity.Editor.Host.Services.Windows;

/// <summary>Anity Ultra Agent window backed by the independent Anity.Agent package.</summary>
public sealed class AgentEditorWindow : EditorWindow
{
  private readonly object _stateSync = new();
  private AgentEditorController? _controller;
  private string _baseUrl = "https://api.openai.com/v1";
  private string _model = "gpt-4o-mini";
  private string _credentialId = "default";
  private string _replacementApiKey = string.Empty;
  private string _prompt = string.Empty;
  private string _status = "Disconnected";
  private string _error = string.Empty;
  private AgentToolPermission _defaultPermission = AgentToolPermission.Ask;
  private AgentToolPermission _echoPermission = AgentToolPermission.Allow;
  private AgentToolPermission _systemInfoPermission = AgentToolPermission.Ask;
  private Task? _operation;
  private PendingPermission? _pendingPermission;
  private Vector2 _scroll;

  public static AgentEditorWindow ShowWindow()
  {
    AgentEditorWindow window = GetWindow<AgentEditorWindow>(false, "Anity Agent", true);
    window.minSize = new Vector2(420f, 520f);
    return window;
  }

  protected override void OnEnable()
  {
    titleContent = new GUIContent("Anity Agent");
    minSize = new Vector2(420f, 520f);
    try
    {
      var store = new AgentEditorSettingsStore(Environment.CurrentDirectory);
      _controller = new AgentEditorController(store, AgentCredentialVault.CreateSystemDefault());
      _controller.PermissionPromptAsync = PromptPermissionAsync;
      ReadSettings(_controller.Settings);
      _status = _controller.IsCredentialBackendAvailable
        ? $"Disconnected · {_controller.CredentialBackend}"
        : "Secure credential vault unavailable";
    }
    catch (Exception ex)
    {
      SetError(ex);
    }
  }

  protected override void OnDisable()
  {
    ResolvePendingPermission(false);
    _controller?.Dispose();
    _controller = null;
    _replacementApiKey = string.Empty;
  }

  protected override void OnGUI()
  {
    PollOperation();
    GUILayout.BeginVertical(EditorStyles.inspectorDefaultMargins);
    GUILayout.Label("Anity Ultra Agent", EditorStyles.boldLabel);
    EditorGUILayout.HelpBox(
      "API keys are stored only in the operating-system credential vault. " +
      "ProjectSettings stores Base URL, model, credential id and tool permissions.",
      MessageType.Info);

    _baseUrl = EditorGUILayout.TextField("Base URL", _baseUrl);
    _model = EditorGUILayout.TextField("Model", _model);
    _credentialId = EditorGUILayout.TextField("Credential ID", _credentialId);
    _replacementApiKey = EditorGUILayout.PasswordField(
      "API Key (replace)", _replacementApiKey, '•');

    GUILayout.Space(4);
    GUILayout.Label("Remote tool permissions", EditorStyles.boldLabel);
    _defaultPermission = EditorGUILayout.EnumPopup("Default", _defaultPermission);
    _echoPermission = EditorGUILayout.EnumPopup("echo", _echoPermission);
    _systemInfoPermission = EditorGUILayout.EnumPopup("systeminfo", _systemInfoPermission);

    GUILayout.BeginHorizontal();
    if (GUILayout.Button("Save & Connect"))
      BeginOperation(SaveAndConnectAsync, "Saving secure Agent settings...");
    if (GUILayout.Button("Disconnect"))
    {
      _controller?.Disconnect();
      _status = "Disconnected";
    }
    if (GUILayout.Button("Delete Key"))
      BeginOperation(DeleteCredentialAsync, "Deleting credential...");
    GUILayout.EndHorizontal();

    if (!string.IsNullOrEmpty(_error))
      EditorGUILayout.HelpBox(_error, MessageType.Error);
    else
      EditorGUILayout.HelpBox(_status, MessageType.None);

    DrawPermissionPrompt();
    DrawTranscript();

    _prompt = GUILayout.TextArea(_prompt, GUILayout.MinHeight(64f));
    GUILayout.BeginHorizontal();
    if (GUILayout.Button("Send", GUILayout.Height(28f)))
      BeginOperation(SendAsync, "Agent is working...");
    if (GUILayout.Button("Clear", GUILayout.Height(28f)))
      _controller?.ClearTranscript();
    GUILayout.EndHorizontal();
    GUILayout.EndVertical();
  }

  private void DrawTranscript()
  {
    GUILayout.Space(6);
    GUILayout.Label("Session", EditorStyles.boldLabel);
    _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.MinHeight(180f));
    AgentEditorTranscriptEntry[] entries = _controller?.Transcript.ToArray()
      ?? Array.Empty<AgentEditorTranscriptEntry>();
    if (entries.Length == 0)
      GUILayout.Label("No messages yet", EditorStyles.centeredGreyMiniLabel);
    foreach (AgentEditorTranscriptEntry entry in entries)
    {
      GUILayout.Label(entry.Role.ToString(), EditorStyles.miniBoldLabel);
      GUILayout.Label(entry.Content, EditorStyles.wordWrappedLabel);
      if (entry.Usage is not null)
        GUILayout.Label(
          $"{entry.FinishReason} · {entry.Usage.TotalTokens} tokens " +
          $"({entry.Usage.PromptTokens} prompt / {entry.Usage.CompletionTokens} completion)",
          EditorStyles.miniLabel);
      GUILayout.Space(4);
    }
    string partial = _controller?.PartialResponse ?? string.Empty;
    if (!string.IsNullOrEmpty(partial))
    {
      GUILayout.Label("Assistant · streaming", EditorStyles.miniBoldLabel);
      GUILayout.Label(partial, EditorStyles.wordWrappedLabel);
    }
    GUILayout.EndScrollView();
  }

  private void DrawPermissionPrompt()
  {
    PendingPermission? pending;
    lock (_stateSync) pending = _pendingPermission;
    if (pending is null) return;
    EditorGUILayout.HelpBox(
      $"The model requests tool '{pending.Request.Call.Name}'.\n" +
      Truncate(pending.Request.Call.ArgumentsJson, 512),
      MessageType.Warning);
    GUILayout.BeginHorizontal();
    if (GUILayout.Button("Allow once")) ResolvePendingPermission(true);
    if (GUILayout.Button("Deny")) ResolvePendingPermission(false);
    GUILayout.EndHorizontal();
  }

  private async Task SaveAndConnectAsync()
  {
    AgentEditorController controller = RequireController();
    AgentEditorSettings settings = BuildSettings();
    string key = _replacementApiKey;
    try
    {
      await controller.SaveAsync(settings, string.IsNullOrEmpty(key) ? null : key)
        .ConfigureAwait(false);
      await controller.ConnectAsync().ConfigureAwait(false);
      lock (_stateSync)
      {
        _replacementApiKey = string.Empty;
        _status = $"Connected · {settings.Connection.Model} · {controller.CredentialBackend}";
      }
    }
    finally
    {
      key = string.Empty;
    }
  }

  private async Task SendAsync()
  {
    AgentEditorController controller = RequireController();
    string prompt;
    lock (_stateSync) prompt = _prompt;
    string response = await controller.SendAsync(prompt).ConfigureAwait(false);
    lock (_stateSync)
    {
      _prompt = string.Empty;
      _status = $"Completed · {response.Length} characters";
    }
  }

  private async Task DeleteCredentialAsync()
  {
    bool deleted = await RequireController().DeleteCredentialAsync().ConfigureAwait(false);
    lock (_stateSync) _status = deleted ? "Credential deleted" : "Credential not found";
  }

  private AgentEditorSettings BuildSettings()
  {
    return new AgentEditorSettings
    {
      Connection = new AgentConnectionProfile
      {
        BaseUrl = _baseUrl,
        Model = _model,
        CredentialId = _credentialId
      },
      DefaultToolPermission = _defaultPermission,
      ToolPermissions = new(StringComparer.OrdinalIgnoreCase)
      {
        ["echo"] = _echoPermission,
        ["systeminfo"] = _systemInfoPermission
      }
    }.Snapshot();
  }

  private void ReadSettings(AgentEditorSettings settings)
  {
    AgentEditorSettings snapshot = settings.Snapshot();
    _baseUrl = snapshot.Connection.BaseUrl;
    _model = snapshot.Connection.Model;
    _credentialId = snapshot.Connection.CredentialId;
    _defaultPermission = snapshot.DefaultToolPermission;
    _echoPermission = snapshot.ToolPermissions.TryGetValue("echo", out AgentToolPermission echo)
      ? echo : snapshot.DefaultToolPermission;
    _systemInfoPermission = snapshot.ToolPermissions.TryGetValue("systeminfo", out AgentToolPermission info)
      ? info : snapshot.DefaultToolPermission;
  }

  private Task<bool> PromptPermissionAsync(
    AgentToolAuthorizationRequest request,
    CancellationToken cancellationToken)
  {
    var pending = new PendingPermission(request);
    lock (_stateSync)
    {
      if (_pendingPermission is not null) return Task.FromResult(false);
      _pendingPermission = pending;
      _status = $"Permission required · {request.Call.Name}";
    }
    cancellationToken.Register(() =>
    {
      lock (_stateSync)
      {
        if (ReferenceEquals(_pendingPermission, pending)) _pendingPermission = null;
      }
      pending.Completion.TrySetCanceled(cancellationToken);
    });
    Repaint();
    return pending.Completion.Task;
  }

  private void ResolvePendingPermission(bool allow)
  {
    PendingPermission? pending;
    lock (_stateSync)
    {
      pending = _pendingPermission;
      _pendingPermission = null;
    }
    pending?.Completion.TrySetResult(allow);
  }

  private void BeginOperation(Func<Task> action, string status)
  {
    if (_operation is { IsCompleted: false }) return;
    _error = string.Empty;
    _status = status;
    try { _operation = action(); }
    catch (Exception ex) { SetError(ex); }
  }

  private void PollOperation()
  {
    Task? operation = _operation;
    if (operation is null || !operation.IsCompleted) return;
    _operation = null;
    if (operation.IsFaulted)
      SetError(operation.Exception?.GetBaseException()
        ?? new InvalidOperationException("Agent editor operation failed."));
    else if (operation.IsCanceled)
      _status = "Operation canceled";
  }

  private AgentEditorController RequireController()
    => _controller ?? throw new InvalidOperationException(
      "The Anity Agent editor controller is unavailable.");

  private void SetError(Exception exception)
  {
    lock (_stateSync)
    {
      _error = Truncate(exception.Message, 1024);
      _status = "Error";
      _replacementApiKey = string.Empty;
    }
  }

  private static string Truncate(string value, int length)
    => string.IsNullOrEmpty(value) || value.Length <= length
      ? value ?? string.Empty
      : value.Substring(0, length);

  private sealed class PendingPermission
  {
    public AgentToolAuthorizationRequest Request { get; }
    public TaskCompletionSource<bool> Completion { get; } =
      new(TaskCreationOptions.RunContinuationsAsynchronously);

    public PendingPermission(AgentToolAuthorizationRequest request) => Request = request;
  }
}
