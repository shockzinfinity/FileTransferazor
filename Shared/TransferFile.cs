using System;
using System.IO;

namespace FileTransferazor.Shared
{
    public class TransferFile : IDisposable
    {
        public string Name { get; set; } = string.Empty;
        public Stream Content { get; set; } = Stream.Null;
        public string ContentType { get; set; } = "application/octet-stream";

        private IDisposable? _responseReference;

        /// <summary>
        /// Holds a reference to the S3 GetObjectResponse so it stays alive while streaming.
        /// </summary>
        public void SetResponseReference(IDisposable response)
        {
            _responseReference = response;
        }

        public void Dispose()
        {
            Content?.Dispose();
            _responseReference?.Dispose();
        }
    }
}
