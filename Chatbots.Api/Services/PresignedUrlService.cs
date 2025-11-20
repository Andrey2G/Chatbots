using Chatbots.Api.Models;

namespace Chatbots.Api.Services;

public class PresignedUrlService
{
    public FileDownloadResponse GenerateDownloadUrl(long fileId, string s3Key, TimeSpan lifetime)
    {
        var expiresAt = DateTimeOffset.UtcNow.Add(lifetime);
        var encodedKey = Uri.EscapeDataString(s3Key);
        var signature = Guid.NewGuid().ToString("N");
        var url = new Uri($"https://example-bucket.s3.amazonaws.com/{encodedKey}?signature={signature}&expires={expiresAt.ToUnixTimeSeconds()}");

        return new FileDownloadResponse
        {
            FileId = fileId,
            DownloadUrl = url,
            ExpiresAt = expiresAt
        };
    }
}
