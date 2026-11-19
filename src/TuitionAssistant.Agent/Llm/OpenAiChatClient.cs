using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using TuitionAssistant.Agent.Configuration;

namespace TuitionAssistant.Agent.Llm;

/// <summary>
/// Talks to the OpenAI Chat Completions API (or any compatible endpoint) with plain HTTP.
/// Plain HTTP keeps the request fully visible, makes the base URL configurable and avoids SDK version drift.
/// </summary>
public sealed class OpenAiChatClient(HttpClient http, IOptions<LlmOptions> options) : ILlmClient
{
    public async Task<LlmResponse> CompleteAsync(string model, IReadOnlyList<LlmMessage> messages, IReadOnlyList<ToolDefinition> tools, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            throw new LlmException("No LLM API key is configured (set OPENAI_API_KEY in .env).");
        }

        var body = BuildRequest(settings, model, messages, tools);
        using var request = new HttpRequestMessage(HttpMethod.Post, settings.BaseUrl.TrimEnd('/') + "/chat/completions")
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);

        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new LlmException("The LLM service could not be reached.", ex);
        }

        using (response)
        {
            var text = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new LlmException($"The LLM service returned HTTP {(int)response.StatusCode}: {Truncate(ExtractError(text), 300)}");
            }

            return ParseResponse(text);
        }
    }

    public static JsonObject BuildRequest(LlmOptions settings, string model, IReadOnlyList<LlmMessage> messages, IReadOnlyList<ToolDefinition> tools)
    {
        var body = new JsonObject
        {
            ["model"] = model,
            ["messages"] = new JsonArray(messages.Select(ToJson).ToArray()),
        };

        if (tools.Count > 0)
        {
            body["tools"] = new JsonArray(tools.Select(t => (JsonNode)new JsonObject
            {
                ["type"] = "function",
                ["function"] = new JsonObject
                {
                    ["name"] = t.Name,
                    ["description"] = t.Description,
                    ["parameters"] = t.ParametersSchema.DeepClone(),
                },
            }).ToArray());
            body["tool_choice"] = "auto";
        }

        if (settings.Temperature is { } temperature)
        {
            body["temperature"] = temperature;
        }

        if (!string.IsNullOrWhiteSpace(settings.ReasoningEffort))
        {
            body["reasoning_effort"] = settings.ReasoningEffort;
        }

        return body;
    }

    public static LlmResponse ParseResponse(string json)
    {
        var root = JsonNode.Parse(json)?.AsObject() ?? throw new LlmException("The LLM returned an empty response.");
        var choice = root["choices"]?.AsArray().FirstOrDefault() ?? throw new LlmException("The LLM response has no choices.");
        var message = choice["message"] ?? throw new LlmException("The LLM response has no message.");

        var calls = new List<ToolCall>();
        foreach (var call in message["tool_calls"]?.AsArray() ?? [])
        {
            calls.Add(new ToolCall(
                call?["id"]?.GetValue<string>() ?? Guid.NewGuid().ToString("N"),
                call?["function"]?["name"]?.GetValue<string>() ?? "",
                call?["function"]?["arguments"]?.GetValue<string>() ?? "{}"));
        }

        var usage = new LlmUsage(
            root["usage"]?["prompt_tokens"]?.GetValue<int>() ?? 0,
            root["usage"]?["completion_tokens"]?.GetValue<int>() ?? 0);

        return new LlmResponse(message["content"]?.GetValue<string>(), calls, choice["finish_reason"]?.GetValue<string>() ?? "", usage);
    }

    private static JsonNode ToJson(LlmMessage m)
    {
        var node = new JsonObject { ["role"] = m.Role, ["content"] = m.Content };
        if (m.ToolCalls is { Count: > 0 })
        {
            node["tool_calls"] = new JsonArray(m.ToolCalls.Select(c => (JsonNode)new JsonObject
            {
                ["id"] = c.Id,
                ["type"] = "function",
                ["function"] = new JsonObject { ["name"] = c.Name, ["arguments"] = c.ArgumentsJson },
            }).ToArray());
        }

        if (m.ToolCallId is not null)
        {
            node["tool_call_id"] = m.ToolCallId;
        }

        return node;
    }

    private static string ExtractError(string body)
    {
        try
        {
            return JsonNode.Parse(body)?["error"]?["message"]?.GetValue<string>() ?? body;
        }
        catch (JsonException)
        {
            return body;
        }
    }

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max] + "…";
}
