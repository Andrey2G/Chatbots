using System.ComponentModel.DataAnnotations;

namespace Chatbots.Web.Models;

public record SessionDto(
    long Id,
    long ChatbotId,
    string SessionId,
    string? UserIdentity,
    string? Title,
    DateTimeOffset CreatedAt,
    int MessageCount);

public class SessionCreateRequest
{
    [Required]
    [StringLength(128)]
    public string SessionId { get; set; } = string.Empty;

    [StringLength(128)]
    public string? UserIdentity { get; set; }

    [StringLength(128)]
    public string? Title { get; set; }
}

public record MessageDto(
    long Id,
    long SessionId,
    string SenderType,
    string? Content,
    string? ResponseId,
    string? ParentResponseId,
    string? Metadata,
    string? Usage,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    List<FileAttachmentDto> Files);

public record FileAttachmentDto(
    long Id,
    string S3Key,
    string FileName,
    string MimeType,
    long FileSize,
    DateTimeOffset CreatedAt);

public class SendMessageRequest
{
    [Required]
    public string SenderType { get; set; } = "user";

    [Required]
    [StringLength(4000)]
    public string Content { get; set; } = string.Empty;

    public string? ResponseId { get; set; }
    public string? ParentResponseId { get; set; }
    public string? Metadata { get; set; }
    public string? Usage { get; set; }
    public List<IBrowserFile> Files { get; set; } = new();
}
