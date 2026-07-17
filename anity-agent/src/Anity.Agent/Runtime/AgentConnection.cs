using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Anity.Agent;

/// <summary>Connection settings for an OpenAI-compatible chat-completions endpoint.</summary>
public sealed class AgentConnectionOptions
{
    public const string ApiKeyEnvironmentVariable = "ANITY_AGENT_API_KEY";
    public const string BaseUrlEnvironmentVariable = "ANITY_AGENT_BASE_URL";
    public const string ModelEnvironmentVariable = "ANITY_AGENT_MODEL";

    public string ApiKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://api.openai.com/v1";
    public string Model { get; set; } = "gpt-4o-mini";
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(100);
    public int MaxRetries { get; set; } = 2;
    public int MaxResponseBytes { get; set; } = 4 * 1024 * 1024;

    public static AgentConnectionOptions FromEnvironment(Func<string, string?>? read = null)
    {
        read ??= Environment.GetEnvironmentVariable;
        return new AgentConnectionOptions
        {
            ApiKey = read(ApiKeyEnvironmentVariable) ?? string.Empty,
            BaseUrl = read(BaseUrlEnvironmentVariable) ?? "https://api.openai.com/v1",
            Model = read(ModelEnvironmentVariable) ?? "gpt-4o-mini"
        };
    }

    public AgentConnectionOptions Validate()
    {
        if (string.IsNullOrWhiteSpace(ApiKey))
            throw new ArgumentException("An Agent API key is required.", nameof(ApiKey));
        string bearerToken = ApiKey.Trim();
        if (Encoding.UTF8.GetByteCount(bearerToken) > AgentCredentialVault.MaxCredentialBytes)
            throw new ArgumentOutOfRangeException(nameof(ApiKey),
                $"The Agent API key must be at most {AgentCredentialVault.MaxCredentialBytes} UTF-8 bytes.");
        if (!IsValidBearerToken(bearerToken))
            throw new ArgumentException("The Agent API key contains invalid characters.", nameof(ApiKey));
        try
        {
            _ = new AuthenticationHeaderValue("Bearer", bearerToken);
        }
        catch (FormatException ex)
        {
            throw new ArgumentException(
                "The Agent API key cannot be encoded as a Bearer credential.",
                nameof(ApiKey), ex);
        }
        if (string.IsNullOrWhiteSpace(Model))
            throw new ArgumentException("An Agent model is required.", nameof(Model));
        if (!Uri.TryCreate(BaseUrl, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)
            || string.IsNullOrWhiteSpace(uri.Host)
            || !string.IsNullOrEmpty(uri.UserInfo)
            || !string.IsNullOrEmpty(uri.Query)
            || !string.IsNullOrEmpty(uri.Fragment))
            throw new ArgumentException("Agent Base URL must be an absolute HTTP(S) URL without a query or fragment.", nameof(BaseUrl));
        if (Timeout <= TimeSpan.Zero || Timeout > TimeSpan.FromMinutes(10))
            throw new ArgumentOutOfRangeException(nameof(Timeout), "Agent timeout must be between zero and ten minutes.");
        if (MaxRetries < 0 || MaxRetries > 5)
            throw new ArgumentOutOfRangeException(nameof(MaxRetries), "Agent retries must be between zero and five.");
        if (MaxResponseBytes < 1024 || MaxResponseBytes > 64 * 1024 * 1024)
            throw new ArgumentOutOfRangeException(nameof(MaxResponseBytes), "Agent response limit must be between 1 KiB and 64 MiB.");
        return this;
    }

    private static bool IsValidBearerToken(string value)
    {
        if (value.Length == 0) return false;
        bool padding = false;
        foreach (char character in value)
        {
            if (character == '=')
            {
                padding = true;
                continue;
            }
            if (padding) return false;
            bool valid = character is >= 'a' and <= 'z'
                or >= 'A' and <= 'Z'
                or >= '0' and <= '9'
                or '-' or '.' or '_' or '~' or '+' or '/';
            if (!valid) return false;
        }
        return true;
    }

