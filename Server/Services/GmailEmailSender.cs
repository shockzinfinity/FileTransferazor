using System;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FileTransferazor.Server.Options;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Gmail.v1;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FileTransferazor.Server.Services
{
    public class GmailEmailSender : IEmailSender
    {
        private readonly AwsParameterStoreClient _awsParameterStoreClient;
        private readonly ILogger<GmailEmailSender> _logger;
        private readonly GmailOptions _gmailOptions;

        public GmailEmailSender(
            AwsParameterStoreClient awsParameterStoreClient,
            ILogger<GmailEmailSender> logger,
            IOptionsSnapshot<GmailOptions> gmailOptions)
        {
            _awsParameterStoreClient = awsParameterStoreClient ?? throw new ArgumentNullException(nameof(awsParameterStoreClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _gmailOptions = gmailOptions.Value;
        }

        public async Task SendEmailAsync(string to, string title, string body)
        {
            using var client = new SmtpClient("smtp.gmail.com", 587);
            client.UseDefaultCredentials = false;
            client.EnableSsl = true;

            var username = await _awsParameterStoreClient.GetValueAsync("SMTP-username");
            var password = await _awsParameterStoreClient.GetValueAsync("SMTP-password");

            client.Credentials = new NetworkCredential(username, password);
            client.DeliveryMethod = SmtpDeliveryMethod.Network;

            MailMessage mailMessage = new();
            mailMessage.From = new MailAddress(username);
            mailMessage.To.Add(to);
            mailMessage.Subject = title;
            mailMessage.Body = body;

            client.Send(mailMessage);
        }

        public async Task SendEmailApiAsync(string to, string title, string body)
        {
            string[] scopes = { GmailService.Scope.GmailReadonly, GmailService.Scope.GmailSend };
            string applicationName = "fileTransferazorApi";

            UserCredential credential;
            using (var stream = new FileStream(_gmailOptions.CredentialFilePath, FileMode.Open, FileAccess.Read))
            {
                string credPath = "token.json";
                credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                    GoogleClientSecrets.FromStream(stream).Secrets,
                    scopes,
                    _gmailOptions.GmailUser,
                    CancellationToken.None,
                    new FileDataStore(credPath, true));
            }

            var service = new GmailService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = applicationName
            });

            var fromEncoded = Base64UrlEncode("File TransferazorApp");
            StringBuilder sbMailCont = new StringBuilder();
            sbMailCont.AppendFormat("From: =?UTF-8?B?{1}?=<{2}>{0}", Environment.NewLine, fromEncoded, _gmailOptions.FromAddress);
            sbMailCont.AppendFormat("To: {1}{0}", Environment.NewLine, to);
            sbMailCont.AppendFormat("Subject: =?UTF-8?B?{1}?={0}", Environment.NewLine, Base64UrlEncode(title));
            sbMailCont.AppendFormat("Content-Type: text/html; charset=utf-8{0}", Environment.NewLine);
            sbMailCont.AppendFormat("{0}{1}", Environment.NewLine, body);

            var message = new Google.Apis.Gmail.v1.Data.Message();
            message.Raw = Base64UrlEncode(sbMailCont.ToString());
            await service.Users.Messages.Send(message, "me").ExecuteAsync();
        }

        public async Task SendEmailWithServiceAccountAsync(string to, string title, string body)
        {
            string[] scopes = { GmailService.Scope.GmailReadonly, GmailService.Scope.GmailSend };
            string applicationName = "fileTransferazorApi";
            var keyFile = JsonSerializer.Deserialize<ServiceAccountKeyFile>(
                File.ReadAllText(_gmailOptions.ServiceAccountCredentialFilePath));
            var serviceAccountCredentialInitializer = new ServiceAccountCredential.Initializer(_gmailOptions.ServiceAccountEmail)
            {
                User = _gmailOptions.GmailUser,
                Scopes = scopes
            }.FromPrivateKey(keyFile.private_key);
            var credential = new ServiceAccountCredential(serviceAccountCredentialInitializer);
            if (!await credential.RequestAccessTokenAsync(CancellationToken.None))
                throw new InvalidOperationException("Access token failed.");

            var service = new GmailService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = applicationName
            });

            var gmailUser = _gmailOptions.GmailUser;
            StringBuilder sbMailCont = new StringBuilder();
            sbMailCont.AppendFormat($"From: =?UTF-8?B?{Base64UrlEncode("File Transferazor Api")}?=<{gmailUser}>{Environment.NewLine}");
            sbMailCont.AppendFormat($"To: {to}{Environment.NewLine}");
            sbMailCont.AppendFormat($"Subject: =?UTF-8?B?{Base64UrlEncode(title)}?={Environment.NewLine}");
            sbMailCont.AppendFormat($"Content-Type: text/html; charset=utf-8{Environment.NewLine}");
            sbMailCont.AppendFormat($"{Environment.NewLine}{body}");

            var message = new Google.Apis.Gmail.v1.Data.Message();
            message.Raw = Base64UrlEncode(sbMailCont.ToString());

            var result = await service.Users.Messages.Send(message, gmailUser).ExecuteAsync();
            _logger.LogInformation(result.Raw);
        }

        public string Base64UrlEncode(string input)
        {
            if (!string.IsNullOrEmpty(input))
            {
                var inputBytes = Encoding.UTF8.GetBytes(input);
                return Convert.ToBase64String(inputBytes).Replace("+", "-").Replace("/", "_").Replace("=", "");
            }
            return string.Empty;
        }

        internal class ServiceAccountKeyFile
        {
            public string private_key { get; set; }
        }
    }
}
