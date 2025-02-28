using FileUploader.Common.Communication;
using Microsoft.AspNetCore.Components.Forms;
using System.Net.Http.Json;
using FileUploader.Client.Services.AlertService;
using System.Net.Http.Headers;

namespace FileUploader.Client.Services.FileUploadService
{
    public class FileUploadService : IFileUploadService
    {
        private readonly HttpClient m_http;
        private readonly IAlertService m_alertService;

        public FileUploadService(HttpClient httpClient, IAlertService alertService)
        {
            m_http = httpClient;
            m_alertService = alertService;
        }

        public async Task UploadFileInChunks(IBrowserFile file, long chunkSize = 1000000)
        {
            long TotalBytes = file.Size;
            long numChunks = TotalBytes / chunkSize;
            long remainder = TotalBytes % chunkSize;

            long uploadedBytes = 0;

            string nameOnly = Path.GetFileNameWithoutExtension(file.Name);
            string extension = Path.GetExtension(file.Name);

            bool firstChunk = true;
            using (Stream stream = file.OpenReadStream(long.MaxValue))
            {
                for (int i = 0; i < numChunks; i++)
                {
                    FileChunk chunk = await CreateFileChunk(file.Name, uploadedBytes, firstChunk, stream, chunkSize, false);
                    if (!await MakeApiChunkRangeRequest(chunk))
                    {
                        return;
                    }

                    uploadedBytes += chunkSize;
                    firstChunk = false;
                }

                if (remainder > 0)
                {
                    FileChunk chunk = await CreateFileChunk(file.Name, uploadedBytes, firstChunk, stream, remainder, true);
                    if (!await MakeApiChunkRangeRequest(chunk))
                    {
                        return;
                    }

                    uploadedBytes += chunkSize;
                }
            }

            Console.WriteLine("File uploaded successfully in chunks! Name: " + file.Name);
        }

        public async Task<FileChunk> CreateFileChunk(string fileName, long uploadedBytesProgress, bool isFirstChunk, Stream stream, long chunkSize, bool isLastChunk)
        {
            byte[] buffer = new byte[chunkSize];
            await stream.ReadAsync(buffer, 0, buffer.Length);

            FileChunk chunk = new FileChunk
            {
                Data = buffer,
                FileName = fileName,
                Offset = uploadedBytesProgress,
                FirstChunk = isFirstChunk,
                LastChunk = isLastChunk
            };

            return chunk;
        }

        public async Task<bool> MakeApiChunkRangeRequest(FileChunk fileChunk)
        {
            HttpResponseMessage response = await m_http.PostAsJsonAsync("/files/addFileChunk", fileChunk);

            if (!response.IsSuccessStatusCode)
            {
                m_alertService.ShowAlert(await response.Content.ReadAsStringAsync());
                Console.WriteLine("File chunk upload fail! " + response.ReasonPhrase);
                return false;
            }

            return true;
        }

        public void CreateStreamContentForFiles(List<IBrowserFile> smallFiles, long maxFileSize, MultipartFormDataContent content)
        {
            foreach (IBrowserFile file in smallFiles)
            {
                StreamContent fileContent = new StreamContent(file.OpenReadStream(maxFileSize));
                fileContent.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType);

                content.Add(content: fileContent, name: "\"filesList\"", fileName: file.Name);
            }
        }

        public async Task<List<UploadResult>> MakeApiRequestForSmallFiles(HttpContent content)
        {
            HttpResponseMessage response = await m_http.PostAsync("/files/addFiles", content);
            List<UploadResult> uploadResults = new List<UploadResult>();

            if (!response.IsSuccessStatusCode)
            {
                m_alertService.ShowAlert(await response.Content.ReadAsStringAsync());
                Console.WriteLine("File upload fail! " + response.ReasonPhrase);
                return uploadResults;
            }

            List<UploadResult> responseUploadResults = await response.Content.ReadFromJsonAsync<List<UploadResult>>();

            if (responseUploadResults != null)
            {
                uploadResults.Concat(responseUploadResults).ToList();
            }

            return uploadResults;
        }

    }
}
