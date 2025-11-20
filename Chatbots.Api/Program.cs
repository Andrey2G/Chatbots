using System.Text.Json;
using System.Text.Json.Serialization;
using Chatbots.Api.DTOs;
using Chatbots.Api.Models;
using Chatbots.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton<InMemoryStore>();
builder.Services.AddSingleton<PresignedUrlService>();
builder.Services.AddSingleton<VectorStoreService>();
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapGet("/chatbots", (InMemoryStore store) =>
{
    var chatbots = store.GetChatbots().Select(ToResponse);
    return Results.Ok(chatbots);
});

app.MapGet("/chatbots/{chatbotId:long}", (long chatbotId, InMemoryStore store) =>
{
    if (!store.TryGetChatbot(chatbotId, out var chatbot))
    {
        return Results.NotFound(new { message = "Chatbot not found" });
    }

    return Results.Ok(ToResponse(chatbot));
});

app.MapPost("/chatbots", (ChatbotCreateRequest request, InMemoryStore store) =>
{
    if (!ValidationExtensions.TryValidate(request, out var errors))
    {
        return Results.ValidationProblem(errors);
    }

    var chatbot = new Chatbot
    {
        Id = store.NextChatbotId(),
        Name = request.Name,
        Description = request.Description,
        Meta = request.Meta,
        InitialResponseId = request.InitialResponseId,
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow
    };

    store.AddChatbot(chatbot);

    return Results.Created($"/chatbots/{chatbot.Id}", ToResponse(chatbot));
});

app.MapPut("/chatbots/{chatbotId:long}", (long chatbotId, ChatbotUpdateRequest request, InMemoryStore store) =>
{
    if (!store.TryGetChatbot(chatbotId, out var existing))
    {
        return Results.NotFound(new { message = "Chatbot not found" });
    }

    if (!ValidationExtensions.TryValidate(request, out var errors))
    {
        return Results.ValidationProblem(errors);
    }

    existing.Name = request.Name ?? existing.Name;
    existing.Description = request.Description ?? existing.Description;
    existing.InitialResponseId = request.InitialResponseId ?? existing.InitialResponseId;
    existing.Meta = request.Meta ?? existing.Meta;
    existing.UpdatedAt = DateTimeOffset.UtcNow;

    store.UpdateChatbot(chatbotId, existing);

    return Results.Ok(ToResponse(existing));
});

app.MapDelete("/chatbots/{chatbotId:long}", (long chatbotId, InMemoryStore store, VectorStoreService vectorStore) =>
{
    if (!store.DeleteChatbot(chatbotId, out _))
    {
        return Results.NotFound(new { message = "Chatbot not found" });
    }

    vectorStore.RemoveChatbot(chatbotId);

    return Results.NoContent();
});

app.MapGet("/chatbots/{chatbotId:long}/files", (long chatbotId, InMemoryStore store) =>
{
    if (!store.TryGetChatbot(chatbotId, out _))
    {
        return Results.NotFound(new { message = "Chatbot not found" });
    }

    var files = store.GetChatbotFiles(chatbotId)
        .OrderByDescending(f => f.CreatedAt)
        .Select(ToChatbotFileResponse);

    return Results.Ok(files);
});

app.MapPost("/chatbots/{chatbotId:long}/files", async (
    long chatbotId,
    [FromForm] UploadChatbotFilesRequest request,
    InMemoryStore store,
    VectorStoreService vectorStore,
    CancellationToken cancellationToken) =>
{
    if (!store.TryGetChatbot(chatbotId, out _))
    {
        return Results.NotFound(new { message = "Chatbot not found" });
    }

    if (!ValidationExtensions.TryValidate(request, out var errors))
    {
        return Results.ValidationProblem(errors);
    }

    var metadata = request.MetadataForFiles ?? new List<ChatbotFileMetadataDto>();
    var files = request.Files ?? new FormFileCollection();

    var metadataErrors = new Dictionary<string, string[]>();
    for (int i = 0; i < metadata.Count; i++)
    {
        if (!ValidationExtensions.TryValidate(metadata[i], out var fileErrors))
        {
            metadataErrors[$"MetadataForFiles[{i}]"] = fileErrors.SelectMany(e => e.Value).ToArray();
        }
    }

    if (files.Count == 0)
    {
        metadataErrors[nameof(request.Files)] = new[] { "At least one file must be provided." };
    }

    if (metadata.Count > 0 && metadata.Count != files.Count)
    {
        metadataErrors[nameof(request.MetadataForFiles)] = new[] { "Metadata entries must match the number of uploaded files." };
    }

    if (metadataErrors.Count > 0)
    {
        return Results.ValidationProblem(metadataErrors);
    }

    var created = new List<ChatbotFileResponse>();

    for (int i = 0; i < files.Count; i++)
    {
        var file = files[i];
        var meta = metadata.ElementAtOrDefault(i);
        var chatbotFile = new ChatbotFile
        {
            Id = store.NextChatbotFileId(),
            ChatbotId = chatbotId,
            S3Key = meta?.S3Key ?? $"chatbots/{chatbotId}/files/{Guid.NewGuid()}",
            FileName = meta?.FileName ?? file.FileName,
            MimeType = meta?.MimeType ?? file.ContentType ?? "application/octet-stream",
            FileSize = meta?.FileSize ?? file.Length,
            CreatedAt = DateTimeOffset.UtcNow
        };

        store.AddChatbotFile(chatbotFile);

        await using var stream = file.OpenReadStream();
        await vectorStore.IndexChatbotFileAsync(chatbotFile, stream, cancellationToken);

        created.Add(ToChatbotFileResponse(chatbotFile));
    }

    return Results.Created($"/chatbots/{chatbotId}/files", created);
});

