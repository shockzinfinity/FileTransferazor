using FileTransferazor.Server.Data;
using FileTransferazor.Server.Options;
using FileTransferazor.Server.Repositories;
using FileTransferazor.Server.Services;
using GmailOptions = FileTransferazor.Server.Options.GmailOptions;
using Amazon;
using Amazon.S3;
using Hangfire;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Options
builder.Services.Configure<AwsS3Options>(builder.Configuration.GetSection(AwsS3Options.SectionName));
builder.Services.Configure<GmailOptions>(builder.Configuration.GetSection(GmailOptions.SectionName));

// Kestrel - 10 GB max request body
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 10L * 1024L * 1024L * 1024L;
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

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

var app = builder.Build();

// Hangfire Dashboard: Development 환경에서만 접근 허용
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
app.MapFallbackToFile("index.html");

app.Run();
