using FileTransferazor.Server.Data;
using FileTransferazor.Server.Options;
using FileTransferazor.Server.Repositories;
using FileTransferazor.Server.Services;
using GmailOptions = FileTransferazor.Server.Options.GmailOptions;
using Amazon;
using Amazon.S3;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Hangfire.PostgreSql;
using tusdotnet;
using tusdotnet.Models;
using tusdotnet.Models.Configuration;
using tusdotnet.Stores;

var builder = WebApplication.CreateBuilder(args);

// Options
builder.Services.Configure<AwsS3Options>(builder.Configuration.GetSection(AwsS3Options.SectionName));
builder.Services.Configure<GmailOptions>(builder.Configuration.GetSection(GmailOptions.SectionName));
builder.Services.Configure<TusOptions>(builder.Configuration.GetSection(TusOptions.SectionName));

// Kestrel - disable max request body size (tusdotnet manages its own limit)
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = null;
});

// Database
builder.Services.AddDbContext<FileTransferazorDbContext>(options =>
{
    // options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"));
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"));
});

// Hangfire
// builder.Services.AddHangfire(x => x.UseSqlServerStorage(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddHangfire(x => x.UsePostgreSqlStorage(c =>
    c.UseNpgsqlConnection(builder.Configuration.GetConnectionString("DefaultConnection"))));
builder.Services.AddHangfireServer();

// AWS (GmailEmailSender에서도 사용하므로 항상 등록)
builder.Services.AddScoped(sp => new AwsParameterStoreClient(RegionEndpoint.APNortheast2));

// File Storage Provider (S3 or Local)
var fileStorageProvider = builder.Configuration.GetValue<string>("FileStorage:Provider") ?? "Local";
if (fileStorageProvider.Equals("S3", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddAWSService<IAmazonS3>();
    builder.Services.AddScoped<IFileStorageProvider, AwsS3FileManager>();
}
else
{
    builder.Services.Configure<LocalFileStorageOptions>(
        builder.Configuration.GetSection(LocalFileStorageOptions.SectionName));
    builder.Services.AddScoped<IFileStorageProvider, LocalFileStorageProvider>();
}

builder.Services.AddScoped<IFileRepository, FileRepository>();
builder.Services.AddScoped<IEmailSender, GmailEmailSender>();

// Tus
builder.Services.AddSingleton<TusUploadService>();

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

var app = builder.Build();

// Ensure tus storage directory exists
var tusOptions = builder.Configuration.GetSection(TusOptions.SectionName).Get<TusOptions>() ?? new TusOptions();
Directory.CreateDirectory(tusOptions.StoragePath);

// Clean up stale tus temp files on startup (older than ExpirationHours)
foreach (var file in Directory.GetFiles(tusOptions.StoragePath))
{
    if (File.GetLastWriteTimeUtc(file) < DateTime.UtcNow.AddHours(-tusOptions.ExpirationHours))
    {
        File.Delete(file);
    }
}

// Hangfire Dashboard: Development only
if (app.Environment.IsDevelopment())
{
    app.UseHangfireDashboard();
}

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseBlazorFrameworkFiles();
app.UseStaticFiles();

app.UseRouting();

app.MapRazorPages();
app.MapControllers();

app.MapTus("/api/tus", async httpContext =>
{
    var tusUploadService = httpContext.RequestServices.GetRequiredService<TusUploadService>();

    return new DefaultTusConfiguration
    {
        Store = new TusDiskStore(tusOptions.StoragePath),
        MaxAllowedUploadSizeInBytesLong = tusOptions.MaxFileSizeInBytes,
        Events = new Events
        {
            OnBeforeCreateAsync = ctx =>
            {
                var logger = ctx.HttpContext.RequestServices.GetRequiredService<ILogger<TusUploadService>>();
                var filename = ctx.Metadata.TryGetValue("filename", out var fn) ? fn.GetString(System.Text.Encoding.UTF8) : "unknown";
                logger.LogInformation("tus CREATE: {Filename}, UploadLength={Length}", filename, ctx.UploadLength);

                var metadata = ctx.Metadata;
                if (!metadata.ContainsKey("senderEmail") || !metadata.ContainsKey("receiverEmail"))
                {
                    ctx.FailRequest("senderEmail and receiverEmail metadata are required");
                }
                return Task.CompletedTask;
            },
            OnBeforeWriteAsync = ctx =>
            {
                var logger = ctx.HttpContext.RequestServices.GetRequiredService<ILogger<TusUploadService>>();
                logger.LogInformation("tus WRITE: FileId={FileId}, Offset={Offset}", ctx.FileId, ctx.UploadOffset);
                return Task.CompletedTask;
            },
            OnFileCompleteAsync = ctx => tusUploadService.OnFileCompleteAsync(ctx)
        }
    };
});

app.MapFallbackToFile("index.html");

app.Run();
