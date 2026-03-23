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
        try
        {
        var file = await ctx.GetFileAsync();
        var metadata = await file.GetMetadataAsync(ctx.CancellationToken);

        var groupId = metadata.TryGetValue("groupId", out var gid) ? gid.GetString(System.Text.Encoding.UTF8) : null;
        var senderEmail = metadata.TryGetValue("senderEmail", out var se) ? se.GetString(System.Text.Encoding.UTF8) : null;
        var receiverEmail = metadata.TryGetValue("receiverEmail", out var re) ? re.GetString(System.Text.Encoding.UTF8) : null;
        var filename = FileNameNormalizer.Normalize(
            metadata.TryGetValue("filename", out var fn) ? fn.GetString(System.Text.Encoding.UTF8) : "unknown");
        var contentType = metadata.TryGetValue("contentType", out var ct) ? ct.GetString(System.Text.Encoding.UTF8) : "application/octet-stream";

        if (string.IsNullOrWhiteSpace(groupId) || string.IsNullOrWhiteSpace(senderEmail) || string.IsNullOrWhiteSpace(receiverEmail))
        {
            _logger.LogError("Missing required metadata for tus upload {FileId}", file.Id);
            return;
        }

        _logger.LogInformation("Tus upload complete: {FileId}, group={GroupId}, file={Filename}", file.Id, groupId, filename);

        using var scope = _scopeFactory.CreateScope();
        var fileStorage = scope.ServiceProvider.GetRequiredService<IFileStorageProvider>();
        var dbContext = scope.ServiceProvider.GetRequiredService<FileTransferazorDbContext>();
        var backgroundJobs = scope.ServiceProvider.GetRequiredService<IBackgroundJobClient>();

        // Upsert FileSendData by groupId (조회 우선, race condition 시 재조회)
        var fileSendData = await dbContext.FileSendDatas.FirstOrDefaultAsync(f => f.GroupId == groupId);
        if (fileSendData == null)
        {
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
                // 동시에 다른 파일이 먼저 삽입한 경우
                dbContext.ChangeTracker.Clear();
                fileSendData = await dbContext.FileSendDatas.FirstAsync(f => f.GroupId == groupId);
            }
        }

        // 중복 체크: 같은 그룹 내 같은 파일명이 이미 저장되어 있으면 건너뛰기
        var existingFile = await dbContext.FileStorageDatas
            .AnyAsync(f => f.FileSendDataId == fileSendData.Id && f.OriginalFileName == filename);
        if (existingFile)
        {
            _logger.LogInformation("Skipping duplicate file: {Filename} in group {GroupId}", filename, groupId);
            // tus 임시 파일만 삭제하고 종료
            var diskStore = (tusdotnet.Stores.TusDiskStore)ctx.Store;
            await diskStore.DeleteFileAsync(file.Id, ctx.CancellationToken);
            return;
        }

        // Upload to storage
        await using var fileContent = await file.GetContentAsync(ctx.CancellationToken);
        var fileKey = await fileStorage.UploadFileAsync(filename, contentType, fileContent);

        // Save FileStorageData
        var fileStorageData = new FileStorageData
        {
            FileSendDataId = fileSendData.Id,
            FileUri = fileKey,
            OriginalFileName = filename,
            ContentType = contentType
        };
        dbContext.FileStorageDatas.Add(fileStorageData);
        await dbContext.SaveChangesAsync();

        // Schedule 24h deletion
        backgroundJobs.Schedule<IFileStorageProvider>(
            manager => manager.DeleteFileAsync(fileKey),
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "OnFileCompleteAsync failed");
            throw;
        }
    }
}
