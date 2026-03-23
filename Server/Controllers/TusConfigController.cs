using FileTransferazor.Server.Options;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace FileTransferazor.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TusConfigController : ControllerBase
    {
        [HttpGet]
        public IActionResult Get([FromServices] IOptions<TusOptions> options)
        {
            var tus = options.Value;
            return Ok(new
            {
                tus.MaxFileSizeInBytes,
                tus.ChunkSizeInBytes
            });
        }
    }
}
