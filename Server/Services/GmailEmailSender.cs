using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Gmail.v1;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using Microsoft.AspNetCore.Authentication;
using Microsoft.IdentityModel.Tokens;

namespace FileTransferazor.Server.Services
{
    // TODO: use GCP
    public class GmailEmailSender : IEmailSender
    {
        private readonly AwsParameterStoreClient _awsParameterStoreClient;

        public GmailEmailSender(AwsParameterStoreClient awsParameterStoreClient)
        {
            _awsParameterStoreClient = awsParameterStoreClient ?? throw new ArgumentNullException(nameof(awsParameterStoreClient));
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

        public string Base64UrlEncode(string input)
        {
            string strRtn = "";
            if (input != "")
            {
                var inputBytes = System.Text.Encoding.UTF8.GetBytes(input);
                strRtn = Convert.ToBase64String(inputBytes).Replace("+", "-").Replace("/", "_").Replace("=", "");
            }
            return strRtn;
        }
    }
}
