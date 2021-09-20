using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FileTransferazor.Shared
{
    public class FileStorageData
    {
        [Key]
        [ForeignKey("FileSendData")]
        public int FileSendDataId { get; set; }
        public FileSendData FileSendData { get; set; }
        [DataType(DataType.Url)]
        public string FileUri { get; set; }
    }
}
