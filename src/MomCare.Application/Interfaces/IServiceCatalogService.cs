using MomCare.Dto;

namespace MomCare.Interfaces;

public interface IServiceCatalogService
{
    Task<IEnumerable<ServiceDetailDto>> BrowseAsync(bool? isActive, string? search);
    Task<ServiceDetailDto?> GetByIdAsync(int id);
    Task<ServiceDetailDto> CreateAsync(UpsertServiceDto dto);
    Task<bool> UpdateAsync(int id, UpsertServiceDto dto);
    Task<bool> DeleteAsync(int id);
}
