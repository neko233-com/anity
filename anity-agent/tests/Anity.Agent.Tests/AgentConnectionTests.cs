using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Anity.Agent;
using Xunit;

namespace Anity.Agent.Tests;

/// <summary>Custom API key/Base URL provider coverage — normal, edge, error, async and concurrency paths.</summary>
public class AgentConnectionTests
{
    [Fact]
    public void Options_RejectMissingApiKey()
    {
        var options = NewOptions();
        options.ApiKey = " ";
        Assert.Throws<ArgumentException>(() => options.Validate());
    }

    [Theory]
    [InlineData("bad key")]
    [InlineData("bad\tkey")]
    public void Options_RejectApiKeyThatCannotFormBearerHeader(string apiKey)
    {
        var options = NewOptions();
        options.ApiKey = apiKey;
        Assert.Throws<ArgumentException>(() => options.Validate());
    }

    [Theory]
    [InlineData("file:///tmp/agent")]
    [InlineData("relative/v1")]
    [InlineData("https://example.test/v1?secret=x")]
    public void Options_RejectInvalidBaseUrl(string url)
    {
        var options = NewOptions();
        options.BaseUrl = url;
        Assert.Throws<ArgumentException>(() => options.Validate());
    }

    [Fact]
    public void Options_FromEnvironmentReader_UsesCustomValues()
    {
        var values = new Dictionary<string, string>
        {
            [AgentConnectionOptions.ApiKeyEnvironmentVariable] = "env-key",
            [AgentConnectionOptions.BaseUrlEnvironmentVariable] = "http://127.0.0.1:11434/v1",
            [AgentConnectionOptions.ModelEnvironmentVariable] = "local-model"
        };
        var options = AgentConnectionOptions.FromEnvironment(name => values.GetValueOrDefault(name));
        Assert.Equal("env-key", options.ApiKey);
        Assert.Equal("http://127.0.0.1:11434/v1", options.BaseUrl);
        Assert.Equal("local-model", options.Model);
    }

    [Fact]
    public void Options_ToString_RedactsApiKey()
    {
        var options = NewOptions();
        Assert.DoesNotContain(options.ApiKey, options.ToString());
        Assert.Contains("ApiKey=***", options.ToString());
    }

    [Fact]
    public async Task Provider_UsesCustomEndpointBearerModelAndHistory()
    {
        var handler = new RecordingHandler(_ => JsonResponse("model reply"));
        using var client = new HttpClient(handler);
        using var provider = new OpenAiCompatibleAgentProvider(NewOptions(), client);

        string result = await provider.CompleteAsync(new[]
        {
            new AgentMessage(AgentRole.System, "You are Anity."),
            new AgentMessage(AgentRole.User, "Build a scene")
        });

        Assert.Equal("model reply", result);
        Assert.Equal("https://gateway.example/v1/chat/completions", handler.LastRequestUri?.AbsoluteUri);
        Assert.Equal("Bearer", handler.AuthorizationScheme);
        Assert.Equal("test-secret", handler.AuthorizationParameter);
        Assert.Contains("\"model\":\"unity-agent\"", handler.Body);
        Assert.Contains("\"role\":\"system\"", handler.Body);
        Assert.Contains("Build a scene", handler.Body);
    }

