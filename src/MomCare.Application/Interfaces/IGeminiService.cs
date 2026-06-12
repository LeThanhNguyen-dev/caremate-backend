using MomCare.Dto;

namespace MomCare.Interfaces;

public interface IGeminiService
{
    Task<GeminiGenerateResponse> GenerateAsync(GeminiGenerateRequest request, CancellationToken cancellationToken);
}
