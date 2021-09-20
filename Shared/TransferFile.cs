using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileTransferazor.Shared
{
    public class TransferFile
    {
        public string Name { get; set; }
        //public byte[] Content { get; set; }
        public Stream Content { get; set; }
    }
}
