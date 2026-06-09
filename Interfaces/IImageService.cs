namespace PortfolioApi.Interfaces;

/// <summary>
/// Abstracts cloud image storage so the provider can be swapped without
/// touching the rest of the codebase (SOLID: Open/Closed Principle).
/// </summary>
public interface IImageService
{
    /// <summary>
    /// Upload a file and return its public secure URL and cloud public ID.
    /// </summary>
    Task<(string SecureUrl, string PublicId)> UploadAsync(
        IFormFile file,
        string    folder = "portfolio");

    /// <summary>
    /// Upload a video file (e.g. .mp4) and return its secure URL and public ID.
    /// </summary>
    Task<(string SecureUrl, string PublicId)> UploadVideoAsync(
        IFormFile file,
        string    folder = "portfolio/videos");

    /// <summary>
    /// Delete an image from cloud storage by its public ID.
    /// Called automatically when an article image is replaced or deleted.
    /// </summary>
    Task<bool> DeleteAsync(string publicId);

    /// <summary>Delete a video from cloud storage by its public ID.</summary>
    Task<bool> DeleteVideoAsync(string publicId);
}
