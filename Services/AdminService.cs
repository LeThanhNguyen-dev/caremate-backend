using MomCare.Dto;
using MomCare.Enums;
using MomCare.Interfaces;
using MomCare.Models;

namespace MomCare.Services;

public class AdminService : IAdminService
{
    private readonly IUnitOfWork _unitOfWork;

    public AdminService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<IEnumerable<NurseProfileDetailDto>> GetPendingNursesAsync()
    {
        // Find users with NurseUnconfirmed role
        var users = await _unitOfWork.Users.FindAllAsync(
            u => u.UserRoles.Any(ur => ur.Role.Code == AppRoles.NurseUnconfirmed),
            "UserRoles.Role"
        );

        var pendingList = new List<NurseProfileDetailDto>();

        foreach (var user in users)
        {
            var profile = await _unitOfWork.NurseProfiles.FindAsync(
                np => np.UserId == user.Id,
                "Documents"
            );

            if (profile != null)
            {
                pendingList.Add(new NurseProfileDetailDto
                {
                    UserId = user.Id,
                    FullName = user.FullName,
                    Email = user.Email ?? "",
                    Phone = user.Phone,
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
                });
            }
        }

        return pendingList;
    }

    public async Task<NurseProfileDetailDto?> GetNurseDetailsAsync(int userId)
    {
        var user = await _unitOfWork.Users.FindAsync(u => u.Id == userId, "UserRoles.Role");
        if (user == null) return null;

        var profile = await _unitOfWork.NurseProfiles.FindAsync(
            np => np.UserId == userId,
            "Documents"
        );

        if (profile == null) return null;

        return new NurseProfileDetailDto
        {
            UserId = user.Id,
            FullName = user.FullName,
            Email = user.Email ?? "",
            Phone = user.Phone,
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

    public async Task<bool> ReviewNurseAsync(int userId, ReviewNurseProfileDto reviewDto)
    {
        var user = await _unitOfWork.Users.FindAsync(u => u.Id == userId, "UserRoles.Role");
        if (user == null) return false;

        var profile = await _unitOfWork.NurseProfiles.FindAsync(
            np => np.UserId == userId,
            "Documents"
        );
        if (profile == null) return false;

        if (reviewDto.IsApproved)
        {
            // Update Role: nurse_unconfirmed -> nurse_confirmed
            var unconfirmedRole = await _unitOfWork.Roles.FindAsync(r => r.Code == AppRoles.NurseUnconfirmed);
            var confirmedRole = await _unitOfWork.Roles.FindAsync(r => r.Code == AppRoles.NurseConfirmed);

            if (confirmedRole == null)
            {
                confirmedRole = new Role { Code = AppRoles.NurseConfirmed, Name = "Nurse (Confirmed)" };
                await _unitOfWork.Roles.AddAsync(confirmedRole);
                await _unitOfWork.CompleteAsync();
            }

            var userRoleEntry = user.UserRoles.FirstOrDefault(ur => ur.Role.Code == AppRoles.NurseUnconfirmed);
            if (userRoleEntry != null)
            {
                _unitOfWork.UserRoles.Remove(userRoleEntry);
            }

            if (!user.UserRoles.Any(ur => ur.Role.Code == AppRoles.NurseConfirmed))
            {
                await _unitOfWork.UserRoles.AddAsync(new UserRole { UserId = user.Id, RoleId = confirmedRole.Id });
            }

            profile.IsVerified = "verified";
            profile.ConfirmedAt = DateTime.UtcNow;

            // Approve all documents
            foreach (var doc in profile.Documents)
            {
                doc.Status = "approved";
                _unitOfWork.Documents.Update(doc);
            }
        }
        else
        {
            profile.IsVerified = "rejected";
            profile.ConfirmedAt = null;

            // Reject all documents that were pending
            foreach (var doc in profile.Documents.Where(d => d.Status == "pending_review"))
            {
                doc.Status = "rejected";
                _unitOfWork.Documents.Update(doc);
            }
        }

        _unitOfWork.NurseProfiles.Update(profile);
        return await _unitOfWork.CompleteAsync() > 0;
    }
}
