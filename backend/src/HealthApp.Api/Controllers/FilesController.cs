using HealthApp.Application.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace HealthApp.Api.Controllers;

[ApiController, Route("api/files"), Authorize]
public sealed class FilesController(IFileStorage fileStorage, IMediaService mediaService, ICurrentUser current) : ControllerBase
{
    private static readonly IReadOnlyDictionary<string, string[]> AllowedExtensions =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["recipes"] = [".jpg", ".jpeg", ".png", ".webp"],
            ["outlets"] = [".jpg", ".jpeg", ".png", ".webp"],
            ["avatars"] = [".jpg", ".jpeg", ".png", ".webp"]
        };

    [HttpPost("{folder}")]
    [RequestSizeLimit(10_000_000)]
    public async Task<IActionResult> Upload(string folder, IFormFile file)
    {
        if (!AllowedExtensions.TryGetValue(folder, out var allowed))
            return BadRequest(new { message = "Unsupported file folder." });

        var role = current.Role;
        var allowedForRole = role switch
        {
            "Customer" => folder.Equals("avatars", StringComparison.OrdinalIgnoreCase),
            "OutletAdmin" => folder.Equals("recipes", StringComparison.OrdinalIgnoreCase)
                            || folder.Equals("outlets", StringComparison.OrdinalIgnoreCase)
                            || folder.Equals("avatars", StringComparison.OrdinalIgnoreCase),
            "SuperAdmin" => true,
            _ => false
        };

        if (!allowedForRole)
            return Forbid();

        if (file is null || file.Length == 0)
            return BadRequest(new { message = "Please select a file." });

        if (file.Length > 10_000_000)
            return BadRequest(new { message = "Image must be 10 MB or smaller. It will be resized and compressed automatically." });

        var extension = Path.GetExtension(file.FileName);
        if (!allowed.Contains(extension, StringComparer.OrdinalIgnoreCase))
            return BadRequest(new
            {
                message = $"Unsupported file type for {folder}.",
                allowedExtensions = allowed
            });

        var tenantFolder = current.OutletId is Guid outletId
            ? $"outlets/{outletId:N}/{folder}"
            : $"platform/{folder}";

        await using var stream = file.OpenReadStream();
        var profile = folder.Equals("recipes", StringComparison.OrdinalIgnoreCase)
            ? MediaImageProfile.Recipe
            : folder.Equals("avatars", StringComparison.OrdinalIgnoreCase)
                ? MediaImageProfile.Avatar
                : MediaImageProfile.OutletHero;

        var optimized = await mediaService.UploadImageAsync(
            stream,
            file.FileName,
            file.ContentType,
            tenantFolder,
            profile,
            HttpContext.RequestAborted);

        return Ok(new
        {
            url = optimized.Url,
            key = optimized.Key,
            contentType = optimized.ContentType,
            thumbnailUrl = optimized.Thumbnail?.Url,
            smallUrl = optimized.Small?.Url,
            mediumUrl = optimized.Medium?.Url,
            largeUrl = optimized.Large?.Url,
            originalSizeBytes = optimized.OriginalSizeBytes,
            originalWidth = optimized.OriginalWidth,
            originalHeight = optimized.OriginalHeight
        });
    }
}
