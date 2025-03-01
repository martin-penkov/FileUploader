using FileUploader.Common.Communication;
using FileUploader.Common.Types;
using FileUploader.Db.Entities;
using FileUploader.Tests.Helpers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System.Net;
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
        public async Task AddSmallValidFileReturnsOkAndStoresFile()
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
                if (!string.IsNullOrEmpty(smallFilePath) && File.Exists(smallFilePath))
                {
                    File.Delete(smallFilePath);
                }
            }
        }
    }
}