using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MomCare.Data;
using MomCare.Dto;
using MomCare.Enums;
using MomCare.Interfaces;
using MomCare.Models;

namespace MomCare.Services;

public class AdminService : IAdminService
{
    private readonly MomCareContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;

    public AdminService(
        MomCareContext context,
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager)
    {
        _context = context;
        _userManager = userManager;
        _roleManager = roleManager;
    }

    public async Task<IEnumerable<NurseProfileDetailDto>> GetPendingNursesAsync()
    {
        var users = await _userManager.GetUsersInRoleAsync(AppRoles.NurseUnconfirmed);
        var userIds = users.Select(u => u.Id).ToList();

        var profiles = await _context.NurseProfiles
            .Include(np => np.Documents)
            .Where(np => userIds.Contains(np.UserId))
            .ToListAsync();

        var userMap = users.ToDictionary(u => u.Id, u => u);

        return profiles.Select(profile =>
        {
            var user = userMap[profile.UserId];
            return new NurseProfileDetailDto
            {
                UserId = user.Id,
                FullName = user.FullName,
                Email = user.Email ?? string.Empty,
                Phone = user.PhoneNumber,
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
        }).ToList();
    }

    public async Task<NurseProfileDetailDto?> GetNurseDetailsAsync(int userId)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
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
            UserId = user.Id,
            FullName = user.FullName,
            Email = user.Email ?? string.Empty,
            Phone = user.PhoneNumber,
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
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return false;
        }

        var profile = await _context.NurseProfiles
            .Include(np => np.Documents)
            .FirstOrDefaultAsync(np => np.UserId == userId);

        if (profile == null)
        {
            return false;
        }

        if (reviewDto.IsApproved)
        {
            await EnsureRoleExistsAsync(AppRoles.NurseConfirmed, "Nurse (Confirmed)");

            if (await _userManager.IsInRoleAsync(user, AppRoles.NurseUnconfirmed))
            {
                await _userManager.RemoveFromRoleAsync(user, AppRoles.NurseUnconfirmed);
            }

            if (!await _userManager.IsInRoleAsync(user, AppRoles.NurseConfirmed))
            {
                await _userManager.AddToRoleAsync(user, AppRoles.NurseConfirmed);
            }

            profile.IsVerified = "verified";
            profile.ConfirmedAt = DateTime.UtcNow;

            foreach (var doc in profile.Documents)
            {
                doc.Status = "approved";
            }
        }
        else
        {
            profile.IsVerified = "rejected";
            profile.ConfirmedAt = null;

            foreach (var doc in profile.Documents.Where(d => d.Status == "pending_review"))
            {
                doc.Status = "rejected";
            }
        }

        return await _context.SaveChangesAsync() > 0;
    }

    private async Task EnsureRoleExistsAsync(string roleCode, string displayName)
    {
        var normalizedRoleCode = _roleManager.NormalizeKey(roleCode);
        var role = await _roleManager.FindByNameAsync(roleCode);
        role ??= await _roleManager.Roles.FirstOrDefaultAsync(r => r.Name == roleCode);

        if (role == null)
        {
            var createResult = await _roleManager.CreateAsync(new ApplicationRole
            {
                Name = roleCode,
                DisplayName = displayName
            });

            if (createResult.Succeeded)
            {
                return;
            }

            role = await _roleManager.Roles.FirstOrDefaultAsync(r => r.Name == roleCode);
            if (role == null)
            {
                throw new InvalidOperationException(
                    $"Unable to create role '{roleCode}': {string.Join(", ", createResult.Errors.Select(e => e.Description))}");
            }
        }

        var changed = false;
        if (role.DisplayName != displayName)
        {
            role.DisplayName = displayName;
            changed = true;
        }

        if (role.NormalizedName != normalizedRoleCode)
        {
            role.NormalizedName = normalizedRoleCode;
            changed = true;
        }

        if (changed)
        {
            var updateResult = await _roleManager.UpdateAsync(role);
            if (!updateResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Unable to update role '{roleCode}': {string.Join(", ", updateResult.Errors.Select(e => e.Description))}");
            }
        }
    }

    public async Task<AdminDashboardDto> GetDashboardAsync()
    {
        var totalUsers = await _userManager.Users.CountAsync();
        var totalNurses = await _context.NurseProfiles.CountAsync();
        var pendingApprovals = (await _userManager.GetUsersInRoleAsync(AppRoles.NurseUnconfirmed)).Count;
        var openDisputes = await _context.Disputes.CountAsync(d => d.Status == "open");
        var pendingBookings = await _context.Bookings.CountAsync(b => b.Status == "pending_confirm");

        return new AdminDashboardDto
        {
            TotalUsers = totalUsers,
            TotalNurses = totalNurses,
            PendingNurseApprovals = pendingApprovals,
            OpenDisputes = openDisputes,
            PendingBookings = pendingBookings
        };
    }

    public async Task<IEnumerable<AdminBookingSummaryDto>> GetBookingsAsync(string? status)
    {
        var query = _context.Bookings.AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalized = status.Trim().ToLowerInvariant();
            query = query.Where(b => b.Status == normalized);
        }

        return await query
            .OrderByDescending(b => b.CreatedAt)
            .Select(b => new AdminBookingSummaryDto
            {
                Id = b.Id,
                CustomerId = b.CustomerId,
                NurseId = b.NurseId,
                Status = b.Status,
                TotalPrice = b.TotalPrice,
                StartTime = b.StartTime,
                EndTime = b.EndTime
            })
            .ToListAsync();
    }

    public async Task<IEnumerable<Dispute>> GetDisputesAsync(string? status)
    {
        var query = _context.Disputes.AsQueryable();
        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalized = status.Trim().ToLowerInvariant();
            query = query.Where(d => d.Status == normalized);
        }

        return await query
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync();
    }
}
