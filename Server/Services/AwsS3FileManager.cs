using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Transfer;
using FileTransferazor.Server.Options;
using FileTransferazor.Shared;
using Microsoft.Extensions.Options;
// IOptionsSnapshot: Scoped 수명 — 요청마다 appsettings.json 변경 사항을 재배포 없이 반영
using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;

namespace FileTransferazor.Server.Services
{
    public class AwsS3FileManager : IAwsS3FileManager
    {
        private readonly IAmazonS3 _s3Client;
        private readonly string _bucket;

        public AwsS3FileManager(IAmazonS3 s3Client, IOptionsSnapshot<AwsS3Options> options)
        {
            _s3Client = s3Client ?? throw new ArgumentNullException(nameof(s3Client));
            _bucket = options.Value.BucketName;
        }

        public async Task DeleteFileAsync(string fileName)
        {
            var deleteObjectRequest = new DeleteObjectRequest
            {
                BucketName = _bucket,
                Key = fileName
            };

            await _s3Client.DeleteObjectAsync(deleteObjectRequest);
        }

        public async Task<TransferFile> DownloadFileAsync(string fileName)
        {
            var request = new GetObjectRequest
            {
                BucketName = _bucket,
                Key = fileName
            };

            var objectReference = await _s3Client.GetObjectAsync(request);

            if (objectReference.HttpStatusCode == HttpStatusCode.NotFound)
            {
                objectReference.Dispose();
                throw new FileNotFoundException($"S3에서 파일을 찾을 수 없습니다: {fileName}");
            }

            var transferFile = new TransferFile
            {
                Name = fileName,
                Content = objectReference.ResponseStream,
                ContentType = objectReference.Headers.ContentType ?? "application/octet-stream"
            };
            transferFile.SetResponseReference(objectReference);

            return transferFile;
        }

        public async Task<string> UploadFileAsync(string fileName, string contentType, Stream fileStream)
        {
            var s3FileName = $"{DateTime.UtcNow.Ticks}-{WebUtility.HtmlEncode(fileName)}";

            var transferRequest = new TransferUtilityUploadRequest()
            {
                ContentType = contentType,
                InputStream = fileStream,
                BucketName = _bucket,
                Key = s3FileName
            };
            transferRequest.Metadata.Add("x-amz-meta-title", fileName);

            var fileTransferUtility = new TransferUtility(_s3Client);
            await fileTransferUtility.UploadAsync(transferRequest);

            return s3FileName;
        }
    }
}
