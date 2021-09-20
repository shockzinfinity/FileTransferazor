using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FileTransferazor.Server.Services
{
    public interface IEmailSender
    {
        void SendEmail(string to, string title, string body);
        void SendEmailApi(string to, string title, string body);
    }
}
