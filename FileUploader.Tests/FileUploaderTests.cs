using FileUploader.Common;
using FileUploader.Common.Communication;
using FileUploader.Common.Types;
using FileUploader.Db.Entities;
using FileUploader.Tests.Helpers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
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
        public async Task CanAddFilesToDb()    // For testing the DB read/write
        {
            customAppFactory.TestFixture.AppDbContext.FileAssets.Add(new Db.Entities.EFileAsset
            {
                Name = "test",
                Location = "test",
                Extension = ".png",
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
        public async Task TestDbIfNamesWithSameFullNameButDifferentNameAndExtensionWhenCheckedSeparatelyIsHandled()
        {
            string firstValidName = "test.prod";
            string firstValidExtension = ".txt";

            string secondValidName = "test";
            string secondValidExtension = ".prod.txt";

            try
            {
                customAppFactory.TestFixture.AppDbContext.FileAssets.Add(new Db.Entities.EFileAsset
                {
                    Name = firstValidName,
                    Location = "random",
                    Extension = firstValidExtension,
                    UploadDate = DateTime.UtcNow,
                    Size = 0,
                    Status = Status.Complete
                });

                await customAppFactory.TestFixture.AppDbContext.SaveChangesAsync();

                customAppFactory.TestFixture.AppDbContext.FileAssets.Add(new Db.Entities.EFileAsset
                {
                    Name = secondValidName,
                    Location = "random",
                    Extension = secondValidExtension,
                    UploadDate = DateTime.UtcNow,
                    Size = 0,
                    Status = Status.Complete
                });

                await customAppFactory.TestFixture.AppDbContext.SaveChangesAsync();
            }
            finally
            {
                // Clean up the test data
                List<EFileAsset> testFiles = await customAppFactory.TestFixture.AppDbContext.FileAssets
                    .Where(f => (f.Name == firstValidName && f.Extension == firstValidExtension) ||
                                (f.Name == secondValidName && f.Extension == secondValidExtension))
                    .ToListAsync();

                if (testFiles.Count > 0)
                {
                    customAppFactory.TestFixture.AppDbContext.FileAssets.RemoveRange(testFiles);
                    await customAppFactory.TestFixture.AppDbContext.SaveChangesAsync();
                }
            }
        }

        [Fact]
        public async Task TestDbIfEntryWithSameNameAndSameExtensionPairAreRejected()
        {
            string validName = "randomName";
            string validExtension = ".txt";

            try
            {
                customAppFactory.TestFixture.AppDbContext.FileAssets.Add(new EFileAsset
                {
                    Name = validName,
                    Location = "random",
                    Extension = validExtension,
                    UploadDate = DateTime.UtcNow,
                    Size = 0,
                    Status = Status.Complete
                });

                customAppFactory.TestFixture.AppDbContext.FileAssets.Add(new EFileAsset
                {
                    Name = validName,
                    Location = "random",
                    Extension = validExtension,
                    UploadDate = DateTime.UtcNow,
                    Size = 0,
                    Status = Status.Complete
                });

                await Assert.ThrowsAsync<DbUpdateException>(async () =>
                {
                    await customAppFactory.TestFixture.AppDbContext.SaveChangesAsync();
                });
            }
            finally
            {
                customAppFactory.TestFixture.AppDbContext.ChangeTracker.Clear();

                List<EntityEntry<EFileAsset>> trackedEntities = customAppFactory.TestFixture.AppDbContext.ChangeTracker
                    .Entries<EFileAsset>()
                    .Where(e => e.Entity.Name == validName && e.Entity.Extension == validExtension)
                    .ToList();

                foreach (var entry in trackedEntities)
                {
                    entry.State = EntityState.Detached;
                }

                // This clean up shouldnt usually happen, only if the db doesnt work as expected...
                List<EFileAsset> testFiles = await customAppFactory.TestFixture.AppDbContext.FileAssets
                    .Where(f => f.Name == validName && f.Extension == validExtension)
                    .ToListAsync();

                if (testFiles.Any())
                {
                    customAppFactory.TestFixture.AppDbContext.FileAssets.RemoveRange(testFiles);
                    await customAppFactory.TestFixture.AppDbContext.SaveChangesAsync();
                }
            }
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

                string nameOnly = Path.GetFileNameWithoutExtension(smallFilePath);
                string extension = Path.GetExtension(smallFilePath);
                EFileAsset? dbEntry = await customAppFactory.TestFixture.AppDbContext.FileAssets.FirstOrDefaultAsync(e => e.Name == nameOnly && e.Extension == extension);

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
            long chunkSize = 1000000; // 1MB

            await FileHelper.UploadChunkedFileAndVerify(bigFileSize, chunkSize, _client, customAppFactory.TestFixture.AppDbContext, m_hostEnvironment.WebRootPath);
        }

        [Fact]
        public async Task TestSmallFileSmallerThanChunkReturnsOkAndStoresFile()
        {
            const long fileSize = 500000; // 500 KB
            long chunkSize = 1000000; // 1MB

            await FileHelper.UploadChunkedFileAndVerify(fileSize, chunkSize, _client, customAppFactory.TestFixture.AppDbContext, m_hostEnvironment.WebRootPath);
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

                    uploadedBytes += remainder;
                }

                string nameOnly = Path.GetFileNameWithoutExtension(bigFilePath);
                EFileAsset? dbEntry = await customAppFactory.TestFixture.AppDbContext.FileAssets.FirstOrDefaultAsync(e => e.Name == nameOnly && e.Extension == extension);
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

                string nameOnly = Path.GetFileNameWithoutExtension(smallFilePath);
                string extension = Path.GetExtension(smallFilePath);
                EFileAsset? dbEntry = await customAppFactory.TestFixture.AppDbContext.FileAssets.FirstOrDefaultAsync(e => e.Name == nameOnly && e.Extension == extension);
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
            const long bigFileSize = 3000000; // 3 MB
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

                // send only one chunk
                byte[] buffer = new byte[chunkSize];
                Array.Copy(fileBytes, 0, buffer, 0, chunkSize);

                FileChunk chunk = FileHelper.CreateFileChunk(samplefileName, 0, true, buffer, chunkSize, false);

                HttpResponseMessage response = await _client.PostAsJsonAsync("/files/addFileChunk", chunk);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);

                string nameOnly = Path.GetFileNameWithoutExtension(bigFilePath);
                EFileAsset? dbEntry = await customAppFactory.TestFixture.AppDbContext.FileAssets.FirstOrDefaultAsync(e => e.Name == nameOnly && e.Extension == extension);
                Assert.True(dbEntry != null, "File doesnt exist in DB");
                Assert.True(dbEntry.Status == Status.InProgress, "File should be in progress!");

                string contentRoot = m_hostEnvironment.WebRootPath;
                string filePath = Path.Combine(contentRoot, dbEntry.Location);

                HttpResponseMessage getAllResponse = await _client.GetAsync("/files/publicFiles");
                List<UploadResult>? finalFiles = await getAllResponse.Content.ReadFromJsonAsync<List<UploadResult>>();

                Assert.Null(finalFiles.FirstOrDefault(file => file.FileName == samplefileName));

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