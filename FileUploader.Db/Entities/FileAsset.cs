namespace FileUploader.Db.Entities
{
    public class EFileAsset
    {
        public long Id { get; set; }

        public string Name { get; set; }

        public string FullName { get; set; }

        public string Location { get; set; }

        public string Extension { get; set; }

        public long Size { get; set; }

        public DateTime UploadDate { get; set; }
    }
}
