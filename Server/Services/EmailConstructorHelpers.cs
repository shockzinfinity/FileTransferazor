using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FileTransferazor.Server.Services
{
    public class EmailConstructorHelpers
    {
        //private const string _url = ""; // NOTE: email rejected.....
        public static string CreatedNewFileReceivedEmailBody(IEnumerable<string> files, string from)
        {
            var body = $"You received a new file from ...";
            body += "<br /><br />";

            foreach (var item in files)
            {
                body += $"- {item}<br />";
            }

            return body;
        }
    }
}
