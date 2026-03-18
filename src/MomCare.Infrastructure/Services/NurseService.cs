using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MomCare.Data;
using MomCare.Dto;
using MomCare.Interfaces;
using MomCare.Models;

namespace MomCare.Services;

public class NurseService : INurseService
{
    private readonly MomCareContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public NurseService(MomCareContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public async Task<NurseProfileDetailDto?> GetProfileAsync(int userId)
    {
        var nurse = await _userManager.FindByIdAsync(userId.ToString());
        if (nurse == null)
        {
            return null;
        }

        var profile = await _context.NurseProfiles
            .Include(np => np.Documents)
            .FirstOrDefaultAsync(np => np.UserId == userId);

        if (profile == null)
        {
            return null;
        }

        return new NurseProfileDetailDto
        {
            UserId = nurse.Id,
            FullName = nurse.FullName,
            Email = nurse.Email ?? string.Empty,
            Phone = nurse.PhoneNumber,
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
        var profile = await _context.NurseProfiles.FirstOrDefaultAsync(np => np.UserId == userId);
        if (profile == null)
        {
            return false;
        }

        profile.Bio = updateDto.Bio;
        profile.YearsExperience = updateDto.YearsExperience;
        profile.ServiceRadiusKm = updateDto.ServiceRadiusKm;

        return await _context.SaveChangesAsync() > 0;
    }

    public async Task<bool> AddDocumentAsync(int userId, UploadDocumentDto uploadDto)
    {
        var profile = await _context.NurseProfiles.FirstOrDefaultAsync(np => np.UserId == userId);
        if (profile == null)
        {
            return false;
        }

        var document = new Document
        {
            NurseProfileId = profile.Id,
            Type = uploadDto.Type,
            FileUrl = uploadDto.FileUrl,
            Status = "pending_review",
            CreatedAt = DateTime.UtcNow
        };

        _context.Documents.Add(document);
        return await _context.SaveChangesAsync() > 0;
    }
}
