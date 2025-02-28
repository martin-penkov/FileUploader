using FileUploader.Caching;
using FileUploader.Common;
using FileUploader.Common.Communication;
using FileUploader.Common.Types;
using FileUploader.Db;
using FileUploader.Db.Entities;
using Microsoft.EntityFrameworkCore;

namespace FileUploader.Services.FileService
{
    public class FileService : IFileService
    {
        IWebHostEnvironment m_hostEnvironment;
        ILogger m_logger;
        private readonly IFileUploaderCache m_fileUploaderCache;
        private readonly AppDbContext m_context;

        public FileService(ILoggerFactory loggerFactory, IWebHostEnvironment hostEnvironment, IFileUploaderCache fileUploaderCache, AppDbContext context)
        {
            m_logger = loggerFactory.CreateLogger("fileService");
            m_hostEnvironment = hostEnvironment;
            m_fileUploaderCache = fileUploaderCache;
            m_context = context;
        }

        public async Task<UploadResult> UploadAsync(string name, IFormFile file)
        {
            if (System.String.IsNullOrEmpty(name) || file == null)
            {
                m_logger.LogInformation("Upload failed  - Empty file or name.");
                return GenerateErrorResult(name);
            }

            try
            {
                FileDescription resultFile = PrepareFileDescription(name);
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
                return GenerateErrorResult(name);
            }
        }

        public UploadResult UploadChunkAsync(FileChunk fileChunk)
        {
            try
            {
                FileDescription? resultFile = m_fileUploaderCache.Get(fileChunk.FileName);

                if (resultFile == null)
                {
                    return GenerateErrorResult(fileChunk.FileName);
                }

                long size = fileChunk.Data.Length;

                if (fileChunk.FirstChunk && DoesFileExist(resultFile.RelativeLocation))
                {
                    Delete(resultFile.RelativeLocation);   // IF here this means there is no entry in db for this file, so we should delete it...
                }

                using (Stream stream = File.OpenWrite(resultFile.PhysicalPath))
                {
                    stream.Seek(fileChunk.Offset, SeekOrigin.Begin);
                    stream.Write(fileChunk.Data, 0, fileChunk.Data.Length);
                }

                resultFile.Size += size;
                m_fileUploaderCache.AddOrUpdate(fileChunk.FileName, resultFile);

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
                return GenerateErrorResult(fileChunk.FileName);
            }
        }

        public async Task<bool> CreateDbEntryOnFirstChunkAsync(string fileName)
        {
            bool doesFileAlreadyExist = await m_context.FileAssets.AnyAsync(fa => fa.FullName == fileName);
            if (doesFileAlreadyExist)
            {
                return false;
            }

            FileDescription fileDescr = PrepareFileDescription(fileName);

            m_context.FileAssets.Add(new EFileAsset
            {
                FullName = fileName,
                Name = fileDescr.NameWithoutExtension,
                Location = fileDescr.RelativeLocation,
                Extension = fileDescr.Extension,
                UploadDate = DateTime.UtcNow,
                Size = 0,
                Status = Status.InProgress
            });

            await m_context.SaveChangesAsync();
            m_fileUploaderCache.AddOrUpdate(fileName, fileDescr);

            return true;
        }

        public async Task ClearStateAsync(string fileName)
        {
            m_fileUploaderCache.ClearEntry(fileName);

            EFileAsset? fileAsset = await m_context.FileAssets.FirstOrDefaultAsync(fa => fa.FullName == fileName);
            m_context.FileAssets.Remove(fileAsset!);
            await m_context.SaveChangesAsync();

            Delete(fileAsset!.Location);
        }

        public async Task<bool> FinalizeChunkedFileUploadAsync(string fileName)
        {
            EFileAsset? fileAsset = await m_context.FileAssets.FirstOrDefaultAsync(fa => fa.FullName == fileName);

            if (fileAsset == null)
            {
                m_fileUploaderCache.ClearEntry(fileName);
                Delete(fileAsset!.Location);

                return false;
            }

            FileDescription fileDescr = m_fileUploaderCache.Get(fileName);

            fileAsset.Status = Status.Complete;
            fileAsset.Size = fileDescr.Size;

            m_fileUploaderCache.ClearEntry(fileName);
            await m_context.SaveChangesAsync();

            return true;
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

        public FileDescription PrepareFileDescription(string srcFileName)
        {
            string directoryName = "filesLibrary";

            string nameWithoutExtension = Path.GetFileNameWithoutExtension(srcFileName);
            string fileExtension = Path.GetExtension(srcFileName);
            string randomizedFileName = Path.GetRandomFileName().Replace(".", "").Replace("\\", "");

            string relativePath = Path.Combine(directoryName, $"{randomizedFileName}{fileExtension}");

            string physicalPath = MapToPhysical(relativePath);
            string relativeWebLocation = relativePath.Replace("\\", "/");

            CreateDirectoryIfDoesntExist(directoryName);

            m_logger.LogInformation("Prepared file - Name: {file_name}, Path: {path}", directoryName, randomizedFileName, relativePath);

            return new FileDescription
            {
                NameWithoutExtension = nameWithoutExtension,
                PhysicalPath = physicalPath,
                RelativeLocation = relativeWebLocation,
                Extension = fileExtension,
                Size = 0
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

        UploadResult GenerateErrorResult(string name)
        {
            return new UploadResult()
            {
                Uploaded = false,
                FileName = name,
                Size = 0
            };
        }
    }
}
