using System.ComponentModel.DataAnnotations;
using Chatbots.Api.Models;
using Microsoft.AspNetCore.Http;

namespace Chatbots.Api.DTOs;

public class FileMetadataDto
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

public class SendMessageRequest
{
    [Required]
    [MinLength(1)]
    public string Content { get; set; } = string.Empty;

    [Required]
    public SenderType SenderType { get; set; }

    public long? ResponseId { get; set; }

    public long? ParentResponseId { get; set; }

    public Dictionary<string, object?>? Metadata { get; set; }

    public Dictionary<string, object?>? Usage { get; set; }

    public IFormFileCollection? Files { get; set; }

    public List<FileMetadataDto>? MetadataForFiles { get; set; }
}

public record FileAttachmentResponse(long Id, string S3Key, string FileName, string? MimeType, long? FileSize, DateTimeOffset CreatedAt);

public record MessageResponse(
    long Id,
    long SessionId,
    SenderType SenderType,
    string Content,
    long? ResponseId,
    long? ParentResponseId,
    IReadOnlyDictionary<string, object?>? Metadata,
    IReadOnlyDictionary<string, object?>? Usage,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<FileAttachmentResponse> Files);
