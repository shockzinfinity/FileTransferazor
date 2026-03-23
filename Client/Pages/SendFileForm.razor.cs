using FileTransferazor.Client.Services;
using FileTransferazor.Client.Shared;
using FileTransferazor.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using MudBlazor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace FileTransferazor.Client.Pages
{
    public partial class SendFileForm : IDisposable
    {
        private const long MaxFileSize = 10L * 1024L * 1024L * 1024L; // 10 GB

        private FileSendData _fileSendData = new FileSendData();
        private List<IBrowserFile> loadedFiles = new();
        private bool _isLoading;
        private DotNetObjectReference<SendFileForm>? _dotNetRef;
        private Dictionary<string, UploadState> _uploadStates = new();

        [Inject] public IDialogService Dialog { get; set; } = default!;
        [Inject] public NavigationManager NavigationManager { get; set; } = default!;
        [Inject] public ISnackbar Snackbar { get; set; } = default!;
        [Inject] public ILogger<SendFileForm> Logger { get; set; } = default!;
        [Inject] public HttpClient Http { get; set; } = default!;
        [Inject] public TusInteropService TusService { get; set; } = default!;

        public bool IsUploading => _uploadStates.Any(s => !s.Value.IsComplete && !s.Value.HasError);

        public double OverallProgress
        {
            get
            {
                if (!_uploadStates.Any()) return 0;
                var totalBytes = _uploadStates.Values.Sum(s => s.TotalBytes);
                if (totalBytes == 0) return 0;
                var uploaded = _uploadStates.Values.Sum(s => s.BytesUploaded);
                return (double)uploaded / totalBytes * 100;
            }
        }

        public async Task HandleValidSubmit()
        {
            if (!loadedFiles.Any())
            {
                Snackbar.Add("Please select at least one file.", Severity.Warning);
                return;
            }

            _dotNetRef = DotNetObjectReference.Create(this);
            _isLoading = true;

            // 서버에서 tus 설정 가져오기
            var chunkSize = 5 * 1024 * 1024; // fallback
            try
            {
                var configResponse = await Http.GetFromJsonAsync<TusConfigResponse>("api/TusConfig");
                if (configResponse != null)
                    chunkSize = configResponse.ChunkSizeInBytes;
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to fetch tus config, using default chunk size");
            }

            var fileNames = loadedFiles.Select(f => f.Name).ToArray();
            var senderEmail = _fileSendData.SenderEmail ?? string.Empty;
            var receiverEmail = _fileSendData.ReceiverEmail ?? string.Empty;

            _uploadStates.Clear();

            // B: 이전 세션 복원 시도 (같은 이메일 + 같은 파일 구성이면 groupId 재사용)
            string groupId;
            var previousSession = await TusService.LoadSessionAsync(senderEmail, receiverEmail, fileNames);
            if (previousSession != null)
            {
                groupId = previousSession.GroupId;
                Logger.LogInformation("Resuming previous session: {GroupId}", groupId);
                // 이전 세션 유지 (덮어쓰지 않음)
            }
            else
            {
                groupId = Guid.NewGuid().ToString();
                // 새 세션만 저장
                await TusService.SaveSessionAsync(groupId, senderEmail, receiverEmail, fileNames);
            }

            var endpoint = $"{NavigationManager.BaseUri}api/tus";

            for (var i = 0; i < loadedFiles.Count; i++)
            {
                var file = loadedFiles[i];

                // A + B: 이미 완료된 파일은 건너뛰기
                var alreadyComplete = await TusService.IsFileCompleteAsync(groupId, file.Name);
                if (alreadyComplete)
                {
                    // 이전 세션에서 완료된 파일 — UI에 완료 상태로 표시
                    var skipId = Guid.NewGuid().ToString();
                    _uploadStates[skipId] = new UploadState
                    {
                        FileName = file.Name,
                        BytesUploaded = file.Size,
                        TotalBytes = file.Size,
                        IsComplete = true
                    };
                    Logger.LogInformation("Skipping already completed file: {FileName}", file.Name);
                    continue;
                }

                var uploadId = Guid.NewGuid().ToString();

                _uploadStates[uploadId] = new UploadState
                {
                    FileName = file.Name,
                    BytesUploaded = 0,
                    TotalBytes = file.Size,
                    IsComplete = false,
                    HasError = false,
                    ErrorMessage = string.Empty
                };

                var metadata = new Dictionary<string, string>
                {
                    { "groupId", groupId },
                    { "senderEmail", senderEmail },
                    { "receiverEmail", receiverEmail },
                    { "filename", file.Name },
                    { "contentType", file.ContentType ?? "application/octet-stream" }
                };

                await TusService.StartUploadAsync(_dotNetRef, uploadId, endpoint, "fileInput", i, metadata, chunkSize);
            }

            StateHasChanged();

            // 모든 파일이 이미 완료 상태면 바로 성공 처리
            if (_uploadStates.Values.All(s => s.IsComplete))
            {
                _isLoading = false;
                await TusService.ClearSessionAsync();
                await ExecuteDialog("Success", "All files have been uploaded successfully.", Color.Primary, "Ok");
                loadedFiles.Clear();
                _uploadStates.Clear();
                StateHasChanged();
            }
        }

        [JSInvokable]
        public async Task OnTusProgress(string uploadId, long bytesUploaded, long bytesTotal)
        {
            if (_uploadStates.TryGetValue(uploadId, out var state))
            {
                state.BytesUploaded = bytesUploaded;
                state.TotalBytes = bytesTotal;
            }
            await InvokeAsync(StateHasChanged);
        }

        [JSInvokable]
        public async Task OnTusSuccess(string uploadId)
        {
            if (_uploadStates.TryGetValue(uploadId, out var state))
            {
                state.IsComplete = true;
                state.BytesUploaded = state.TotalBytes;
            }

            await InvokeAsync(StateHasChanged);

            if (_uploadStates.Values.All(s => s.IsComplete))
            {
                _isLoading = false;
                await TusService.ClearSessionAsync();
                await InvokeAsync(StateHasChanged);
                await ExecuteDialog("Success", "All files have been uploaded successfully.", Color.Primary, "Ok");
                loadedFiles.Clear();
                _uploadStates.Clear();
                await InvokeAsync(StateHasChanged);
            }
        }

        [JSInvokable]
        public async Task OnTusError(string uploadId, string error)
        {
            if (_uploadStates.TryGetValue(uploadId, out var state))
            {
                state.HasError = true;
                state.ErrorMessage = error;
            }

            Snackbar.Add($"Upload failed for {_uploadStates.GetValueOrDefault(uploadId)?.FileName}: {error}", Severity.Error);
            await InvokeAsync(StateHasChanged);
        }

        public async Task CancelUpload(string uploadId)
        {
            try
            {
                await TusService.AbortUploadAsync(uploadId);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error aborting upload {UploadId}", uploadId);
            }

            if (_uploadStates.TryGetValue(uploadId, out var state))
            {
                state.HasError = true;
                state.ErrorMessage = "Cancelled by user";
            }

            StateHasChanged();
        }

        public async Task HandleSelected(InputFileChangeEventArgs e)
        {
            _isLoading = true;
            loadedFiles.Clear();
            _uploadStates.Clear();
            var maximumFileCount = 10;

            if (e.FileCount > maximumFileCount)
            {
                await ExecuteDialog("Info", $"Maximum File count is {maximumFileCount}. ({e.FileCount} selected)", Color.Error, "Ok");
                _isLoading = false;
                StateHasChanged();
                return;
            }

            foreach (var file in e.GetMultipleFiles(maximumFileCount: maximumFileCount))
            {
                try
                {
                    loadedFiles.Add(file);
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Error loading file {FileName}", file.Name);
                }
            }

            Snackbar.Add($"{e.FileCount} file(s) selected", Severity.Info);
            _isLoading = false;
        }

        public static string FormatBytes(long bytes)
        {
            string[] suffixes = ["B", "KB", "MB", "GB", "TB"];
            var index = 0;
            var size = (double)bytes;
            while (size >= 1024 && index < suffixes.Length - 1)
            {
                size /= 1024;
                index++;
            }
            return $"{size:F1} {suffixes[index]}";
        }

        private async Task ExecuteDialog(string title, string content, Color color, string buttonText)
        {
            var parameters = new DialogParameters
            {
                { "Content", content },
                { "ButtonColor", color },
                { "ButtonText", buttonText }
            };

            var dialog = await Dialog.ShowAsync<DialogNotification>(title, parameters);
            var result = await dialog.Result;
            if (result is not null && !result.Canceled)
            {
                bool.TryParse(result.Data?.ToString(), out bool shouldNavigate);
                if (shouldNavigate) NavigationManager.NavigateTo("/");
            }
        }

        public void Dispose()
        {
            _dotNetRef?.Dispose();
        }

        private class TusConfigResponse
        {
            public long MaxFileSizeInBytes { get; set; }
            public int ChunkSizeInBytes { get; set; }
        }

        public class UploadState
        {
            public string FileName { get; set; } = string.Empty;
            public long BytesUploaded { get; set; }
            public long TotalBytes { get; set; }
            public bool IsComplete { get; set; }
            public bool HasError { get; set; }
            public string ErrorMessage { get; set; } = string.Empty;

            public double ProgressPercent => TotalBytes == 0 ? 0 : (double)BytesUploaded / TotalBytes * 100;
        }
    }
}
