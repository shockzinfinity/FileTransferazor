using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Transfer;
using FileTransferazor.Shared;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace FileTransferazor.Server.Services
{
    public class AwsS3FileManager : IAwsS3FileManager
    {
        private readonly IAmazonS3 _s3Client;
        private readonly string _bucket;

        public AwsS3FileManager(IAmazonS3 s3Client)
        {
            _s3Client = s3Client ?? throw new ArgumentNullException(nameof(s3Client));
            _bucket = "transferazor-bucket";
        }

        public async Task DeleteFileAsync(string fileName)
        {
            // TODO: test
            try
            {
                var deleteObjectRequest = new DeleteObjectRequest
                {
                    BucketName = _bucket,
                    Key = fileName
                };

                var result = await _s3Client.DeleteObjectAsync(deleteObjectRequest);
            }
            catch (AmazonS3Exception)
            {
                throw;
            }
            catch (Exception)
            {
                throw;
            }
        }

        public async Task<TransferFile> DownloadFileAsync(string fileName)
        {
            // TODO: just return stream

            var request = new GetObjectRequest
            {
                BucketName = _bucket,
                Key = fileName
            };

            using (var objectReference = await _s3Client.GetObjectAsync(request))
            {
                if (objectReference.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    throw new Exception("Could not find file.");
                }

                using (var responseStream = objectReference.ResponseStream)
                {
                    return new TransferFile
                    {
                        Name = fileName,
                        Content = responseStream
                    };
                }
            }
        }

        public async Task<string> UploadFileAsync(string fileName, Stream fileStream)
        {
            var s3FileName = $"{DateTime.Now.Ticks}-{WebUtility.HtmlEncode(fileName)}";

            var transferRequest = new TransferUtilityUploadRequest()
            {
                ContentType = "application/zip",
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
