using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MomCare.Enums;
using MomCare.Interfaces;
using MomCare.Models;

namespace MomCare.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = AppRoles.Admin)]
public class AdminController : ControllerBase
{
    private readonly IUnitOfWork _unitOfWork;

    public AdminController(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    [HttpPost("nurses/{id}/confirm")]
    public async Task<IActionResult> ConfirmNurse(int id)
    {
        var user = await _unitOfWork.Users.FindAsync(u => u.Id == id, "UserRoles.Role");
        if (user == null) return NotFound("User not found");

        var nurseProfile = await _unitOfWork.NurseProfiles.FindAsync(np => np.UserId == id);
        if (nurseProfile == null) return NotFound("Nurse profile not found");

        // Update Role: Replace NurseUnconfirmed with NurseConfirmed
        var unconfirmedRole = await _unitOfWork.Roles.FindAsync(r => r.Code == AppRoles.NurseUnconfirmed);
        var confirmedRole = await _unitOfWork.Roles.FindAsync(r => r.Code == AppRoles.NurseConfirmed);
        
        // Ensure confirmed role exists
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
            await _unitOfWork.CompleteAsync(); 
            // Save remove first or just swap? EF Core tracking might handle swap if we add new one.
            // Better to remove and save, then add.
        }
        
        // Check if already confirmed to avoid duplicate
        if (!user.UserRoles.Any(ur => ur.Role.Code == AppRoles.NurseConfirmed))
        {
             await _unitOfWork.UserRoles.AddAsync(new UserRole { UserId = user.Id, RoleId = confirmedRole.Id });
        }

        // Update Profile
        nurseProfile.IsVerified = "verified";
        nurseProfile.ConfirmedAt = DateTime.UtcNow;
        _unitOfWork.NurseProfiles.Update(nurseProfile);

        await _unitOfWork.CompleteAsync();

        return Ok(new { message = "Nurse confirmed successfully" });
    }

    [HttpPost("nurses/{id}/unconfirm")]
    public async Task<IActionResult> UnconfirmNurse(int id)
    {
        var user = await _unitOfWork.Users.FindAsync(u => u.Id == id, "UserRoles.Role");
        if (user == null) return NotFound("User not found");

        var nurseProfile = await _unitOfWork.NurseProfiles.FindAsync(np => np.UserId == id);
        if (nurseProfile == null) return NotFound("Nurse profile not found");

        var confirmedRole = await _unitOfWork.Roles.FindAsync(r => r.Code == AppRoles.NurseConfirmed);
        var unconfirmedRole = await _unitOfWork.Roles.FindAsync(r => r.Code == AppRoles.NurseUnconfirmed);

         if (unconfirmedRole == null)
        {
             unconfirmedRole = new Role { Code = AppRoles.NurseUnconfirmed, Name = "Nurse (Unconfirmed)" };
             await _unitOfWork.Roles.AddAsync(unconfirmedRole);
             await _unitOfWork.CompleteAsync();
        }

        var userRoleEntry = user.UserRoles.FirstOrDefault(ur => ur.Role.Code == AppRoles.NurseConfirmed);
        
        if (userRoleEntry != null)
        {
            _unitOfWork.UserRoles.Remove(userRoleEntry);
            await _unitOfWork.CompleteAsync();
        }

        if (!user.UserRoles.Any(ur => ur.Role.Code == AppRoles.NurseUnconfirmed))
        {
             await _unitOfWork.UserRoles.AddAsync(new UserRole { UserId = user.Id, RoleId = unconfirmedRole.Id });
        }

        // Update Profile
        nurseProfile.IsVerified = "unverified"; // or pending?
        nurseProfile.ConfirmedAt = null;
        _unitOfWork.NurseProfiles.Update(nurseProfile);

        await _unitOfWork.CompleteAsync();

        return Ok(new { message = "Nurse unconfirmed successfully" });
    }
}
