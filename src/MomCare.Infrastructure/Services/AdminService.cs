using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
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
    private const decimal PlatformFeeRate = 0.15m;

    private static readonly string[] RequiredDocumentTypes =
    [
        DocumentTypes.IdCardFront,
        DocumentTypes.IdCardBack,
        DocumentTypes.Certificate
    ];

    public AdminService(
        MomCareContext context,
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        ICloudinaryService cloudinaryService,
        IConfiguration configuration,
        ICccdOcrService cccdOcrService,
        IHttpClientFactory httpClientFactory)
    {
        _context = context;
        _userManager = userManager;
        _roleManager = roleManager;
        _cloudinaryService = cloudinaryService;
        _configuration = configuration;
        _cccdOcrService = cccdOcrService;
        _httpClientFactory = httpClientFactory;
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
            await _context.SaveChangesAsync();
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

        if (profile.VerificationSubmissionStatus != "submitted")
        {
            throw new ArgumentException("Only submitted nurse profiles can be reviewed.");
        }

        var submittedDocumentTypes = profile.Documents
            .Select(d => d.Type)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missingDocumentTypes = RequiredDocumentTypes
            .Where(requiredType => !submittedDocumentTypes.Contains(requiredType))
            .ToList();

        if (missingDocumentTypes.Count > 0)
        {
            throw new ArgumentException($"Verification dossier is incomplete. Missing: {string.Join(", ", missingDocumentTypes)}.");
        }

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

        return await _cccdOcrService.ExtractAsync(document.Type, formFile, cancellationToken);
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
