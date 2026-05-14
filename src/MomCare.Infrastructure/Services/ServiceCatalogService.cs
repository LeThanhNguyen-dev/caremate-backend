using Microsoft.EntityFrameworkCore;
using MomCare.Data;
using MomCare.Dto;
using MomCare.Interfaces;
using MomCare.Models;

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

        return await query
            .OrderBy(s => s.Name)
            .Select(s => new ServiceDetailDto
            {
                Id = s.Id,
                Name = s.Name,
                Category = s.Category,
                Description = s.Description,
                BasePrice = s.BasePrice,
                EstimatedDurationMinutes = s.EstimatedDurationMinutes,
                Status = s.Status
            })
            .ToListAsync();
    }

    public async Task<ServiceDetailDto?> GetByIdAsync(int id)
    {
        return await _context.Services
            .Where(s => s.Id == id)
            .Select(s => new ServiceDetailDto
            {
                Id = s.Id,
                Name = s.Name,
                Category = s.Category,
                Description = s.Description,
                BasePrice = s.BasePrice,
                EstimatedDurationMinutes = s.EstimatedDurationMinutes,
                Status = s.Status
            })
            .FirstOrDefaultAsync();
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
            Status = dto.Status,
            CreatedAt = DateTime.UtcNow
        };

        _context.Services.Add(service);
        await _context.SaveChangesAsync();

        return new ServiceDetailDto
        {
            Id = service.Id,
            Name = service.Name,
            Category = service.Category,
            Description = service.Description,
            BasePrice = service.BasePrice,
            EstimatedDurationMinutes = service.EstimatedDurationMinutes,
            Status = service.Status
        };
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

        _context.Services.Remove(service);
        return await _context.SaveChangesAsync() > 0;
    }
}
