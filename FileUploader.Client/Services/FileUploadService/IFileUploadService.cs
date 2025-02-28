using FileUploader.Common.Communication;
using Microsoft.AspNetCore.Components.Forms;

namespace FileUploader.Client.Services.FileUploadService
{
    public interface IFileUploadService
    {
        Task UploadFileInChunks(IBrowserFile file, long chunkSize = 1000000);

        Task<FileChunk> CreateFileChunk(string fileName, long uploadedBytesProgress, bool isFirstChunk, Stream stream, long chunkSize, bool isLastChunk);

        Task<bool> MakeApiChunkRangeRequest(FileChunk fileChunk);

        HttpContent CreateStreamContentForFiles(List<IBrowserFile> smallFiles, long maxFileSize);

        Task<List<UploadResult>> MakeApiRequestForSmallFiles(HttpContent content);
    }
}
