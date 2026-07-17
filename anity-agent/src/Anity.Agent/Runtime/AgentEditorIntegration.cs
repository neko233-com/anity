using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Anity.Agent;

/// <summary>
/// Non-secret, project-persistable connection profile. The API key is referenced only by
/// CredentialId and must remain in an <see cref="IAgentCredentialVault"/>.
/// </summary>
public sealed class AgentConnectionProfile
{
    public string ProviderId { get; set; } = "openai-compatible";
    public string BaseUrl { get; set; } = "https://api.openai.com/v1";
    public string Model { get; set; } = "gpt-4o-mini";
    public string CredentialId { get; set; } = "default";
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(100);
    public int MaxRetries { get; set; } = 2;
    public int MaxResponseBytes { get; set; } = 4 * 1024 * 1024;

    public AgentConnectionProfile Validate()
    {
        if (!string.Equals(ProviderId, "openai-compatible", StringComparison.Ordinal))
            throw new ArgumentException("Only the openai-compatible provider is currently supported.", nameof(ProviderId));
        _ = AgentCredentialVault.ValidateCredentialId(CredentialId);
        _ = new AgentConnectionOptions
        {
            ApiKey = "profile-validation",
            BaseUrl = BaseUrl,
            Model = Model,
            Timeout = Timeout,
            MaxRetries = MaxRetries,
            MaxResponseBytes = MaxResponseBytes
        }.Validate();
        return this;
    }

    public AgentConnectionProfile Snapshot()
    {
        Validate();
        return new AgentConnectionProfile
        {
            ProviderId = ProviderId,
            BaseUrl = BaseUrl.TrimEnd('/'),
            Model = Model.Trim(),
            CredentialId = CredentialId,
            Timeout = Timeout,
            MaxRetries = MaxRetries,
            MaxResponseBytes = MaxResponseBytes
        };
    }

    public async Task<AgentConnectionOptions> ResolveAsync(
        IAgentCredentialVault vault,
        CancellationToken cancellationToken = default)
    {
        if (vault is null) throw new ArgumentNullException(nameof(vault));
        AgentConnectionProfile profile = Snapshot();
        string? apiKey = await vault.RetrieveAsync(
            profile.CredentialId, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException(
                $"Agent credential '{profile.CredentialId}' was not found in {vault.BackendName}.");
        return new AgentConnectionOptions
        {
            ApiKey = apiKey,
            BaseUrl = profile.BaseUrl,
            Model = profile.Model,
            Timeout = profile.Timeout,
            MaxRetries = profile.MaxRetries,
            MaxResponseBytes = profile.MaxResponseBytes
        }.Snapshot();
    }

    public override string ToString()
        => $"Provider={ProviderId}; BaseUrl={BaseUrl}; Model={Model}; CredentialId={CredentialId}; ApiKey=***";
}

public enum AgentToolPermission
{
    Deny = 0,
    Ask = 1,
    Allow = 2
}

public sealed class AgentToolAuthorizationRequest
{
    public AgentToolCall Call { get; }
    public AgentSession Session { get; }

    public AgentToolAuthorizationRequest(AgentToolCall call, AgentSession session)
    {
        Call = call ?? throw new ArgumentNullException(nameof(call));
        Session = session ?? throw new ArgumentNullException(nameof(session));
    }
}

public interface IAgentToolAuthorizationPolicy
{
    Task<bool> AuthorizeAsync(
        AgentToolAuthorizationRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Thread-safe per-tool permission policy. Ask fails closed when no prompt callback is
/// installed, so headless runs cannot silently approve a model action.
/// </summary>
public sealed class AgentToolPermissionPolicy : IAgentToolAuthorizationPolicy
{
    private readonly ConcurrentDictionary<string, AgentToolPermission> _permissions =
        new(StringComparer.OrdinalIgnoreCase);
    private AgentToolPermission _defaultPermission;

    public AgentToolPermission DefaultPermission
    {
        get => _defaultPermission;
        set
        {
            if (!Enum.IsDefined(typeof(AgentToolPermission), value))
                throw new ArgumentOutOfRangeException(nameof(value));
            _defaultPermission = value;
        }
    }

    public Func<AgentToolAuthorizationRequest, CancellationToken, Task<bool>>? PromptAsync { get; set; }

    public AgentToolPermissionPolicy(AgentToolPermission defaultPermission = AgentToolPermission.Deny)
    {
        DefaultPermission = defaultPermission;
    }

    public void SetPermission(string toolName, AgentToolPermission permission)
    {
        ValidateToolName(toolName);
        if (!Enum.IsDefined(typeof(AgentToolPermission), permission))
            throw new ArgumentOutOfRangeException(nameof(permission));
        _permissions[toolName] = permission;
    }

    public bool RemovePermission(string toolName)
    {
        ValidateToolName(toolName);
        return _permissions.TryRemove(toolName, out _);
    }

    public AgentToolPermission GetPermission(string toolName)
    {
        ValidateToolName(toolName);
        return _permissions.TryGetValue(toolName, out AgentToolPermission permission)
            ? permission
            : DefaultPermission;
    }

    public IReadOnlyDictionary<string, AgentToolPermission> Snapshot()
        => _permissions.OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);

    public async Task<bool> AuthorizeAsync(
        AgentToolAuthorizationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null) throw new ArgumentNullException(nameof(request));
        cancellationToken.ThrowIfCancellationRequested();
        switch (GetPermission(request.Call.Name))
        {
            case AgentToolPermission.Allow:
                return true;
            case AgentToolPermission.Deny:
                return false;
            case AgentToolPermission.Ask:
                if (PromptAsync is null) return false;
                return await PromptAsync(request, cancellationToken).ConfigureAwait(false);
            default:
                return false;
        }
    }

    private static void ValidateToolName(string toolName)
    {
        if (string.IsNullOrWhiteSpace(toolName))
            throw new ArgumentException("Tool name is required.", nameof(toolName));
        _ = new AgentToolDefinition(toolName, string.Empty, "{}");
    }
}

internal sealed class AllowAllAgentToolAuthorizationPolicy : IAgentToolAuthorizationPolicy
{
    public static AllowAllAgentToolAuthorizationPolicy Instance { get; } = new();

    public Task<bool> AuthorizeAsync(
        AgentToolAuthorizationRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(true);
    }
}
