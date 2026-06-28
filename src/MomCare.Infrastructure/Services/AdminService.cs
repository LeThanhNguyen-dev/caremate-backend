using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Text.Json;
using MomCare.Data;
using MomCare.Dto;
using MomCare.Enums;
using MomCare.Infrastructure.Configurations;
using MomCare.Interfaces;
using MomCare.Models;

namespace MomCare.Services;

public class AdminService : IAdminService
{
    private readonly MomCareContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly ICloudinaryService _cloudinaryService;
    private readonly IConfiguration _configuration;
    private readonly ICccdOcrService _cccdOcrService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly INotificationService _notificationService;
    private const decimal PlatformFeeRate = 0.15m;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public AdminService(
        MomCareContext context,
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        ICloudinaryService cloudinaryService,
        IConfiguration configuration,
        ICccdOcrService cccdOcrService,
        IHttpClientFactory httpClientFactory,
        INotificationService notificationService)
    {
        _context = context;
        _userManager = userManager;
        _roleManager = roleManager;
        _cloudinaryService = cloudinaryService;
        _configuration = configuration;
        _cccdOcrService = cccdOcrService;
        _httpClientFactory = httpClientFactory;
        _notificationService = notificationService;
    }

    public async Task<IEnumerable<AdminUserDto>> GetUsersAsync()
    {
        var users = await _userManager.Users
            .AsNoTracking()
            .OrderByDescending(u => u.CreatedAt)
            .ToListAsync();

        var userIds = users.Select(u => u.Id).ToList();

        var rolesByUserId = await (
            from userRole in _context.Set<ApplicationUserRole>().AsNoTracking()
            join role in _roleManager.Roles.AsNoTracking() on userRole.RoleId equals role.Id
            where userIds.Contains(userRole.UserId)
            select new
            {
                userRole.UserId,
                RoleCode = role.Name
            })
            .ToListAsync();

        var adminUserIds = rolesByUserId
            .Where(x => x.RoleCode == AppRoles.Admin)
            .Select(x => x.UserId)
            .ToHashSet();

        users = users
            .Where(u => !adminUserIds.Contains(u.Id))
            .ToList();

        userIds = users.Select(u => u.Id).ToList();

        var primaryRoleByUserId = rolesByUserId
            .GroupBy(x => x.UserId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(x => x.RoleCode)
                    .OrderByDescending(GetRolePriority)
                    .FirstOrDefault() ?? AppRoles.Customer);

        var nurseProfilesByUserId = await _context.NurseProfiles
            .AsNoTracking()
            .Where(np => userIds.Contains(np.UserId))
            .ToDictionaryAsync(np => np.UserId);

        var bookingCountsByUserId = await _context.Bookings
            .AsNoTracking()
            .Where(b => userIds.Contains(b.CustomerId) || userIds.Contains(b.NurseId))
            .GroupBy(b => b.CustomerId)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToListAsync();

        var nurseBookingCountsByUserId = await _context.Bookings
            .AsNoTracking()
            .Where(b => userIds.Contains(b.NurseId))
            .GroupBy(b => b.NurseId)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToListAsync();

        var combinedBookingCounts = bookingCountsByUserId
            .Concat(nurseBookingCountsByUserId)
            .GroupBy(x => x.UserId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Count));

