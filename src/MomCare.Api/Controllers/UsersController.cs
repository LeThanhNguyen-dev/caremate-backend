using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MomCare.Data;
using MomCare.Dto;
using MomCare.Models;

namespace MomCare.Controllers;

[ApiController]
[Route("api/users")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly MomCareContext _context;

    public UsersController(UserManager<ApplicationUser> userManager, MomCareContext context)
    {
        _userManager = userManager;
        _context = context;
    }

    [HttpGet("me/profile")]
    public async Task<IActionResult> GetMyProfile()
    {
        var user = await _userManager.FindByIdAsync(GetUserId().ToString());
        if (user == null)
        {
            return NotFound();
        }

        return Ok(new
        {
            userId = user.Id,
            fullName = user.FullName,
            email = user.Email,
            phone = user.PhoneNumber,
            phoneNumber = user.PhoneNumber,
            address = await GetDefaultAddressAsync(user.Id),
            avatar = user.Avatar,
            bankBin = user.BankBin,
            bankAccountNumber = user.BankAccountNumber,
            bankAccountName = user.BankAccountName,
            status = user.Status
        });
    }

    [HttpPut("me/profile")]
    public async Task<IActionResult> UpdateMyProfile([FromBody] UpdateMyProfileDto dto)
    {
        var user = await _userManager.FindByIdAsync(GetUserId().ToString());
        if (user == null)
        {
            return NotFound();
        }

        user.FullName = dto.FullName;
        user.PhoneNumber = string.IsNullOrWhiteSpace(dto.PhoneNumber) ? null : dto.PhoneNumber.Trim();
        user.Avatar = string.IsNullOrWhiteSpace(dto.Avatar) ? null : dto.Avatar.Trim();
        user.BankBin = string.IsNullOrWhiteSpace(dto.BankBin) ? null : dto.BankBin.Trim();
        user.BankAccountNumber = string.IsNullOrWhiteSpace(dto.BankAccountNumber) ? null : dto.BankAccountNumber.Trim();
        user.BankAccountName = string.IsNullOrWhiteSpace(dto.BankAccountName) ? null : dto.BankAccountName.Trim();
        user.UpdatedAt = DateTime.UtcNow;

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            return BadRequest(new { message = "Profile update failed" });
        }

        await UpsertDefaultAddressAsync(user.Id, dto.Address);

        return Ok(new { message = "Profile updated successfully" });
    }

    private async Task<string?> GetDefaultAddressAsync(int userId)
    {
        return await _context.Addresses
            .Where(a => a.UserId == userId && a.Type == "customer_home" && a.IsDefault)
            .Select(a => a.FullAddress)
            .FirstOrDefaultAsync();
    }

    private async Task UpsertDefaultAddressAsync(int userId, string? fullAddress)
    {
        var address = await _context.Addresses
            .FirstOrDefaultAsync(a => a.UserId == userId && a.Type == "customer_home" && a.IsDefault);

        if (string.IsNullOrWhiteSpace(fullAddress))
        {
            if (address != null)
            {
                _context.Addresses.Remove(address);
                await _context.SaveChangesAsync();
            }

            return;
        }

        if (address == null)
        {
            _context.Addresses.Add(new Address
            {
                UserId = userId,
                FullAddress = fullAddress.Trim(),
                Type = "customer_home",
                IsDefault = true
            });
        }
        else
        {
            address.FullAddress = fullAddress.Trim();
        }

        await _context.SaveChangesAsync();
    }

    private int GetUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub);
        return int.Parse(raw ?? "0");
    }
}
