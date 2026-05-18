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

    public async Task<IEnumerable<ServiceDetailDto>> BrowseAsync(bool? isActive, string? search)
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
            query = query.Where(s => s.Name.Contains(keyword));
        }

        var services = await query
            .OrderBy(s => s.Name)
            .ToListAsync();

        return services.Select(MapToDto);
    }

    public async Task<ServiceDetailDto?> GetByIdAsync(int id)
    {
        var service = await _context.Services.FirstOrDefaultAsync(s => s.Id == id);
        return service == null ? null : MapToDto(service);
    }

    public async Task<ServiceDetailDto> CreateAsync(UpsertServiceDto dto)
    {
        var service = new Service
        {
            Name = dto.Name,
            Category = dto.Category,
            Description = dto.Description,
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

    private static ServiceDetailDto MapToDto(Service service)
    {
        return new ServiceDetailDto
        {
            Id = service.Id,
            Name = service.Name,
            Category = service.Category,
            Description = service.Description,
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
