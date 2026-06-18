using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Taskpilot.API.Services;

namespace Taskpilot.API.Controllers;

/// <summary>
/// Endpoints for uploading and downloading files. All require authentication.
/// </summary>
[ApiController]
[Authorize]
[Route("api/files")]
public class FilesController : BaseApiController
{
    private readonly IFileService _fileService;

    public FilesController(IFileService fileService)
    {
        _fileService = fileService;
    }

    /// <summary>Uploads a file (multipart/form-data, field name "file").</summary>
    /// <returns>201 with the file metadata, or 400 when the file is missing/too large.</returns>
    [HttpPost]
    public async Task<IActionResult> Upload(IFormFile file)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _fileService.SaveAsync(file, userId.Value);
        return result.Succeeded
            ? StatusCode(StatusCodes.Status201Created, result.Value)
            : BadRequest(new { error = result.Error });
    }

    /// <summary>Downloads a previously uploaded file by id.</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Download(Guid id)
    {
        var result = await _fileService.GetForDownloadAsync(id);
        if (!result.Succeeded)
            return NotFound(new { error = result.Error });

        var download = result.Value!;
        return PhysicalFile(download.PhysicalPath, download.ContentType, download.FileName);
    }
}
