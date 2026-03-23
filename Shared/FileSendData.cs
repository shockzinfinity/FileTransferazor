using System;
using System.ComponentModel.DataAnnotations;

namespace FileTransferazor.Shared
{
    public class FileSendData
    {
        public int Id { get; set; }
        [DataType(DataType.EmailAddress)]
        public string SenderEmail { get; set; } = string.Empty;
        [DataType(DataType.EmailAddress)]
        public string ReceiverEmail { get; set; } = string.Empty;
        public string? GroupId { get; set; }
    }
}
