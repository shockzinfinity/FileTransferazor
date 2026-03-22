using FileTransferazor.Server.Repositories;
using Microsoft.AspNetCore.Mvc;
using System;
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

            HttpContext.Response.RegisterForDispose(file);
            return File(file.Content, file.ContentType, file.Name, enableRangeProcessing: false);
        }
    }
}
