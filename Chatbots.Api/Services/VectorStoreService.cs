using System.Collections.Concurrent;
using Chatbots.Api.Models;

namespace Chatbots.Api.Services;

public class VectorStoreService
{
    private readonly ConcurrentDictionary<long, List<ChatbotFile>> _indexedFiles = new();

    public async Task IndexChatbotFileAsync(ChatbotFile file, Stream content, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var reader = new StreamReader(content, leaveOpen: true);
        await reader.ReadToEndAsync(cancellationToken);

        var list = _indexedFiles.GetOrAdd(file.ChatbotId, _ => new List<ChatbotFile>());
        lock (list)
        {
            if (!list.Contains(file))
            {
                list.Add(file);
            }
        }

        file.IndexedAt = DateTimeOffset.UtcNow;
    }

    public IReadOnlyList<ChatbotFile> GetIndexedFiles(long chatbotId)
    {
        if (_indexedFiles.TryGetValue(chatbotId, out var files))
        {
            lock (files)
            {
                return files.ToList();
            }
        }

        return Array.Empty<ChatbotFile>();
    }

    public void RemoveChatbotFile(long chatbotId, long fileId)
    {
        if (_indexedFiles.TryGetValue(chatbotId, out var files))
        {
            lock (files)
            {
                files.RemoveAll(f => f.Id == fileId);
            }
        }
    }

    public void RemoveChatbot(long chatbotId)
    {
        _indexedFiles.TryRemove(chatbotId, out _);
    }
}
