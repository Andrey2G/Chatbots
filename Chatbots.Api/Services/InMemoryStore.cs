using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using Chatbots.Api.Models;

namespace Chatbots.Api.Services;

public class InMemoryStore
{
    private long _chatbotIdSequence;
    private long _sessionIdSequence;
    private long _messageIdSequence;
    private long _fileIdSequence;
    private long _chatbotFileIdSequence;

    private readonly ConcurrentDictionary<long, Chatbot> _chatbots = new();
    private readonly ConcurrentDictionary<long, Session> _sessions = new();
    private readonly ConcurrentDictionary<string, Session> _sessionsBySessionId = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<long, FileAttachment> _files = new();
    private readonly ConcurrentDictionary<long, ChatbotFile> _chatbotFiles = new();

    public IEnumerable<Chatbot> GetChatbots() => _chatbots.Values;

    public bool TryGetChatbot(long id, out Chatbot chatbot) => _chatbots.TryGetValue(id, out chatbot!);

    public Chatbot AddChatbot(Chatbot chatbot)
    {
        _chatbots[chatbot.Id] = chatbot;
        return chatbot;
    }

    public bool UpdateChatbot(long id, Chatbot chatbot)
    {
        _chatbots[id] = chatbot;
        return true;
    }

    public bool DeleteChatbot(long id, out Chatbot? chatbot)
    {
        if (_chatbots.TryRemove(id, out var removed))
        {
            chatbot = removed;
            foreach (var chatbotFile in _chatbotFiles.Values.Where(f => f.ChatbotId == id))
            {
                _chatbotFiles.TryRemove(chatbotFile.Id, out _);
            }
            foreach (var session in _sessions.Values.Where(c => c.ChatbotId == id))
            {
                foreach (var message in session.Messages)
                {
                    foreach (var file in message.Files)
                    {
                        _files.TryRemove(file.Id, out _);
                    }
                }

                _sessions.TryRemove(session.Id, out _);
                _sessionsBySessionId.TryRemove(session.SessionId, out _);
            }
            return true;
        }

        chatbot = null;
        return false;
    }

    public Session AddSession(Session session)
    {
        _sessions[session.Id] = session;
        _sessionsBySessionId[session.SessionId] = session;
        return session;
    }

    public bool TryGetSession(long sessionId, out Session session) =>
        _sessions.TryGetValue(sessionId, out session!);

    public bool TryGetSessionByIdentifier(string sessionId, out Session session) =>
        _sessionsBySessionId.TryGetValue(sessionId, out session!);

    public IEnumerable<Session> GetSessionsForChatbot(long chatbotId) =>
        _sessions.Values.Where(c => c.ChatbotId == chatbotId);

    public bool DeleteSession(long sessionId)
    {
        if (_sessions.TryRemove(sessionId, out var removed))
        {
            _sessionsBySessionId.TryRemove(removed.SessionId, out _);
            foreach (var message in removed.Messages)
            {
                foreach (var file in message.Files)
                {
                    _files.TryRemove(file.Id, out _);
                }
            }

            return true;
        }

        return false;
    }

    public FileAttachment AddFile(FileAttachment file)
    {
        _files[file.Id] = file;
        return file;
    }

    public bool TryGetMessageFile(long fileId, out FileAttachment file) => _files.TryGetValue(fileId, out file!);

    public ChatbotFile AddChatbotFile(ChatbotFile file)
    {
        _chatbotFiles[file.Id] = file;
        return file;
    }

    public IEnumerable<ChatbotFile> GetChatbotFiles(long chatbotId) => _chatbotFiles.Values.Where(f => f.ChatbotId == chatbotId);

    public bool TryGetChatbotFile(long fileId, out ChatbotFile file) => _chatbotFiles.TryGetValue(fileId, out file!);

    public bool DeleteChatbotFile(long fileId, out ChatbotFile? file)
    {
        if (_chatbotFiles.TryRemove(fileId, out var removed))
        {
            file = removed;
            return true;
        }

        file = null;
        return false;
    }

    public long NextChatbotId() => Interlocked.Increment(ref _chatbotIdSequence);
    public long NextSessionId() => Interlocked.Increment(ref _sessionIdSequence);
    public long NextMessageId() => Interlocked.Increment(ref _messageIdSequence);
    public long NextFileId() => Interlocked.Increment(ref _fileIdSequence);
    public long NextChatbotFileId() => Interlocked.Increment(ref _chatbotFileIdSequence);
}
