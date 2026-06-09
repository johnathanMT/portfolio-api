using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using PortfolioApi.Interfaces;

namespace PortfolioApi.Services;

/// <summary>
/// Implements IImageService using Cloudinary's free tier.
/// Render has an ephemeral filesystem, so all images must live in the cloud.
/// </summary>
public class CloudinaryImageService : IImageService
{
    private readonly Cloudinary _cloudinary;
    private readonly ILogger<CloudinaryImageService> _logger;

    // Max upload size: 10 MB for images, 100 MB for video
    private const long MaxFileSizeBytes  = 10  * 1024 * 1024;
    private const long MaxVideoSizeBytes = 100 * 1024 * 1024;

    private static readonly HashSet<string> AllowedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/jpg", "image/png", "image/gif",
        "image/webp", "image/avif"
    };

    private static readonly HashSet<string> AllowedVideoMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "video/mp4", "video/webm", "video/quicktime", "video/x-matroska", "video/ogg"
    };

    public CloudinaryImageService(
        IConfiguration config,
        ILogger<CloudinaryImageService> logger)
    {
        _logger = logger;

        var cloudName  = config["Cloudinary:CloudName"]  ?? throw new InvalidOperationException("Cloudinary:CloudName not set.");
        var apiKey     = config["Cloudinary:ApiKey"]     ?? throw new InvalidOperationException("Cloudinary:ApiKey not set.");
        var apiSecret  = config["Cloudinary:ApiSecret"]  ?? throw new InvalidOperationException("Cloudinary:ApiSecret not set.");

        var account    = new Account(cloudName, apiKey, apiSecret);
        _cloudinary    = new Cloudinary(account) { Api = { Secure = true } };
    }

    // ──────────────────────────────────────────────────────────
    public async Task<(string SecureUrl, string PublicId)> UploadAsync(
        IFormFile file,
        string    folder = "portfolio")
    {
        // Security: validate type and size BEFORE reading the stream
        if (!AllowedMimeTypes.Contains(file.ContentType))
            throw new InvalidOperationException(
                $"Unsupported file type: {file.ContentType}. Allowed: JPEG, PNG, GIF, WebP, AVIF.");

        if (file.Length > MaxFileSizeBytes)
            throw new InvalidOperationException(
                $"File exceeds the 10 MB limit ({file.Length / 1024 / 1024} MB uploaded).");

        await using var stream = file.OpenReadStream();

        var uploadParams = new ImageUploadParams
        {
            File           = new FileDescription(file.FileName, stream),
            Folder         = folder,
            // Auto-generate a unique public ID
            PublicId       = $"{folder}/{Guid.NewGuid()}",
            // Optimise on upload
            Transformation = new Transformation()
                                 .Quality("auto:good")
                                 .FetchFormat("auto"),
            // Enforce image content (prevents SVG/PDF injection)
            AllowedFormats = new[] { "jpg", "jpeg", "png", "gif", "webp", "avif" },
            Overwrite      = true,
        };

        var result = await _cloudinary.UploadAsync(uploadParams);

        if (result.StatusCode != System.Net.HttpStatusCode.OK)
        {
            _logger.LogError("Cloudinary upload failed: {Error}", result.Error?.Message);
            throw new Exception($"Image upload failed: {result.Error?.Message}");
        }

        _logger.LogInformation(
            "Image uploaded to Cloudinary. PublicId={PublicId}, Url={Url}",
            result.PublicId,
            result.SecureUrl);

        return (result.SecureUrl.ToString(), result.PublicId);
    }

    // ──────────────────────────────────────────────────────────
    public async Task<(string SecureUrl, string PublicId)> UploadVideoAsync(
        IFormFile file,
        string    folder = "portfolio/videos")
    {
        if (!AllowedVideoMimeTypes.Contains(file.ContentType))
            throw new InvalidOperationException(
                $"Unsupported video type: {file.ContentType}. Allowed: MP4, WebM, MOV, MKV, OGG.");

        if (file.Length > MaxVideoSizeBytes)
            throw new InvalidOperationException(
                $"Video exceeds the 100 MB limit ({file.Length / 1024 / 1024} MB uploaded).");

        await using var stream = file.OpenReadStream();

        var uploadParams = new VideoUploadParams
        {
            File      = new FileDescription(file.FileName, stream),
            Folder    = folder,
            PublicId  = $"{folder}/{Guid.NewGuid()}",
            Overwrite = true,
        };

        var result = await _cloudinary.UploadAsync(uploadParams);

        if (result.StatusCode != System.Net.HttpStatusCode.OK)
        {
            _logger.LogError("Cloudinary video upload failed: {Error}", result.Error?.Message);
            throw new Exception($"Video upload failed: {result.Error?.Message}");
        }

        _logger.LogInformation("Video uploaded to Cloudinary. PublicId={PublicId}", result.PublicId);
        return (result.SecureUrl.ToString(), result.PublicId);
    }

    // ──────────────────────────────────────────────────────────
    public async Task<bool> DeleteAsync(string publicId)
    {
        var deleteParams = new DeletionParams(publicId) { ResourceType = ResourceType.Image };
        var result       = await _cloudinary.DestroyAsync(deleteParams);

        if (result.Result == "ok")
        {
            _logger.LogInformation("Cloudinary image deleted: {PublicId}", publicId);
            return true;
        }

        _logger.LogWarning("Cloudinary delete returned non-ok for {PublicId}: {Result}", publicId, result.Result);
        return false;
    }

    public async Task<bool> DeleteVideoAsync(string publicId)
    {
        var deleteParams = new DeletionParams(publicId) { ResourceType = ResourceType.Video };
        var result       = await _cloudinary.DestroyAsync(deleteParams);

        if (result.Result == "ok")
        {
            _logger.LogInformation("Cloudinary video deleted: {PublicId}", publicId);
            return true;
        }

        _logger.LogWarning("Cloudinary video delete returned non-ok for {PublicId}: {Result}", publicId, result.Result);
        return false;
    }
}
