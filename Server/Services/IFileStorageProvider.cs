using FileTransferazor.Shared;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace FileTransferazor.Server.Services
{
    public interface IFileStorageProvider
    {
        Task<string> UploadFileAsync(string fileName, string contentType, Stream fileStream);
        Task<TransferFile> DownloadFileAsync(string fileName);
        Task DeleteFileAsync(string fileName);
    }

    /// <summary>
    /// 파일명 정규화 유틸리티.
    /// macOS(NFD)와 Windows/Linux(NFC)에서 한글 등 유니코드 파일명이
    /// 다르게 인코딩되는 문제를 방지합니다.
    /// </summary>
    public static class FileNameNormalizer
    {
        public static string Normalize(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return fileName;

            return fileName.IsNormalized(NormalizationForm.FormC)
                ? fileName
                : fileName.Normalize(NormalizationForm.FormC);
        }
    }
}
