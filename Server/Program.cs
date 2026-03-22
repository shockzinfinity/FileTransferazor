using FileTransferazor.Server.Data;
using FileTransferazor.Server.Options;
using FileTransferazor.Server.Repositories;
using FileTransferazor.Server.Services;
using GmailOptions = FileTransferazor.Server.Options.GmailOptions;
using Amazon;
using Amazon.S3;
using Hangfire;
using Microsoft.EntityFrameworkCore;
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
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"));
});

// Hangfire
builder.Services.AddHangfire(x => x.UseSqlServerStorage(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddHangfireServer();

// AWS & Services
builder.Services.AddScoped(sp => new AwsParameterStoreClient(RegionEndpoint.APNortheast2));
builder.Services.AddAWSService<IAmazonS3>();
builder.Services.AddScoped<IAwsS3FileManager, AwsS3FileManager>();
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
                var metadata = ctx.Metadata;
                if (!metadata.ContainsKey("senderEmail") || !metadata.ContainsKey("receiverEmail"))
                {
                    ctx.FailRequest("senderEmail and receiverEmail metadata are required");
                }
                return Task.CompletedTask;
            },
            OnFileCompleteAsync = ctx => tusUploadService.OnFileCompleteAsync(ctx)
        }
    };
});

app.MapFallbackToFile("index.html");

app.Run();
