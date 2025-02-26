using FileUploader.Common.Communication;

namespace FileUploader.Services.FileService
{
    public interface IFileService
    {
        Task<UploadResult> UploadAsync(string name, IFormFile file);

        void Delete(string relativePath);
    }
}
