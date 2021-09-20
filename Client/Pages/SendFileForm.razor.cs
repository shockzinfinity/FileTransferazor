using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using FileTransferazor.Client.Shared;
using FileTransferazor.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Logging;
using MudBlazor;

namespace FileTransferazor.Client.Pages
{
    public partial class SendFileForm
    {
        private const long MaxFileSize = 10L * 1024L * 1024L * 1024L; // 10 GB
        private FileSendData _fileSendData = new FileSendData();
        [Inject] public IDialogService Dialog { get; set; }
        [Inject] public NavigationManager NavigationManager { get; set; }
        [Inject] public ISnackbar Snackbar { get; set; }
        private List<IBrowserFile> loadedFiles = new();
        private bool _isLoading;
        [Inject] public ILogger<SendFileForm> Logger { get; set; }
        [Inject] public HttpClient Http { get; set; }

        public async Task HandleValidSubmit()
        {
            using var content = new MultipartFormDataContent();

            foreach (var item in loadedFiles)
            {
                var fileStreamContent = new StreamContent(item.OpenReadStream(MaxFileSize));
                fileStreamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(item.ContentType);
                content.Add(content: fileStreamContent, name: "\"FileToUploads\"", fileName: item.Name);
            }

            content.Add(new StringContent(JsonSerializer.Serialize(_fileSendData), Encoding.UTF8, "application/json"), "Data");

            var response = await Http.PostAsync("api/FileWithData", content);
            Logger.LogInformation(await response.Content.ReadAsStringAsync());

            await ExecuteDialog();
            loadedFiles.Clear();
            // TODO: clear temp datas;
        }

        public void HandleSelected(InputFileChangeEventArgs e)
        {
            _isLoading = true;
            loadedFiles.Clear();

            foreach (var file in e.GetMultipleFiles())
            {
                try
                {
                    loadedFiles.Add(file);

                    // TODO: if images selected, send data uri to client
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Files: {file.Name} Error: {ex.Message}");
                }
            }

            Snackbar.Add("image selected", Severity.Info);
            _isLoading = false;
        }

        private async Task ExecuteDialog()
        {
            var parameters = new DialogParameters
            {
                { "Content", "You have successfully created a file data." },
                { "ButtonColor", Color.Primary },
                { "ButtonText", "Ok" }
            };

            var dialog = Dialog.Show<DialogNotification>("Success", parameters);
            var result = await dialog.Result;
            if (!result.Cancelled)
            {
                bool.TryParse(result.Data.ToString(), out bool shouldNavigate);
                if (shouldNavigate) NavigationManager.NavigateTo("/");
            }
        }
    }
}
