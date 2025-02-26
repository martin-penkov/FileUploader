using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Database.FileUploader.Entities
{
    public class EFileAsset
    {
        public long Id { get; set; }
        public string Name { get; set; }
        public string Extension { get; set; }
        public long Size { get; set; }
        public DateTime UploadDate { get; set; }
        public byte[] Data { get; set; }
    }
}
