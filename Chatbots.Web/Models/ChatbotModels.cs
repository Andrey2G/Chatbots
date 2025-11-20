using System.ComponentModel.DataAnnotations;

namespace Chatbots.Web.Models;

public record ChatbotDto(
    long Id,
    string Name,
    string? Description,
    string? Meta,
    string? InitialResponseId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public class ChatbotRequest
{
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [StringLength(512)]
    public string? Description { get; set; }

    public string? Meta { get; set; }

    public string? InitialResponseId { get; set; }
}

public record ChatbotFileDto(
    long Id,
    long ChatbotId,
    string S3Key,
    string FileName,
    string MimeType,
    long FileSize,
    DateTimeOffset CreatedAt,
    DateTimeOffset? IndexedAt);

public class UploadFilesRequest
{
    public List<IBrowserFile> Files { get; set; } = new();
}
