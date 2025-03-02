using FileUploader.Common.Types;
using System.ComponentModel.DataAnnotations.Schema;

namespace FileUploader.Db.Entities
{
    public class EFileAsset
    {
        public long Id { get; set; }

        public string Name { get; set; }

        public string Location { get; set; }

        public string Extension { get; set; }

        public long Size { get; set; }

        public DateTime UploadDate { get; set; }

        public Status Status { get; set; }
    }
}
