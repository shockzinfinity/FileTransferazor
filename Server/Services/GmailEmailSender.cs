using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Amazon.Runtime.Internal.Util;
using Amazon.Util.Internal;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Gmail.v1;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace FileTransferazor.Server.Services
{
    // TODO: use GCP
    public class GmailEmailSender : IEmailSender
    {
        private readonly AwsParameterStoreClient _awsParameterStoreClient;
        private readonly ILogger<GmailEmailSender> _logger;

        public GmailEmailSender(AwsParameterStoreClient awsParameterStoreClient, ILogger<GmailEmailSender> logger)
        {
            _awsParameterStoreClient = awsParameterStoreClient ?? throw new ArgumentNullException(nameof(awsParameterStoreClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async void SendEmail(string to, string title, string body)
        {
            using (var client = new SmtpClient("smtp.gmail.com", 587))
            {
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
        }

        public async void SendEmailApi(string to, string title, string body)
        {
            string[] scopes = { GmailService.Scope.GmailReadonly, GmailService.Scope.GmailSend };
            string applicationName = "fileTransferazorApi";

            UserCredential credential;
            using (var stream = new FileStream("credential.json", FileMode.Open, FileAccess.Read))
            {
                string credPath = "token.json";
                credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                    GoogleClientSecrets.FromStream(stream).Secrets,
                    scopes,
                    "shockz@ironpot42.com",
                    CancellationToken.None,
                    new FileDataStore(credPath, true));
            }

            var service = new GmailService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = applicationName
            });

            StringBuilder sbMailCont = new StringBuilder();
            sbMailCont.AppendFormat("From: =?UTF-8?B?{1}?=<~~~~~~~@gmail.com>{0}", System.Environment.NewLine, Base64UrlEncode("File TransferazorApp"));
            sbMailCont.AppendFormat("To: {1}{0}", System.Environment.NewLine, to);
            sbMailCont.AppendFormat("Subject: =?UTF-8?B?{1}?={0}", System.Environment.NewLine, Base64UrlEncode(title));
            sbMailCont.AppendFormat("Content-Type: text/html; charset=utf-8{0}", System.Environment.NewLine);
            sbMailCont.AppendFormat("{0}{1}", System.Environment.NewLine, body);

            var message = new Google.Apis.Gmail.v1.Data.Message();
            message.Raw = Base64UrlEncode(sbMailCont.ToString());
            service.Users.Messages.Send(message, "me").Execute();
        }

        public async void SendEmailWithServiceAccount(string to, string title, string body)
        {
            string[] scopes = { GmailService.Scope.GmailReadonly, GmailService.Scope.GmailSend };
            string applicationName = "fileTransferazorApi";
            var credentialFile = "filetransferazorapi-864227e8ceed.json";
            var serviceAccount = "filetransferadmin@filetransferazorapi.iam.gserviceaccount.com";
            var keyFile = JsonSerializer.Deserialize<ServiceAccountKeyFile>(File.ReadAllText(credentialFile));
            var gmailUser = "shockz@ironpot42.com";
            var serviceAccountCredentilalInitializer = new ServiceAccountCredential.Initializer(serviceAccount)
            {
                User = gmailUser,
                Scopes = scopes
            }.FromPrivateKey(keyFile.private_key);
            var credential = new ServiceAccountCredential(serviceAccountCredentilalInitializer);
            if (!credential.RequestAccessTokenAsync(CancellationToken.None).Result)
                throw new InvalidOperationException("Access token failed.");

            var service = new GmailService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = applicationName
            });

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
