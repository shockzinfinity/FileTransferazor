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
        private readonly IFileRepository _fileRepository;
        private readonly ILogger<FileWithDataController> _logger;

        public FileWithDataController(ILogger<FileWithDataController> logger, IFileRepository fileRepository)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _fileRepository = fileRepository ?? throw new ArgumentNullException(nameof(fileRepository));
        }

        [HttpPost]
        public async Task<IActionResult> Upload([FromForm] FormDataDto dto)
        {
            await _fileRepository.UploadFileToS3Async(dto.Data, dto.FileToUploads);
            //_logger.LogInformation(dto.Data.SenderEmail);
            //_logger.LogInformation(dto.Data.ReceiverEmail);
            //foreach (var item in dto.FileToUploads)
            //{
            //    _logger.LogInformation(item.FileName);
            //    _logger.LogInformation(item.ContentType);
            //    _logger.LogInformation(item.Length.ToString());
            //}

            return Ok();
        }
    }
}
