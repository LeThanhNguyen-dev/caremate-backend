using MomCare.Dto;

namespace MomCare.Interfaces;

public interface IServiceCatalogService
{
    Task<IEnumerable<ServiceDetailDto>> BrowseAsync(bool? isActive, string? search, string? language = null);
    Task<ServiceDetailDto?> GetByIdAsync(int id, string? language = null);
    Task<ServiceDetailDto> CreateAsync(UpsertServiceDto dto);
    Task<bool> UpdateAsync(int id, UpsertServiceDto dto);
    Task<bool> DeleteAsync(int id);
}
