using FileUploader.Common.Communication;

namespace FileUploader.Services.FileService
{
    public interface IFileService
    {
        Task<UploadResult> UploadAsync(string name, IFormFile file);

        Task<UploadResult> UploadChunkAsync(FileChunk fileChunk);

        bool Delete(string relativePath);

        bool DoesFileExist(string location);
    }
}
