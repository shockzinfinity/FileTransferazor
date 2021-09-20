using Amazon.Runtime.Internal.Util;
using FileTransferazor.Server.Models;
using FileTransferazor.Server.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FileTransferazor.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class FileWithDataController : ControllerBase
    {
        private const long MaxFileSize = 10L * 1024L * 1024L * 1024L; // 10 GB
        private readonly IFileRepository _fileRepository;
        private readonly ILogger<FileWithDataController> _logger;

        public FileWithDataController(ILogger<FileWithDataController> logger, IFileRepository fileRepository)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _fileRepository = fileRepository ?? throw new ArgumentNullException(nameof(fileRepository));
        }

        [HttpPost]
        //[DisableFormValueModelBinding] // NOTE: form data binding 안됨
        [RequestSizeLimit(MaxFileSize)]
        [RequestFormLimits(MultipartBodyLengthLimit = MaxFileSize)]
        public async Task<IActionResult> Upload([FromForm] FormDataDto dto)
        {
            // TODO: uploadresult return
            // TODO: server resources monitoring

            if (dto.FileToUploads.Count == 0 || dto.FileToUploads.Any(f => f.Length == 0))
            {
                return BadRequest("Upload one more files or not empty file.");
            }

            if (string.IsNullOrWhiteSpace(dto.Data.ReceiverEmail) || string.IsNullOrWhiteSpace(dto.Data.SenderEmail))
            {
                return BadRequest("SenderEmail or ReceiverEmail is empty.");
            }

            await _fileRepository.UploadFileToS3Async(dto.Data, dto.FileToUploads);

            // TODO: return Created
            return Ok();
        }
    }
}
