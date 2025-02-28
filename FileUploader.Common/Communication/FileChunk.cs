using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileUploader.Common.Communication
{
    public class FileChunk
    {
        public string FileName { get; set; }

        public long Offset { get; set; }

        public byte[]? Data { get; set; }

        public bool FirstChunk { get; set; }

        public bool LastChunk { get; set; }
    }
}
