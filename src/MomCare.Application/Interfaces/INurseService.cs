using MomCare.Dto;

namespace MomCare.Interfaces;

public interface INurseService
{
    Task<NurseProfileDetailDto?> GetProfileAsync(int userId);
    Task<bool> UpdateProfileAsync(int userId, UpdateNurseProfileDto updateDto);
    Task<bool> AddDocumentAsync(int userId, UploadDocumentDto uploadDto);
}
