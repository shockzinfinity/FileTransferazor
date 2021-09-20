using FileTransferazor.Shared;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace FileTransferazor.Server.Services
{
    public interface IAwsS3FileManager
    {
        Task<string> UploadFileAsync(string fileName, Stream fileStream);
        Task<TransferFile> DownloadFileAsync(string fileName);
        Task DeleteFileAsync(string fileName);
    }
}
