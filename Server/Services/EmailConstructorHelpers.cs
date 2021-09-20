using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FileTransferazor.Server.Services
{
    public class EmailConstructorHelpers
    {
        private const string _url = ""; // TODO: production host
        public static string CreatedNewFileReceivedEmailBody(IEnumerable<string> files, string from)
        {
            var body = $"You received a new file from {from}.";
            body += $"{Environment.NewLine}{Environment.NewLine}";

            foreach (var item in files)
            {
                body += $"- {_url}/api/files/{item} to download it.";
            }

            return body;
        }
    }
}
