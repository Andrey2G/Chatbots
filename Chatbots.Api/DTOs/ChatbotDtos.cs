using System.ComponentModel.DataAnnotations;

namespace Chatbots.Api.DTOs;

public class ChatbotCreateRequest
{
    [Required]
    [StringLength(255)]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Required]
    public Dictionary<string, object?> Meta { get; set; } = new();

    [Required]
    [MinLength(1)]
    public string InitialResponseId { get; set; } = string.Empty;
}

public class ChatbotUpdateRequest
{
    [StringLength(255)]
    public string? Name { get; set; }

    public string? Description { get; set; }

    public Dictionary<string, object?>? Meta { get; set; }

    [MinLength(1)]
    public string? InitialResponseId { get; set; }
}

public record ChatbotResponse(
    long Id,
    string Name,
    string? Description,
    IReadOnlyDictionary<string, object?> Meta,
    string InitialResponseId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
