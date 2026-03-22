using System.Threading.Tasks;

namespace FileTransferazor.Server.Services
{
    public interface IEmailSender
    {
        Task SendEmailAsync(string to, string title, string body);
        Task SendEmailApiAsync(string to, string title, string body);
        Task SendEmailWithServiceAccountAsync(string to, string title, string body);
    }
}
