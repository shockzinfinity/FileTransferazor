using FileTransferazor.Server.Data;
using FileTransferazor.Shared;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using tusdotnet.Models;
using tusdotnet.Models.Configuration;

namespace FileTransferazor.Server.Services;

public class TusUploadService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TusUploadService> _logger;

    public TusUploadService(IServiceScopeFactory scopeFactory, ILogger<TusUploadService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task OnFileCompleteAsync(FileCompleteContext ctx)
    {
        var file = await ctx.GetFileAsync();
        var metadata = await file.GetMetadataAsync(ctx.CancellationToken);

        var groupId = metadata.TryGetValue("groupId", out var gid) ? gid.GetString(System.Text.Encoding.UTF8) : null;
        var senderEmail = metadata.TryGetValue("senderEmail", out var se) ? se.GetString(System.Text.Encoding.UTF8) : null;
        var receiverEmail = metadata.TryGetValue("receiverEmail", out var re) ? re.GetString(System.Text.Encoding.UTF8) : null;
        var filename = metadata.TryGetValue("filename", out var fn) ? fn.GetString(System.Text.Encoding.UTF8) : "unknown";
        var contentType = metadata.TryGetValue("contentType", out var ct) ? ct.GetString(System.Text.Encoding.UTF8) : "application/octet-stream";

        if (string.IsNullOrWhiteSpace(groupId) || string.IsNullOrWhiteSpace(senderEmail) || string.IsNullOrWhiteSpace(receiverEmail))
        {
            _logger.LogError("Missing required metadata for tus upload {FileId}", file.Id);
            return;
        }

        _logger.LogInformation("Tus upload complete: {FileId}, group={GroupId}, file={Filename}", file.Id, groupId, filename);

        using var scope = _scopeFactory.CreateScope();
        var s3Manager = scope.ServiceProvider.GetRequiredService<IAwsS3FileManager>();
        var dbContext = scope.ServiceProvider.GetRequiredService<FileTransferazorDbContext>();
        var backgroundJobs = scope.ServiceProvider.GetRequiredService<IBackgroundJobClient>();

        // Upsert FileSendData by groupId
        FileSendData fileSendData;
        try
        {
            fileSendData = new FileSendData
            {
                SenderEmail = senderEmail,
                ReceiverEmail = receiverEmail,
                GroupId = groupId
            };
            dbContext.FileSendDatas.Add(fileSendData);
            await dbContext.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Unique constraint violation — group already exists
            dbContext.ChangeTracker.Clear();
            fileSendData = await dbContext.FileSendDatas.FirstAsync(f => f.GroupId == groupId);
        }

        // Upload to S3
        await using var fileContent = await file.GetContentAsync(ctx.CancellationToken);
        var s3Key = await s3Manager.UploadFileAsync(filename, contentType, fileContent);

        // Save FileStorageData
        var fileStorageData = new FileStorageData
        {
            FileSendDataId = fileSendData.Id,
            FileUri = s3Key,
            OriginalFileName = filename
        };
        dbContext.FileStorageDatas.Add(fileStorageData);
        await dbContext.SaveChangesAsync();

        // Schedule 24h deletion
        backgroundJobs.Schedule<IAwsS3FileManager>(
            manager => manager.DeleteFileAsync(s3Key),
            TimeSpan.FromHours(24));

        // Delete tus temp file from disk
        var store = (tusdotnet.Stores.TusDiskStore)ctx.Store;
        try
        {
            await store.DeleteFileAsync(file.Id, ctx.CancellationToken);
            _logger.LogInformation("Deleted tus temp file {FileId}", file.Id);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete tus temp file {FileId}", file.Id);
        }
    }
}
