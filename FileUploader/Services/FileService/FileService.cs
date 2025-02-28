using FileUploader.Common.Communication;
using FileUploader.Common.Types;
using FileUploader.Db.Entities;

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

        public async Task<UploadResult> UploadChunkAsync(FileChunk fileChunk)
        {
            try
            {
                FileDescription resultFile = PrepareFile(fileChunk.FileName);
                long size = fileChunk.Data.Length;

                if (fileChunk.FirstChunk && DoesFileExist(resultFile.RelativeLocation))
                {
                    Delete(resultFile.RelativeLocation);    //TODO check if there is one in db and retunr false if there is   (MAYBE DO THE CHECK IN THE CONTROLLER AND RETURN THERE IN THE BEGINNING)
                }

                using (Stream stream = File.OpenWrite(resultFile.PhysicalPath))
                {
                    stream.Seek(fileChunk.Offset, SeekOrigin.Begin);
                    stream.Write(fileChunk.Data, 0, fileChunk.Data.Length);
                }

                return new UploadResult()
                {
                    Uploaded = true,
                    FileName = Path.GetFileNameWithoutExtension(fileChunk.FileName),
                    RelativePath = resultFile.RelativeLocation,
                    Extension = resultFile.Extension,
                    Size = size
                };
            }
            catch (Exception ex)
            {
                m_logger.LogWarning(ex, "Error while writing file chunk: {file_name}", fileChunk.FileName);
                return new UploadResult()
                {
                    Uploaded = false,
                    FileName = fileChunk.FileName,
                    Size = 0
                };
            }
        }

        public bool Delete(string relativePath)
        {
            string physicalPath = MapToPhysical(relativePath);

            if (File.Exists(physicalPath))
            {
                File.Delete(physicalPath);
                return true;
            }
            return false;
        }

        public bool DoesFileExist(string relativePath)
        {
            string physicalPath = MapToPhysical(relativePath);

            return File.Exists(physicalPath);
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
            
            return Path.Combine(m_hostEnvironment.WebRootPath, relativePath);
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
