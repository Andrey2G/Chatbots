namespace Chatbots.Api.Models;

public class Session
{
    public long Id { get; set; }
    public long ChatbotId { get; set; }
    public string SessionId { get; set; } = string.Empty;
    public string? UserIdentity { get; set; }
    public string? Title { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public IList<Message> Messages { get; } = new List<Message>();
}

public class Message
{
    public long Id { get; set; }
    public long SessionId { get; set; }
    public SenderType SenderType { get; set; }
    public string Content { get; set; } = string.Empty;
    public long? ResponseId { get; set; }
    public long? ParentResponseId { get; set; }
    public Dictionary<string, object?>? Metadata { get; set; }
    public Dictionary<string, object?>? Usage { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public IList<FileAttachment> Files { get; } = new List<FileAttachment>();
}

public class FileAttachment
{
    public long Id { get; set; }
    public long MessageId { get; set; }
    public string S3Key { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string? MimeType { get; set; }
    public long? FileSize { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public enum SenderType
{
    User,
    Assistant,
    System
}

public class FileDownloadResponse
{
    public long FileId { get; set; }
    public Uri DownloadUrl { get; set; } = default!;
    public DateTimeOffset ExpiresAt { get; set; }
}
