using FileTransferazor.Shared;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace FileTransferazor.Server.Repositories
{
    public interface IFileRepository
    {
        Task UploadFileToS3Async(FileSendData fileSendData, IEnumerable<IFormFile> files);
        Task<TransferFile> DownloadFileAsync(string fileName);
    }
}
