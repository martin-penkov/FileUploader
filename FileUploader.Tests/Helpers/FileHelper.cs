using System.Security.Cryptography;

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
    }

}
