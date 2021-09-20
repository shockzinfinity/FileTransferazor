using System;
using System.Collections.Generic;
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
        private FileSendData _fileSendData = new FileSendData();
        [Inject] public IDialogService Dialog { get; set; }
        [Inject] public NavigationManager NavigationManager { get; set; }
        [Inject] public ISnackbar Snackbar { get; set; }
        private List<IBrowserFile> loadedFiles = new();
        private bool _isLoading;
        [Inject] public ILogger<SendFileForm> Logger { get; set; }

        public async Task HandleValidSubmit()
        {
            await ExecuteDialog();
        }

        public async Task HandleSelected(InputFileChangeEventArgs e)
        {
            _isLoading = true;
            loadedFiles.Clear();

            foreach (var file in e.GetMultipleFiles())
            {
                try
                {
                    loadedFiles.Add(file);
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
