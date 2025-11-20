using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Chatbots.Web.Models;
using Microsoft.AspNetCore.Components.Forms;

namespace Chatbots.Web.Services;

public class ApiClient
{
    private readonly HttpClient _httpClient;

    public ApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public Uri BaseAddress => _httpClient.BaseAddress ?? new Uri("/");

    public async Task<List<ChatbotDto>> GetChatbotsAsync()
    {
        return await _httpClient.GetFromJsonAsync<List<ChatbotDto>>("chatbots")
            ?? new List<ChatbotDto>();
    }

    public async Task<ChatbotDto?> GetChatbotAsync(long id)
    {
        return await _httpClient.GetFromJsonAsync<ChatbotDto>($"chatbots/{id}");
    }

    public async Task<ChatbotDto?> CreateChatbotAsync(ChatbotRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("chatbots", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ChatbotDto>();
    }

    public async Task UpdateChatbotAsync(long id, ChatbotRequest request)
    {
        var response = await _httpClient.PutAsJsonAsync($"chatbots/{id}", request);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteChatbotAsync(long id)
    {
        var response = await _httpClient.DeleteAsync($"chatbots/{id}");
        response.EnsureSuccessStatusCode();
    }

    public async Task<List<ChatbotFileDto>> GetChatbotFilesAsync(long chatbotId)
    {
        return await _httpClient.GetFromJsonAsync<List<ChatbotFileDto>>($"chatbots/{chatbotId}/files")
            ?? new List<ChatbotFileDto>();
    }

    public async Task UploadChatbotFilesAsync(long chatbotId, IEnumerable<IBrowserFile> files)
    {
        using var content = new MultipartFormDataContent();
        foreach (var file in files)
        {
            var streamContent = new StreamContent(file.OpenReadStream(long.MaxValue));
            streamContent.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType);
            content.Add(streamContent, "Files", file.Name);
        }

        var response = await _httpClient.PostAsync($"chatbots/{chatbotId}/files", content);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteChatbotFileAsync(long chatbotId, long fileId)
    {
        var response = await _httpClient.DeleteAsync($"chatbots/{chatbotId}/files/{fileId}");
        response.EnsureSuccessStatusCode();
    }

    public async Task<List<SessionDto>> GetSessionsAsync(long chatbotId)
    {
        return await _httpClient.GetFromJsonAsync<List<SessionDto>>($"chatbots/{chatbotId}/sessions")
            ?? new List<SessionDto>();
    }

    public async Task<SessionDto?> CreateSessionAsync(long chatbotId, SessionCreateRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync($"chatbots/{chatbotId}/sessions", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<SessionDto>();
    }

    public async Task DeleteSessionAsync(long chatbotId, string sessionId)
    {
        var response = await _httpClient.DeleteAsync($"chatbots/{chatbotId}/sessions/{sessionId}");
        response.EnsureSuccessStatusCode();
    }

    public async Task<List<MessageDto>> GetMessagesAsync(long chatbotId, string sessionId)
    {
        return await _httpClient.GetFromJsonAsync<List<MessageDto>>($"chatbots/{chatbotId}/sessions/{sessionId}/messages")
            ?? new List<MessageDto>();
    }

    public async Task<MessageDto?> SendMessageAsync(long chatbotId, string sessionId, SendMessageRequest request)
    {
        using var content = new MultipartFormDataContent();
        content.Add(new StringContent(request.SenderType), nameof(request.SenderType));
        content.Add(new StringContent(request.Content), nameof(request.Content));

        if (!string.IsNullOrWhiteSpace(request.ResponseId))
        {
            content.Add(new StringContent(request.ResponseId), nameof(request.ResponseId));
        }

        if (!string.IsNullOrWhiteSpace(request.ParentResponseId))
        {
            content.Add(new StringContent(request.ParentResponseId), nameof(request.ParentResponseId));
        }

        if (!string.IsNullOrWhiteSpace(request.Metadata))
        {
            content.Add(new StringContent(request.Metadata), nameof(request.Metadata));
        }

        if (!string.IsNullOrWhiteSpace(request.Usage))
        {
            content.Add(new StringContent(request.Usage), nameof(request.Usage));
        }

        for (int i = 0; i < request.Files.Count; i++)
        {
            var file = request.Files[i];
            var streamContent = new StreamContent(file.OpenReadStream(long.MaxValue));
            streamContent.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType);
            content.Add(streamContent, $"Files", file.Name);

            content.Add(new StringContent(file.Name), $"MetadataForFiles[{i}].FileName");
            content.Add(new StringContent(file.ContentType ?? "application/octet-stream"), $"MetadataForFiles[{i}].MimeType");
            content.Add(new StringContent(file.Size.ToString()), $"MetadataForFiles[{i}].FileSize");
            content.Add(new StringContent($"messages/{sessionId}/files/{Guid.NewGuid()}", System.Text.Encoding.UTF8), $"MetadataForFiles[{i}].S3Key");
        }

        var response = await _httpClient.PostAsync($"chatbots/{chatbotId}/sessions/{sessionId}/messages", content);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<MessageDto>();
    }

    public async Task<string?> GetDownloadUrlAsync(long fileId)
    {
        var response = await _httpClient.GetAsync($"files/{fileId}/download-url");
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var json = await response.Content.ReadFromJsonAsync<JsonElement?>();
        return json?.GetProperty("url").GetString();
    }
}
