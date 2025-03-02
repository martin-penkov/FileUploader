using FileUploader.Common.Communication;
using FileUploader.Db;
using FileUploader.Db.Entities;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;

namespace FileUploader.Tests.Helpers
{
    public static class FileHelper
    {
        /// <summary>
        /// Creates a temporary file with random data of the specified size.
        /// </summary>
        /// <param name="sizeInBytes">The desired file size in bytes.</param>
        /// <returns>The file path of the generated temporary file.</returns>
        public static string CreateTempFile(long sizeInBytes)
        {
            string tempFilePath = Path.GetTempFileName();

            using (FileStream fs = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                byte[] buffer = new byte[8192];
                long bytesRemaining = sizeInBytes;
                using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
                {
                    while (bytesRemaining > 0)
                    {
                        int bytesToWrite = (int)Math.Min(buffer.Length, bytesRemaining);
                        rng.GetBytes(buffer, 0, bytesToWrite);
                        fs.Write(buffer, 0, bytesToWrite);
                        bytesRemaining -= bytesToWrite;
                    }
                }
            }

            return tempFilePath;
        }

        public static FileChunk CreateFileChunk(string fileName, long uploadedBytesProgress, bool isFirstChunk, byte[] buffer, long chunkSize, bool isLastChunk)
        {
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

        public static async Task UploadChunkedFileAndVerify(long fileSize, long chunkSize, HttpClient httpClient, AppDbContext dbContext, string webRootPath)
        {
            string bigFilePath = null;
            try
            {
                bigFilePath = FileHelper.CreateTempFile(fileSize);

                Assert.True(File.Exists(bigFilePath));
                Assert.Equal(fileSize, new FileInfo(bigFilePath).Length);
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

                    HttpResponseMessage response = await httpClient.PostAsJsonAsync("/files/addFileChunk", chunk);
                    Assert.Equal(HttpStatusCode.OK, response.StatusCode);

                    uploadedBytes += chunkSize;
                    firstChunk = false;
                }

                if (remainder > 0)
                {
                    byte[] buffer = new byte[remainder];
                    Array.Copy(fileBytes, numChunks * chunkSize, buffer, 0, remainder);

                    FileChunk chunk = FileHelper.CreateFileChunk(samplefileName, uploadedBytes, firstChunk, buffer, remainder, true);

                    HttpResponseMessage response = await httpClient.PostAsJsonAsync("/files/addFileChunk", chunk);
                    Assert.Equal(HttpStatusCode.OK, response.StatusCode);

                    uploadedBytes += remainder;
                }

                string nameOnly = Path.GetFileNameWithoutExtension(samplefileName);
                EFileAsset? dbEntry = await dbContext.FileAssets.FirstOrDefaultAsync(e => e.Name == nameOnly && e.Extension == extension);
                Assert.True(dbEntry != null, "File doesnt exist in DB");

                string filePath = Path.Combine(webRootPath, dbEntry.Location);
                byte[] finalFileBytes = File.ReadAllBytes(filePath);

                Assert.True(File.Exists(filePath), $"Uploaded file not found at: {filePath}");
                Assert.Equal(FileHelper.GetFileHash(fileBytes), FileHelper.GetFileHash(finalFileBytes)); // verify if both files are the same after upload

                File.Delete(filePath);
            }
            finally
            {
                // Clean up temp files
                FileHelper.CleanFile(bigFilePath);
            }
        }

        public static string GetFileHash(byte[] bytes)
        {
            SHA1Managed hash = new SHA1Managed();
            byte[] hashedBytes = hash.ComputeHash(bytes);
            return ConvertBytesToHex(hashedBytes);
        }

        public static string ConvertBytesToHex(byte[] bytes)
        {
            StringBuilder sb = new StringBuilder();

            for (var i = 0; i < bytes.Length; i++)
            {
                sb.Append(bytes[i].ToString("x"));
            }
            return sb.ToString();
        }

        public static void CleanFile(string filePath)
        {
            if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }
}
