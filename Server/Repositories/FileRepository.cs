using FileTransferazor.Server.Data;
using FileTransferazor.Server.Services;
using FileTransferazor.Shared;
using Hangfire;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace FileTransferazor.Server.Repositories
{
    public class FileRepository : IFileRepository
    {
        private readonly FileTransferazorDbContext _dbContext;
        private readonly ILogger<FileRepository> _logger;
        private readonly IFileStorageProvider _fileStorage;
        private readonly IBackgroundJobClient _backgroundJobClient;

        public FileRepository(FileTransferazorDbContext dbContext, ILogger<FileRepository> logger, IFileStorageProvider fileStorage, IBackgroundJobClient backgroundJobClient)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _fileStorage = fileStorage ?? throw new ArgumentNullException(nameof(fileStorage));
            _backgroundJobClient = backgroundJobClient ?? throw new ArgumentNullException(nameof(backgroundJobClient));
        }

        public async Task<TransferFile> DownloadFileAsync(string fileName)
        {
            var dbFileData = await _dbContext.FileStorageDatas.Include(f => f.FileSendData).SingleOrDefaultAsync(f => f.FileUri == fileName);
            if (dbFileData == null)
            {
                return new TransferFile();
            }

            var file = await _fileStorage.DownloadFileAsync(fileName);
            file.ContentType = dbFileData.ContentType;
            file.Name = dbFileData.OriginalFileName;

            _dbContext.FileSendDatas.Remove(dbFileData.FileSendData);
            _dbContext.FileStorageDatas.Remove(dbFileData);
            await _dbContext.SaveChangesAsync();

            _backgroundJobClient.Schedule<IFileStorageProvider>(a => a.DeleteFileAsync(fileName), TimeSpan.FromMinutes(30));

            return file;
        }

        public async Task UploadFileToS3Async(FileSendData fileSendData, IEnumerable<IFormFile> files)
        {
            _dbContext.FileSendDatas.Add(fileSendData);

            List<string> scheduledFiles = new();

            foreach (var item in files)
            {
                var contentType = item.ContentType ?? "application/octet-stream";
                var s3FileName = await _fileStorage.UploadFileAsync(item.FileName, contentType, item.OpenReadStream());
                scheduledFiles.Add(s3FileName);
                _dbContext.FileStorageDatas.Add(new FileStorageData
                {
                    FileSendDataId = fileSendData.Id,
                    FileSendData = fileSendData,
                    FileUri = s3FileName,
                    OriginalFileName = FileNameNormalizer.Normalize(item.FileName),
                    ContentType = contentType
                });
            }

            await _dbContext.SaveChangesAsync();

            foreach (var item in scheduledFiles)
            {
                _backgroundJobClient.Schedule<IFileStorageProvider>(f => f.DeleteFileAsync(item), TimeSpan.FromHours(24));
            }
        }
    }
}
