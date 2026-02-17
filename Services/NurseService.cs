using MomCare.Dto;
using MomCare.Interfaces;
using MomCare.Models;

namespace MomCare.Services;

public class NurseService : INurseService
{
    private readonly IUnitOfWork _unitOfWork;

    public NurseService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<NurseProfileDetailDto?> GetProfileAsync(int userId)
    {
        var nurse = await _unitOfWork.Users.FindAsync(
            u => u.Id == userId, 
            "UserRoles.Role");
        
        if (nurse == null) return null;

        var profile = await _unitOfWork.NurseProfiles.FindAsync(
            np => np.UserId == userId,
            "Documents");

        if (profile == null) return null;

        return new NurseProfileDetailDto
        {
            UserId = nurse.Id,
            FullName = nurse.FullName,
            Email = nurse.Email ?? "",
            Phone = nurse.Phone,
            Bio = profile.Bio,
            YearsExperience = profile.YearsExperience,
            ServiceRadiusKm = profile.ServiceRadiusKm,
            IsVerified = profile.IsVerified,
            Documents = profile.Documents.Select(d => new NurseDocumentDto
            {
                Id = d.Id,
                Type = d.Type,
                FileUrl = d.FileUrl,
                Status = d.Status
            }).ToList()
        };
    }

    public async Task<bool> UpdateProfileAsync(int userId, UpdateNurseProfileDto updateDto)
    {
        var profile = await _unitOfWork.NurseProfiles.FindAsync(np => np.UserId == userId);
        if (profile == null) return false;

        profile.Bio = updateDto.Bio;
        profile.YearsExperience = updateDto.YearsExperience;
        profile.ServiceRadiusKm = updateDto.ServiceRadiusKm;

        _unitOfWork.NurseProfiles.Update(profile);
        return await _unitOfWork.CompleteAsync() > 0;
    }

    public async Task<bool> AddDocumentAsync(int userId, UploadDocumentDto uploadDto)
    {
        var profile = await _unitOfWork.NurseProfiles.FindAsync(np => np.UserId == userId);
        if (profile == null) return false;

        var document = new Document
        {
            NurseProfileId = profile.Id,
            Type = uploadDto.Type,
            FileUrl = uploadDto.FileUrl,
            Status = "pending_review",
            CreatedAt = DateTime.UtcNow
        };

        await _unitOfWork.Documents.AddAsync(document);
        return await _unitOfWork.CompleteAsync() > 0;
    }
}
