using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Anity.Agent;

namespace Anity.Editor.Host.Services.Agent;

public sealed class AgentToolAuditVerificationResult
{
  public long RecordCount { get; }
  public long FirstSequence { get; }
  public long LastSequence { get; }
  public string LastHash { get; }
  public int FileCount { get; }

  public AgentToolAuditVerificationResult(
    long recordCount, long firstSequence, long lastSequence,
    string lastHash, int fileCount)
  {
    RecordCount = recordCount;
    FirstSequence = firstSequence;
    LastSequence = lastSequence;
    LastHash = lastHash ?? string.Empty;
    FileCount = fileCount;
  }
}

/// <summary>
/// Bounded append-only JSONL audit with a SHA-256 chain across rotated files. The payload
/// contains only redacted <see cref="AgentToolAuditEvent"/> fields.
/// </summary>
public sealed class HashChainedAgentToolAuditLog : IAgentToolAuditSink, IDisposable
{
  private const int MaxLineBytes = 16 * 1024;
  private readonly SemaphoreSlim _gate = new(1, 1);
  private readonly FileStream _processLock;
  private readonly int _maxFileBytes;
  private readonly int _maxArchiveFiles;
  private long _nextSequence;
  private string _previousHash = new('0', 64);
  private int _disposed;

  public string FilePath { get; }
  public int MaxFileBytes => _maxFileBytes;
  public int MaxArchiveFiles => _maxArchiveFiles;

  public HashChainedAgentToolAuditLog(
    string projectPath,
    int maxFileBytes = 4 * 1024 * 1024,
    int maxArchiveFiles = 8)
  {
    if (string.IsNullOrWhiteSpace(projectPath))
      throw new ArgumentException("Project path is required.", nameof(projectPath));
    if (maxFileBytes < 1024 || maxFileBytes > 64 * 1024 * 1024)
      throw new ArgumentOutOfRangeException(nameof(maxFileBytes));
    if (maxArchiveFiles < 1 || maxArchiveFiles > 32)
      throw new ArgumentOutOfRangeException(nameof(maxArchiveFiles));
    _maxFileBytes = maxFileBytes;
    _maxArchiveFiles = maxArchiveFiles;
    string root = Path.GetFullPath(projectPath);
    string directory = Path.Combine(root, "Library", "AnityAgent", "Audit");
    Directory.CreateDirectory(directory);
    RestrictUnixPermissions(directory, 448); // 0700
    FilePath = Path.Combine(directory, "tool-audit.jsonl");
    string lockPath = Path.Combine(directory, "tool-audit.lock");
    FileStream? processLock = null;
    try
    {
      processLock = new FileStream(
        lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None,
        1, FileOptions.WriteThrough);
      RestrictUnixPermissions(lockPath, 384); // 0600
      _processLock = processLock;
      AgentToolAuditVerificationResult state = VerifyFiles();
      _nextSequence = state.RecordCount == 0 ? 1 : checked(state.LastSequence + 1);
      if (state.RecordCount > 0) _previousHash = state.LastHash;
    }
    catch
    {
      processLock?.Dispose();
      throw;
    }
  }

  public async Task WriteAsync(
    AgentToolAuditEvent auditEvent,
    CancellationToken cancellationToken = default)
  {
    if (auditEvent is null) throw new ArgumentNullException(nameof(auditEvent));
    ThrowIfDisposed();
    await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
    try
    {
      ThrowIfDisposed();
      cancellationToken.ThrowIfCancellationRequested();
      byte[] payload = SerializePayload(_nextSequence, _previousHash, auditEvent);
      byte[] hashBytes;
      using (SHA256 sha256 = SHA256.Create()) hashBytes = sha256.ComputeHash(payload);
      string hash = ToLowerHex(hashBytes);
      Array.Clear(hashBytes, 0, hashBytes.Length);
      byte[] line = SerializeEnvelope(payload, hash);
      Array.Clear(payload, 0, payload.Length);
      if (line.Length > MaxLineBytes)
        throw new InvalidDataException("Agent tool audit record exceeds 16 KiB.");

      try
      {
        RotateIfNeeded(line.Length);
        using var stream = new FileStream(
          FilePath, FileMode.Append, FileAccess.Write, FileShare.Read,
          4096, FileOptions.WriteThrough);
        RestrictUnixPermissions(FilePath, 384); // 0600
        stream.Write(line, 0, line.Length);
        stream.Flush(true);
        _previousHash = hash;
        _nextSequence = checked(_nextSequence + 1);
      }
      finally
      {
        Array.Clear(line, 0, line.Length);
      }
    }
    finally
    {
      _gate.Release();
    }
  }

  public async Task<AgentToolAuditVerificationResult> VerifyAsync(
    CancellationToken cancellationToken = default)
  {
    ThrowIfDisposed();
    await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
    try
    {
      ThrowIfDisposed();
      cancellationToken.ThrowIfCancellationRequested();
      return VerifyFiles();
    }
    finally
    {
      _gate.Release();
    }
  }

  public void Dispose()
  {
    if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
    _gate.Wait();
    try
    {
      _processLock.Dispose();
    }
    finally
    {
      _gate.Release();
      _gate.Dispose();
    }
  }

