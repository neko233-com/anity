using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Anity.Agent;

public enum AgentToolAuditPhase
{
    Requested = 0,
    Completed = 1
}

public enum AgentToolAuditOutcome
{
    Pending = 0,
    Succeeded = 1,
    Denied = 2,
    InvalidArguments = 3,
    Unavailable = 4,
    ToolError = 5,
    TimedOut = 6,
    Canceled = 7,
    AuthorizationError = 8
}

public enum AgentAuditFailureMode
{
    Continue = 0,
    FailClosed = 1
}

/// <summary>
/// Redacted tool audit event. Raw arguments and results are deliberately absent; only
/// their UTF-8 sizes and the arguments SHA-256 digest are retained.
/// </summary>
public sealed class AgentToolAuditEvent
{
    public DateTimeOffset TimestampUtc { get; }
    public AgentToolAuditPhase Phase { get; }
    public AgentToolAuditOutcome Outcome { get; }
    public string SessionId { get; }
    public string ToolCallId { get; }
    public string ToolName { get; }
    public string ArgumentsSha256 { get; }
    public int ArgumentsBytes { get; }
    public int ResultBytes { get; }
    public long DurationMilliseconds { get; }

    public AgentToolAuditEvent(
        DateTimeOffset timestampUtc,
        AgentToolAuditPhase phase,
        AgentToolAuditOutcome outcome,
        string sessionId,
        string toolCallId,
        string toolName,
        string argumentsSha256,
        int argumentsBytes,
        int resultBytes,
        long durationMilliseconds)
    {
        if (timestampUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("Audit timestamp must be UTC.", nameof(timestampUtc));
        if (!Enum.IsDefined(typeof(AgentToolAuditPhase), phase))
            throw new ArgumentOutOfRangeException(nameof(phase));
        if (!Enum.IsDefined(typeof(AgentToolAuditOutcome), outcome))
            throw new ArgumentOutOfRangeException(nameof(outcome));
        if (phase == AgentToolAuditPhase.Requested && outcome != AgentToolAuditOutcome.Pending)
            throw new ArgumentException("Requested audit events must use the Pending outcome.", nameof(outcome));
        if (phase == AgentToolAuditPhase.Completed && outcome == AgentToolAuditOutcome.Pending)
            throw new ArgumentException("Completed audit events require a terminal outcome.", nameof(outcome));
        SessionId = ValidateIdentifier(sessionId, 128, nameof(sessionId));
        ToolCallId = ValidateIdentifier(toolCallId, 128, nameof(toolCallId));
        ToolName = ValidateIdentifier(toolName, 64, nameof(toolName));
        if (argumentsSha256 is null || argumentsSha256.Length != 64)
            throw new ArgumentException("Arguments SHA-256 must contain 64 lowercase hexadecimal characters.", nameof(argumentsSha256));
        foreach (char character in argumentsSha256)
            if (!(character is >= '0' and <= '9' or >= 'a' and <= 'f'))
                throw new ArgumentException("Arguments SHA-256 must contain lowercase hexadecimal characters.", nameof(argumentsSha256));
        if (argumentsBytes < 0 || argumentsBytes > 64 * 1024)
            throw new ArgumentOutOfRangeException(nameof(argumentsBytes));
        if (resultBytes < 0 || resultBytes > 4 * 1024 * 1024)
            throw new ArgumentOutOfRangeException(nameof(resultBytes));
        if (durationMilliseconds < 0 || durationMilliseconds > 10 * 60 * 1000L)
            throw new ArgumentOutOfRangeException(nameof(durationMilliseconds));
        TimestampUtc = timestampUtc;
        Phase = phase;
        Outcome = outcome;
        ArgumentsSha256 = argumentsSha256;
        ArgumentsBytes = argumentsBytes;
        ResultBytes = resultBytes;
        DurationMilliseconds = durationMilliseconds;
    }

    public static AgentToolAuditEvent Create(
        AgentToolAuditPhase phase,
        AgentToolAuditOutcome outcome,
        AgentSession session,
        AgentToolCall call,
        int resultBytes = 0,
        long durationMilliseconds = 0)
    {
        if (session is null) throw new ArgumentNullException(nameof(session));
        if (call is null) throw new ArgumentNullException(nameof(call));
        byte[] arguments = Encoding.UTF8.GetBytes(call.ArgumentsJson);
        try
        {
            using SHA256 sha256 = SHA256.Create();
            string digest = ToLowerHex(sha256.ComputeHash(arguments));
            return new AgentToolAuditEvent(
                DateTimeOffset.UtcNow, phase, outcome, session.Id,
                call.Id, call.Name, digest, arguments.Length,
                resultBytes, durationMilliseconds);
        }
        finally
        {
            Array.Clear(arguments, 0, arguments.Length);
        }
    }

    private static string ValidateIdentifier(string value, int maxLength, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Audit identifier is required.", parameterName);
        if (value.Length > maxLength)
            throw new ArgumentOutOfRangeException(parameterName,
                $"Audit identifier must be at most {maxLength} characters.");
        foreach (char character in value)
            if (char.IsControl(character))
                throw new ArgumentException("Audit identifiers cannot contain control characters.", parameterName);
        return value;
    }

    internal static string ToLowerHex(byte[] bytes)
    {
        const string alphabet = "0123456789abcdef";
        var characters = new char[bytes.Length * 2];
        for (int index = 0; index < bytes.Length; index++)
        {
            characters[index * 2] = alphabet[bytes[index] >> 4];
            characters[index * 2 + 1] = alphabet[bytes[index] & 15];
        }
        return new string(characters);
    }
}

public interface IAgentToolAuditSink
{
    Task WriteAsync(
        AgentToolAuditEvent auditEvent,
        CancellationToken cancellationToken = default);
}

public sealed class AgentAuditException : Exception
{
    public AgentAuditException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}

internal sealed class NullAgentToolAuditSink : IAgentToolAuditSink
{
    public static NullAgentToolAuditSink Instance { get; } = new();

    public Task WriteAsync(
        AgentToolAuditEvent auditEvent,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }
}
