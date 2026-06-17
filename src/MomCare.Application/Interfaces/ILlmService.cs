using MomCare.Dto;

namespace MomCare.Interfaces;

public interface ILlmService
{
    Task<GeminiGenerateResponse> GenerateAsync(GeminiGenerateRequest request, CancellationToken cancellationToken);
}