    [Fact]
    public async Task Provider_ParsesMultipartTextContent()
    {
        using var client = new HttpClient(new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"choices\":[{\"message\":{\"content\":[{\"type\":\"text\",\"text\":\"hello \"},{\"type\":\"text\",\"text\":\"world\"}]}}]}", Encoding.UTF8, "application/json")
        }));
        using var provider = new OpenAiCompatibleAgentProvider(NewOptions(), client);
        Assert.Equal("hello world", await provider.CompleteAsync(UserMessages()));
    }

    [Fact]
    public async Task Provider_InvalidJson_ThrowsTypedException()
    {
        using var client = new HttpClient(new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("not-json")
        }));
        using var provider = new OpenAiCompatibleAgentProvider(NewOptions(), client);
        var error = await Assert.ThrowsAsync<AgentProviderException>(() => provider.CompleteAsync(UserMessages()));
        Assert.Contains("invalid JSON", error.Message);
    }

    [Fact]
    public async Task Provider_RejectsOversizedResponse()
    {
        using var client = new HttpClient(new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(new string('x', 2048))
        }));
        var options = NewOptions();
        options.MaxResponseBytes = 1024;
        using var provider = new OpenAiCompatibleAgentProvider(options, client);
        var error = await Assert.ThrowsAsync<AgentProviderException>(() => provider.CompleteAsync(UserMessages()));
        Assert.Contains("size limit", error.Message);
    }

    [Fact]
    public async Task Provider_HttpError_IsTypedAndSecretIsRedacted()
    {
        using var client = new HttpClient(new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent("{\"error\":{\"message\":\"bad test-secret\"}}", Encoding.UTF8, "application/json")
        }));
        using var provider = new OpenAiCompatibleAgentProvider(NewOptions(), client);
        var error = await Assert.ThrowsAsync<AgentProviderException>(() => provider.CompleteAsync(UserMessages()));
        Assert.Equal(HttpStatusCode.Unauthorized, error.StatusCode);
        Assert.DoesNotContain("test-secret", error.Message);
        Assert.Contains("***", error.Message);
    }

    [Fact]
    public async Task Provider_NetworkExceptionDoesNotLeakSecretThroughInnerException()
    {
        using var client = new HttpClient(new RecordingHandler((_, _) =>
            Task.FromException<HttpResponseMessage>(
                new HttpRequestException("socket failed for test-secret"))));
        using var provider = new OpenAiCompatibleAgentProvider(NewOptions(), client);

        AgentProviderException error = await Assert.ThrowsAsync<AgentProviderException>(
            () => provider.CompleteAsync(UserMessages()));

        Assert.DoesNotContain("test-secret", error.ToString());
        Assert.Null(error.InnerException);
    }

    [Fact]
    public async Task Provider_RetriesTransientFailure()
    {
        int calls = 0;
        using var client = new HttpClient(new RecordingHandler(_ =>
        {
            calls++;
            return calls == 1
                ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) { Content = new StringContent("busy") }
                : JsonResponse("recovered");
        }));
        var options = NewOptions();
        options.MaxRetries = 1;
        using var provider = new OpenAiCompatibleAgentProvider(options, client);
        Assert.Equal("recovered", await provider.CompleteAsync(UserMessages()));
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task Provider_Cancellation_Propagates()
    {
        using var client = new HttpClient(new RecordingHandler(async (_, token) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(10), token);
            return JsonResponse("late");
        }));
        using var provider = new OpenAiCompatibleAgentProvider(NewOptions(), client);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(25));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => provider.CompleteAsync(UserMessages(), cancellation.Token));
    }

    [Fact]
    public async Task Provider_Timeout_IsTypedAndTransient()
    {
        using var client = new HttpClient(new RecordingHandler(async (_, token) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(10), token);
            return JsonResponse("late");
        }));
        var options = NewOptions();
        options.Timeout = TimeSpan.FromMilliseconds(25);
        using var provider = new OpenAiCompatibleAgentProvider(options, client);
        var error = await Assert.ThrowsAsync<AgentProviderException>(() => provider.CompleteAsync(UserMessages()));
        Assert.True(error.IsTransient);
        Assert.Contains("timed out", error.Message);
    }

    [Fact]
    public async Task Session_RemoteProvider_AppendsAssistantHistory()
    {
        using var client = new HttpClient(new RecordingHandler(_ => JsonResponse("remote answer")));
        using var runtime = new AgentRuntime(NewOptions(), client);
        var session = runtime.CreateSession("remote");
        var reply = await session.RunTurnAsync("hello");
        Assert.Equal("remote answer", reply.Content);
        Assert.Equal(new[] { AgentRole.User, AgentRole.Assistant }, session.History.Select(x => x.Role));
    }

    [Fact]
    public async Task Session_LocalTool_DoesNotCallRemoteProvider()
    {
        int calls = 0;
        using var client = new HttpClient(new RecordingHandler(_ =>
        {
            calls++;
            return JsonResponse("unexpected");
        }));
        using var runtime = new AgentRuntime(NewOptions(), client);
        var reply = await runtime.CreateSession().RunTurnAsync("tool:echo local");
        Assert.Equal("local", reply.Content);
        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task Session_ConcurrentTurns_AreSerialized()
    {
        int active = 0;
        int maxActive = 0;
        using var client = new HttpClient(new RecordingHandler(async (_, token) =>
        {
            int now = Interlocked.Increment(ref active);
            maxActive = Math.Max(maxActive, now);
            await Task.Delay(20, token);
            Interlocked.Decrement(ref active);
            return JsonResponse("ok");
        }));
        using var runtime = new AgentRuntime(NewOptions(), client);
        var session = runtime.CreateSession();
        await Task.WhenAll(session.RunTurnAsync("one"), session.RunTurnAsync("two"));
        Assert.Equal(1, maxActive);
        Assert.Equal(4, session.History.Count);
    }

    [Fact]
    public async Task Stream_UsesCustomEndpointBearerModelAndStreamFlag()
    {
        var handler = new RecordingHandler(_ => SseResponse(
            "{\"choices\":[{\"delta\":{\"content\":\"hello\"}}]}", "[DONE]"));
        using var client = new HttpClient(handler);
        using var provider = new OpenAiCompatibleAgentProvider(NewOptions(), client);

        List<AgentStreamUpdate> updates = await CollectAsync(
            provider.StreamAsync(UserMessages()));

        Assert.Equal("hello", updates[0].ContentDelta);
        Assert.True(updates[^1].IsCompleted);
        Assert.Equal("https://gateway.example/v1/chat/completions",
            handler.LastRequestUri?.AbsoluteUri);
        Assert.Equal("Bearer", handler.AuthorizationScheme);
        Assert.Equal("test-secret", handler.AuthorizationParameter);
        Assert.Contains("\"model\":\"unity-agent\"", handler.Body);
        Assert.Contains("\"stream\":true", handler.Body);
    }

    [Fact]
    public async Task Stream_OneByteChunksPreserveUnicode()
    {
        string body = SseBody(
            "{\"choices\":[{\"delta\":{\"content\":\"你好🎮\"}}]}", "[DONE]");
        using var content = new StreamContent(new FragmentedReadStream(
            Encoding.UTF8.GetBytes(body), 1));
        using var client = new HttpClient(new RecordingHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK) { Content = content }));
        using var provider = new OpenAiCompatibleAgentProvider(NewOptions(), client);

        List<AgentStreamUpdate> updates = await CollectAsync(
            provider.StreamAsync(UserMessages()));

        Assert.Equal("你好🎮", updates[0].ContentDelta);
        Assert.True(updates[^1].IsCompleted);
    }

    [Fact]
    public async Task Stream_MultilineDataAndMultipartContentAreParsed()
    {
        string body =
            "event: message\n" +
            "data: {\"choices\":\n" +
            "data: [{\"delta\":{\"content\":[{\"text\":\"left \"},{\"text\":\"right\"}]}}]}\n\n" +
            "data: [DONE]\n\n";
        using var client = new HttpClient(new RecordingHandler(_ => RawResponse(body)));
        using var provider = new OpenAiCompatibleAgentProvider(NewOptions(), client);

        List<AgentStreamUpdate> updates = await CollectAsync(
            provider.StreamAsync(UserMessages()));

        Assert.Equal("left right", updates[0].ContentDelta);
        Assert.True(updates[^1].IsCompleted);
    }

    [Fact]
    public async Task Stream_UsageChunkIsExposedWithoutInventingText()
    {
        using var client = new HttpClient(new RecordingHandler(_ => SseResponse(
            "{\"choices\":[],\"usage\":{\"prompt_tokens\":7,\"completion_tokens\":3,\"total_tokens\":10}}",
            "[DONE]")));
        using var provider = new OpenAiCompatibleAgentProvider(NewOptions(), client);

        List<AgentStreamUpdate> updates = await CollectAsync(
            provider.StreamAsync(UserMessages()));

        AgentTokenUsage usage = Assert.IsType<AgentTokenUsage>(updates[0].Usage);
        Assert.Equal(7, usage.PromptTokens);
        Assert.Equal(3, usage.CompletionTokens);
        Assert.Equal(10, usage.TotalTokens);
        Assert.Empty(updates[0].ContentDelta);
    }

    [Fact]
    public async Task Stream_CommentsMetadataAndRoleOnlyChunksAreIgnored()
    {
        string body =
            ": keep-alive\n" +
            "id: 9\n" +
            "retry: 1\n" +
            "data:\n\n" +
            "data: {\"choices\":[{\"delta\":{\"role\":\"assistant\"}}]}\n\n" +
            "data: {\"choices\":[{\"delta\":{\"content\":\"ok\"}}]}\n\n" +
            "data: [DONE]\n\n";
        using var client = new HttpClient(new RecordingHandler(_ => RawResponse(body)));
        using var provider = new OpenAiCompatibleAgentProvider(NewOptions(), client);

        List<AgentStreamUpdate> updates = await CollectAsync(
            provider.StreamAsync(UserMessages()));

        Assert.Equal(2, updates.Count);
        Assert.Equal("ok", updates[0].ContentDelta);
        Assert.True(updates[1].IsCompleted);
    }

    [Fact]
    public async Task Stream_EofWithoutDoneSynthesizesCompletion()
    {
        using var client = new HttpClient(new RecordingHandler(_ => RawResponse(
            "data: {\"choices\":[{\"delta\":{\"content\":\"tail\"}}]}\n\n")));
        using var provider = new OpenAiCompatibleAgentProvider(NewOptions(), client);

        List<AgentStreamUpdate> updates = await CollectAsync(
            provider.StreamAsync(UserMessages()));

        Assert.Equal("tail", updates[0].ContentDelta);
        Assert.True(updates[^1].IsCompleted);
    }

    [Fact]
    public async Task Stream_InvalidJsonIsTyped()
    {
        using var client = new HttpClient(new RecordingHandler(_ => SseResponse(
            "not-json", "[DONE]")));
        using var provider = new OpenAiCompatibleAgentProvider(NewOptions(), client);

        AgentProviderException error = await Assert.ThrowsAsync<AgentProviderException>(
            () => CollectAsync(provider.StreamAsync(UserMessages())));

        Assert.Contains("invalid streaming JSON", error.Message);
        Assert.False(error.IsTransient);
    }

    [Fact]
    public async Task Stream_InvalidUtf8IsTyped()
    {
        byte[] bytes = Encoding.ASCII.GetBytes("data: x\n\n");
        bytes[6] = 0xff;
        using var content = new StreamContent(new MemoryStream(bytes));
        using var client = new HttpClient(new RecordingHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK) { Content = content }));
        using var provider = new OpenAiCompatibleAgentProvider(NewOptions(), client);

        AgentProviderException error = await Assert.ThrowsAsync<AgentProviderException>(
            () => CollectAsync(provider.StreamAsync(UserMessages())));

        Assert.Contains("invalid UTF-8", error.Message);
    }

    [Fact]
    public async Task Stream_UnknownLengthBodyHonorsResponseLimit()
    {
        using var content = new StreamContent(new FragmentedReadStream(
            Encoding.UTF8.GetBytes(new string('x', 2048)), 17));
        using var client = new HttpClient(new RecordingHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK) { Content = content }));
        var options = NewOptions();
        options.MaxResponseBytes = 1024;
        using var provider = new OpenAiCompatibleAgentProvider(options, client);

        AgentProviderException error = await Assert.ThrowsAsync<AgentProviderException>(
            () => CollectAsync(provider.StreamAsync(UserMessages())));

        Assert.Contains("size limit", error.Message);
    }

    [Fact]
    public async Task Stream_HttpErrorIsRedactedBeforeAnyDelta()
    {
        using var client = new HttpClient(new RecordingHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent(
                    "{\"error\":{\"message\":\"bad test-secret\"}}",
                    Encoding.UTF8, "application/json")
            }));
        using var provider = new OpenAiCompatibleAgentProvider(NewOptions(), client);

        AgentProviderException error = await Assert.ThrowsAsync<AgentProviderException>(
            () => CollectAsync(provider.StreamAsync(UserMessages())));

        Assert.Equal(HttpStatusCode.Unauthorized, error.StatusCode);
        Assert.DoesNotContain("test-secret", error.Message);
        Assert.Contains("***", error.Message);
    }

    [Fact]
    public async Task Stream_RetriesTransientHeadersBeforeYielding()
    {
        int calls = 0;
        using var client = new HttpClient(new RecordingHandler(_ =>
        {
            calls++;
            return calls == 1
                ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                    { Content = new StringContent("busy") }
                : SseResponse(
                    "{\"choices\":[{\"delta\":{\"content\":\"recovered\"}}]}",
                    "[DONE]");
        }));
        var options = NewOptions();
        options.MaxRetries = 1;
        using var provider = new OpenAiCompatibleAgentProvider(options, client);

        List<AgentStreamUpdate> updates = await CollectAsync(
            provider.StreamAsync(UserMessages()));

        Assert.Equal("recovered", updates[0].ContentDelta);
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task Stream_CancellationInterruptsBlockedBodyRead()
    {
        string first = SseBody(
            "{\"choices\":[{\"delta\":{\"content\":\"first\"}}]}");
        using var content = new StreamContent(new FirstThenBlockStream(
            Encoding.UTF8.GetBytes(first)));
        using var client = new HttpClient(new RecordingHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK) { Content = content }));
        using var provider = new OpenAiCompatibleAgentProvider(NewOptions(), client);
        using var cancellation = new CancellationTokenSource();
        await using IAsyncEnumerator<AgentStreamUpdate> iterator =
            provider.StreamAsync(UserMessages(), cancellation.Token).GetAsyncEnumerator();

        Assert.True(await iterator.MoveNextAsync());
        Assert.Equal("first", iterator.Current.ContentDelta);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await iterator.MoveNextAsync().AsTask());
    }

    [Fact]
    public async Task Stream_TimeoutInterruptsBlockedBodyReadAsTypedTransientFailure()
    {
        string first = SseBody(
            "{\"choices\":[{\"delta\":{\"content\":\"first\"}}]}");
        using var content = new StreamContent(new FirstThenBlockStream(
            Encoding.UTF8.GetBytes(first)));
        using var client = new HttpClient(new RecordingHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK) { Content = content }));
        var options = NewOptions();
        options.Timeout = TimeSpan.FromMilliseconds(25);
        using var provider = new OpenAiCompatibleAgentProvider(options, client);
        await using IAsyncEnumerator<AgentStreamUpdate> iterator =
            provider.StreamAsync(UserMessages()).GetAsyncEnumerator();

        Assert.True(await iterator.MoveNextAsync());
        AgentProviderException error = await Assert.ThrowsAsync<AgentProviderException>(
            async () => await iterator.MoveNextAsync().AsTask());

        Assert.True(error.IsTransient);
        Assert.Contains("timed out", error.Message);
    }

    [Fact]
    public async Task StreamingSession_CommitsOneAtomicAggregatedTurn()
    {
        using var client = new HttpClient(new RecordingHandler(_ => SseResponse(
            "{\"choices\":[{\"delta\":{\"content\":\"hello \"}}]}",
            "{\"choices\":[{\"delta\":{\"content\":\"world\"}}]}",
            "[DONE]")));
        using var runtime = new AgentRuntime(NewOptions(), client);
        AgentSession session = runtime.CreateSession("stream");

        List<AgentStreamUpdate> updates = await CollectAsync(
            session.RunTurnStreamAsync("hi"));

        Assert.Equal("hello world", string.Concat(
            updates.Select(update => update.ContentDelta)));
        Assert.Equal(new[] { AgentRole.User, AgentRole.Assistant },
            session.History.Select(message => message.Role));
        Assert.Equal("hello world", session.History[1].Content);
    }

    [Fact]
    public async Task StreamingSession_FailureDoesNotLeavePartialHistory()
    {
        using var client = new HttpClient(new RecordingHandler(_ => SseResponse(
            "{\"choices\":[{\"delta\":{\"content\":\"partial\"}}]}",
            "not-json")));
        using var runtime = new AgentRuntime(NewOptions(), client);
        AgentSession session = runtime.CreateSession("failed-stream");

        await Assert.ThrowsAsync<AgentProviderException>(
            () => CollectAsync(session.RunTurnStreamAsync("hi")));

        Assert.Empty(session.History);
        Assert.False(session.Memory.TryGet("last_user", out _));
    }

    [Fact]
    public async Task StreamingSession_ConcurrentTurnsRemainSerializedThroughCompletion()
    {
        var provider = new GateStreamingProvider();
        using var runtime = new AgentRuntime(provider);
        AgentSession session = runtime.CreateSession("serialized-stream");

        await Task.WhenAll(
            CollectAsync(session.RunTurnStreamAsync("one")),
            CollectAsync(session.RunTurnStreamAsync("two")));

        Assert.Equal(1, provider.MaxActive);
        Assert.Equal(4, session.History.Count);
        Assert.Equal(new[] { "one", "one-reply", "two", "two-reply" },
            session.History.Select(message => message.Content));
    }

    [Fact]
    public async Task ToolCalling_NonStreamingExecutesAndReturnsToolMessage()
    {
        int calls = 0;
        var handler = new RecordingHandler(_ => ++calls == 1
            ? ToolCallResponse("call_1", "remote_test", "{\"value\":\"ping\"}")
            : JsonResponse("tool accepted"));
        using var client = new HttpClient(handler);
        using var runtime = new AgentRuntime(NewOptions(), client);
        var tool = new RecordingRemoteTool();
        runtime.Tools.Register(tool);
        AgentSession session = runtime.CreateSession("tool-nonstream");

        AgentMessage reply = await session.RunTurnAsync("use the tool");

        Assert.Equal("tool accepted", reply.Content);
        Assert.Equal(1, tool.InvocationCount);
        Assert.Equal("{\"value\":\"ping\"}", tool.LastArguments);
        Assert.Equal(new[] { AgentRole.User, AgentRole.Assistant, AgentRole.Tool, AgentRole.Assistant },
            session.History.Select(message => message.Role));
        Assert.Equal("call_1", session.History[2].ToolCallId);
        Assert.Contains("\"tools\":", handler.Bodies[0]);
        Assert.DoesNotContain("screenshot", handler.Bodies[0]);
        Assert.Contains("\"tool_call_id\":\"call_1\"", handler.Bodies[1]);
        Assert.Contains("\"role\":\"tool\"", handler.Bodies[1]);
    }

    [Fact]
    public async Task ToolCalling_StreamFragmentsAreAssembledBeforeInvocation()
    {
        int calls = 0;
        var handler = new RecordingHandler(_ => ++calls == 1
            ? SseResponse(
                ToolDelta(0, "call_", "remote_", "{\"value\":\""),
                ToolDelta(0, "1", "test", "pong\"}"),
                FinishDelta("tool_calls"), "[DONE]")
            : SseResponse(
                TextDelta("stream final"), FinishDelta("stop"), "[DONE]"));
        using var client = new HttpClient(handler);
        using var runtime = new AgentRuntime(NewOptions(), client);
        var tool = new RecordingRemoteTool();
        runtime.Tools.Register(tool);
        AgentSession session = runtime.CreateSession("tool-stream");

        List<AgentStreamUpdate> updates = await CollectAsync(
            session.RunTurnStreamAsync("stream tool"));

        Assert.Equal(1, tool.InvocationCount);
        Assert.Equal("{\"value\":\"pong\"}", tool.LastArguments);
        Assert.Equal("stream final", string.Concat(updates.Select(x => x.ContentDelta)));
        Assert.True(updates[^1].IsCompleted);
        Assert.Equal(4, session.History.Count);
        Assert.Equal("call_1", session.History[1].ToolCalls[0].Id);
    }

    [Fact]
    public async Task ToolCalling_StreamPreservesParallelCallIndices()
    {
        using var client = new HttpClient(new RecordingHandler(_ => SseResponse(
            ToolDelta(1, "b", "second", "{}"),
            ToolDelta(0, "a", "first", "{}"),
            FinishDelta("tool_calls"), "[DONE]")));
        using var provider = new OpenAiCompatibleAgentProvider(NewOptions(), client);
        var tools = new[]
        {
            new AgentToolDefinition("first", "", "{}"),
            new AgentToolDefinition("second", "", "{}")
        };

        List<AgentStreamUpdate> updates = await CollectAsync(
            provider.StreamWithToolsAsync(UserMessages(), tools));

        Assert.Equal(new[] { 1, 0 }, updates
            .SelectMany(update => update.ToolCallDeltas)
            .Select(delta => delta.Index));
        Assert.Contains(updates, update => update.FinishReason == "tool_calls");
    }

    [Fact]
    public async Task ToolCalling_StreamRejectsDeltaWithoutIndex()
    {
        string malformed = "{\"choices\":[{\"delta\":{\"tool_calls\":[{\"id\":\"x\"}]}}]}";
        using var client = new HttpClient(new RecordingHandler(_ => SseResponse(
            malformed, "[DONE]")));
        using var provider = new OpenAiCompatibleAgentProvider(NewOptions(), client);

        AgentProviderException error = await Assert.ThrowsAsync<AgentProviderException>(
            () => CollectAsync(provider.StreamWithToolsAsync(
                UserMessages(), new[] { new AgentToolDefinition("x", "", "{}") })));

        Assert.Contains("valid index", error.Message);
    }

    [Fact]
    public async Task ToolCalling_InvalidArgumentsReturnErrorWithoutInvokingTool()
    {
        int calls = 0;
        var handler = new RecordingHandler(_ => ++calls == 1
            ? ToolCallResponse("bad_args", "remote_test", "not-json")
            : JsonResponse("recovered"));
        using var client = new HttpClient(handler);
        using var runtime = new AgentRuntime(NewOptions(), client);
        var tool = new RecordingRemoteTool();
        runtime.Tools.Register(tool);
        AgentSession session = runtime.CreateSession("bad-args");

        AgentMessage result = await session.RunTurnAsync("bad args");

        Assert.Equal("recovered", result.Content);
        Assert.Equal(0, tool.InvocationCount);
        Assert.Contains("invalid tool arguments JSON", session.History[2].Content);
    }

    [Fact]
    public async Task ToolCalling_UnadvertisedToolReturnsUnavailableResult()
    {
        int calls = 0;
        var handler = new RecordingHandler(_ => ++calls == 1
            ? ToolCallResponse("hidden", "screenshot", "{}")
            : JsonResponse("handled"));
        using var client = new HttpClient(handler);
        using var runtime = new AgentRuntime(NewOptions(), client);
        AgentSession session = runtime.CreateSession("hidden-tool");

        Assert.Equal("handled", (await session.RunTurnAsync("hidden")).Content);
        Assert.Contains("unavailable", session.History[2].Content);
        Assert.DoesNotContain("screenshot", handler.Bodies[0]);
    }

    [Fact]
    public async Task ToolCalling_DuplicateIdsRejectTurnAtomically()
    {
        using var client = new HttpClient(new RecordingHandler(_ => ToolCallsResponse(
            new AgentToolCall("same", "echo", "{\"args\":\"a\"}"),
            new AgentToolCall("same", "systeminfo", "{}"))));
        using var runtime = new AgentRuntime(NewOptions(), client);
        AgentSession session = runtime.CreateSession("duplicate-tools");

        AgentProviderException error = await Assert.ThrowsAsync<AgentProviderException>(
            () => session.RunTurnAsync("duplicate"));

        Assert.Contains("duplicate", error.Message);
        Assert.Empty(session.History);
    }

    [Fact]
    public async Task ToolCalling_TooManyCallsRejectTurnAtomically()
    {
        AgentToolCall[] calls = Enumerable.Range(0, 17)
            .Select(index => new AgentToolCall($"c{index}", "echo", "{\"args\":\"x\"}"))
            .ToArray();
        using var client = new HttpClient(new RecordingHandler(_ => ToolCallsResponse(calls)));
        using var runtime = new AgentRuntime(NewOptions(), client);
        AgentSession session = runtime.CreateSession("too-many-tools");

        AgentProviderException error = await Assert.ThrowsAsync<AgentProviderException>(
            () => session.RunTurnAsync("many"));

        Assert.Contains("too many", error.Message);
        Assert.Empty(session.History);
    }

    [Fact]
    public async Task ToolCalling_CancellationDuringRemoteToolLeavesHistoryEmpty()
    {
        using var client = new HttpClient(new RecordingHandler(_ =>
            ToolCallResponse("wait", "remote_test", "{}")));
        using var runtime = new AgentRuntime(NewOptions(), client);
        var tool = new RecordingRemoteTool(block: true);
        runtime.Tools.Register(tool);
        AgentSession session = runtime.CreateSession("cancel-tool");
        using var cancellation = new CancellationTokenSource(25);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => session.RunTurnAsync("wait", cancellation.Token));

        Assert.Empty(session.History);
    }

    [Fact]
    public async Task ToolCalling_ReusedIdAcrossRoundsRejectsTurnAtomically()
    {
        using var client = new HttpClient(new RecordingHandler(_ =>
            ToolCallResponse("reused", "remote_test", "{}")));
        using var runtime = new AgentRuntime(NewOptions(), client);
        var tool = new RecordingRemoteTool();
        runtime.Tools.Register(tool);
        AgentSession session = runtime.CreateSession("reused-tool-id");

        AgentProviderException error = await Assert.ThrowsAsync<AgentProviderException>(
            () => session.RunTurnAsync("repeat"));

        Assert.Contains("reused", error.Message);
        Assert.Equal(1, tool.InvocationCount);
        Assert.Empty(session.History);
    }

    [Fact]
    public async Task ToolCalling_RemoteToolTimeoutReturnsErrorAndModelCanRecover()
    {
        int calls = 0;
        using var client = new HttpClient(new RecordingHandler(_ => ++calls == 1
            ? ToolCallResponse("slow", "remote_test", "{}")
            : JsonResponse("timeout handled")));
        using var runtime = new AgentRuntime(NewOptions(), client)
        {
            RemoteToolTimeout = TimeSpan.FromMilliseconds(25)
        };
        runtime.Tools.Register(new RecordingRemoteTool(block: true));
        AgentSession session = runtime.CreateSession("tool-timeout");

        AgentMessage result = await session.RunTurnAsync("slow tool");

        Assert.Equal("timeout handled", result.Content);
        Assert.Contains("timed out", session.History[2].Content);
        Assert.Equal(4, session.History.Count);
    }

    private static AgentConnectionOptions NewOptions() => new()
    {
        ApiKey = "test-secret",
        BaseUrl = "https://gateway.example/v1/",
        Model = "unity-agent",
        Timeout = TimeSpan.FromSeconds(2),
        MaxRetries = 0
    };

    private static AgentMessage[] UserMessages() => new[] { new AgentMessage(AgentRole.User, "hello") };

    private static async Task<List<AgentStreamUpdate>> CollectAsync(
        IAsyncEnumerable<AgentStreamUpdate> updates)
    {
        var result = new List<AgentStreamUpdate>();
        await foreach (AgentStreamUpdate update in updates) result.Add(update);
        return result;
    }

    private static HttpResponseMessage SseResponse(params string[] data)
        => RawResponse(SseBody(data));

    private static string SseBody(params string[] data)
        => string.Concat(data.Select(value => $"data: {value}\n\n"));

    private static HttpResponseMessage RawResponse(string body) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(body, Encoding.UTF8, "text/event-stream")
    };

    private static HttpResponseMessage ToolCallResponse(
        string id, string name, string arguments)
        => ToolCallsResponse(new AgentToolCall(id, name, arguments));

    private static HttpResponseMessage ToolCallsResponse(params AgentToolCall[] calls)
        => new(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(new
            {
                choices = new[] { new
                {
                    message = new
                    {
                        role = "assistant",
                        content = (string?)null,
                        tool_calls = calls.Select(call => new
                        {
                            id = call.Id,
                            type = "function",
                            function = new { name = call.Name, arguments = call.ArgumentsJson }
                        }).ToArray()
                    },
                    finish_reason = "tool_calls"
                }}
            }), Encoding.UTF8, "application/json")
        };

    private static string ToolDelta(
        int index, string id, string name, string arguments)
        => JsonSerializer.Serialize(new
        {
            choices = new[] { new
            {
                delta = new
                {
                    tool_calls = new[] { new
                    {
                        index,
                        id,
                        type = "function",
                        function = new { name, arguments }
                    }}
                },
                finish_reason = (string?)null
            }}
        });

    private static string FinishDelta(string reason)
        => JsonSerializer.Serialize(new
        {
            choices = new[] { new { delta = new { }, finish_reason = reason } }
        });

    private static string TextDelta(string content)
        => JsonSerializer.Serialize(new
        {
            choices = new[] { new
            {
                delta = new { content }, finish_reason = (string?)null
            }}
        });

    private static HttpResponseMessage JsonResponse(string content) => new(HttpStatusCode.OK)
    {
        Content = new StringContent($"{{\"choices\":[{{\"message\":{{\"content\":\"{content}\"}}}}]}}", Encoding.UTF8, "application/json")
    };

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _send;

        public Uri? LastRequestUri { get; private set; }
        public string? AuthorizationScheme { get; private set; }
        public string? AuthorizationParameter { get; private set; }
        public string Body { get; private set; } = string.Empty;
        public List<string> Bodies { get; } = new();

        public RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> send)
            : this((request, _) => Task.FromResult(send(request)))
        {
        }

        public RecordingHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send)
        {
            _send = send;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri;
            AuthorizationScheme = request.Headers.Authorization?.Scheme;
            AuthorizationParameter = request.Headers.Authorization?.Parameter;
            Body = request.Content == null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            Bodies.Add(Body);
            return await _send(request, cancellationToken);
        }
    }

    private sealed class FragmentedReadStream : MemoryStream
    {
        private readonly int _fragmentSize;

        public FragmentedReadStream(byte[] bytes, int fragmentSize) : base(bytes)
            => _fragmentSize = fragmentSize;

        public override Task<int> ReadAsync(
            byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => base.ReadAsync(buffer, offset, Math.Min(count, _fragmentSize), cancellationToken);
    }

    private sealed class FirstThenBlockStream : Stream
    {
        private readonly byte[] _first;
        private int _offset;

        public FirstThenBlockStream(byte[] first) => _first = first;
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count)
            => throw new NotSupportedException();
        public override async Task<int> ReadAsync(
            byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (_offset < _first.Length)
            {
                int copy = Math.Min(count, _first.Length - _offset);
                Array.Copy(_first, _offset, buffer, offset, copy);
                _offset += copy;
                return copy;
            }
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return 0;
        }
        public override long Seek(long offset, SeekOrigin origin)
            => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count)
            => throw new NotSupportedException();
    }

    private sealed class GateStreamingProvider : IStreamingAgentProvider
    {
        private int _active;
        private int _maxActive;
        public int MaxActive => Volatile.Read(ref _maxActive);

        public Task<string> CompleteAsync(
            IReadOnlyList<AgentMessage> messages,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public async IAsyncEnumerable<AgentStreamUpdate> StreamAsync(
            IReadOnlyList<AgentMessage> messages,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            int active = Interlocked.Increment(ref _active);
            while (true)
            {
                int current = Volatile.Read(ref _maxActive);
                if (current >= active
                    || Interlocked.CompareExchange(ref _maxActive, active, current) == current)
                    break;
            }
            try
            {
                await Task.Delay(20, cancellationToken);
                string prompt = messages[^1].Content;
                yield return new AgentStreamUpdate(prompt + "-reply");
                yield return new AgentStreamUpdate(isCompleted: true);
            }
            finally
            {
                Interlocked.Decrement(ref _active);
            }
        }
    }

    private sealed class RecordingRemoteTool : IRemoteAgentTool
    {
        private readonly bool _block;
        public int InvocationCount { get; private set; }
        public string LastArguments { get; private set; } = string.Empty;
        public string Name => "remote_test";
        public string Description => "Remote test tool";
        public string ParametersJsonSchema =>
            "{\"type\":\"object\",\"properties\":{\"value\":{\"type\":\"string\"}},\"additionalProperties\":false}";

        public RecordingRemoteTool(bool block = false) => _block = block;
        public string Invoke(string args, AgentSession session) => args;

        public async Task<string> InvokeRemoteAsync(
            string argumentsJson, AgentSession session,
            CancellationToken cancellationToken = default)
        {
            InvocationCount++;
            LastArguments = argumentsJson;
            if (_block)
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return "tool-result";
        }
    }
}
