using FileUploader.Caching;
using FileUploader.Common;
using FileUploader.Common.Communication;
using FileUploader.Common.Types;
using FileUploader.Db;
using FileUploader.Db.Entities;
using FileUploader.Services.FileService;
using FileUploader.Utility;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FileUploader.Controllers
{
    [ApiController]
    [Route("files")]
    public class FileController : ControllerBase
    {
        #region Members 

        private readonly ILogger<FileController> m_logger;
        private readonly IFileService m_fileService;
        private readonly AppDbContext m_context;
        private readonly IFileUploaderCache m_fileUploaderCache;
        IWebHostEnvironment m_hostEnvironment;

        #endregion

        public FileController(ILogger<FileController> logger, IFileService fileService, AppDbContext context, IFileUploaderCache fileUploaderCache, IWebHostEnvironment hostEnvironment)
        {
            m_logger = logger;
            m_fileService = fileService;
            m_context = context;
            m_fileUploaderCache = fileUploaderCache;
            m_hostEnvironment = hostEnvironment;
        }

        [HttpGet("publicFiles")]
        public async Task<ActionResult<IList<UploadResult>>> GetPublicFiles()
        {
            List<EFileAsset> fileAssets = await m_context.FileAssets.Where(fa => fa.Status == Status.Complete).ToListAsync();
            List<UploadResult> uploadResults = new List<UploadResult>();

            foreach (EFileAsset asset in fileAssets)
            {
                uploadResults.Add(FileAssetsMapper.CreateFromDb(asset));
            }

            return Ok(uploadResults);
        }

        [RequestFormLimits(ValueLengthLimit = int.MaxValue, MultipartBodyLengthLimit = int.MaxValue)]
        [HttpPost("addFiles")]
        public async Task<ActionResult<IList<UploadResult>>> AddFiles([FromForm] List<IFormFile> filesList)
        {
            List<UploadResult> results = new List<UploadResult>();

            foreach (IFormFile fileData in filesList)
            {
                m_logger.LogInformation("Uploading small file using StreamContent");

                if(await m_context.FileAssets.AnyAsync(fa => fa.FullName == fileData.FileName))
                {
                    return BadRequest(ErrorMessage.FileWithSameNameExists);
                }

                UploadResult result = await m_fileService.UploadAsync(fileData.FileName, fileData);
                if (!result.Uploaded)
                {
                    return BadRequest(ErrorMessage.ErrorDuringFileUpload);
                }

                EFileAsset fileAsset = new EFileAsset
                {
                    FullName = result.FileName + result.Extension,
                    Name = result.FileName,
                    Location = result.RelativePath!,
                    Extension = result.Extension!,
                    UploadDate = DateTime.UtcNow,
                    Size = result.Size,
                    Status = Status.Complete
                };

                m_context.FileAssets.Add(fileAsset);
                await m_context.SaveChangesAsync();

                results.Add(result);
            }

            return Ok(results);
        }

        [HttpPost("addFileChunk")]
        public async Task<ActionResult> AddFileChunk([FromBody] FileChunk fileChunk)
        {
            try
            {
                if (fileChunk.FirstChunk)
                {
                    m_logger.LogInformation("Start Uploading file chunks for {FileName}", fileChunk.FileName);

                    bool isInitialized = await m_fileService.CreateDbEntryOnFirstChunkAsync(fileChunk.FileName);

                    if (!isInitialized)
                    {
                        return BadRequest(ErrorMessage.FileWithSameNameExists);
                    }
                }

                UploadResult uploadResult = m_fileService.UploadChunkAsync(fileChunk);

                if (!uploadResult.Uploaded)
                {
                    await m_fileService.ClearStateAsync(fileChunk.FileName);

                    return BadRequest(ErrorMessage.ErrorDuringFileUpload);
                }

                if (fileChunk.LastChunk)
                {
                    bool isFinalized = await m_fileService.FinalizeChunkedFileUploadAsync(fileChunk.FileName);

                    if (!isFinalized)
                    {
                        return BadRequest(ErrorMessage.ErrorDuringFileUpload);
                    }
                }

                m_logger.LogInformation("wrote chunk");

                return Ok();
            }
            catch (Exception ex)
            {
                await m_fileService.ClearStateAsync(fileChunk.FileName);
                return BadRequest(ErrorMessage.ErrorDuringFileUpload);
            }
        }

        [HttpDelete("delete")]
        public async Task<ActionResult> Delete([FromQuery] string fileName)
        {
            EFileAsset? fileAsset = m_context.FileAssets.FirstOrDefault(fa => fa.Name == fileName);

            if (fileAsset == null)
            {
                return NotFound(ErrorMessage.FileNotFound);
            }

            bool doesPhysicalFileExist = m_fileService.DoesFileExist(fileAsset.Location);

            if (!doesPhysicalFileExist)
            {
                return NotFound(ErrorMessage.PhysicalFileNotFound);
            }

            m_context.FileAssets.Remove(fileAsset);
            await m_context.SaveChangesAsync();

            m_fileService.Delete(fileAsset.Location);

            return Ok();
        }

        [HttpGet("download")]
        public async Task<ActionResult> DownloadFile([FromQuery] string fileName)
        {
            EFileAsset? fileAsset = m_context.FileAssets.FirstOrDefault(fa => fa.Name == fileName);

            if (fileAsset == null)
            {
                return NotFound(ErrorMessage.FileNotFound);
            }

            string absolutePath = Path.Combine(m_hostEnvironment.WebRootPath, fileAsset.Location);

            if (!System.IO.File.Exists(absolutePath))
            {
                return NotFound(ErrorMessage.PhysicalFileNotFound);
            }

            // MIME type
            string contentType = "application/octet-stream";

            byte[] fileBytes = System.IO.File.ReadAllBytes(absolutePath);
            return File(fileBytes, contentType, fileAsset.FullName);
        }
    }
}