    internal AgentConnectionOptions Snapshot()
    {
        Validate();
        return new AgentConnectionOptions
        {
            ApiKey = ApiKey.Trim(),
            BaseUrl = BaseUrl.TrimEnd('/'),
            Model = Model.Trim(),
            Timeout = Timeout,
            MaxRetries = MaxRetries,
            MaxResponseBytes = MaxResponseBytes
        };
    }

    public override string ToString()
        => $"BaseUrl={BaseUrl}; Model={Model}; ApiKey=***; Timeout={Timeout.TotalSeconds:0.###}s; MaxRetries={MaxRetries}";
}

/// <summary>Pluggable model provider used by Agent sessions.</summary>
public interface IAgentProvider
{
    Task<string> CompleteAsync(IReadOnlyList<AgentMessage> messages, CancellationToken cancellationToken = default);
}

/// <summary>Token accounting reported by a compatible streaming endpoint.</summary>
public sealed class AgentTokenUsage
{
    public int PromptTokens { get; }
    public int CompletionTokens { get; }
    public int TotalTokens { get; }

    public AgentTokenUsage(int promptTokens, int completionTokens, int totalTokens)
    {
        if (promptTokens < 0) throw new ArgumentOutOfRangeException(nameof(promptTokens));
        if (completionTokens < 0) throw new ArgumentOutOfRangeException(nameof(completionTokens));
        if (totalTokens < 0) throw new ArgumentOutOfRangeException(nameof(totalTokens));
        PromptTokens = promptTokens;
        CompletionTokens = completionTokens;
        TotalTokens = totalTokens;
    }
}

public sealed class AgentToolDefinition
{
    public string Name { get; }
    public string Description { get; }
    public string ParametersJsonSchema { get; }

    public AgentToolDefinition(string name, string description, string parametersJsonSchema)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Tool name is required.", nameof(name));
        if (name.Length > 64 || name.Any(character => !IsToolNameCharacter(character)))
            throw new ArgumentException("Tool name must contain only ASCII letters, digits, '_' or '-' and be at most 64 characters.", nameof(name));
        if (string.IsNullOrWhiteSpace(parametersJsonSchema))
            throw new ArgumentException("Tool parameters schema is required.", nameof(parametersJsonSchema));
        try
        {
            using JsonDocument schema = JsonDocument.Parse(parametersJsonSchema);
            if (schema.RootElement.ValueKind != JsonValueKind.Object)
                throw new ArgumentException("Tool parameters schema must be a JSON object.", nameof(parametersJsonSchema));
        }
        catch (JsonException ex)
        {
            throw new ArgumentException("Tool parameters schema must be valid JSON.", nameof(parametersJsonSchema), ex);
        }
        Name = name;
        Description = description ?? string.Empty;
        ParametersJsonSchema = parametersJsonSchema;
    }

    private static bool IsToolNameCharacter(char character)
        => character is >= 'a' and <= 'z'
            or >= 'A' and <= 'Z'
            or >= '0' and <= '9'
            or '_' or '-';
}

public sealed class AgentToolCall
{
    public string Id { get; }
    public string Name { get; }
    public string ArgumentsJson { get; }

    public AgentToolCall(string id, string name, string argumentsJson)
    {
        Id = id ?? string.Empty;
        Name = name ?? string.Empty;
        ArgumentsJson = argumentsJson ?? string.Empty;
    }
}

public sealed class AgentToolCallDelta
{
    public int Index { get; }
    public string IdDelta { get; }
    public string NameDelta { get; }
    public string ArgumentsDelta { get; }

    public AgentToolCallDelta(
        int index, string? idDelta = null, string? nameDelta = null,
        string? argumentsDelta = null)
    {
        if (index < 0) throw new ArgumentOutOfRangeException(nameof(index));
        Index = index;
        IdDelta = idDelta ?? string.Empty;
        NameDelta = nameDelta ?? string.Empty;
        ArgumentsDelta = argumentsDelta ?? string.Empty;
    }
}

public sealed class AgentModelTurn
{
    public string Content { get; }
    public IReadOnlyList<AgentToolCall> ToolCalls { get; }
    public string FinishReason { get; }
    public AgentTokenUsage? Usage { get; }

