using Microsoft.AspNetCore.Http;
using MomCare.Dto;

namespace MomCare.Interfaces;

public interface INurseService
{
    Task<NurseProfileDetailDto?> GetProfileAsync(int userId);
    Task<bool> UpdateProfileAsync(int userId, UpdateNurseProfileDto updateDto);
    Task<string?> UploadAvatarAsync(int userId, IFormFile file);
    Task<NurseDocumentDto?> UploadDocumentAsync(int userId, UploadDocumentDto uploadDto);
    Task<IReadOnlyList<NurseDocumentDto>> UploadDocumentsAsync(int userId, UploadDocumentsDto uploadDto);
    Task<bool> SubmitVerificationAsync(int userId);
    Task<NurseDocumentDto?> ReplaceDocumentAsync(int userId, int documentId, UploadDocumentDto uploadDto);
    Task<bool> DeleteDocumentAsync(int userId, int documentId);
    Task<string?> GetDocumentSignedUrlAsync(int userId, int documentId);
}
