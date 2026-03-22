using System.Collections.Generic;

namespace FileTransferazor.Server.Services
{
    public class EmailConstructorHelpers
    {
        public static string CreatedNewFileReceivedEmailBody(IEnumerable<string> files, string from)
        {
            var body = $"You received a new file from {from}";
            body += "<br /><br />";

            foreach (var item in files)
            {
                body += $"- {item}<br />";
            }

            return body;
        }
    }
}
