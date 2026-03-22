using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.JSInterop;

namespace FileTransferazor.Client.Services;

public class TusInteropService(IJSRuntime jsRuntime)
{
    public ValueTask StartUploadAsync(
        DotNetObjectReference<Pages.SendFileForm> dotNetRef,
        string uploadId,
        string endpoint,
        string fileInputId,
        int fileIndex,
        Dictionary<string, string> metadata,
        int chunkSize = 5 * 1024 * 1024)
    {
        return jsRuntime.InvokeVoidAsync("tusInterop.startUpload",
            dotNetRef, uploadId, endpoint, fileInputId, fileIndex, metadata, chunkSize);
    }

    public ValueTask AbortUploadAsync(string uploadId)
        => jsRuntime.InvokeVoidAsync("tusInterop.abortUpload", uploadId);

    public ValueTask PauseUploadAsync(string uploadId)
        => jsRuntime.InvokeVoidAsync("tusInterop.pauseUpload", uploadId);

    public ValueTask ResumeUploadAsync(string uploadId)
        => jsRuntime.InvokeVoidAsync("tusInterop.resumeUpload", uploadId);
}
