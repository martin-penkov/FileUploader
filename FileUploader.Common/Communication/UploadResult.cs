
namespace FileUploader.Common.Communication
{
    public class UploadResult
    {
        public bool Uploaded { get; set; }

        public string FileName { get; set; }

        public long Size { get; set; }

        public string? RelativePath { get; set; }

        public string? Extension { get; set; }

        public int? ErrorCode { get; set; }
    }
}