    public AgentModelTurn(
        string? content, IReadOnlyList<AgentToolCall>? toolCalls = null,
        string? finishReason = null, AgentTokenUsage? usage = null)
    {
        Content = content ?? string.Empty;
        ToolCalls = toolCalls?.ToArray() ?? Array.Empty<AgentToolCall>();
        FinishReason = finishReason ?? string.Empty;
        Usage = usage;
    }
}

/// <summary>One ordered server-sent event from an Agent streaming turn.</summary>
public sealed class AgentStreamUpdate
{
    public string ContentDelta { get; }
    public AgentTokenUsage? Usage { get; }
    public bool IsCompleted { get; }
    public IReadOnlyList<AgentToolCallDelta> ToolCallDeltas { get; }
    public string FinishReason { get; }

    public AgentStreamUpdate(
        string? contentDelta = null, AgentTokenUsage? usage = null,
        bool isCompleted = false,
        IReadOnlyList<AgentToolCallDelta>? toolCallDeltas = null,
        string? finishReason = null)
    {
        ContentDelta = contentDelta ?? string.Empty;
        Usage = usage;
        IsCompleted = isCompleted;
        ToolCallDeltas = toolCallDeltas?.ToArray() ?? Array.Empty<AgentToolCallDelta>();
        FinishReason = finishReason ?? string.Empty;
    }
}

public interface IToolCallingAgentProvider : IStreamingAgentProvider
{
    Task<AgentModelTurn> CompleteWithToolsAsync(
        IReadOnlyList<AgentMessage> messages,
        IReadOnlyList<AgentToolDefinition> tools,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<AgentStreamUpdate> StreamWithToolsAsync(
        IReadOnlyList<AgentMessage> messages,
        IReadOnlyList<AgentToolDefinition> tools,
        CancellationToken cancellationToken = default);
}

/// <summary>Optional incremental extension implemented by streaming providers.</summary>
public interface IStreamingAgentProvider : IAgentProvider
{
    IAsyncEnumerable<AgentStreamUpdate> StreamAsync(
        IReadOnlyList<AgentMessage> messages,
        CancellationToken cancellationToken = default);
}

/// <summary>Failure returned by an Agent model provider. Secrets are removed from its message.</summary>
public sealed class AgentProviderException : Exception
{
    public HttpStatusCode? StatusCode { get; }
    public bool IsTransient { get; }

    public AgentProviderException(string message, HttpStatusCode? statusCode = null, bool isTransient = false, Exception? inner = null)
        : base(message, inner)
    {
        StatusCode = statusCode;
        IsTransient = isTransient;
    }
}

/// <summary>
/// Source-controlled OpenAI-compatible provider. It talks directly to
/// &lt;base-url&gt;/chat/completions and has no dependency on a closed-source SDK.
/// </summary>
public sealed class OpenAiCompatibleAgentProvider : IToolCallingAgentProvider, IDisposable
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly AgentConnectionOptions _options;
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;

    public Uri CompletionEndpoint { get; }
    public string Model => _options.Model;

    public OpenAiCompatibleAgentProvider(AgentConnectionOptions options, HttpClient? httpClient = null)
    {
        if (options == null) throw new ArgumentNullException(nameof(options));
        _options = options.Snapshot();
        CompletionEndpoint = new Uri(_options.BaseUrl + "/chat/completions", UriKind.Absolute);
        _httpClient = httpClient ?? new HttpClient();
        _ownsHttpClient = httpClient == null;
    }

    public async Task<string> CompleteAsync(
        IReadOnlyList<AgentMessage> messages,
        CancellationToken cancellationToken = default)
    {
        AgentModelTurn turn = await CompleteTurnRequestAsync(
            messages, Array.Empty<AgentToolDefinition>(), cancellationToken)
            .ConfigureAwait(false);
        if (turn.ToolCalls.Count > 0)
            throw new AgentProviderException(
                "Agent returned tool calls when no tools were advertised.");
        return turn.Content;
    }

    public Task<AgentModelTurn> CompleteWithToolsAsync(
        IReadOnlyList<AgentMessage> messages,
        IReadOnlyList<AgentToolDefinition> tools,
        CancellationToken cancellationToken = default)
        => CompleteTurnRequestAsync(messages, tools, cancellationToken);

