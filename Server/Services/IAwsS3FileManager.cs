using FileTransferazor.Shared;
using System.IO;
using System.Threading.Tasks;

namespace FileTransferazor.Server.Services
{
    public interface IAwsS3FileManager
    {
        Task<string> UploadFileAsync(string fileName, string contentType, Stream fileStream);
        Task<TransferFile> DownloadFileAsync(string fileName);
        Task DeleteFileAsync(string fileName);
    }
}
