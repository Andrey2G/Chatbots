using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Chatbots.Api.Models;

namespace Chatbots.Api.Services;

public record StreamedResponseEvent(string? EventName, string Data, bool IsDone, string ResponseId);

public class OpenAiResponseService
{
    private readonly HttpClient _httpClient;
    private readonly string? _apiKey;
    private readonly string _baseUrl;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_apiKey);

    public OpenAiResponseService(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _apiKey = configuration["OPENAI_API_KEY"] ?? configuration["OpenAi:ApiKey"];
        _baseUrl = configuration["OPENAI_BASE_URL"] ?? configuration["OpenAi:BaseUrl"] ?? "https://api.openai.com/";
    }

    public async IAsyncEnumerable<StreamedResponseEvent> StreamResponseAsync(
        Chatbot chatbot,
        Session session,
        IEnumerable<ChatbotFile> chatbotFiles,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException("OpenAI API key is not configured.");
        }

        var responseId = $"chatbot-{chatbot.Id}-response-{Guid.NewGuid():N}";
        var requestPayload = BuildRequest(chatbot, session, chatbotFiles, responseId);
        var requestJson = JsonSerializer.Serialize(requestPayload);
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(_baseUrl), "v1/responses"))
        {
            Content = new StringContent(requestJson, Encoding.UTF8, "application/json")
        };

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        request.Headers.Add("OpenAI-Beta", "responses=v1");

        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        yield return new StreamedResponseEvent("response_id", responseId, false, responseId);

        await foreach (var evt in ReadServerSentEventsAsync(response, responseId, cancellationToken))
        {
            yield return evt;
        }
    }

    private static object BuildRequest(Chatbot chatbot, Session session, IEnumerable<ChatbotFile> chatbotFiles, string responseId)
    {
        var input = new List<object>();
        var instructions = ExtractInstructions(chatbot.Meta) ?? chatbot.Description;
        if (!string.IsNullOrWhiteSpace(instructions))
        {
            input.Add(new
            {
                role = "system",
                content = instructions
            });
        }

        foreach (var message in session.Messages.OrderBy(m => m.CreatedAt))
        {
            input.Add(new
            {
                role = ToRole(message.SenderType),
                content = message.Content
            });
        }

        var metadata = new Dictionary<string, object?>
        {
            ["chatbot_id"] = chatbot.Id,
            ["session_id"] = session.SessionId,
            ["response_id"] = responseId,
            ["chatbot_response_id"] = chatbot.InitialResponseId
        };

        var attachments = BuildAttachments(chatbotFiles, session);
        var model = ExtractModel(chatbot.Meta) ?? "gpt-4.1-mini";
        var responseConfig = ExtractResponseParameters(chatbot.Meta);

        var request = new Dictionary<string, object?>
        {
            ["model"] = model,
            ["input"] = input,
            ["attachments"] = attachments,
            ["metadata"] = metadata,
            ["stream"] = true,
            ["response_format"] = "auto"
        };

        foreach (var config in responseConfig)
        {
            request[config.Key] = config.Value;
        }

        return request;
    }

    private static List<Dictionary<string, object?>> BuildAttachments(IEnumerable<ChatbotFile> chatbotFiles, Session session)
    {
        var attachments = new List<Dictionary<string, object?>>();

        foreach (var file in chatbotFiles)
        {
            attachments.Add(new Dictionary<string, object?>
            {
                ["file_id"] = file.S3Key,
                ["display_name"] = file.FileName,
                ["source"] = "chatbot",
                ["mime_type"] = file.MimeType
            });
        }

        foreach (var message in session.Messages)
        {
            foreach (var file in message.Files)
            {
                attachments.Add(new Dictionary<string, object?>
                {
                    ["file_id"] = file.S3Key,
                    ["display_name"] = file.FileName,
                    ["source"] = "message",
                    ["message_id"] = message.Id,
                    ["mime_type"] = file.MimeType
                });
            }
        }

        return attachments;
    }

    private static string ToRole(SenderType sender)
    {
        return sender switch
        {
            SenderType.Assistant => "assistant",
            SenderType.System => "system",
            _ => "user"
        };
    }

    private async IAsyncEnumerable<StreamedResponseEvent> ReadServerSentEventsAsync(
        HttpResponseMessage response,
        string responseId,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        var dataBuilder = new StringBuilder();
        string? currentEvent = null;

        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync();
            cancellationToken.ThrowIfCancellationRequested();
            if (line is null)
            {
                break;
            }

            if (line.StartsWith("event:"))
            {
                currentEvent = line[6..].Trim();
            }
            else if (line.StartsWith("data:"))
            {
                dataBuilder.AppendLine(line[5..].Trim());
            }
            else if (string.IsNullOrWhiteSpace(line))
            {
                if (dataBuilder.Length > 0)
                {
                    var data = dataBuilder.ToString().TrimEnd();
                    var isDone = string.Equals(data, "[DONE]", StringComparison.OrdinalIgnoreCase);
                    yield return new StreamedResponseEvent(currentEvent, data, isDone, responseId);
                    dataBuilder.Clear();
                    currentEvent = null;
                }
            }
        }

        if (dataBuilder.Length > 0)
        {
            var data = dataBuilder.ToString().TrimEnd();
            var isDone = string.Equals(data, "[DONE]", StringComparison.OrdinalIgnoreCase);
            yield return new StreamedResponseEvent(currentEvent, data, isDone, responseId);
        }
    }

    private static string? ExtractInstructions(Dictionary<string, object?> meta)
    {
        if (TryGetFromMeta(meta, "instructions", out string? instructions))
        {
            return instructions;
        }

        if (TryGetFromMeta(meta, "system_prompt", out string? prompt))
        {
            return prompt;
        }

        return null;
    }

    private static string? ExtractModel(Dictionary<string, object?> meta)
    {
        if (TryGetFromMeta(meta, "model", out string? model))
        {
            return model;
        }

        return null;
    }

    private static Dictionary<string, object?> ExtractResponseParameters(Dictionary<string, object?> meta)
    {
        if (TryGetFromMeta(meta, "response_parameters", out Dictionary<string, object?>? parameters) && parameters is not null)
        {
            return parameters;
        }

        return new Dictionary<string, object?>();
    }

    private static bool TryGetFromMeta<T>(Dictionary<string, object?> meta, string key, out T? value)
    {
        if (meta.TryGetValue(key, out var raw) && raw is not null)
        {
            if (raw is JsonElement element)
            {
                try
                {
                    value = element.Deserialize<T>();
                    return true;
                }
                catch (Exception)
                {
                    value = default;
                    return false;
                }
            }

            if (raw is T casted)
            {
                value = casted;
                return true;
            }
        }

        value = default;
        return false;
    }
}
