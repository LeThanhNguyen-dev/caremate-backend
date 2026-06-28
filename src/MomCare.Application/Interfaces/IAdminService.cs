using MomCare.Dto;

namespace MomCare.Interfaces;

public interface IAdminService
{
    Task<IEnumerable<AdminUserDto>> GetUsersAsync();
    Task<AdminUserDto?> CreateUserAsync(CreateAdminUserDto dto);
    Task<AdminUserDto?> UpdateUserStatusAsync(int userId, UpdateAdminUserStatusDto dto);
    Task<IEnumerable<NurseProfileDetailDto>> GetPendingNursesAsync();
    Task<NurseProfileDetailDto?> GetNurseDetailsAsync(int userId);
    Task<bool> ReviewNurseAsync(int userId, ReviewNurseProfileDto reviewDto);
    Task<AdminDashboardDto> GetDashboardAsync();
    Task<IEnumerable<AdminBookingSummaryDto>> GetBookingsAsync(string? status);
    Task<IEnumerable<DisputeDto>> GetDisputesAsync(string? status);
    Task<IEnumerable<AdminRefundDto>> GetRefundsAsync(string? refundStatus);
    Task<bool> CompleteRefundAsync(int bookingId, CompleteRefundDto dto);
    Task<IEnumerable<AdminPayoutDto>> GetPayoutsAsync(string? payoutStatus);
    Task<bool> CompletePayoutAsync(int payoutId, CompletePayoutDto dto);
    Task<IEnumerable<PayOsWebhookLogDto>> GetPayOsWebhookLogsAsync(string? status);
    Task<IEnumerable<TransactionHistoryItemDto>> GetTransactionHistoryAsync(string? type, string? status, int? userId, int? bookingId, DateTime? from, DateTime? to);
    Task<AdminFinanceAnalyticsDto> GetFinanceAnalyticsAsync(DateTime? from, DateTime? to);
    Task<IEnumerable<AuditLogDto>> GetAuditLogsAsync(int? actorUserId, string? path, DateTime? from, DateTime? to);
    Task<AdminOcrSettingsDto> GetOcrSettingsAsync();
    Task<CccdOcrResultDto?> OcrNurseDocumentAsync(int documentId, CancellationToken cancellationToken);
    Task<IEnumerable<NurseDocumentOcrLogDto>> GetNurseOcrLogsAsync(int nurseUserId);
    Task<bool> UpdateNurseDocumentStatusAsync(int nurseUserId, int documentId, string status, ReviewNurseDocumentDto dto);
    Task<bool> DeleteNurseDocumentAsync(int nurseUserId, int documentId);
}
