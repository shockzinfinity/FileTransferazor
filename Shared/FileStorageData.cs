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
        public FileSendData FileSendData { get; set; }
        [DataType(DataType.Url)]
        public string FileUri { get; set; }
        public string OriginalFileName { get; set; }
    }
}
