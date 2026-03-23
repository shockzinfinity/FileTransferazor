using FileTransferazor.Shared;
using Microsoft.Extensions.Options;
using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace FileTransferazor.Server.Services
{
    public class LocalFileStorageOptions
    {
        public const string SectionName = "LocalFileStorage";
        public string StoragePath { get; set; } = Path.Combine(Path.GetTempPath(), "filetransferazor-storage");
    }

    public class LocalFileStorageProvider : IFileStorageProvider
    {
        private readonly string _storagePath;

        public LocalFileStorageProvider(IOptions<LocalFileStorageOptions> options)
        {
            _storagePath = Path.GetFullPath(options.Value.StoragePath);
            Directory.CreateDirectory(_storagePath);
        }

        public async Task<string> UploadFileAsync(string fileName, string contentType, Stream fileStream)
        {
            var extension = SanitizeExtension(fileName);
            var fileKey = $"{DateTime.UtcNow.Ticks}-{Guid.NewGuid():N}{extension}";
            var filePath = GetSafePath(fileKey);

            await using var fileOutputStream = new FileStream(filePath, FileMode.Create, FileAccess.Write);
            await fileStream.CopyToAsync(fileOutputStream);

            return fileKey;
        }

        public async Task<TransferFile> DownloadFileAsync(string fileName)
        {
            var filePath = GetSafePath(fileName);

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"로컬 저장소에서 파일을 찾을 수 없습니다: {fileName}");
            }

            var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read);

            return new TransferFile
            {
                Name = fileName,
                Content = stream
            };
        }

        public Task DeleteFileAsync(string fileName)
        {
            var filePath = GetSafePath(fileName);

            if (File.Exists(filePath))
                File.Delete(filePath);

            return Task.CompletedTask;
        }

        /// <summary>
        /// Path Traversal 방지: 결합된 경로가 저장소 밖으로 벗어나지 않도록 검증
        /// </summary>
        private string GetSafePath(string fileKey)
        {
            var fullPath = Path.GetFullPath(Path.Combine(_storagePath, fileKey));
            if (!fullPath.StartsWith(_storagePath, StringComparison.OrdinalIgnoreCase))
            {
                throw new UnauthorizedAccessException($"잘못된 파일 경로: {fileKey}");
            }
            return fullPath;
        }

        /// <summary>
        /// 확장자에서 안전한 문자만 허용 (영숫자, 점, 하이픈)
        /// </summary>
        private static string SanitizeExtension(string fileName)
        {
            var ext = Path.GetExtension(fileName);
            if (string.IsNullOrEmpty(ext))
                return string.Empty;

            // 영숫자, 점, 하이픈만 허용
            ext = Regex.Replace(ext, @"[^a-zA-Z0-9.\-]", "");
            return string.IsNullOrEmpty(ext) ? string.Empty : $".{ext.TrimStart('.')}";
        }
    }
}
