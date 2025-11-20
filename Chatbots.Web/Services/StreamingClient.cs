using Microsoft.JSInterop;

namespace Chatbots.Web.Services;

public class StreamingClient : IAsyncDisposable
{
    private readonly Lazy<Task<IJSObjectReference>> _moduleTask;

    public StreamingClient(IJSRuntime jsRuntime)
    {
        _moduleTask = new(() => jsRuntime.InvokeAsync<IJSObjectReference>("import", "./streaming.js").AsTask());
    }

    public async Task StartAsync<T>(string key, string url, DotNetObjectReference<T> dotNetObject) where T : class
    {
        var module = await _moduleTask.Value;
        await module.InvokeVoidAsync("startEventStream", key, url, dotNetObject);
    }

    public async Task StopAsync(string key)
    {
        var module = await _moduleTask.Value;
        await module.InvokeVoidAsync("stopEventStream", key);
    }

    public async ValueTask DisposeAsync()
    {
        if (_moduleTask.IsValueCreated)
        {
            var module = await _moduleTask.Value;
            await module.DisposeAsync();
        }
    }
}
