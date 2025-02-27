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
                UploadResult result = await m_fileService.UploadAsync(fileData.FileName, fileData);
                if (!result.Uploaded)
                {
                    return BadRequest("Upload image failed");
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
        [HttpGet("download")]
        public IActionResult DownloadFile([FromQuery] string filePath)
        {
            string absolutePath = Path.Combine(m_hostEnvironment.WebRootPath, filePath);

            if (!System.IO.File.Exists(absolutePath))
            {
                return NotFound("File not found.");
            }

            // Get MIME type
            string contentType = "application/octet-stream";
            string fileName = Path.GetFileName(absolutePath);

            // Return the file
            byte[] fileBytes = System.IO.File.ReadAllBytes(absolutePath);
            return File(fileBytes, contentType, fileName);
        }
    }
}
