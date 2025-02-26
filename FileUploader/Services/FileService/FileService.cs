using FileUploader.Common.Communication;
using FileUploader.Common.Types;

namespace FileUploader.Services.FileService
{
    public class FileService : IFileService
    {
        IWebHostEnvironment m_hostEnvironment;
        ILogger m_logger;

        public FileService(ILoggerFactory loggerFactory, IWebHostEnvironment hostEnvironment)
        {
            m_logger = loggerFactory.CreateLogger("fileService");
            m_hostEnvironment = hostEnvironment;
        }

        public async Task<UploadResult> UploadAsync(string name, IFormFile file)
        {
            if (System.String.IsNullOrEmpty(name) || file == null)
            {
                m_logger.LogInformation("Upload failed  - Empty file or name.");
            }

            try
            {
                FileDescription resultFile = PrepareFile(name);
                long size = 0;
                using (FileStream stream = new FileStream(resultFile.PhysicalPath, FileMode.Create))
                {
                    await file!.CopyToAsync(stream);
                    size = stream.Length;
                }

                m_logger.LogDebug("File uploaded successfully: {file_path}", resultFile.RelativeLocation);

                return new UploadResult()
                {
                    Uploaded = true,
                    FileName = Path.GetFileNameWithoutExtension(name),
                    RelativePath = resultFile.RelativeLocation,
                    Extension = resultFile.Extension,
                    Size = size
                };
            }
            catch (Exception ex)
            {
                m_logger.LogWarning(ex, "Error while uploading file: {file_name}", name);
                return new UploadResult()
                {
                    Uploaded = false,
                    FileName = name,
                    Size = 0
                };
            }
        }

        public void Delete(string relativePath)
        {
            throw new NotImplementedException();
        }

        FileDescription PrepareFile(string srcFileName)
        {
            string directoryName = "filesLibrary";

            string fileExtension = Path.GetExtension(srcFileName);
            string randomizedFileName = Path.GetRandomFileName().Replace(".", "").Replace("\\", "");

            string relativePath = Path.Combine(directoryName, $"{randomizedFileName}{fileExtension}");

            string physicalPath = MapToPhysical(relativePath);
            string relativeWebLocation = relativePath.Replace("\\", "/");

            CreateDirectoryIfDoesntExist(directoryName);

            m_logger.LogInformation("Prepared file - Name: {file_name}, Path: {path}", directoryName, randomizedFileName, relativePath);

            return new FileDescription
            {
                PhysicalPath = physicalPath,
                RelativeLocation = relativeWebLocation,
                Extension = fileExtension
            };
        }

        string MapToPhysical(string relativePath)
        {
            if (relativePath.StartsWith("\\"))
            {
                relativePath = relativePath.TrimStart('\\');
            }

            return Path.Combine(m_hostEnvironment.ContentRootPath, relativePath);
        }

        void CreateDirectoryIfDoesntExist(string directory)
        {
            string path = MapToPhysical(directory);

            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }
    }
}
