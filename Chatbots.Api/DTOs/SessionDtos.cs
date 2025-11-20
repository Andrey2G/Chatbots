using System.ComponentModel.DataAnnotations;

namespace Chatbots.Api.DTOs;

public class SessionCreateRequest
{
    [Required]
    [StringLength(50, MinimumLength = 1)]
    public string SessionId { get; set; } = string.Empty;

    [StringLength(255)]
    public string? UserIdentity { get; set; }

    [StringLength(255)]
    public string? Title { get; set; }
}

public record SessionResponse(
    long Id,
    long ChatbotId,
    string SessionId,
    string? UserIdentity,
    string? Title,
    DateTimeOffset CreatedAt,
    int MessageCount);