    private async Task<AgentModelTurn> CompleteTurnRequestAsync(
        IReadOnlyList<AgentMessage> messages,
        IReadOnlyList<AgentToolDefinition> tools,
        CancellationToken cancellationToken)
    {
        string json = SerializeRequest(messages, tools, stream: false);

        for (int attempt = 0; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(_options.Timeout);

            try
            {
                using var request = CreateRequest(json);
                using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token)
                    .ConfigureAwait(false);

                if (response.Content.Headers.ContentLength > _options.MaxResponseBytes)
                    throw new AgentProviderException("Agent response exceeded the configured size limit.", response.StatusCode);

                string body = await ReadBodyAsync(response, timeout.Token).ConfigureAwait(false);

                if (response.IsSuccessStatusCode)
                    return ParseModelTurn(body);

                bool transient = IsTransient(response.StatusCode);
                if (transient && attempt < _options.MaxRetries)
                {
                    await Task.Delay(GetRetryDelay(response, attempt), cancellationToken).ConfigureAwait(false);
                    continue;
                }

                throw CreateHttpException(response.StatusCode, body, transient);
            }
            catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                throw new AgentProviderException(
                    $"Agent request timed out after {_options.Timeout.TotalSeconds:0.###} seconds.",
                    null,
                    true,
                    ex);
            }
            catch (HttpRequestException) when (attempt < _options.MaxRetries)
            {
                await Task.Delay(GetRetryDelay(null, attempt), cancellationToken).ConfigureAwait(false);
            }
            catch (HttpRequestException ex)
            {
                throw new AgentProviderException(
                    "Agent endpoint could not be reached: " + Sanitize(ex.Message),
                    null, true);
            }
        }
    }

    public async IAsyncEnumerable<AgentStreamUpdate> StreamAsync(
        IReadOnlyList<AgentMessage> messages,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (AgentStreamUpdate update in StreamTurnRequestAsync(
            messages, Array.Empty<AgentToolDefinition>(), cancellationToken)
            .ConfigureAwait(false))
            yield return update;
    }

    public async IAsyncEnumerable<AgentStreamUpdate> StreamWithToolsAsync(
        IReadOnlyList<AgentMessage> messages,
        IReadOnlyList<AgentToolDefinition> tools,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (AgentStreamUpdate update in StreamTurnRequestAsync(
            messages, tools, cancellationToken).ConfigureAwait(false))
            yield return update;
    }

    private async IAsyncEnumerable<AgentStreamUpdate> StreamTurnRequestAsync(
        IReadOnlyList<AgentMessage> messages,
        IReadOnlyList<AgentToolDefinition> tools,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        string json = SerializeRequest(messages, tools, stream: true);
        using StreamingResponseLease lease = await OpenStreamingResponseAsync(
            json, cancellationToken).ConfigureAwait(false);
        bool completed = false;
        await foreach (string data in ReadSseDataAsync(
            lease.Response, lease.TimeoutToken, cancellationToken).ConfigureAwait(false))
        {
            string normalized = data.Trim();
            if (normalized.Length == 0) continue;
            if (string.Equals(normalized, "[DONE]", StringComparison.Ordinal))
            {
                completed = true;
                yield return new AgentStreamUpdate(isCompleted: true);
                break;
            }

            AgentStreamUpdate? update = ParseStreamUpdate(data);
            if (update != null)
                yield return update;
        }

        if (!completed)
            yield return new AgentStreamUpdate(isCompleted: true);
    }

    private string SerializeRequest(
        IReadOnlyList<AgentMessage> messages,
        IReadOnlyList<AgentToolDefinition> tools, bool stream)
    {
        if (messages == null) throw new ArgumentNullException(nameof(messages));
        if (messages.Count == 0)
            throw new ArgumentException("At least one Agent message is required.", nameof(messages));
        if (messages.Any(message => message == null))
            throw new ArgumentException("Agent messages cannot contain null entries.", nameof(messages));
        if (tools == null) throw new ArgumentNullException(nameof(tools));
        if (tools.Any(tool => tool == null))
            throw new ArgumentException("Agent tools cannot contain null entries.", nameof(tools));

        return JsonSerializer.Serialize(new ChatCompletionRequest
        {
            Model = _options.Model,
            Stream = stream ? true : null,
            Messages = messages.Select(message => new ChatCompletionMessage
            {
                Role = ToWireRole(message.Role),
                Content = message.Content,
                ToolCallId = message.ToolCallId,
                Name = message.Name,
                ToolCalls = message.ToolCalls.Count == 0 ? null : message.ToolCalls.Select(call =>
                    new ChatToolCall
                    {
                        Id = call.Id,
                        Function = new ChatFunctionCall
                        {
                            Name = call.Name,
                            Arguments = call.ArgumentsJson
                        }
                    }).ToArray()
            }).ToArray(),
            Tools = tools.Count == 0 ? null : tools.Select(tool => new ChatTool
            {
                Function = new ChatFunctionDefinition
                {
                    Name = tool.Name,
                    Description = tool.Description,
                    Parameters = ParseSchema(tool.ParametersJsonSchema)
                }
            }).ToArray()
        }, JsonOptions);
    }

    private static JsonElement ParseSchema(string schema)
    {
        using JsonDocument document = JsonDocument.Parse(schema);
        return document.RootElement.Clone();
    }

    private async Task<StreamingResponseLease> OpenStreamingResponseAsync(
        string json, CancellationToken cancellationToken)
    {
        for (int attempt = 0; ; ++attempt)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(_options.Timeout);
            HttpResponseMessage? response = null;
            bool transferred = false;
            try
            {
                using var request = CreateRequest(json);
                response = await _httpClient.SendAsync(
                    request, HttpCompletionOption.ResponseHeadersRead, timeout.Token)
                    .ConfigureAwait(false);

                if (response.Content.Headers.ContentLength > _options.MaxResponseBytes)
                    throw new AgentProviderException(
                        "Agent response exceeded the configured size limit.",
                        response.StatusCode);

                if (response.IsSuccessStatusCode)
                {
                    transferred = true;
                    return new StreamingResponseLease(response, timeout);
                }

                string body = await ReadBodyAsync(response, timeout.Token).ConfigureAwait(false);
                bool transient = IsTransient(response.StatusCode);
                if (!transient || attempt >= _options.MaxRetries)
                    throw CreateHttpException(response.StatusCode, body, transient);

                await Task.Delay(GetRetryDelay(response, attempt), cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                throw new AgentProviderException(
                    $"Agent request timed out after {_options.Timeout.TotalSeconds:0.###} seconds.",
                    null, true, ex);
            }
            catch (HttpRequestException) when (attempt < _options.MaxRetries)
            {
                await Task.Delay(GetRetryDelay(null, attempt), cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (HttpRequestException ex)
            {
                throw new AgentProviderException(
                    "Agent endpoint could not be reached: " + Sanitize(ex.Message),
                    null, true);
            }
            finally
            {
                if (!transferred)
                {
                    response?.Dispose();
                    timeout.Dispose();
                }
            }
        }
    }

    private async IAsyncEnumerable<string> ReadSseDataAsync(
        HttpResponseMessage response, CancellationToken timeoutToken,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        Stream stream;
        try
        {
            stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException || ex is IOException)
        {
            throw new AgentProviderException(
                "Agent stream could not be opened: " + Sanitize(ex.Message),
                response.StatusCode, true);
        }

        using (stream)
        using (var line = new MemoryStream())
        {
            var dataLines = new List<string>();
            var chunk = new byte[81920];
            long totalBytes = 0;
            while (true)
            {
                int read;
                try
                {
                    read = await stream.ReadAsync(
                        chunk, 0, chunk.Length, timeoutToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
                {
                    throw new AgentProviderException(
                        $"Agent request timed out after {_options.Timeout.TotalSeconds:0.###} seconds.",
                        null, true, ex);
                }
                catch (Exception ex) when (ex is HttpRequestException || ex is IOException)
                {
                    throw new AgentProviderException(
                        "Agent stream was interrupted: " + Sanitize(ex.Message),
                        response.StatusCode, true);
                }

                if (read == 0) break;
                totalBytes += read;
                if (totalBytes > _options.MaxResponseBytes)
                    throw new AgentProviderException(
                        "Agent response exceeded the configured size limit.",
                        response.StatusCode);

                for (int index = 0; index < read; ++index)
                {
                    byte value = chunk[index];
                    if (value != (byte)'\n')
                    {
                        line.WriteByte(value);
                        continue;
                    }

                    if (ConsumeSseLine(line, dataLines, out string? data))
                        yield return data!;
                }
            }

            if (line.Length > 0)
                _ = ConsumeSseLine(line, dataLines, out _);
            if (dataLines.Count > 0)
                yield return string.Join("\n", dataLines);
        }
    }

    private static bool ConsumeSseLine(
        MemoryStream line, List<string> dataLines, out string? completedData)
    {
        byte[] bytes = line.ToArray();
        line.SetLength(0);
        int length = bytes.Length;
        if (length > 0 && bytes[length - 1] == (byte)'\r') --length;
        if (length == 0)
        {
            if (dataLines.Count == 0)
            {
                completedData = null;
                return false;
            }
            completedData = string.Join("\n", dataLines);
            dataLines.Clear();
            return true;
        }

        string text;
        try
        {
            text = StrictUtf8.GetString(bytes, 0, length);
        }
        catch (DecoderFallbackException ex)
        {
            throw new AgentProviderException(
                "Agent stream contained invalid UTF-8.", null, false, ex);
        }
        if (text.StartsWith(":", StringComparison.Ordinal))
        {
            completedData = null;
            return false;
        }

        int colon = text.IndexOf(':');
        string field = colon < 0 ? text : text.Substring(0, colon);
        if (!string.Equals(field, "data", StringComparison.Ordinal))
        {
            completedData = null;
            return false;
        }
        string value = colon < 0 ? string.Empty : text.Substring(colon + 1);
        if (value.StartsWith(" ", StringComparison.Ordinal)) value = value.Substring(1);
        dataLines.Add(value);
        completedData = null;
        return false;
    }

    private AgentStreamUpdate? ParseStreamUpdate(string data)
    {
        try
        {
            using var document = JsonDocument.Parse(data);
            JsonElement root = document.RootElement;
            string contentDelta = string.Empty;
            AgentTokenUsage? usage = null;
            string finishReason = string.Empty;
            var toolCallDeltas = new List<AgentToolCallDelta>();

            if (root.TryGetProperty("choices", out JsonElement choices)
                && choices.ValueKind == JsonValueKind.Array
                && choices.GetArrayLength() > 0)
            {
                JsonElement choice = choices[0];
                if (choice.TryGetProperty("finish_reason", out JsonElement finish)
                    && finish.ValueKind == JsonValueKind.String)
                    finishReason = finish.GetString() ?? string.Empty;
                if (choice.TryGetProperty("delta", out JsonElement delta)
                    && delta.ValueKind == JsonValueKind.Object)
                {
                    if (delta.TryGetProperty("content", out JsonElement content))
                        contentDelta = ReadTextContent(content);
                    if (delta.TryGetProperty("tool_calls", out JsonElement calls)
                        && calls.ValueKind != JsonValueKind.Array)
                        throw new AgentProviderException(
                            "Agent stream returned malformed tool calls.");
                    if (delta.TryGetProperty("tool_calls", out calls)
                        && calls.ValueKind == JsonValueKind.Array)
                    {
                        foreach (JsonElement call in calls.EnumerateArray())
                        {
                            if (!call.TryGetProperty("index", out JsonElement indexValue)
                                || !indexValue.TryGetInt32(out int index) || index < 0)
                                throw new AgentProviderException(
                                    "Agent stream returned a tool-call delta without a valid index.");
                            string id = ReadOptionalString(call, "id");
                            string name = string.Empty;
                            string arguments = string.Empty;
                            if (call.TryGetProperty("function", out JsonElement function)
                                && function.ValueKind == JsonValueKind.Object)
                            {
                                name = ReadOptionalString(function, "name");
                                arguments = ReadOptionalString(function, "arguments");
                            }
                            toolCallDeltas.Add(new AgentToolCallDelta(
                                index, id, name, arguments));
                        }
                    }
                }
            }

            if (root.TryGetProperty("usage", out JsonElement usageElement)
                && usageElement.ValueKind == JsonValueKind.Object)
            {
                usage = new AgentTokenUsage(
                    ReadNonNegativeInt(usageElement, "prompt_tokens"),
                    ReadNonNegativeInt(usageElement, "completion_tokens"),
                    ReadNonNegativeInt(usageElement, "total_tokens"));
            }

            return contentDelta.Length == 0 && usage == null
                    && toolCallDeltas.Count == 0 && finishReason.Length == 0
                ? null
                : new AgentStreamUpdate(
                    contentDelta, usage, toolCallDeltas: toolCallDeltas,
                    finishReason: finishReason);
        }
        catch (JsonException ex)
        {
            throw new AgentProviderException(
                "Agent endpoint returned invalid streaming JSON.",
                null, false, ex);
        }
    }

    private static string ReadTextContent(JsonElement content)
    {
        if (content.ValueKind == JsonValueKind.String)
            return content.GetString() ?? string.Empty;
        if (content.ValueKind != JsonValueKind.Array) return string.Empty;
        var text = new StringBuilder();
        foreach (JsonElement part in content.EnumerateArray())
        {
            if (part.ValueKind == JsonValueKind.Object
                && part.TryGetProperty("text", out JsonElement value)
                && value.ValueKind == JsonValueKind.String)
                text.Append(value.GetString());
        }
        return text.ToString();
    }

    private static int ReadNonNegativeInt(JsonElement value, string name)
    {
        if (!value.TryGetProperty(name, out JsonElement property)
            || !property.TryGetInt32(out int result)
            || result < 0)
            return 0;
        return result;
    }

    private static string ReadOptionalString(JsonElement value, string name)
        => value.TryGetProperty(name, out JsonElement property)
            && property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? string.Empty
            : string.Empty;

    private HttpRequestMessage CreateRequest(string json)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, CompletionEndpoint)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.UserAgent.ParseAdd("Anity-Agent/0.6");
        return request;
    }

    private async Task<string> ReadBodyAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        while (true)
        {
            int read = await stream.ReadAsync(chunk, 0, chunk.Length, cancellationToken).ConfigureAwait(false);
            if (read == 0) break;
            if (buffer.Length + read > _options.MaxResponseBytes)
                throw new AgentProviderException("Agent response exceeded the configured size limit.", response.StatusCode);
            buffer.Write(chunk, 0, read);
        }
        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    private AgentModelTurn ParseModelTurn(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            JsonElement root = document.RootElement;
            if (!root.TryGetProperty("choices", out var choices)
                || choices.ValueKind != JsonValueKind.Array
                || choices.GetArrayLength() == 0
                || !choices[0].TryGetProperty("message", out var message)
                || message.ValueKind != JsonValueKind.Object)
                throw new AgentProviderException("Agent endpoint returned no assistant message.");

            string content = string.Empty;
            if (message.TryGetProperty("content", out JsonElement contentValue)
                && contentValue.ValueKind != JsonValueKind.Null)
            {
                if (contentValue.ValueKind is not JsonValueKind.String
                    and not JsonValueKind.Array)
                    throw new AgentProviderException(
                        "Agent endpoint returned unsupported assistant content.");
                content = ReadTextContent(contentValue);
            }
            var toolCalls = new List<AgentToolCall>();
            if (message.TryGetProperty("tool_calls", out JsonElement calls)
                && calls.ValueKind != JsonValueKind.Array)
                throw new AgentProviderException(
                    "Agent endpoint returned malformed tool calls.");
            if (message.TryGetProperty("tool_calls", out calls)
                && calls.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement call in calls.EnumerateArray())
                {
                    string id = ReadOptionalString(call, "id");
                    string name = string.Empty;
                    string arguments = string.Empty;
                    if (call.TryGetProperty("function", out JsonElement function)
                        && function.ValueKind == JsonValueKind.Object)
                    {
                        name = ReadOptionalString(function, "name");
                        arguments = ReadOptionalString(function, "arguments");
                    }
                    toolCalls.Add(new AgentToolCall(id, name, arguments));
                }
            }
            string finishReason = ReadOptionalString(choices[0], "finish_reason");
            AgentTokenUsage? usage = null;
            if (root.TryGetProperty("usage", out JsonElement usageValue)
                && usageValue.ValueKind == JsonValueKind.Object)
                usage = new AgentTokenUsage(
                    ReadNonNegativeInt(usageValue, "prompt_tokens"),
                    ReadNonNegativeInt(usageValue, "completion_tokens"),
                    ReadNonNegativeInt(usageValue, "total_tokens"));
            return new AgentModelTurn(content, toolCalls, finishReason, usage);
        }
        catch (JsonException ex)
        {
            throw new AgentProviderException("Agent endpoint returned invalid JSON.", null, false, ex);
        }
    }

    private AgentProviderException CreateHttpException(HttpStatusCode statusCode, string body, bool transient)
    {
        string detail = string.Empty;
        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.TryGetProperty("error", out var error))
            {
                if (error.ValueKind == JsonValueKind.Object
                    && error.TryGetProperty("message", out var message)
                    && message.ValueKind == JsonValueKind.String)
                    detail = message.GetString() ?? string.Empty;
                else if (error.ValueKind == JsonValueKind.String)
                    detail = error.GetString() ?? string.Empty;
            }
        }
        catch (JsonException)
        {
            detail = body;
        }

        detail = Sanitize(detail);
        string suffix = string.IsNullOrWhiteSpace(detail) ? string.Empty : ": " + detail;
        return new AgentProviderException($"Agent endpoint returned HTTP {(int)statusCode} ({statusCode}){suffix}", statusCode, transient);
    }

    private string Sanitize(string? value)
    {
        string sanitized = (value ?? string.Empty).Replace(_options.ApiKey, "***");
        return sanitized.Length <= 2048 ? sanitized : sanitized.Substring(0, 2048);
    }

    private static string ToWireRole(AgentRole role) => role switch
    {
        AgentRole.System => "system",
        AgentRole.User => "user",
        AgentRole.Assistant => "assistant",
        AgentRole.Tool => "tool",
        _ => throw new ArgumentOutOfRangeException(nameof(role))
    };

    private static bool IsTransient(HttpStatusCode statusCode)
        => statusCode == HttpStatusCode.RequestTimeout
           || (int)statusCode == 429
           || (int)statusCode >= 500;

    private static TimeSpan GetRetryDelay(HttpResponseMessage? response, int attempt)
    {
        TimeSpan? retryAfter = response?.Headers.RetryAfter?.Delta;
        if (retryAfter.HasValue && retryAfter.Value > TimeSpan.Zero && retryAfter.Value <= TimeSpan.FromSeconds(30))
            return retryAfter.Value;
        return TimeSpan.FromMilliseconds(Math.Min(2000, 200 * (1 << attempt)));
    }

    public void Dispose()
    {
        if (_ownsHttpClient) _httpClient.Dispose();
    }

    private sealed class ChatCompletionRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;

        [JsonPropertyName("messages")]
        public ChatCompletionMessage[] Messages { get; set; } = Array.Empty<ChatCompletionMessage>();

        [JsonPropertyName("stream")]
        public bool? Stream { get; set; }

        [JsonPropertyName("tools")]
        public ChatTool[]? Tools { get; set; }
    }

    private sealed class ChatCompletionMessage
    {
        [JsonPropertyName("role")]
        public string Role { get; set; } = string.Empty;

        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;

        [JsonPropertyName("tool_call_id")]
        public string? ToolCallId { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("tool_calls")]
        public ChatToolCall[]? ToolCalls { get; set; }
    }

    private sealed class ChatTool
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "function";

        [JsonPropertyName("function")]
        public ChatFunctionDefinition Function { get; set; } = new();
    }

    private sealed class ChatFunctionDefinition
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("parameters")]
        public JsonElement Parameters { get; set; }
    }

    private sealed class ChatToolCall
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("type")]
        public string Type { get; set; } = "function";

        [JsonPropertyName("function")]
        public ChatFunctionCall Function { get; set; } = new();
    }

    private sealed class ChatFunctionCall
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("arguments")]
        public string Arguments { get; set; } = string.Empty;
    }

    private sealed class StreamingResponseLease : IDisposable
    {
        private readonly CancellationTokenSource _timeout;
        public HttpResponseMessage Response { get; }
        public CancellationToken TimeoutToken => _timeout.Token;

        public StreamingResponseLease(
            HttpResponseMessage response, CancellationTokenSource timeout)
        {
            Response = response;
            _timeout = timeout;
        }

        public void Dispose()
        {
            Response.Dispose();
            _timeout.Dispose();
        }
    }
}
