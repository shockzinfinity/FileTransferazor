using FileTransferazor.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;

namespace FileTransferazor.Server.Models
{
    public class FormDataDto
    {
        public List<IFormFile> FileToUploads { get; set; }
        [ModelBinder(BinderType = typeof(FormDataJsonBinder))]
        public FileSendData Data { get; set; }
    }
}
