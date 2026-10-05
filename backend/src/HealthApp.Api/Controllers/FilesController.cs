using HealthApp.Application.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace HealthApp.Api.Controllers;

[ApiController, Route("api/files"), Authorize]
public sealed class FilesController(IFileStorage fileStorage, ICurrentUser current) : ControllerBase
{
    private static readonly IReadOnlyDictionary<string, string[]> AllowedExtensions =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["recipes"] = [".jpg", ".jpeg", ".png", ".webp"],
            ["outlets"] = [".jpg", ".jpeg", ".png", ".webp"],
            ["avatars"] = [".jpg", ".jpeg", ".png", ".webp"],
            ["documents"] = [".pdf", ".jpg", ".jpeg", ".png", ".webp", ".doc", ".docx", ".xls", ".xlsx"]
        };

    [HttpPost("{folder}")]
    [RequestSizeLimit(20_000_000)]
    public async Task<IActionResult> Upload(string folder, IFormFile file)
    {
        if (!AllowedExtensions.TryGetValue(folder, out var allowed))
            return BadRequest(new { message = "Unsupported file folder." });

        if (file is null || file.Length == 0)
            return BadRequest(new { message = "Please select a file." });

        if (file.Length > 20_000_000)
            return BadRequest(new { message = "File must be 20 MB or smaller." });

        var extension = Path.GetExtension(file.FileName);
        if (!allowed.Contains(extension, StringComparer.OrdinalIgnoreCase))
            return BadRequest(new
            {
                message = $"Unsupported file type for {folder}.",
                allowedExtensions = allowed
            });

        await using var stream = file.OpenReadStream();
        var tenantFolder = current.OutletId is Guid outletId
            ? $"outlets/{outletId:N}/{folder}"
            : $"platform/{folder}";

        var stored = await fileStorage.UploadAsync(
            stream,
            file.FileName,
            file.ContentType,
            tenantFolder,
            HttpContext.RequestAborted);

        return Ok(new
        {
            url = stored.Url,
            key = stored.Key,
            contentType = stored.ContentType
        });
    }
}