  private void RotateIfNeeded(int incomingBytes)
  {
    if (!File.Exists(FilePath)) return;
    long currentLength = new FileInfo(FilePath).Length;
    if (currentLength == 0 || currentLength + incomingBytes <= _maxFileBytes) return;

    string oldest = ArchivePath(_maxArchiveFiles);
    if (File.Exists(oldest)) File.Delete(oldest);
    for (int index = _maxArchiveFiles - 1; index >= 1; index--)
    {
      string source = ArchivePath(index);
      if (!File.Exists(source)) continue;
      File.Move(source, ArchivePath(index + 1));
    }
    File.Move(FilePath, ArchivePath(1));
  }

  private AgentToolAuditVerificationResult VerifyFiles()
  {
    List<string> paths = ExistingPathsOldestFirst();
    long count = 0;
    long firstSequence = 0;
    long lastSequence = 0;
    string lastHash = string.Empty;

    foreach (string path in paths)
    {
      using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
      using var reader = new StreamReader(
        stream, new UTF8Encoding(false, true), false, 4096, false);
      while (reader.ReadLine() is { } line)
      {
        if (string.IsNullOrWhiteSpace(line))
          throw new InvalidDataException("Agent audit contains an empty record.");
        if (Encoding.UTF8.GetByteCount(line) > MaxLineBytes)
          throw new InvalidDataException("Agent audit record exceeds 16 KiB.");
        VerifiedEnvelope envelope = VerifyLine(line);
        if (count > 0)
        {
          if (envelope.Sequence != checked(lastSequence + 1))
            throw new InvalidDataException("Agent audit sequence is not contiguous.");
          if (!string.Equals(envelope.PreviousHash, lastHash, StringComparison.Ordinal))
            throw new InvalidDataException("Agent audit hash chain is broken.");
        }
        else
        {
          firstSequence = envelope.Sequence;
        }
        count++;
        lastSequence = envelope.Sequence;
        lastHash = envelope.Hash;
      }
    }

    return new AgentToolAuditVerificationResult(
      count, firstSequence, lastSequence, lastHash, paths.Count);
  }

  private List<string> ExistingPathsOldestFirst()
  {
    var result = new List<string>();
    bool foundArchive = false;
    for (int index = _maxArchiveFiles; index >= 1; index--)
    {
      string path = ArchivePath(index);
      if (!File.Exists(path))
      {
        if (foundArchive)
          throw new InvalidDataException("Agent audit archive sequence contains a gap.");
        continue;
      }
      foundArchive = true;
      result.Add(path);
    }
    if (File.Exists(FilePath)) result.Add(FilePath);
    else if (result.Count > 0)
      throw new InvalidDataException("Agent audit current file is missing.");
    return result;
  }

  private static VerifiedEnvelope VerifyLine(string line)
  {
    try
    {
      using JsonDocument document = JsonDocument.Parse(
        line, new JsonDocumentOptions { MaxDepth = 8 });
      JsonElement root = document.RootElement;
      if (root.ValueKind != JsonValueKind.Object
        || root.EnumerateObject().Count() != 2
        || !root.TryGetProperty("payload", out JsonElement payload)
        || !root.TryGetProperty("hash", out JsonElement hashElement))
        throw new InvalidDataException("Agent audit envelope is invalid.");
      string hash = hashElement.GetString() ?? string.Empty;
      ValidateHash(hash, "hash");
      byte[] payloadBytes = Encoding.UTF8.GetBytes(payload.GetRawText());
      try
      {
        byte[] computedBytes;
        using (SHA256 sha256 = SHA256.Create())
          computedBytes = sha256.ComputeHash(payloadBytes);
        string computed = ToLowerHex(computedBytes);
        Array.Clear(computedBytes, 0, computedBytes.Length);
        if (!string.Equals(computed, hash, StringComparison.Ordinal))
          throw new InvalidDataException("Agent audit record hash does not match its payload.");
      }
      finally
      {
        Array.Clear(payloadBytes, 0, payloadBytes.Length);
      }

      if (payload.ValueKind != JsonValueKind.Object
        || payload.EnumerateObject().Count() != 13)
        throw new InvalidDataException("Agent audit payload shape is invalid.");
      int version = RequiredInt32(payload, "version");
      if (version != 1) throw new InvalidDataException("Agent audit version is unsupported.");
      long sequence = RequiredInt64(payload, "sequence");
      if (sequence <= 0) throw new InvalidDataException("Agent audit sequence must be positive.");
      string previousHash = RequiredString(payload, "previousHash");
      ValidateHash(previousHash, "previousHash");
      string timestampText = RequiredString(payload, "timestampUtc");
      if (!DateTimeOffset.TryParseExact(
        timestampText, "O", CultureInfo.InvariantCulture,
        DateTimeStyles.RoundtripKind, out DateTimeOffset timestamp)
        || timestamp.Offset != TimeSpan.Zero)
        throw new InvalidDataException("Agent audit UTC timestamp is invalid.");
      if (!Enum.TryParse(RequiredString(payload, "phase"), false, out AgentToolAuditPhase phase)
        || !Enum.IsDefined(typeof(AgentToolAuditPhase), phase))
        throw new InvalidDataException("Agent audit phase is invalid.");
      if (!Enum.TryParse(RequiredString(payload, "outcome"), false, out AgentToolAuditOutcome outcome)
        || !Enum.IsDefined(typeof(AgentToolAuditOutcome), outcome))
        throw new InvalidDataException("Agent audit outcome is invalid.");
      _ = new AgentToolAuditEvent(
        timestamp, phase, outcome,
        RequiredString(payload, "sessionId"),
        RequiredString(payload, "toolCallId"),
        RequiredString(payload, "toolName"),
        RequiredString(payload, "argumentsSha256"),
        RequiredInt32(payload, "argumentsBytes"),
        RequiredInt32(payload, "resultBytes"),
        RequiredInt64(payload, "durationMilliseconds"));
      return new VerifiedEnvelope(sequence, previousHash, hash);
    }
    catch (JsonException ex)
    {
      throw new InvalidDataException("Agent audit contains invalid JSON.", ex);
    }
  }

