using FileUploader.Common.Communication;
using FileUploader.Common.Types;

namespace FileUploader.Services.FileService
{
    public interface IFileService
    {
        Task<UploadResult> UploadAsync(string name, IFormFile file);

        UploadResult UploadChunkAsync(FileChunk fileChunk);

        bool Delete(string relativePath);

        bool DoesFileExist(string location);

        FileDescription PrepareFileDescription(string srcFileName);

        Task<bool> CreateDbEntryOnFirstChunkAsync(string fileName);

        Task ClearStateAsync(string fileName);

        Task<bool> FinalizeChunkedFileUploadAsync(string fileName);
    }
}
