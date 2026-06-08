using Microsoft.AspNetCore.Http;
using MomCare.Dto;

namespace MomCare.Interfaces;

public interface ICccdOcrService
{
    Task<CccdOcrResultDto> ExtractAsync(string documentType, IFormFile file, CancellationToken cancellationToken);
}