app.MapDelete("/chatbots/{chatbotId:long}/files/{fileId:long}", (long chatbotId, long fileId, InMemoryStore store, VectorStoreService vectorStore) =>
{
    if (!store.TryGetChatbot(chatbotId, out _))
    {
        return Results.NotFound(new { message = "Chatbot not found" });
    }

    if (!store.TryGetChatbotFile(fileId, out var chatbotFile) || chatbotFile.ChatbotId != chatbotId)
    {
        return Results.NotFound(new { message = "File not found for chatbot" });
    }

    store.DeleteChatbotFile(fileId, out _);
    vectorStore.RemoveChatbotFile(chatbotId, fileId);

    return Results.NoContent();
});

app.MapPost("/chatbots/{chatbotId:long}/sessions", (long chatbotId, SessionCreateRequest request, InMemoryStore store) =>
{
    if (!store.TryGetChatbot(chatbotId, out _))
    {
        return Results.NotFound(new { message = "Chatbot not found" });
    }

    if (!ValidationExtensions.TryValidate(request, out var errors))
    {
        return Results.ValidationProblem(errors);
    }

    var session = new Session
    {
        Id = store.NextSessionId(),
        ChatbotId = chatbotId,
        SessionId = request.SessionId,
        UserIdentity = request.UserIdentity,
        Title = request.Title,
        CreatedAt = DateTimeOffset.UtcNow
    };

    store.AddSession(session);

    var response = new SessionResponse(
        session.Id,
        chatbotId,
        session.SessionId,
        session.UserIdentity,
        session.Title,
        session.CreatedAt,
        session.Messages.Count);

    return Results.Created($"/chatbots/{chatbotId}/sessions/{session.SessionId}", response);
});

app.MapDelete("/chatbots/{chatbotId:long}/sessions/{sessionId}", (long chatbotId, string sessionId, InMemoryStore store) =>
{
    if (!store.TryGetSessionByIdentifier(sessionId, out var session) || session.ChatbotId != chatbotId)
    {
        return Results.NotFound(new { message = "Session not found for chatbot" });
    }

    store.DeleteSession(session.Id);
    return Results.NoContent();
});

