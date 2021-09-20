using System;
using FileTransferazor.Shared;
using Microsoft.EntityFrameworkCore;

namespace FileTransferazor.Server.Data
{
    public class FileTransferazorDbContext : DbContext
    {
        public FileTransferazorDbContext(DbContextOptions<FileTransferazorDbContext> options) : base(options)
        {
        }

        public DbSet<FileSendData> FileSendDatas { get; set; }
        public DbSet<FileStorageData> FileStorageDatas { get; set; }
    }
}
