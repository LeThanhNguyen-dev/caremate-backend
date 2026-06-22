using Microsoft.EntityFrameworkCore;
using MomCare.Data;
using MomCare.Dto;
using MomCare.Interfaces;
using MomCare.Models;
using System.Text.Json;

namespace MomCare.Services;

public class ServiceCatalogService : IServiceCatalogService
{
    private readonly MomCareContext _context;

    public ServiceCatalogService(MomCareContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<ServiceDetailDto>> BrowseAsync(bool? isActive, string? search, string? language = null)
    {
        var query = _context.Services.AsQueryable();

        if (isActive.HasValue)
        {
            var status = isActive.Value ? "active" : "inactive";
            query = query.Where(s => s.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var keyword = search.Trim();
            query = query.Where(s => s.Name.Contains(keyword) || (s.NameEn != null && s.NameEn.Contains(keyword)));
        }

        var services = await query
            .OrderBy(s => s.Name)
            .ToListAsync();

        return services.Select(s => MapToDto(s, language));
    }

    public async Task<ServiceDetailDto?> GetByIdAsync(int id, string? language = null)
    {
        var service = await _context.Services.FirstOrDefaultAsync(s => s.Id == id);
        return service == null ? null : MapToDto(service, language);
    }

    public async Task<ServiceDetailDto> CreateAsync(UpsertServiceDto dto)
    {
        var service = new Service
        {
            Name = dto.Name,
            Category = dto.Category,
            Description = dto.Description,
            NameEn = dto.NameEn,
            DescriptionEn = dto.DescriptionEn,
            BasePrice = dto.BasePrice,
            EstimatedDurationMinutes = dto.EstimatedDurationMinutes,
            ServiceKind = dto.ServiceKind,
            PackageDays = dto.PackageDays,
            IncludedServiceKeys = dto.IncludedServiceKeys,
            PackageScheduleJson = dto.PackageScheduleJson,
            Status = dto.Status,
            CreatedAt = DateTime.UtcNow
        };

        _context.Services.Add(service);
        await _context.SaveChangesAsync();

        return MapToDto(service);
    }

    public async Task<bool> UpdateAsync(int id, UpsertServiceDto dto)
    {
        var service = await _context.Services.FirstOrDefaultAsync(s => s.Id == id);
        if (service == null)
        {
            return false;
        }

        service.Name = dto.Name;
        service.Category = dto.Category;
        service.Description = dto.Description;
        service.NameEn = dto.NameEn;
        service.DescriptionEn = dto.DescriptionEn;
        service.BasePrice = dto.BasePrice;
        service.EstimatedDurationMinutes = dto.EstimatedDurationMinutes;
        service.ServiceKind = dto.ServiceKind;
        service.PackageDays = dto.PackageDays;
        service.IncludedServiceKeys = dto.IncludedServiceKeys;
        if (dto.PackageScheduleJson != null)
        {
            service.PackageScheduleJson = dto.PackageScheduleJson;
        }
        service.Status = dto.Status;

        return await _context.SaveChangesAsync() > 0;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var service = await _context.Services.FirstOrDefaultAsync(s => s.Id == id);
        if (service == null)
        {
            return false;
        }

        // Soft delete: set status to inactive instead of removing
        // This prevents FK constraint violations from existing bookings
        service.Status = "inactive";
        return await _context.SaveChangesAsync() > 0;
    }

    private static ServiceDetailDto MapToDto(Service service, string? language = null)
    {
        bool isEn = language?.StartsWith("en", StringComparison.OrdinalIgnoreCase) == true;
        return new ServiceDetailDto
        {
            Id = service.Id,
            Name = isEn && !string.IsNullOrWhiteSpace(service.NameEn) ? service.NameEn : service.Name,
            Category = service.Category,
            Description = isEn && !string.IsNullOrWhiteSpace(service.DescriptionEn) ? service.DescriptionEn : service.Description,
            NameEn = service.NameEn,
            DescriptionEn = service.DescriptionEn,
            BasePrice = service.BasePrice,
            EstimatedDurationMinutes = service.EstimatedDurationMinutes,
            ServiceKind = service.ServiceKind,
            PackageDays = service.PackageDays,
            IncludedServiceKeys = service.IncludedServiceKeys,
            PackageSchedule = ParsePackageSchedule(service.PackageScheduleJson),
            Status = service.Status
        };
    }

    private static List<PackageScheduleEntryDto> ParsePackageSchedule(string? scheduleJson)
    {
        if (string.IsNullOrWhiteSpace(scheduleJson))
        {
            return new List<PackageScheduleEntryDto>();
        }

        try
        {
            return JsonSerializer.Deserialize<List<PackageScheduleEntryDto>>(
                scheduleJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<PackageScheduleEntryDto>();
        }
        catch
        {
            return new List<PackageScheduleEntryDto>();
        }
    }
}
