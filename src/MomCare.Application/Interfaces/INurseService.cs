using MomCare.Dto;

namespace MomCare.Interfaces;

public interface INurseService
{
    Task<NurseProfileDetailDto?> GetProfileAsync(int userId);
    Task<bool> UpdateProfileAsync(int userId, UpdateNurseProfileDto updateDto);
    Task<NurseDocumentDto?> UploadDocumentAsync(int userId, UploadDocumentDto uploadDto);
    Task<NurseDocumentDto?> ReplaceDocumentAsync(int userId, int documentId, UploadDocumentDto uploadDto);
    Task<bool> DeleteDocumentAsync(int userId, int documentId);
    Task<string?> GetDocumentSignedUrlAsync(int userId, int documentId);
}
