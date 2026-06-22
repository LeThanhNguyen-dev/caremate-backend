using Microsoft.EntityFrameworkCore;
using MomCare.Data;
using MomCare.Dto;
using MomCare.Interfaces;
using MomCare.Models;
using NurseServiceModel = MomCare.Models.NurseService;

namespace MomCare.Services;

public class NurseServiceManagementService : INurseServiceManagementService
{
    private readonly MomCareContext _context;

    public NurseServiceManagementService(MomCareContext context)
    {
        _context = context;
    }

    public async Task<NurseServiceDto?> AddServiceAsync(int nurseUserId, CreateNurseServiceDto dto, string? language = null)
    {
        // Verify nurse profile exists and is verified
        var nurseProfile = await _context.NurseProfiles
            .FirstOrDefaultAsync(np => np.UserId == nurseUserId && np.IsActive);

        if (nurseProfile == null)
        {
            return null;
        }

        // Check if service exists
        var service = await _context.Services
            .FirstOrDefaultAsync(s => s.Id == dto.ServiceId && s.Status == "active");

        if (service == null)
        {
            return null;
        }

        // Check if nurse already offers this service
        var existing = await _context.NurseServices
            .FirstOrDefaultAsync(ns => ns.NurseProfileId == nurseProfile.Id && ns.ServiceId == dto.ServiceId);

        if (existing != null)
        {
            return null; // Already offering this service
        }

        var nurseService = new NurseServiceModel
        {
            NurseProfileId = nurseProfile.Id,
            ServiceId = dto.ServiceId,
            Price = dto.Price,
            Unit = dto.Unit,
            Status = "enabled"
        };

        _context.NurseServices.Add(nurseService);
        await _context.SaveChangesAsync();

        return ToNurseServiceDto(nurseService, service, language);
    }

    public async Task<IEnumerable<NurseServiceDto>> GetMyServicesAsync(int nurseUserId, string? language = null)
    {
        var nurseProfile = await _context.NurseProfiles
            .FirstOrDefaultAsync(np => np.UserId == nurseUserId);

        if (nurseProfile == null)
        {
            return Enumerable.Empty<NurseServiceDto>();
        }

        var nurseServices = await _context.NurseServices
            .Include(ns => ns.Service)
            .Where(ns => ns.NurseProfileId == nurseProfile.Id)
            .ToListAsync();

        return nurseServices.Select(ns => ToNurseServiceDto(ns, ns.Service, language));
    }

    public async Task<NurseServiceDto?> UpdateServiceAsync(int nurseUserId, int serviceId, UpdateNurseServiceDto dto, string? language = null)
    {
        var nurseProfile = await _context.NurseProfiles
            .FirstOrDefaultAsync(np => np.UserId == nurseUserId);

        if (nurseProfile == null)
        {
            return null;
        }

        var nurseService = await _context.NurseServices
            .Include(ns => ns.Service)
            .FirstOrDefaultAsync(ns => ns.Id == serviceId && ns.NurseProfileId == nurseProfile.Id);

        if (nurseService == null)
        {
            return null;
        }

        nurseService.Price = dto.Price;
        nurseService.Unit = dto.Unit;
        if (!string.IsNullOrWhiteSpace(dto.Status))
        {
            nurseService.Status = dto.Status.Trim().ToLowerInvariant() switch
            {
                "enabled" => "enabled",
                "disabled" => "disabled",
                _ => nurseService.Status
            };
        }

        await _context.SaveChangesAsync();

        return ToNurseServiceDto(nurseService, nurseService.Service, language);
    }

    public async Task<bool> RemoveServiceAsync(int nurseUserId, int serviceId)
    {
        var nurseProfile = await _context.NurseProfiles
            .FirstOrDefaultAsync(np => np.UserId == nurseUserId);

        if (nurseProfile == null)
        {
            return false;
        }

        var nurseService = await _context.NurseServices
            .FirstOrDefaultAsync(ns => ns.Id == serviceId && ns.NurseProfileId == nurseProfile.Id);

        if (nurseService == null)
        {
            return false;
        }

        _context.NurseServices.Remove(nurseService);
        await _context.SaveChangesAsync();

        return true;
    }

    private static NurseServiceDto ToNurseServiceDto(NurseServiceModel nurseService, Service service, string? language = null)
    {
        bool isEn = language?.StartsWith("en", StringComparison.OrdinalIgnoreCase) == true;
        return new NurseServiceDto
        {
            Id = nurseService.Id,
            NurseProfileId = nurseService.NurseProfileId,
            ServiceId = nurseService.ServiceId,
            ServiceName = isEn && !string.IsNullOrWhiteSpace(service.NameEn) ? service.NameEn : service.Name,
            Price = nurseService.Price,
            Unit = nurseService.Unit,
            Status = nurseService.Status,
            CreatedAt = DateTime.UtcNow
        };
    }
}
