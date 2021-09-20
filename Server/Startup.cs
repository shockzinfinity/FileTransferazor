using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Linq;
using FileTransferazor.Server.Data;
using Microsoft.EntityFrameworkCore;
using FileTransferazor.Server.Services;
using Amazon;
using Amazon.S3;
using FileTransferazor.Server.Repositories;
using Hangfire;

namespace FileTransferazor.Server
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }
        public AwsParameterStoreClient AwsParameterStoreClient { get { return new AwsParameterStoreClient(RegionEndpoint.APNortheast2); } }

        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddDbContext<FileTransferazorDbContext>(options =>
            {
                options.UseSqlServer(Configuration.GetConnectionString("DefaultConnection"));
                //options.UseSqlServer(AwsParameterStoreClient.GetValue("FileTransferazorDb"));
            });

            services.AddHangfire(x => x.UseSqlServerStorage(Configuration.GetConnectionString("DefaultConnection")));
            services.AddHangfireServer();

            services.AddScoped(sp => new AwsParameterStoreClient(RegionEndpoint.APNortheast2));
            services.AddAWSService<IAmazonS3>();
            services.AddScoped<IAwsS3FileManager, AwsS3FileManager>();
            services.AddScoped<IFileRepository, FileRepository>();
            services.AddScoped<IEmailSender, GmailEmailSender>();

            services.AddControllersWithViews();
            services.AddRazorPages();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            app.UseHangfireDashboard(); // TODO: production level 에서는 안뜨도록 env.IsDevelopment() 로 넘겨야 함

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseWebAssemblyDebugging();
            }
            else
            {
                app.UseExceptionHandler("/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseBlazorFrameworkFiles();
            app.UseStaticFiles();

            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapRazorPages();
                endpoints.MapControllers();
                endpoints.MapFallbackToFile("index.html");
            });
        }
    }
}
