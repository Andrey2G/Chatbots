namespace Chatbots.Api.Models;

public class ChatbotFile
{
    public long Id { get; set; }
    public long ChatbotId { get; set; }
    public string S3Key { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string? MimeType { get; set; }
    public long? FileSize { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? IndexedAt { get; set; }
}
