using FileUploader.Common;
using FileUploader.Common.Communication;
using FileUploader.Common.Types;
using FileUploader.Db.Entities;
using FileUploader.Tests.Helpers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace FileUploader.Tests
{
    [CollectionDefinition("Database collection")]
    public class FileUploaderIntegrationTests : IClassFixture<TestFixture>
    {
        CustomAppFactory customAppFactory;
        private readonly HttpClient _client;
        IWebHostEnvironment m_hostEnvironment;

        public FileUploaderIntegrationTests(TestFixture testFixture)
        {
            customAppFactory = new CustomAppFactory(testFixture);
            _client = customAppFactory.CreateClient();
            m_hostEnvironment = customAppFactory.GetHostEnvironment();
        }

        [Fact]
        public async Task CanAddFilesToDb()
        {
            customAppFactory.TestFixture.AppDbContext.FileAssets.Add(new Db.Entities.EFileAsset
            {
                FullName = "test",
                Name = "test",
                Location = "test",
                Extension = "test",
                UploadDate = DateTime.UtcNow,
                Size = 0,
                Status = Status.Complete
            });

            await customAppFactory.TestFixture.AppDbContext.SaveChangesAsync();


            HttpResponseMessage response = await customAppFactory.CreateClient().GetAsync("/files/publicFiles");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            List<EFileAsset>? files = await response.Content.ReadFromJsonAsync<List<EFileAsset>>();
            Assert.NotEmpty(files);
        }

        [Fact]
        public async Task TestSmallValidFileReturnsOkAndStoresFile()
        {
            const long smallFileSize = 1024; // 1 KB
            string smallFilePath = null;

            try
            {
                smallFilePath = FileHelper.CreateTempFile(smallFileSize);

                Assert.True(File.Exists(smallFilePath));
                Assert.Equal(smallFileSize, new FileInfo(smallFilePath).Length);
                string samplefileName = Path.GetFileName(smallFilePath);

                byte[] fileBytes = File.ReadAllBytes(smallFilePath);

                MultipartFormDataContent multipartContent = new MultipartFormDataContent();
                ByteArrayContent fileContent = new ByteArrayContent(fileBytes);
                fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                multipartContent.Add(fileContent, "filesList", samplefileName);

                HttpResponseMessage response = await _client.PostAsync("/files/addFiles", multipartContent);
                response.EnsureSuccessStatusCode();

                string jsonResponse = await response.Content.ReadAsStringAsync();
                List<UploadResult> uploadResults = JsonConvert.DeserializeObject<List<UploadResult>>(jsonResponse);

                Assert.NotNull(uploadResults);
                Assert.True(uploadResults.Count > 0, "The upload result list is empty.");
                UploadResult result = uploadResults[0];

                EFileAsset? dbEntry = await customAppFactory.TestFixture.AppDbContext.FileAssets.FirstOrDefaultAsync(fa => fa.FullName == samplefileName);
                Assert.True(dbEntry != null, "File doesnt exist in DB");

                string contentRoot = m_hostEnvironment.WebRootPath;
                string filePath = Path.Combine(contentRoot, dbEntry.Location);

                Assert.True(File.Exists(filePath), $"Uploaded file not found at: {filePath}");

                File.Delete(filePath);
            }
            finally
            {
                // Clean up temp files
                FileHelper.CleanFile(smallFilePath);
            }
        }

        [Fact]
        public async Task TestBigChunkedValidFileReturnsOkAndStoresFile()
        {
            const long bigFileSize = 1000000000; // 1 GB
            string bigFilePath = null;
            long chunkSize = 1000000; // 1MB

            try
            {
                bigFilePath = FileHelper.CreateTempFile(bigFileSize);

                Assert.True(File.Exists(bigFilePath));
                Assert.Equal(bigFileSize, new FileInfo(bigFilePath).Length);
                string samplefileName = Path.GetFileName(bigFilePath);
                string extension = Path.GetExtension(bigFilePath);

                byte[] fileBytes = File.ReadAllBytes(bigFilePath);

                long numChunks = fileBytes.Length / chunkSize;
                long remainder = fileBytes.Length % chunkSize;
                long uploadedBytes = 0;

                bool firstChunk = true;

                for (int i = 0; i < numChunks; i++)
                {
                    byte[] buffer = new byte[chunkSize];
                    Array.Copy(fileBytes, i * chunkSize, buffer, 0, chunkSize);

                    FileChunk chunk = FileHelper.CreateFileChunk(samplefileName, uploadedBytes, firstChunk, buffer, chunkSize, false);

                    HttpResponseMessage response = await _client.PostAsJsonAsync("/files/addFileChunk", chunk);
                    Assert.Equal(HttpStatusCode.OK, response.StatusCode);

                    uploadedBytes += chunkSize;
                    firstChunk = false;
                }

                if (remainder > 0)
                {
                    byte[] buffer = new byte[remainder];
                    Array.Copy(fileBytes, numChunks * chunkSize, buffer, 0, remainder);

                    FileChunk chunk = FileHelper.CreateFileChunk(samplefileName, uploadedBytes, firstChunk, buffer, remainder, true);

                    HttpResponseMessage response = await _client.PostAsJsonAsync("/files/addFileChunk", chunk);
                    Assert.Equal(HttpStatusCode.OK, response.StatusCode);

                    uploadedBytes += chunkSize;
                }

                EFileAsset? dbEntry = await customAppFactory.TestFixture.AppDbContext.FileAssets.FirstOrDefaultAsync(fa => fa.FullName == samplefileName);
                Assert.True(dbEntry != null, "File doesnt exist in DB");

                string contentRoot = m_hostEnvironment.WebRootPath;
                string filePath = Path.Combine(contentRoot, dbEntry.Location);
                byte[] finalFileBytes = File.ReadAllBytes(filePath);

                Assert.True(File.Exists(filePath), $"Uploaded file not found at: {filePath}");
                Assert.Equal(FileHelper.GetFileHash(fileBytes), FileHelper.GetFileHash(finalFileBytes));// verify if both files are the same after upload

                File.Delete(filePath);
            }
            finally
            {
                // Clean up temp files
                FileHelper.CleanFile(bigFilePath);
            }
        }

        [Fact]
        public async Task TestChunkedDuplicateFileNameError()
        {
            const long bigFileSize = 10000000; // 10 MB
            string bigFilePath = null;
            long chunkSize = 1000000; // 1MB

            try
            {
                bigFilePath = FileHelper.CreateTempFile(bigFileSize);

                Assert.True(File.Exists(bigFilePath));
                Assert.Equal(bigFileSize, new FileInfo(bigFilePath).Length);
                string samplefileName = Path.GetFileName(bigFilePath);
                string extension = Path.GetExtension(bigFilePath);

                byte[] fileBytes = File.ReadAllBytes(bigFilePath);

                long numChunks = fileBytes.Length / chunkSize;
                long remainder = fileBytes.Length % chunkSize;
                long uploadedBytes = 0;

                bool firstChunk = true;

                for (int i = 0; i < numChunks; i++)
                {
                    byte[] buffer = new byte[chunkSize];
                    Array.Copy(fileBytes, i * chunkSize, buffer, 0, chunkSize);

                    FileChunk chunk = FileHelper.CreateFileChunk(samplefileName, uploadedBytes, firstChunk, buffer, chunkSize, false);

                    HttpResponseMessage response = await _client.PostAsJsonAsync("/files/addFileChunk", chunk);
                    Assert.Equal(HttpStatusCode.OK, response.StatusCode);

                    uploadedBytes += chunkSize;
                    firstChunk = false;
                }

                if (remainder > 0)
                {
                    byte[] buffer = new byte[remainder];
                    Array.Copy(fileBytes, numChunks * chunkSize, buffer, 0, remainder);

                    FileChunk chunk = FileHelper.CreateFileChunk(samplefileName, uploadedBytes, firstChunk, buffer, remainder, true);

                    HttpResponseMessage response = await _client.PostAsJsonAsync("/files/addFileChunk", chunk);
                    Assert.Equal(HttpStatusCode.OK, response.StatusCode);

                    uploadedBytes += chunkSize;
                }

                EFileAsset? dbEntry = await customAppFactory.TestFixture.AppDbContext.FileAssets.FirstOrDefaultAsync(fa => fa.FullName == samplefileName);
                Assert.True(dbEntry != null, "File doesnt exist in DB");

                string contentRoot = m_hostEnvironment.WebRootPath;
                string filePath = Path.Combine(contentRoot, dbEntry.Location);
                byte[] finalFileBytes = File.ReadAllBytes(filePath);

                Assert.True(File.Exists(filePath), $"Uploaded file not found at: {filePath}");

                //Attempt to send the same first chunk again
                FileChunk emptyChunk = FileHelper.CreateFileChunk(samplefileName, 0, true, [], 0, false);
                HttpResponseMessage errorResponse = await _client.PostAsJsonAsync("/files/addFileChunk", emptyChunk);
                Assert.Equal(HttpStatusCode.BadRequest, errorResponse.StatusCode);
                Assert.Equal(ErrorMessage.FileWithSameNameExists, await errorResponse.Content.ReadAsStringAsync());

                File.Delete(filePath);
            }
            finally
            {
                // Clean up temp files
                FileHelper.CleanFile(bigFilePath);
            }
        }

        [Fact]
        public async Task TestSmallDuplicateFileNameError()
        {
            const long smallFileSize = 1024; // 1 KB
            string smallFilePath = null;

            try
            {
                smallFilePath = FileHelper.CreateTempFile(smallFileSize);

                Assert.True(File.Exists(smallFilePath));
                Assert.Equal(smallFileSize, new FileInfo(smallFilePath).Length);
                string samplefileName = Path.GetFileName(smallFilePath);

                byte[] fileBytes = File.ReadAllBytes(smallFilePath);

                MultipartFormDataContent multipartContent = new MultipartFormDataContent();
                ByteArrayContent fileContent = new ByteArrayContent(fileBytes);
                fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                multipartContent.Add(fileContent, "filesList", samplefileName);

                HttpResponseMessage response = await _client.PostAsync("/files/addFiles", multipartContent);
                response.EnsureSuccessStatusCode();

                string jsonResponse = await response.Content.ReadAsStringAsync();
                List<UploadResult> uploadResults = JsonConvert.DeserializeObject<List<UploadResult>>(jsonResponse);

                Assert.NotNull(uploadResults);
                Assert.True(uploadResults.Count > 0, "The upload result list is empty.");
                UploadResult result = uploadResults[0];

                EFileAsset? dbEntry = await customAppFactory.TestFixture.AppDbContext.FileAssets.FirstOrDefaultAsync(fa => fa.FullName == samplefileName);
                Assert.True(dbEntry != null, "File doesnt exist in DB");

                string contentRoot = m_hostEnvironment.WebRootPath;
                string filePath = Path.Combine(contentRoot, dbEntry.Location);

                Assert.True(File.Exists(filePath), $"Uploaded file not found at: {filePath}");

                //Attempt to send the same file again
                HttpResponseMessage errorResponse = await _client.PostAsync("/files/addFiles", multipartContent);
                Assert.Equal(HttpStatusCode.BadRequest, errorResponse.StatusCode);
                Assert.Equal(ErrorMessage.FileWithSameNameExists, await errorResponse.Content.ReadAsStringAsync());

                File.Delete(filePath);
            }
            finally
            {
                // Clean up temp files
                FileHelper.CleanFile(smallFilePath);
            }
        }

        [Fact]
        public async Task TestIfFileInProgressWillReturnOnGetAllFiles()
        {
            HttpResponseMessage getAllResponse = await _client.GetAsync("/files/publicFiles");
            List<UploadResult>? initialFiles = await getAllResponse.Content.ReadFromJsonAsync<List<UploadResult>>();

            const long bigFileSize = 3000000; // 1 MB
            string bigFilePath = null;
            long chunkSize = 1000000; // 1MB

            try
            {
                bigFilePath = FileHelper.CreateTempFile(bigFileSize);

                Assert.True(File.Exists(bigFilePath));
                Assert.Equal(bigFileSize, new FileInfo(bigFilePath).Length);
                string samplefileName = Path.GetFileName(bigFilePath);
                string extension = Path.GetExtension(bigFilePath);

                byte[] fileBytes = File.ReadAllBytes(bigFilePath);

                long numChunks = fileBytes.Length / chunkSize;
                long remainder = fileBytes.Length % chunkSize;
                long uploadedBytes = 0;

                bool firstChunk = true;
                // send only one chunk
                byte[] buffer = new byte[chunkSize];
                Array.Copy(fileBytes, 0, buffer, 0, chunkSize);

                FileChunk chunk = FileHelper.CreateFileChunk(samplefileName, uploadedBytes, firstChunk, buffer, chunkSize, false);

                HttpResponseMessage response = await _client.PostAsJsonAsync("/files/addFileChunk", chunk);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);

                uploadedBytes += chunkSize;
                firstChunk = false;

                EFileAsset? dbEntry = await customAppFactory.TestFixture.AppDbContext.FileAssets.FirstOrDefaultAsync(fa => fa.FullName == samplefileName);
                Assert.True(dbEntry != null, "File doesnt exist in DB");
                Assert.True(dbEntry.Status == Status.InProgress, "File should be in progress!");

                string contentRoot = m_hostEnvironment.WebRootPath;
                string filePath = Path.Combine(contentRoot, dbEntry.Location);

                HttpResponseMessage finalGetAllResponse = await _client.GetAsync("/files/publicFiles");
                List<UploadResult>? finalFiles = await finalGetAllResponse.Content.ReadFromJsonAsync<List<UploadResult>>();

                Assert.Equal(initialFiles, finalFiles);

                File.Delete(filePath);
            }
            finally
            {
                // Clean up temp files
                FileHelper.CleanFile(bigFilePath);
            }
        }
    }
}