        return users.Select(user =>
        {
            nurseProfilesByUserId.TryGetValue(user.Id, out var nurseProfile);
            primaryRoleByUserId.TryGetValue(user.Id, out var role);
            combinedBookingCounts.TryGetValue(user.Id, out var bookingCount);

            return new AdminUserDto
            {
                UserId = user.Id,
                FullName = user.FullName,
                Email = user.Email,
                Phone = user.PhoneNumber,
                Role = role ?? AppRoles.Customer,
                Status = user.Status,
                AverageRating = nurseProfile?.AverageRating,
                YearsExperience = nurseProfile?.YearsExperience,
                IsVerified = nurseProfile?.IsVerified,
                BookingCount = bookingCount,
                Bio = nurseProfile?.Bio
            };
        }).ToList();
    }

    public async Task<AdminUserDto?> CreateUserAsync(CreateAdminUserDto dto)
    {
        var roleCode = NormalizeManagedUserRole(dto.Role);
        if (roleCode == null)
        {
            return null;
        }

        var normalizedEmail = dto.Email.Trim();
        if (await _userManager.FindByEmailAsync(normalizedEmail) != null)
        {
            return null;
        }

        var normalizedPhone = NormalizePhone(dto.Phone);
        if (await IsPhoneTakenAsync(normalizedPhone))
        {
            return null;
        }

        var user = new ApplicationUser
        {
            FullName = dto.FullName.Trim(),
            Email = normalizedEmail,
            UserName = normalizedEmail,
            PhoneNumber = normalizedPhone,
            Status = "active",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            EmailConfirmed = true
        };

        var createResult = await _userManager.CreateAsync(user, dto.Password);
        if (!createResult.Succeeded)
        {
            return null;
        }

        await EnsureRoleExistsAsync(roleCode, GetRoleDisplayName(roleCode));
        var roleResult = await _userManager.AddToRoleAsync(user, roleCode);
        if (!roleResult.Succeeded)
        {
            await _userManager.DeleteAsync(user);
            return null;
        }

        if (roleCode == AppRoles.NurseUnconfirmed)
        {
            _context.NurseProfiles.Add(new NurseProfile
            {
                UserId = user.Id,
                YearsExperience = 0,
                ServiceRadiusKm = 0,
                IsVerified = "unverified",
                VerificationSubmissionStatus = "draft"
            });
            try
            {
                await _context.SaveChangesAsync();
            }
            catch
            {
                await _userManager.DeleteAsync(user);
                return null;
            }
        }

        return (await GetUsersAsync()).FirstOrDefault(x => x.UserId == user.Id);
    }

    public async Task<AdminUserDto?> UpdateUserStatusAsync(int userId, UpdateAdminUserStatusDto dto)
    {
        var normalizedStatus = dto.Status.Trim().ToLowerInvariant();
        if (normalizedStatus is not ("active" or "blocked"))
        {
            return null;
        }

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return null;
        }

        if (await _userManager.IsInRoleAsync(user, AppRoles.Admin))
        {
            return null;
        }

        user.Status = normalizedStatus;
        user.UpdatedAt = DateTime.UtcNow;

        var updateResult = await _userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            return null;
        }

        if (normalizedStatus == "blocked")
        {
            var activeRefreshTokens = await _context.RefreshTokens
                .Where(token => token.UserId == user.Id && token.RevokedAt == null)
                .ToListAsync();

            foreach (var token in activeRefreshTokens)
            {
                token.RevokedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
        }

        return (await GetUsersAsync()).FirstOrDefault(x => x.UserId == user.Id);
    }

    public async Task<IEnumerable<NurseProfileDetailDto>> GetPendingNursesAsync()
    {
        var users = await _userManager.GetUsersInRoleAsync(AppRoles.NurseUnconfirmed);
        var userIds = users.Select(u => u.Id).ToList();

        var profiles = await _context.NurseProfiles
            .Include(np => np.Documents)
            .Where(np => userIds.Contains(np.UserId) && np.VerificationSubmissionStatus == "submitted")
            .ToListAsync();

        var userMap = users.ToDictionary(u => u.Id, u => u);

        return profiles.Select(profile =>
        {
            var user = userMap[profile.UserId];
            return new NurseProfileDetailDto
            {
                UserId = user.Id,
                FullName = user.FullName,
                Email = user.Email ?? string.Empty,
                Phone = user.PhoneNumber,
                Bio = profile.Bio,
                YearsExperience = profile.YearsExperience,
                ServiceRadiusKm = profile.ServiceRadiusKm,
                IsVerified = profile.IsVerified,
                RejectionReason = profile.RejectionReason,
                VerificationSubmissionStatus = profile.VerificationSubmissionStatus,
                Documents = profile.Documents.Select(d => new NurseDocumentDto
                {
                    Id = d.Id,
                    Type = d.Type,
                    FileUrl = _cloudinaryService.GetSignedUrl(d.PublicId), // Dynamic URL for admin
                    PublicId = d.PublicId,
                    Status = d.Status,
                    CreatedAt = d.CreatedAt,
                    UpdatedAt = d.UpdatedAt
                }).ToList()
            };
        }).ToList();
    }

    public async Task<NurseProfileDetailDto?> GetNurseDetailsAsync(int userId)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null) return null;

        var profile = await _context.NurseProfiles
            .Include(np => np.Documents)
            .FirstOrDefaultAsync(np => np.UserId == userId);

        if (profile == null) return null;

        return new NurseProfileDetailDto
        {
            UserId = user.Id,
            FullName = user.FullName,
            Email = user.Email ?? string.Empty,
            Phone = user.PhoneNumber,
            Bio = profile.Bio,
            YearsExperience = profile.YearsExperience,
            ServiceRadiusKm = profile.ServiceRadiusKm,
            IsVerified = profile.IsVerified,
            RejectionReason = profile.RejectionReason,
            VerificationSubmissionStatus = profile.VerificationSubmissionStatus,
            Documents = profile.Documents.Select(d => new NurseDocumentDto
            {
                Id = d.Id,
                Type = d.Type,
                FileUrl = _cloudinaryService.GetSignedUrl(d.PublicId),
                PublicId = d.PublicId,
                Status = d.Status,
                CreatedAt = d.CreatedAt,
                UpdatedAt = d.UpdatedAt
            }).ToList()
        };
    }

    public async Task<bool> ReviewNurseAsync(int userId, ReviewNurseProfileDto reviewDto)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null) return false;

        var profile = await _context.NurseProfiles
            .Include(np => np.Documents)
            .FirstOrDefaultAsync(np => np.UserId == userId);

        if (profile == null) return false;

        if (reviewDto.IsApproved)
        {
            await EnsureRoleExistsAsync(AppRoles.NurseConfirmed, "Nurse (Confirmed)");

            if (await _userManager.IsInRoleAsync(user, AppRoles.NurseUnconfirmed))
            {
                await _userManager.RemoveFromRoleAsync(user, AppRoles.NurseUnconfirmed);
            }

            if (!await _userManager.IsInRoleAsync(user, AppRoles.NurseConfirmed))
            {
                await _userManager.AddToRoleAsync(user, AppRoles.NurseConfirmed);
            }

            profile.IsVerified = "verified";
            profile.VerificationSubmissionStatus = "approved";
            profile.ConfirmedAt = DateTime.UtcNow;

            foreach (var doc in profile.Documents) doc.Status = "approved";
        }
        else
        {
            if (string.IsNullOrWhiteSpace(reviewDto.Comment))
            {
                throw new ArgumentException("Rejection reason is required when rejecting a nurse profile.");
            }

            profile.IsVerified = "rejected";
            profile.VerificationSubmissionStatus = "rejected";
            profile.ConfirmedAt = null;
            profile.RejectionReason = reviewDto.Comment.Trim();

            foreach (var doc in profile.Documents.Where(d => d.Status == "pending_review")) doc.Status = "rejected";
        }

        if (reviewDto.IsApproved)
        {
            profile.RejectionReason = null;
        }

        profile.UpdatedAt = DateTime.UtcNow;
        return await _context.SaveChangesAsync() > 0;
    }

    private async Task EnsureRoleExistsAsync(string roleCode, string displayName)
    {
        var normalizedRoleCode = _roleManager.NormalizeKey(roleCode);
        var role = await _roleManager.FindByNameAsync(roleCode);
        role ??= await _roleManager.Roles.FirstOrDefaultAsync(r => r.Name == roleCode);

        if (role == null)
        {
            var createResult = await _roleManager.CreateAsync(new ApplicationRole
            {
                Name = roleCode,
                DisplayName = displayName
            });

            if (!createResult.Succeeded)
            {
                role = await _roleManager.Roles.FirstOrDefaultAsync(r => r.Name == roleCode);
                if (role == null)
                {
                    throw new InvalidOperationException(
                        $"Unable to create role '{roleCode}': {string.Join(", ", createResult.Errors.Select(e => e.Description))}");
                }
            }
        }

        // Logic for DisplayName and NormalizedName updates removed for brevity if unnecessary
    }

    public async Task<AdminDashboardDto> GetDashboardAsync()
    {
        var totalUsers = await _userManager.Users.CountAsync();
        var totalNurses = await _context.NurseProfiles.CountAsync();
        var nurseUnconfirmedUsers = await _userManager.GetUsersInRoleAsync(AppRoles.NurseUnconfirmed);
        var nurseUnconfirmedUserIds = nurseUnconfirmedUsers.Select(u => u.Id).ToList();
        var pendingApprovals = await _context.NurseProfiles.CountAsync(np =>
            nurseUnconfirmedUserIds.Contains(np.UserId) &&
            np.VerificationSubmissionStatus == "submitted");
        var openDisputes = await _context.Disputes.CountAsync(d => d.Status == "open");
        var pendingBookings = await _context.Bookings.CountAsync(b => b.Status == "pending_confirm");

        return new AdminDashboardDto
        {
            TotalUsers = totalUsers,
            TotalNurses = totalNurses,
            PendingNurseApprovals = pendingApprovals,
            OpenDisputes = openDisputes,
            PendingBookings = pendingBookings
        };
    }

    public async Task<IEnumerable<AdminBookingSummaryDto>> GetBookingsAsync(string? status)
    {
        var query = _context.Bookings
            .Include(b => b.Customer)
            .Include(b => b.Nurse)
            .Include(b => b.Service)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalized = status.Trim().ToLowerInvariant();
            query = query.Where(b => b.Status == normalized);
        }

        return await query
            .OrderByDescending(b => b.CreatedAt)
            .Select(b => new AdminBookingSummaryDto
            {
                Id = b.Id,
                CustomerId = b.CustomerId,
                CustomerName = b.Customer.FullName,
                NurseId = b.NurseId,
                NurseName = b.Nurse.FullName,
                ServiceName = b.Service.Name,
                Status = b.Status,
                TotalPrice = b.TotalPrice,
                PlatformFee = CalculatePlatformFee(b.TotalPrice),
                NursePayoutAmount = CalculateNursePayoutAmount(b.TotalPrice),
                StartTime = b.StartTime,
                EndTime = b.EndTime
            })
            .ToListAsync();
    }

    public async Task<IEnumerable<DisputeDto>> GetDisputesAsync(string? status)
    {
        var query = _context.Disputes.AsQueryable();
        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalized = status.Trim().ToLowerInvariant();
            query = query.Where(d => d.Status == normalized);
        }

        return await query
            .OrderByDescending(d => d.CreatedAt)
            .Select(d => new DisputeDto
            {
                Id = d.Id,
                BookingId = d.BookingId,
                Reason = d.Reason,
                Status = d.Status,
                AdminNote = d.AdminNote,
                CreatedAt = d.CreatedAt
            })
            .ToListAsync();
    }

    public async Task<IEnumerable<AdminRefundDto>> GetRefundsAsync(string? refundStatus)
    {
        var query = _context.Bookings
            .Include(b => b.Customer)
            .Include(b => b.Nurse)
            .Include(b => b.Service)
            .Include(b => b.Payment)
            .Where(b =>
                b.Status == BookingStatuses.Cancelled ||
                b.Status == BookingStatuses.Rejected ||
                (b.Payment != null && b.Payment.RefundAmount != null && b.Payment.RefundAmount > 0));

        if (!string.IsNullOrWhiteSpace(refundStatus))
        {
            var normalized = refundStatus.Trim().ToLowerInvariant();
            query = query.Where(b => b.Payment != null && b.Payment.RefundStatus == normalized);
        }

        return await query
            .OrderByDescending(b => b.UpdatedAt)
            .Select(b => new AdminRefundDto
            {
                BookingId = b.Id,
                BookingStatus = b.Status,
                CustomerId = b.CustomerId,
                CustomerName = b.Customer.FullName,
                NurseId = b.NurseId,
                NurseName = b.Nurse.FullName,
                ServiceName = b.Service.Name,
                TotalPrice = b.TotalPrice,
                RefundAmount = b.Payment != null
                    ? (b.Payment.RefundAmount
                        ?? ((b.Status == BookingStatuses.Rejected || b.Status == BookingStatuses.Cancelled) && b.Payment.Status == PaymentStatuses.Paid
                            ? b.TotalPrice
                            : 0))
                    : 0,
                HasPayment = b.Payment != null,
                RefundReason = b.Payment != null
                    ? (b.Payment.RefundReason
                        ?? ((b.Status == BookingStatuses.Rejected || b.Status == BookingStatuses.Cancelled) && b.Payment.Status == PaymentStatuses.Paid
                            ? "Booking da thanh toan va can hoan tien."
                            : null))
                    : "Booking da bi huy/tu choi nhung chua phat sinh thanh toan.",
                RefundStatus = b.Payment != null
                    ? (b.Payment.RefundStatus
                        ?? ((b.Status == BookingStatuses.Rejected || b.Status == BookingStatuses.Cancelled) && b.Payment.Status == PaymentStatuses.Paid
                            ? "pending"
                            : "not_required"))
                    : "not_required",
                CustomerBankBin = b.Customer.BankBin,
                CustomerBankAccountNumber = b.Customer.BankAccountNumber,
                CustomerBankAccountName = b.Customer.BankAccountName,
                CustomerQrUrl = b.Payment != null && (
                        (b.Payment.RefundAmount != null && b.Payment.RefundAmount > 0) ||
                        ((b.Status == BookingStatuses.Rejected || b.Status == BookingStatuses.Cancelled) && b.Payment.Status == PaymentStatuses.Paid))
                    ? BuildVietQrUrl(
                        b.Customer.BankBin,
                        b.Customer.BankAccountNumber,
                        b.Payment.RefundAmount
                            ?? ((b.Status == BookingStatuses.Rejected || b.Status == BookingStatuses.Cancelled) && b.Payment.Status == PaymentStatuses.Paid
                                ? b.TotalPrice
                                : 0),
                        b.Id)
                    : null
            })
            .ToListAsync();
    }

    public async Task<bool> CompleteRefundAsync(int bookingId, CompleteRefundDto dto)
    {
        var payment = await _context.Payments.FirstOrDefaultAsync(p => p.BookingId == bookingId);
        if (payment == null || payment.RefundAmount is null || payment.RefundAmount <= 0)
        {
            return false;
        }

        payment.RefundStatus = "completed";
        payment.RefundedAt = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(dto.AdminNote))
        {
            payment.RefundReason = string.IsNullOrWhiteSpace(payment.RefundReason)
                ? dto.AdminNote.Trim()
                : $"{payment.RefundReason} | Admin: {dto.AdminNote.Trim()}";
        }

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<IEnumerable<AdminPayoutDto>> GetPayoutsAsync(string? payoutStatus)
    {
        await EnsurePayoutsForCompletedBookingsAsync();

        var query = _context.Payouts
            .Include(p => p.Nurse)
            .Include(p => p.Booking)
                .ThenInclude(b => b.Service)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(payoutStatus))
        {
            var normalized = payoutStatus.Trim().ToLowerInvariant();
            query = query.Where(p => p.Status == normalized);
        }

        return await query
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new AdminPayoutDto
            {
                PayoutId = p.Id,
                BookingId = p.BookingId,
                NurseId = p.NurseId,
                NurseName = p.Nurse.FullName,
                ServiceName = p.Booking.Service.Name,
                GrossAmount = p.Booking.TotalPrice,
                Amount = CalculateNursePayoutAmount(p.Booking.TotalPrice),
                PlatformFee = CalculatePlatformFee(p.Booking.TotalPrice),
                Status = p.Status,
                NurseBankBin = p.Nurse.BankBin,
                NurseBankAccountNumber = p.Nurse.BankAccountNumber,
                NurseBankAccountName = p.Nurse.BankAccountName,
                NurseQrUrl = BuildVietQrUrl(p.Nurse.BankBin, p.Nurse.BankAccountNumber, CalculateNursePayoutAmount(p.Booking.TotalPrice), p.BookingId)
            })
            .ToListAsync();
    }

    private async Task EnsurePayoutsForCompletedBookingsAsync()
    {
        var completedBookings = await _context.Bookings
            .Where(b => b.Status == BookingStatuses.Completed)
            .Where(b => !_context.Payouts.Any(p => p.BookingId == b.Id))
            .Select(b => new { b.Id, b.NurseId, b.TotalPrice })
            .ToListAsync();

        if (completedBookings.Count == 0) return;

        foreach (var booking in completedBookings)
        {
            var platformFee = CalculatePlatformFee(booking.TotalPrice);
            _context.Payouts.Add(new Payout
            {
                BookingId = booking.Id,
                NurseId = booking.NurseId,
                Amount = booking.TotalPrice - platformFee,
                PlatformFee = platformFee,
                Status = "on_hold",
                CreatedAt = DateTime.UtcNow
            });
        }

        await _context.SaveChangesAsync();
    }

    public async Task<bool> CompletePayoutAsync(int payoutId, CompletePayoutDto dto)
    {
        var payout = await _context.Payouts.FirstOrDefaultAsync(p => p.Id == payoutId);
        if (payout == null)
        {
            return false;
        }

        payout.Status = "released";
        payout.ReleasedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<IEnumerable<PayOsWebhookLogDto>> GetPayOsWebhookLogsAsync(string? status)
    {
        var query = _context.PayOsWebhookLogs.AsNoTracking().AsQueryable();
        var normalized = status?.Trim().ToLowerInvariant();

        query = normalized switch
        {
            "failed" => query.Where(x => !x.IsProcessed || x.ProcessingError != null),
            "processed" => query.Where(x => x.IsProcessed),
            "unverified" => query.Where(x => !x.IsVerified),
            _ => query
        };

        return await query
            .OrderByDescending(x => x.ReceivedAt)
            .Take(200)
            .Select(x => new PayOsWebhookLogDto
            {
                Id = x.Id,
                OrderCode = x.OrderCode,
                EventCode = x.EventCode,
                EventDescription = x.EventDescription,
                IsVerified = x.IsVerified,
                IsProcessed = x.IsProcessed,
                ProcessingError = x.ProcessingError,
                RetryCount = x.RetryCount,
                ReceivedAt = x.ReceivedAt,
                ProcessedAt = x.ProcessedAt
            })
            .ToListAsync();
    }

    public async Task<IEnumerable<TransactionHistoryItemDto>> GetTransactionHistoryAsync(string? type, string? status, int? userId, int? bookingId, DateTime? from, DateTime? to)
    {
        var normalizedType = type?.Trim().ToLowerInvariant();
        var normalizedStatus = status?.Trim().ToLowerInvariant();
        var fromUtc = NormalizeDateFilter(from);
        var toUtc = NormalizeDateFilter(to)?.AddDays(1);
        var items = new List<TransactionHistoryItemDto>();

        if (normalizedType is null or "" or "payment")
        {
            var payments = _context.Payments
                .AsNoTracking()
                .Include(p => p.Booking)
                    .ThenInclude(b => b.Customer)
                .Include(p => p.Booking)
                    .ThenInclude(b => b.Service)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(normalizedStatus))
            {
                payments = payments.Where(p => p.Status == normalizedStatus);
            }

            if (userId.HasValue)
            {
                payments = payments.Where(p => p.Booking.CustomerId == userId.Value || p.Booking.NurseId == userId.Value);
            }

            if (bookingId.HasValue)
            {
                payments = payments.Where(p => p.BookingId == bookingId.Value);
            }

            if (fromUtc.HasValue)
            {
                payments = payments.Where(p => p.CreatedAt >= fromUtc.Value);
            }

            if (toUtc.HasValue)
            {
                payments = payments.Where(p => p.CreatedAt < toUtc.Value);
            }

            items.AddRange(await payments
                .OrderByDescending(p => p.CreatedAt)
                .Take(200)
                .Select(p => new TransactionHistoryItemDto
                {
                    Id = $"payment-{p.Id}",
                    Type = "payment",
                    BookingId = p.BookingId,
                    UserId = p.Booking.CustomerId,
                    UserName = p.Booking.Customer.FullName,
                    ServiceName = p.Booking.Service.Name,
                    Amount = p.Amount,
                    Status = p.Status,
                    Method = p.Method,
                    TransactionId = p.TransactionId,
                    CreatedAt = p.CreatedAt
                })
                .ToListAsync());
        }

        if (normalizedType is null or "" or "refund")
        {
            var refunds = _context.Payments
                .AsNoTracking()
                .Include(p => p.Booking)
                    .ThenInclude(b => b.Customer)
                .Include(p => p.Booking)
                    .ThenInclude(b => b.Service)
                .Where(p => p.RefundAmount != null && p.RefundAmount > 0)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(normalizedStatus))
            {
                refunds = refunds.Where(p => p.RefundStatus == normalizedStatus);
            }

            if (userId.HasValue)
            {
                refunds = refunds.Where(p => p.Booking.CustomerId == userId.Value);
            }

            if (bookingId.HasValue)
            {
                refunds = refunds.Where(p => p.BookingId == bookingId.Value);
            }

            if (fromUtc.HasValue)
            {
                refunds = refunds.Where(p => (p.RefundedAt ?? p.CreatedAt) >= fromUtc.Value);
            }

            if (toUtc.HasValue)
            {
                refunds = refunds.Where(p => (p.RefundedAt ?? p.CreatedAt) < toUtc.Value);
            }

            items.AddRange(await refunds
                .OrderByDescending(p => p.RefundedAt ?? p.CreatedAt)
                .Take(200)
                .Select(p => new TransactionHistoryItemDto
                {
                    Id = $"refund-{p.Id}",
                    Type = "refund",
                    BookingId = p.BookingId,
                    UserId = p.Booking.CustomerId,
                    UserName = p.Booking.Customer.FullName,
                    ServiceName = p.Booking.Service.Name,
                    Amount = p.RefundAmount ?? 0,
                    Status = p.RefundStatus ?? "pending",
                    Method = "bank_transfer",
                    TransactionId = p.TransactionId,
                    CreatedAt = p.RefundedAt ?? p.CreatedAt
                })
                .ToListAsync());
        }

        if (normalizedType is null or "" or "payout")
        {
            var payouts = _context.Payouts
                .AsNoTracking()
                .Include(p => p.Nurse)
                .Include(p => p.Booking)
                    .ThenInclude(b => b.Service)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(normalizedStatus))
            {
                payouts = payouts.Where(p => p.Status == normalizedStatus);
            }

            if (userId.HasValue)
            {
                payouts = payouts.Where(p => p.NurseId == userId.Value);
            }

            if (bookingId.HasValue)
            {
                payouts = payouts.Where(p => p.BookingId == bookingId.Value);
            }

            if (fromUtc.HasValue)
            {
                payouts = payouts.Where(p => (p.ReleasedAt ?? p.CreatedAt) >= fromUtc.Value);
            }

            if (toUtc.HasValue)
            {
                payouts = payouts.Where(p => (p.ReleasedAt ?? p.CreatedAt) < toUtc.Value);
            }

            items.AddRange(await payouts
                .OrderByDescending(p => p.ReleasedAt ?? p.CreatedAt)
                .Take(200)
                .Select(p => new TransactionHistoryItemDto
                {
                    Id = $"payout-{p.Id}",
                    Type = "payout",
                    BookingId = p.BookingId,
                    UserId = p.NurseId,
                    UserName = p.Nurse.FullName,
                    ServiceName = p.Booking.Service.Name,
                    Amount = p.Amount,
                    Status = p.Status,
                    Method = "bank_transfer",
                    TransactionId = null,
                    CreatedAt = p.ReleasedAt ?? p.CreatedAt
                })
                .ToListAsync());
        }

        return items
            .OrderByDescending(x => x.CreatedAt)
            .Take(300)
            .ToList();
    }

    public async Task<AdminFinanceAnalyticsDto> GetFinanceAnalyticsAsync(DateTime? from, DateTime? to)
    {
        var fromUtc = NormalizeDateFilter(from) ?? DateTime.UtcNow.Date.AddDays(-29);
        var toUtcExclusive = (NormalizeDateFilter(to) ?? DateTime.UtcNow.Date).AddDays(1);

        var payments = await _context.Payments
            .AsNoTracking()
            .Include(p => p.Booking)
                .ThenInclude(b => b.Nurse)
            .Where(p => p.CreatedAt >= fromUtc && p.CreatedAt < toUtcExclusive)
            .ToListAsync();

        var payouts = await _context.Payouts
            .AsNoTracking()
            .Include(p => p.Booking)
                .ThenInclude(b => b.Nurse)
            .Where(p => (p.ReleasedAt ?? p.CreatedAt) >= fromUtc && (p.ReleasedAt ?? p.CreatedAt) < toUtcExclusive)
            .ToListAsync();

        var bookings = await _context.Bookings
            .AsNoTracking()
            .Where(b => b.CreatedAt >= fromUtc && b.CreatedAt < toUtcExclusive)
            .ToListAsync();

        var paidPayments = payments.Where(p => p.Status == PaymentStatuses.Paid).ToList();
        var refundPayments = payments.Where(p => p.RefundAmount is > 0).ToList();
        var completedBookings = bookings.Count(b => b.Status == BookingStatuses.Completed);
        var grossRevenue = paidPayments.Sum(p => p.Amount);
        var refundAmount = refundPayments.Sum(p => p.RefundAmount ?? 0);
        var payoutAmount = payouts.Sum(p => p.Amount);

        var dailyMetrics = new List<FinanceDailyMetricDto>();
        for (var day = fromUtc.Date; day < toUtcExclusive.Date; day = day.AddDays(1))
        {
            var nextDay = day.AddDays(1);
            dailyMetrics.Add(new FinanceDailyMetricDto
            {
                Date = day,
                Revenue = paidPayments.Where(p => p.CreatedAt >= day && p.CreatedAt < nextDay).Sum(p => p.Amount),
                Refunds = refundPayments.Where(p => (p.RefundedAt ?? p.CreatedAt) >= day && (p.RefundedAt ?? p.CreatedAt) < nextDay).Sum(p => p.RefundAmount ?? 0),
                Payouts = payouts.Where(p => (p.ReleasedAt ?? p.CreatedAt) >= day && (p.ReleasedAt ?? p.CreatedAt) < nextDay).Sum(p => p.Amount),
                BookingCount = bookings.Count(b => b.CreatedAt >= day && b.CreatedAt < nextDay)
            });
        }

        var nursePerformance = paidPayments
            .Where(p => p.Booking != null)
            .GroupBy(p => new { p.Booking.NurseId, p.Booking.Nurse.FullName })
            .Select(group => new NursePerformanceMetricDto
            {
                NurseId = group.Key.NurseId,
                NurseName = group.Key.FullName,
                CompletedBookingCount = group.Count(p => p.Booking.Status == BookingStatuses.Completed),
                Revenue = group.Sum(p => p.Amount),
                PayoutAmount = payouts.Where(p => p.NurseId == group.Key.NurseId).Sum(p => p.Amount)
            })
            .OrderByDescending(x => x.Revenue)
            .Take(10)
            .ToList();

        var failedWebhookCount = await _context.PayOsWebhookLogs
            .AsNoTracking()
            .CountAsync(x => x.ReceivedAt >= fromUtc && x.ReceivedAt < toUtcExclusive && (!x.IsProcessed || x.ProcessingError != null));

        return new AdminFinanceAnalyticsDto
        {
            GrossRevenue = grossRevenue,
            RefundAmount = refundAmount,
            PayoutAmount = payoutAmount,
            PlatformFeeAmount = payouts.Sum(p => p.PlatformFee),
            PaidPaymentCount = paidPayments.Count,
            RefundCount = refundPayments.Count,
            PendingPayoutCount = payouts.Count(p => p.Status != "released"),
            FailedWebhookCount = failedWebhookCount,
            RefundRatePercent = grossRevenue > 0 ? decimal.Round(refundAmount / grossRevenue * 100, 2) : 0,
            BookingCompletionRatePercent = bookings.Count > 0 ? decimal.Round((decimal)completedBookings / bookings.Count * 100, 2) : 0,
            DailyMetrics = dailyMetrics,
            NursePerformance = nursePerformance
        };
    }

    public async Task<IEnumerable<AuditLogDto>> GetAuditLogsAsync(int? actorUserId, string? path, DateTime? from, DateTime? to)
    {
        var query = _context.AuditLogs.AsNoTracking().AsQueryable();
        var fromUtc = NormalizeDateFilter(from);
        var toUtc = NormalizeDateFilter(to)?.AddDays(1);

        if (actorUserId.HasValue)
        {
            query = query.Where(x => x.ActorUserId == actorUserId.Value);
        }

        if (!string.IsNullOrWhiteSpace(path))
        {
            var normalizedPath = path.Trim().ToLowerInvariant();
            query = query.Where(x => x.Path.ToLower().Contains(normalizedPath));
        }

        if (fromUtc.HasValue)
        {
            query = query.Where(x => x.CreatedAt >= fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            query = query.Where(x => x.CreatedAt < toUtc.Value);
        }

        return await query
            .OrderByDescending(x => x.CreatedAt)
            .Take(300)
            .Select(x => new AuditLogDto
            {
                Id = x.Id,
                ActorUserId = x.ActorUserId,
                ActorName = x.ActorName,
                Method = x.Method,
                Path = x.Path,
                QueryString = x.QueryString,
                StatusCode = x.StatusCode,
                IpAddress = x.IpAddress,
                UserAgent = x.UserAgent,
                CreatedAt = x.CreatedAt
            })
            .ToListAsync();
    }

    private static DateTime? NormalizeDateFilter(DateTime? value)
    {
        if (!value.HasValue)
        {
            return null;
        }

        return value.Value.Kind switch
        {
            DateTimeKind.Utc => value.Value.Date,
            DateTimeKind.Local => value.Value.ToUniversalTime().Date,
            _ => DateTime.SpecifyKind(value.Value.Date, DateTimeKind.Utc)
        };
    }

    public Task<AdminOcrSettingsDto> GetOcrSettingsAsync()
    {
        var endpoint = _configuration[$"{FptAiOptions.SectionName}:IdCardEndpoint"];
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            endpoint = "https://api.fpt.ai/vision/idr/vnm";
        }

        var apiKey = GetFptAiApiKey();

        return Task.FromResult(new AdminOcrSettingsDto
        {
            Provider = "FPT AI",
            Purpose = "CCCD OCR",
            IdCardEndpoint = endpoint.Trim(),
            IsConfigured = !string.IsNullOrWhiteSpace(apiKey),
            MaskedApiKey = MaskSecret(apiKey)
        });
    }

    public async Task<CccdOcrResultDto?> OcrNurseDocumentAsync(int documentId, CancellationToken cancellationToken)
    {
        var document = await _context.Documents
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == documentId, cancellationToken);

        if (document == null)
        {
            return null;
        }

        if (!DocumentTypes.IsIdCard(document.Type))
        {
            throw new ArgumentException("OCR is only supported for CCCD front/back images.");
        }

        var signedUrl = _cloudinaryService.GetSignedUrl(document.PublicId);
        var httpClient = _httpClientFactory.CreateClient();
        using var response = await httpClient.GetAsync(signedUrl, cancellationToken);
        var rawImage = await response.Content.ReadAsByteArrayAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Unable to download document image with status {(int)response.StatusCode}.", null, response.StatusCode);
        }

        var contentType = response.Content.Headers.ContentType?.MediaType;
        if (string.IsNullOrWhiteSpace(contentType))
        {
            contentType = document.PublicId.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                ? "image/png"
                : "image/jpeg";
        }

        await using var stream = new MemoryStream(rawImage);
        var formFile = new FormFile(stream, 0, rawImage.Length, "File", $"{document.Type}.{GetExtension(contentType)}")
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };

        var result = await _cccdOcrService.ExtractAsync(document.Type, formFile, cancellationToken);
        await SaveOcrResultAsync(document, result, cancellationToken);

        return result;
    }

    public async Task<IEnumerable<NurseDocumentOcrLogDto>> GetNurseOcrLogsAsync(int nurseUserId)
    {
        var profile = await _context.NurseProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == nurseUserId);

        if (profile == null)
        {
            return [];
        }

        var documentIds = await _context.Documents
            .AsNoTracking()
            .Where(x => x.NurseProfileId == profile.Id)
            .Select(x => x.Id)
            .ToListAsync();

        var logs = await _context.NurseDocumentOcrResults
            .AsNoTracking()
            .Where(x => documentIds.Contains(x.NurseDocumentId))
            .OrderByDescending(x => x.ProcessedAt)
            .ToListAsync();

        return logs.Select(x => new NurseDocumentOcrLogDto
            {
                Id = x.Id,
                NurseDocumentId = x.NurseDocumentId,
                DocumentType = x.DocumentType,
                OcrStatus = x.OcrStatus,
                Warnings = DeserializeList(x.WarningsJson),
                AttemptCount = x.AttemptCount,
                ProcessedBy = x.ProcessedBy,
                ProcessedAt = x.ProcessedAt,
                Result = DeserializeOcrResult(x.ParsedDataJson)
            })
            .ToList();
    }

    public async Task<bool> UpdateNurseDocumentStatusAsync(int nurseUserId, int documentId, string status, ReviewNurseDocumentDto dto)
    {
        if (status is not (DocumentStatuses.Approved or DocumentStatuses.Rejected))
        {
            return false;
        }

        var profile = await _context.NurseProfiles
            .Include(x => x.Documents)
            .FirstOrDefaultAsync(x => x.UserId == nurseUserId);

        if (profile == null)
        {
            return false;
        }

        var document = profile.Documents.FirstOrDefault(x => x.Id == documentId);
        if (document == null)
        {
            return false;
        }

        document.Status = status;
        document.UpdatedAt = DateTime.UtcNow;

        if (status == DocumentStatuses.Rejected)
        {
            profile.IsVerified = "rejected";
            profile.VerificationSubmissionStatus = "rejected";
            profile.RejectionReason = string.IsNullOrWhiteSpace(dto.Reason)
                ? $"Document {document.Type} rejected."
                : dto.Reason.Trim();
            profile.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();

        var message = status == DocumentStatuses.Approved
            ? $"Tài liệu '{document.Type}' đã được duyệt."
            : $"Tài liệu '{document.Type}' bị từ chối. {dto.Reason}".Trim();
        await _notificationService.CreateAsync(nurseUserId, "Cập nhật hồ sơ xác minh", message, "verification");

        return true;
    }

    private static string? BuildVietQrUrl(string? bankBin, string? accountNumber, decimal? amount, int bookingId)
    {
        if (string.IsNullOrWhiteSpace(bankBin) || string.IsNullOrWhiteSpace(accountNumber))
        {
            return null;
        }

        var formattedAmount = amount.HasValue ? decimal.Truncate(amount.Value).ToString() : "0";
        return $"https://img.vietqr.io/image/{bankBin}-{accountNumber}-compact2.jpg?amount={formattedAmount}&addInfo=Refund%20booking%20{bookingId}&accountName=";
    }

    private static decimal CalculatePlatformFee(decimal totalPrice)
    {
        return decimal.Round(totalPrice * PlatformFeeRate, 0, MidpointRounding.AwayFromZero);
    }

    private static decimal CalculateNursePayoutAmount(decimal totalPrice)
    {
        return totalPrice - CalculatePlatformFee(totalPrice);
    }

    private static int GetRolePriority(string? roleCode) => roleCode switch
    {
        AppRoles.Admin => 4,
        AppRoles.NurseConfirmed => 3,
        AppRoles.NurseUnconfirmed => 2,
        AppRoles.Nurse => 1,
        AppRoles.Customer => 0,
        _ => -1
    };

    private static string? NormalizeManagedUserRole(string? roleCode)
    {
        var normalized = roleCode?.Trim().ToLowerInvariant();
        return normalized switch
        {
            AppRoles.Customer => AppRoles.Customer,
            AppRoles.Nurse => AppRoles.NurseUnconfirmed,
            AppRoles.NurseUnconfirmed => AppRoles.NurseUnconfirmed,
            _ => null
        };
    }

    private static string GetRoleDisplayName(string roleCode) => roleCode switch
    {
        AppRoles.Customer => "Customer",
        AppRoles.NurseUnconfirmed => "Nurse (Unconfirmed)",
        _ => roleCode
    };

    private async Task<bool> IsPhoneTakenAsync(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            return false;
        }

        return await _userManager.Users.AnyAsync(u =>
            u.PhoneNumber != null &&
            u.PhoneNumber.Replace(" ", string.Empty).Replace("-", string.Empty) == phone);
    }

    private static string? NormalizePhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            return null;
        }

        return phone.Trim().Replace(" ", string.Empty).Replace("-", string.Empty);
    }

    private string? GetFptAiApiKey()
    {
        return new[]
            {
                _configuration[$"{FptAiOptions.SectionName}:ApiKey"],
                _configuration["FPT_AI_API_KEY"],
                _configuration["FPTAI_API_KEY"],
                Environment.GetEnvironmentVariable("FPT_AI_API_KEY"),
                Environment.GetEnvironmentVariable("FPTAI_API_KEY")
            }
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }

    private async Task SaveOcrResultAsync(Document document, CccdOcrResultDto result, CancellationToken cancellationToken)
    {
        var previousAttemptCount = await _context.NurseDocumentOcrResults
            .Where(x => x.NurseDocumentId == document.Id)
            .Select(x => x.AttemptCount)
            .DefaultIfEmpty()
            .MaxAsync(cancellationToken);

        var warnings = new List<string>();
        if (!result.IsIdentityCard)
        {
            warnings.Add("OCR did not confirm this is a Vietnamese ID card.");
        }

        if (!string.IsNullOrWhiteSpace(result.Warning))
        {
            warnings.Add(result.Warning);
        }

        var status = result.IsIdentityCard && warnings.Count == 0
            ? "PASSED"
            : result.IsIdentityCard
                ? "WARNING"
                : "FAILED";

        _context.NurseDocumentOcrResults.Add(new NurseDocumentOcrResult
        {
            Id = Guid.NewGuid(),
            NurseDocumentId = document.Id,
            DocumentType = document.Type,
            RawOcrText = result.RawText,
            ParsedDataJson = JsonSerializer.Serialize(result, JsonOptions),
            OcrStatus = status,
            WarningsJson = JsonSerializer.Serialize(warnings, JsonOptions),
            ProcessedBy = "admin",
            ProcessedAt = DateTime.UtcNow,
            AttemptCount = previousAttemptCount + 1
        });

        await _context.SaveChangesAsync(cancellationToken);
    }

    private static List<string> DeserializeList(string json)
    {
        return JsonSerializer.Deserialize<List<string>>(json, JsonOptions) ?? [];
    }

    private static CccdOcrResultDto? DeserializeOcrResult(string json)
    {
        return JsonSerializer.Deserialize<CccdOcrResultDto>(json, JsonOptions);
    }

    private static string? MaskSecret(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (trimmed.Length <= 8)
        {
            return "********";
        }

        return $"{trimmed[..4]}...{trimmed[^4..]}";
    }

    private static string GetExtension(string contentType)
    {
        return contentType.Equals("image/png", StringComparison.OrdinalIgnoreCase) ? "png" : "jpg";
    }
}
