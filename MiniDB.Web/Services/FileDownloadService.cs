using Microsoft.JSInterop;

namespace MiniDB.Web.Services;

public sealed class FileDownloadService(IJSRuntime js) : IAsyncDisposable
{
    private IJSObjectReference? _module;

    public async Task DownloadAsync(string fileName, byte[] contents, CancellationToken token = default)
    {
        _module ??= await js.InvokeAsync<IJSObjectReference>("import", token, "./js/download.js");
        using var stream = new MemoryStream(contents);
        using var reference = new DotNetStreamReference(stream);
        await _module.InvokeVoidAsync("downloadFile", token, fileName, reference);
    }

    public async ValueTask DisposeAsync()
    {
        if (_module is not null) await _module.DisposeAsync();
    }
}
