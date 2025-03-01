using FileUploader.Common.Communication;
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
