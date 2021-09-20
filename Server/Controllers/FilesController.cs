using FileTransferazor.Server.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FileTransferazor.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class FilesController : ControllerBase
    {
        private readonly IFileRepository _fileRepository;

        public FilesController(IFileRepository fileRepository)
        {
            _fileRepository = fileRepository ?? throw new ArgumentNullException(nameof(fileRepository));
        }

        [HttpGet("{fileName}")]
        public async Task<IActionResult> GetBlobDownload([FromRoute] string fileName)
        {
            var file = await _fileRepository.DownloadFileAsync(fileName);
            if (file.Content == null)
            {
                return Redirect("/");
            }

            // NOTE: multiple files => doesn't need it because the receiver click individual link
            //var content = new System.IO.MemoryStream(file.Content);
            var contentType = "application/octet-stream";

            return File(file.Content, contentType, file.Name);
        }
    }
}