app.MapPost("/chatbots/{chatbotId:long}/sessions/{sessionId}/messages", async (
    long chatbotId,
    string sessionId,
    [FromForm] SendMessageRequest request,
    InMemoryStore store) =>
{
    if (!store.TryGetChatbot(chatbotId, out var chatbot))
    {
        return Results.NotFound(new { message = "Chatbot not found" });
    }

    if (!store.TryGetSessionByIdentifier(sessionId, out var session) || session.ChatbotId != chatbotId)
    {
        return Results.NotFound(new { message = "Session not found for chatbot" });
    }

    if (!ValidationExtensions.TryValidate(request, out var errors))
    {
        return Results.ValidationProblem(errors);
    }

    var fileMetadata = request.MetadataForFiles ?? new List<FileMetadataDto>();
    var files = request.Files ?? new FormFileCollection();

    var metadataErrors = new Dictionary<string, string[]>();
    for (int i = 0; i < fileMetadata.Count; i++)
    {
        if (!ValidationExtensions.TryValidate(fileMetadata[i], out var fileErrors))
        {
            metadataErrors[$"MetadataForFiles[{i}]"] = fileErrors.SelectMany(e => e.Value).ToArray();
        }
    }

    if (fileMetadata.Count != files.Count)
    {
        metadataErrors[nameof(request.MetadataForFiles)] = new[] { "Metadata entries must match the number of uploaded files." };
    }

    if (metadataErrors.Count > 0)
    {
        return Results.ValidationProblem(metadataErrors);
    }

    var message = new Message
    {
        Id = store.NextMessageId(),
        SessionId = session.Id,
        SenderType = request.SenderType,
        Content = request.Content,
        ResponseId = request.ResponseId,
        ParentResponseId = request.ParentResponseId,
        Metadata = request.Metadata,
        Usage = request.Usage,
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow
    };

    for (int i = 0; i < files.Count; i++)
    {
        var file = files[i];
        var fileMeta = fileMetadata.ElementAtOrDefault(i);
        var attachment = new FileAttachment
        {
            Id = store.NextFileId(),
            MessageId = message.Id,
            S3Key = fileMeta?.S3Key ?? $"messages/{message.Id}/files/{Guid.NewGuid()}",
            FileName = fileMeta?.FileName ?? file.FileName,
            MimeType = fileMeta?.MimeType ?? file.ContentType ?? "application/octet-stream",
            FileSize = fileMeta?.FileSize ?? file.Length,
            CreatedAt = DateTimeOffset.UtcNow
        };

        store.AddFile(attachment);
        message.Files.Add(attachment);
    }

    session.Messages.Add(message);

    var response = new MessageResponse(
        message.Id,
        message.SessionId,
        message.SenderType,
        message.Content,
        message.ResponseId,
        message.ParentResponseId,
        message.Metadata,
        message.Usage,
        message.CreatedAt,
        message.UpdatedAt,
        message.Files.Select(ToAttachmentResponse).ToList());

    return Results.Created($"/chatbots/{chatbotId}/sessions/{sessionId}/messages/{message.Id}", response);
});

app.MapGet("/files/{fileId:long}/download-url", (long fileId, InMemoryStore store, PresignedUrlService urlService) =>
{
    if (store.TryGetMessageFile(fileId, out var messageFile))
    {
        var response = urlService.GenerateDownloadUrl(messageFile.Id, messageFile.S3Key, TimeSpan.FromMinutes(15));
        return Results.Ok(response);
    }

    if (store.TryGetChatbotFile(fileId, out var chatbotFile))
    {
        var response = urlService.GenerateDownloadUrl(chatbotFile.Id, chatbotFile.S3Key, TimeSpan.FromMinutes(15));
        return Results.Ok(response);
    }

    return Results.NotFound(new { message = "File not found" });
});

app.MapGet("/chatbots/{chatbotId:long}/sessions/{sessionId}/stream", (
    long chatbotId,
    string sessionId,
    InMemoryStore store,
    CancellationToken cancellationToken) =>
{
    if (!store.TryGetChatbot(chatbotId, out var chatbot))
    {
        return Results.NotFound(new { message = "Chatbot not found" });
    }

    if (!store.TryGetSessionByIdentifier(sessionId, out var session) || session.ChatbotId != chatbotId)
    {
        return Results.NotFound(new { message = "Session not found for chatbot" });
    }

    var serializedMeta = JsonSerializer.Serialize(chatbot.Meta);

    return Results.Stream(async (stream, ct) =>
    {
        await using var writer = new StreamWriter(stream) { AutoFlush = true };
        await writer.WriteLineAsync("event: meta");
        await writer.WriteLineAsync($"data: {serializedMeta}");
        await writer.WriteLineAsync();

        var tokens = new[]
        {
            $"Starting streamed response for session '{session.SessionId}'.",
            "Generating content...",
            "Streaming complete."
        };

        foreach (var token in tokens)
        {
            ct.ThrowIfCancellationRequested();
            await writer.WriteLineAsync($"data: {token}");
            await writer.WriteLineAsync();
            await writer.FlushAsync();
            await Task.Delay(TimeSpan.FromMilliseconds(350), ct);
        }

        await writer.WriteLineAsync("event: done");
        await writer.WriteLineAsync("data: [DONE]");
        await writer.WriteLineAsync();
        await writer.FlushAsync();
    }, "text/event-stream");
});

app.Run();

static ChatbotResponse ToResponse(Chatbot chatbot) => new(
    chatbot.Id,
    chatbot.Name,
    chatbot.Description,
    chatbot.Meta,
    chatbot.InitialResponseId,
    chatbot.CreatedAt,
    chatbot.UpdatedAt);

static FileAttachmentResponse ToAttachmentResponse(FileAttachment file) =>
    new(file.Id, file.S3Key, file.FileName, file.MimeType, file.FileSize, file.CreatedAt);

static ChatbotFileResponse ToChatbotFileResponse(ChatbotFile file) =>
    new(file.Id, file.ChatbotId, file.S3Key, file.FileName, file.MimeType, file.FileSize, file.CreatedAt, file.IndexedAt);
