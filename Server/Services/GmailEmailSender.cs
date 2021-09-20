using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;

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
    }
}
