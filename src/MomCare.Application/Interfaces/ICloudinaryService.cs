using Microsoft.AspNetCore.Http;
using MomCare.Dto;

namespace MomCare.Interfaces;

/// <summary>
/// Cloudinary integration service for uploading, managing, and accessing secure images.
/// </summary>
public interface ICloudinaryService
{
    /// <summary>
    /// Upload an image to Cloudinary with secure access.
    /// </summary>
    Task<CloudinaryUploadResultDto> UploadPrivateAsync(IFormFile file, string folder);

    /// <summary>
    /// Delete an image from Cloudinary by its public ID.
    /// </summary>
    Task<bool> DeleteAsync(string publicId);

    /// <summary>
    /// Generate a signed/temporary URL to access a private resource.
    /// Default expiration: 300 seconds (5 minutes).
    /// </summary>
    string GetSignedUrl(string publicId, int expiresInSeconds = 300);
}
