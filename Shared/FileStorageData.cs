using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FileTransferazor.Shared
{
    public class FileStorageData
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        [ForeignKey("FileSendData")]
        public int FileSendDataId { get; set; }
        public FileSendData FileSendData { get; set; } = null!;
        [DataType(DataType.Url)]
        public string FileUri { get; set; } = string.Empty;
        public string OriginalFileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = "application/octet-stream";
    }
}
