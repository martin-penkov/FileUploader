using FileUploader.Common.Communication;
using FileUploader.Db.Entities;

namespace FileUploader.Utility
{
    public static class FileAssetsMapper
    {
        public static UploadResult CreateFromDb(EFileAsset fromdb)
        {
            return new UploadResult
            {
                Uploaded = true,
                FileName = fromdb.Name,
                Size = fromdb.Size,
                RelativePath = fromdb.Location,
                Extension = fromdb.Extension,
            };
        }

    }
}
