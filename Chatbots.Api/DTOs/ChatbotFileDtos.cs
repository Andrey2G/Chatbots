using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace Chatbots.Api.DTOs;

public class ChatbotFileMetadataDto
{
    [StringLength(1024, MinimumLength = 3)]
    public string? S3Key { get; set; }

    [Required]
    [StringLength(255, MinimumLength = 1)]
    public string FileName { get; set; } = string.Empty;

    [StringLength(255)]
    public string? MimeType { get; set; }

    [Range(1, long.MaxValue, ErrorMessage = "File size must be greater than zero.")]
    public long? FileSize { get; set; }
}

public class UploadChatbotFilesRequest
{
    [Required]
    public IFormFileCollection Files { get; set; } = new FormFileCollection();

    public List<ChatbotFileMetadataDto>? MetadataForFiles { get; set; }
}

public record ChatbotFileResponse(
    long Id,
    long ChatbotId,
    string S3Key,
    string FileName,
    string? MimeType,
    long? FileSize,
    DateTimeOffset CreatedAt,
    DateTimeOffset? IndexedAt);
