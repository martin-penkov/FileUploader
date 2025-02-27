using FileUploader.Common;
using FileUploader.Common.Communication;
using FileUploader.Db;
using FileUploader.Db.Entities;
using FileUploader.Services.FileService;
using FileUploader.Utility;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FileUploader.Controllers
{
    [Authorize]
    [ApiController]
    [Route("files")]
    public class FileController : ControllerBase
    {
        #region Members 

        private readonly ILogger<FileController> m_logger;
        private readonly IFileService m_fileService;
        private readonly AppDbContext m_context;
        IWebHostEnvironment m_hostEnvironment;

        #endregion

        public FileController(ILogger<FileController> logger, IFileService fileService, AppDbContext context, IWebHostEnvironment hostEnvironment)
        {
            m_logger = logger;
            m_fileService = fileService;
            m_context = context;
            m_hostEnvironment = hostEnvironment;
        }

        [AllowAnonymous]
        [HttpGet("publicFiles")]
        public async Task<ActionResult<IList<UploadResult>>> GetPublicFiles()
        {
            List<EFileAsset> fileAssets = await m_context.FileAssets.ToListAsync();
            List<UploadResult> uploadResults = new List<UploadResult>();

            foreach (EFileAsset asset in fileAssets)
            {
                uploadResults.Add(FileAssetsMapper.CreateFromDb(asset));
            }

            return Ok(uploadResults);
        }

        [AllowAnonymous]
        [HttpPost("addFiles")]
        public async Task<ActionResult<IList<UploadResult>>> AddFiles([FromForm] List<IFormFile> filesList)
        {
            List<UploadResult> results = new List<UploadResult>();

            foreach (IFormFile fileData in filesList)
            {
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
                    Size = result.Size
                };

                m_context.FileAssets.Add(fileAsset);
                await m_context.SaveChangesAsync();

                results.Add(result);
            }

            return Ok(results);
        }

        [AllowAnonymous]
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

        [AllowAnonymous]
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