  private static byte[] SerializePayload(
    long sequence, string previousHash, AgentToolAuditEvent auditEvent)
  {
    using var stream = new MemoryStream();
    using (var writer = new Utf8JsonWriter(stream))
    {
      writer.WriteStartObject();
      writer.WriteNumber("version", 1);
      writer.WriteNumber("sequence", sequence);
      writer.WriteString("previousHash", previousHash);
      writer.WriteString("timestampUtc", auditEvent.TimestampUtc.ToString("O", CultureInfo.InvariantCulture));
      writer.WriteString("phase", auditEvent.Phase.ToString());
      writer.WriteString("outcome", auditEvent.Outcome.ToString());
      writer.WriteString("sessionId", auditEvent.SessionId);
      writer.WriteString("toolCallId", auditEvent.ToolCallId);
      writer.WriteString("toolName", auditEvent.ToolName);
      writer.WriteString("argumentsSha256", auditEvent.ArgumentsSha256);
      writer.WriteNumber("argumentsBytes", auditEvent.ArgumentsBytes);
      writer.WriteNumber("resultBytes", auditEvent.ResultBytes);
      writer.WriteNumber("durationMilliseconds", auditEvent.DurationMilliseconds);
      writer.WriteEndObject();
    }
    return stream.ToArray();
  }

  private static byte[] SerializeEnvelope(byte[] payload, string hash)
  {
    using var stream = new MemoryStream();
    using (var writer = new Utf8JsonWriter(stream))
    {
      writer.WriteStartObject();
      writer.WritePropertyName("payload");
      writer.WriteRawValue(payload, true);
      writer.WriteString("hash", hash);
      writer.WriteEndObject();
    }
    stream.WriteByte((byte)'\n');
    return stream.ToArray();
  }

  private static string RequiredString(JsonElement element, string name)
  {
    if (!element.TryGetProperty(name, out JsonElement value)
      || value.ValueKind != JsonValueKind.String)
      throw new InvalidDataException($"Agent audit field '{name}' is missing or invalid.");
    return value.GetString() ?? string.Empty;
  }

  private static int RequiredInt32(JsonElement element, string name)
  {
    if (!element.TryGetProperty(name, out JsonElement value)
      || !value.TryGetInt32(out int result))
      throw new InvalidDataException($"Agent audit field '{name}' is missing or invalid.");
    return result;
  }

  private static long RequiredInt64(JsonElement element, string name)
  {
    if (!element.TryGetProperty(name, out JsonElement value)
      || !value.TryGetInt64(out long result))
      throw new InvalidDataException($"Agent audit field '{name}' is missing or invalid.");
    return result;
  }

  private string ArchivePath(int index) => FilePath + "." + index.ToString(CultureInfo.InvariantCulture);

  private static void ValidateHash(string value, string name)
  {
    if (value.Length != 64 || value.Any(character =>
      !(character is >= '0' and <= '9' or >= 'a' and <= 'f')))
      throw new InvalidDataException($"Agent audit {name} is not a lowercase SHA-256 digest.");
  }

  private static string ToLowerHex(byte[] bytes)
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

  private static void RestrictUnixPermissions(string path, uint mode)
  {
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;
    if (chmod(path, mode) != 0)
      throw new UnauthorizedAccessException(
        $"Unable to restrict Agent audit permissions for '{path}'.");
  }

  private void ThrowIfDisposed()
  {
    if (Volatile.Read(ref _disposed) != 0)
      throw new ObjectDisposedException(nameof(HashChainedAgentToolAuditLog));
  }

  [DllImport("libc", SetLastError = true)]
  private static extern int chmod(string path, uint mode);

  private readonly struct VerifiedEnvelope
  {
    public long Sequence { get; }
    public string PreviousHash { get; }
    public string Hash { get; }

    public VerifiedEnvelope(long sequence, string previousHash, string hash)
    {
      Sequence = sequence;
      PreviousHash = previousHash;
      Hash = hash;
    }
  }
}
