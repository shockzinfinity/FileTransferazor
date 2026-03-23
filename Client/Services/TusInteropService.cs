using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.JSInterop;

namespace FileTransferazor.Client.Services;

public class TusInteropService(IJSRuntime jsRuntime)
{
    public ValueTask StartUploadAsync(
        DotNetObjectReference<Pages.SendFileForm> dotNetRef,
        string uploadId, string endpoint, string fileInputId,
        int fileIndex, Dictionary<string, string> metadata,
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

    // --- Session 관리 ---

    public ValueTask SaveSessionAsync(string groupId, string senderEmail, string receiverEmail, string[] fileNames)
        => jsRuntime.InvokeVoidAsync("tusInterop.saveSession", groupId, senderEmail, receiverEmail, fileNames);

    public ValueTask<SessionInfo?> LoadSessionAsync(string senderEmail, string receiverEmail, string[] fileNames)
        => jsRuntime.InvokeAsync<SessionInfo?>("tusInterop.loadSession", senderEmail, receiverEmail, fileNames);

    public ValueTask ClearSessionAsync()
        => jsRuntime.InvokeVoidAsync("tusInterop.clearSession");

    public ValueTask<bool> IsFileCompleteAsync(string groupId, string filename)
        => jsRuntime.InvokeAsync<bool>("tusInterop.isFileComplete", groupId, filename);

    public class SessionInfo
    {
        public string GroupId { get; set; } = string.Empty;
        public string SenderEmail { get; set; } = string.Empty;
        public string ReceiverEmail { get; set; } = string.Empty;
        public Dictionary<string, string> Files { get; set; } = new();
    }
}
