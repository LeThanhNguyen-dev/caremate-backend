using Microsoft.Extensions.Logging;
using MomCare.Data;
using MomCare.Models;

namespace MomCare.Services;

/// <summary>
/// Persists Gemini call telemetry without affecting the user-facing flow.
/// </summary>
public class GeminiCallLogService
{
    private readonly MomCareContext _context;
    private readonly ILogger<GeminiCallLogService> _logger;

    public GeminiCallLogService(MomCareContext context, ILogger<GeminiCallLogService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Saves a Gemini call log and swallows persistence errors.
    /// </summary>
    public async Task SaveAsync(GeminiCallLog log, CancellationToken cancellationToken)
    {
        try
        {
            _context.GeminiCallLogs.Add(log);
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save Gemini call log.");
        }
    }
}
