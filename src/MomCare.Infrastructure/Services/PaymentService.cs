using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MomCare.Data;
using MomCare.Dto;
using NurseServiceModel = MomCare.Models.NurseService;
using MomCare.Enums;
using PayOSConfig = MomCare.Infrastructure.Configurations.PayOSOptions;
using MomCare.Interfaces;
using MomCare.Models;
using PayOS;
using PayOS.Models.V2.PaymentRequests;
using PayOS.Models.Webhooks;

namespace MomCare.Services;

public class PaymentService : IPaymentService
{
    private readonly MomCareContext _context;
    private readonly INotificationService _notificationService;
    private readonly PayOSConfig _payOSOptions;
    private readonly PayOSClient? _payOSClient;
    private static readonly TimeZoneInfo VietnamTimeZone = ResolveVietnamTimeZone();

    public PaymentService(
        MomCareContext context,
        INotificationService notificationService,
        IOptions<PayOSConfig> payOSOptions)
    {
        _context = context;
        _notificationService = notificationService;
        _payOSOptions = MergeOptions(payOSOptions.Value);

        if (!string.IsNullOrWhiteSpace(_payOSOptions.ClientId)
            && !string.IsNullOrWhiteSpace(_payOSOptions.ApiKey)
            && !string.IsNullOrWhiteSpace(_payOSOptions.ChecksumKey))
        {
            _payOSClient = new PayOSClient(
                _payOSOptions.ClientId,
                _payOSOptions.ApiKey,
                _payOSOptions.ChecksumKey,
                null);
        }
    }

    public async Task<PaymentDto?> UpsertPaymentAsync(int actorUserId, bool isAdmin, int bookingId, UpdatePaymentStatusDto dto)
    {
        var booking = await _context.Bookings.FirstOrDefaultAsync(b => b.Id == bookingId);
        if (booking == null)
        {
            return null;
        }

        if (!isAdmin && booking.CustomerId != actorUserId)
        {
            return null;
        }

        var payment = await _context.Payments.FirstOrDefaultAsync(p => p.BookingId == bookingId);
        if (payment == null)
        {
            payment = new Payment
            {
                BookingId = bookingId,
                Amount = booking.TotalPrice,
                Method = dto.Method,
                Status = dto.Status,
                TransactionId = dto.TransactionId,
                CreatedAt = DateTime.UtcNow
            };

            _context.Payments.Add(payment);
        }
        else
        {
            payment.Method = dto.Method;
            payment.Status = dto.Status;
            payment.TransactionId = dto.TransactionId;
        }

        await _context.SaveChangesAsync();

        var statusText = NotificationVietnameseText.PaymentStatus(payment.Status);
        await _notificationService.CreateAsync(booking.NurseId, "Cập nhật thanh toán", $"Thanh toán cho lịch hẹn #{bookingId} hiện {statusText}.", "payment");
        await _notificationService.CreateAsync(booking.CustomerId, "Cập nhật thanh toán", $"Thanh toán của bạn cho lịch hẹn #{bookingId} hiện {statusText}.", "payment");

        return MapPayment(payment);
    }

    public async Task<PayOSPaymentLinkDto?> CreatePayOSPaymentLinkAsync(int actorUserId, bool isAdmin, int bookingId, CreatePayOSPaymentLinkDto dto)
    {
        EnsurePayOSConfigured();

        var booking = await _context.Bookings
            .Include(b => b.Service)
            .FirstOrDefaultAsync(b => b.Id == bookingId);
        if (booking == null)
        {
            return null;
        }

        if (!isAdmin && booking.CustomerId != actorUserId)
        {
            return null;
        }

        var payment = await _context.Payments.FirstOrDefaultAsync(p => p.BookingId == bookingId);
        var orderCode = BuildOrderCode(bookingId);
        var returnUrl = dto.ReturnUrl ?? _payOSOptions.ReturnUrl;
        var cancelUrl = dto.CancelUrl ?? _payOSOptions.CancelUrl;

        if (string.IsNullOrWhiteSpace(returnUrl) || string.IsNullOrWhiteSpace(cancelUrl))
        {
            throw new InvalidOperationException("PayOS return/cancel URL is not configured.");
        }

        var request = new CreatePaymentLinkRequest
        {
            OrderCode = orderCode,
            Amount = Decimal.ToInt32(decimal.Round(booking.TotalPrice, 0, MidpointRounding.AwayFromZero)),
            Description = BuildDescription(booking),
            ReturnUrl = returnUrl,
            CancelUrl = cancelUrl
        };

        var paymentLink = await _payOSClient!.PaymentRequests.CreateAsync(request);

        if (payment == null)
        {
            payment = new Payment
            {
                BookingId = bookingId,
                Amount = booking.TotalPrice,
                Method = "payos",
                Status = PaymentStatuses.Initiated,
                TransactionId = orderCode.ToString(),
                CreatedAt = DateTime.UtcNow
            };
            _context.Payments.Add(payment);
        }
        else
        {
            payment.Amount = booking.TotalPrice;
            payment.Method = "payos";
            payment.Status = PaymentStatuses.Initiated;
            payment.TransactionId = orderCode.ToString();
        }

        await _context.SaveChangesAsync();

        return new PayOSPaymentLinkDto
        {
            BookingId = bookingId,
            OrderCode = orderCode,
            CheckoutUrl = paymentLink.CheckoutUrl,
            PaymentLinkId = paymentLink.PaymentLinkId
        };
    }

    public async Task<PayOSPaymentLinkDto> CreatePayOSBookingPaymentLinkAsync(int actorUserId, CreatePayOSBookingPaymentDto dto)
    {
        EnsurePayOSConfigured();

        var service = await _context.Services.FirstOrDefaultAsync(s => s.Id == dto.ServiceId && s.Status == "active")
            ?? throw new InvalidOperationException("Service is not available.");
        var nurseProfile = await _context.NurseProfiles
            .FirstOrDefaultAsync(np => np.UserId == dto.NurseId && np.IsActive && np.IsVerified == "verified")
            ?? throw new InvalidOperationException("Nurse is not available.");
        var nurseService = await _context.NurseServices
            .FirstOrDefaultAsync(ns => ns.NurseProfileId == nurseProfile.Id && ns.ServiceId == dto.ServiceId && ns.Status == "enabled")
            ?? throw new InvalidOperationException("Nurse does not provide this service.");

        var amount = CalculateQuoteAmount(dto, service, nurseProfile.Id, nurseService);
        var orderCode = BuildQuoteOrderCode(actorUserId);
        var request = new CreatePaymentLinkRequest
        {
            OrderCode = orderCode,
            Amount = Decimal.ToInt32(decimal.Round(amount, 0, MidpointRounding.AwayFromZero)),
            Description = BuildQuoteDescription(service.Name),
            ReturnUrl = dto.ReturnUrl ?? _payOSOptions.ReturnUrl ?? throw new InvalidOperationException("PayOS return URL is not configured."),
            CancelUrl = dto.CancelUrl ?? _payOSOptions.CancelUrl ?? throw new InvalidOperationException("PayOS cancel URL is not configured.")
        };

        var paymentLink = await _payOSClient!.PaymentRequests.CreateAsync(request);
        return new PayOSPaymentLinkDto
        {
            BookingId = 0,
            OrderCode = orderCode,
            CheckoutUrl = paymentLink.CheckoutUrl,
            PaymentLinkId = paymentLink.PaymentLinkId
        };
    }

    public async Task<bool> HandlePayOSWebhookAsync(PayOSWebhookDto webhook)
    {
        EnsurePayOSConfigured();

        var verifiedData = await _payOSClient!.Webhooks.VerifyAsync(MapWebhook(webhook));
        var orderCode = verifiedData.OrderCode.ToString();

        var payment = await _context.Payments
            .Include(p => p.Booking)
            .FirstOrDefaultAsync(p => p.TransactionId == orderCode);

        if (payment == null)
        {
            return false;
        }

        payment.Method = "payos";
        payment.Status = verifiedData.Code == "00" ? PaymentStatuses.Paid : PaymentStatuses.Failed;

        await _context.SaveChangesAsync();

        var booking = payment.Booking;
        if (booking != null)
        {
            var statusText = NotificationVietnameseText.PaymentStatus(payment.Status);
            await _notificationService.CreateAsync(booking.NurseId, "Cap nhat thanh toan", $"Thanh toan cho lich hen #{booking.Id} hien {statusText}.", "payment");
            await _notificationService.CreateAsync(booking.CustomerId, "Cap nhat thanh toan", $"Thanh toan cua ban cho lich hen #{booking.Id} hien {statusText}.", "payment");
        }

        return true;
    }

    private static PaymentDto MapPayment(Payment p) => new()
    {
        Id = p.Id,
        BookingId = p.BookingId,
        Amount = p.Amount,
        Method = p.Method,
        Status = p.Status,
        TransactionId = p.TransactionId,
        RefundAmount = p.RefundAmount,
        RefundReason = p.RefundReason,
        RefundStatus = p.RefundStatus,
        CreatedAt = p.CreatedAt,
        RefundedAt = p.RefundedAt
    };

    private void EnsurePayOSConfigured()
    {
        if (_payOSClient is null)
        {
            var missing = new List<string>();
            if (string.IsNullOrWhiteSpace(_payOSOptions.ClientId)) missing.Add("PayOS__ClientId");
            if (string.IsNullOrWhiteSpace(_payOSOptions.ApiKey)) missing.Add("PayOS__ApiKey");
            if (string.IsNullOrWhiteSpace(_payOSOptions.ChecksumKey)) missing.Add("PayOS__ChecksumKey");

            throw new InvalidOperationException($"PayOS is not configured. Missing: {string.Join(", ", missing)}");
        }
    }

    private static PayOSConfig MergeOptions(PayOSConfig options)
    {
        return new PayOSConfig
        {
            ClientId = FirstNonEmpty(options.ClientId, Environment.GetEnvironmentVariable("PayOS__ClientId"), Environment.GetEnvironmentVariable("PAYOS_CLIENT_ID")),
            ApiKey = FirstNonEmpty(options.ApiKey, Environment.GetEnvironmentVariable("PayOS__ApiKey"), Environment.GetEnvironmentVariable("PAYOS_API_KEY")),
            ChecksumKey = FirstNonEmpty(options.ChecksumKey, Environment.GetEnvironmentVariable("PayOS__ChecksumKey"), Environment.GetEnvironmentVariable("PAYOS_CHECKSUM_KEY")),
            ReturnUrl = FirstNonEmpty(options.ReturnUrl, Environment.GetEnvironmentVariable("PayOS__ReturnUrl")),
            CancelUrl = FirstNonEmpty(options.CancelUrl, Environment.GetEnvironmentVariable("PayOS__CancelUrl")),
            WebhookUrl = FirstNonEmpty(options.WebhookUrl, Environment.GetEnvironmentVariable("PayOS__WebhookUrl"))
        };
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }

    private static long BuildOrderCode(int bookingId)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return long.Parse($"{bookingId}{timestamp % 1000000000:D9}");
    }

    private static long BuildQuoteOrderCode(int actorUserId)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return long.Parse($"{actorUserId % 1000:D3}{timestamp % 1000000000:D9}");
    }

    private static string BuildDescription(Booking booking)
    {
        var serviceName = booking.Service?.Name ?? "don hang";
        var raw = $"Thanh toan #{booking.Id} {serviceName}";
        return raw.Length <= 25 ? raw : raw[..25];
    }

    private static string BuildQuoteDescription(string serviceName)
    {
        var raw = $"TT truoc {serviceName}";
        return raw.Length <= 25 ? raw : raw[..25];
    }

    private decimal CalculateQuoteAmount(CreatePayOSBookingPaymentDto dto, Service service, int nurseProfileId, NurseServiceModel nurseService)
    {
        if (service.ServiceKind == "package")
        {
            return nurseService.Price;
        }

        if (!dto.AvailabilitySlotId.HasValue)
        {
            throw new InvalidOperationException("Single service requires slot.");
        }

        var requestedStartTime = NormalizeDateTime(dto.StartTime);
        var requestedEndTime = requestedStartTime.AddMinutes(Math.Max(service.EstimatedDurationMinutes, 1));

        if (requestedEndTime <= requestedStartTime)
        {
            throw new InvalidOperationException("End time must be after start time.");
        }

        var slot = _context.AvailabilitySlots
            .FirstOrDefault(a => a.Id == dto.AvailabilitySlotId.Value && a.NurseProfileId == nurseProfileId)
            ?? throw new InvalidOperationException("Availability slot does not exist.");

        if (requestedStartTime < slot.StartTime || requestedEndTime > slot.EndTime)
        {
            throw new InvalidOperationException("Selected time is outside nurse availability.");
        }

        return nurseService.Unit == "hourly"
            ? nurseService.Price * (decimal)(requestedEndTime - requestedStartTime).TotalHours
            : nurseService.Price;
    }

    private static DateTime NormalizeDateTime(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => TimeZoneInfo.ConvertTimeToUtc(value, VietnamTimeZone)
        };
    }

    private static TimeZoneInfo ResolveVietnamTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
        }
    }

    private static Webhook MapWebhook(PayOSWebhookDto source) => new()
    {
        Code = source.Code ?? string.Empty,
        Description = source.Description ?? string.Empty,
        Success = source.Success,
        Signature = source.Signature ?? string.Empty,
        Data = source.Data is null
            ? null
            : new WebhookData
            {
                OrderCode = source.Data.OrderCode,
                Amount = source.Data.Amount,
                Description = source.Data.Description ?? string.Empty,
                AccountNumber = source.Data.AccountNumber ?? string.Empty,
                Reference = source.Data.Reference ?? string.Empty,
                TransactionDateTime = source.Data.TransactionDateTime ?? string.Empty,
                Currency = source.Data.Currency ?? string.Empty,
                PaymentLinkId = source.Data.PaymentLinkId ?? string.Empty,
                Code = source.Data.Code ?? string.Empty,
                Description2 = source.Data.Description2 ?? string.Empty,
                CounterAccountBankId = source.Data.CounterAccountBankId ?? string.Empty,
                CounterAccountBankName = source.Data.CounterAccountBankName ?? string.Empty,
                CounterAccountName = source.Data.CounterAccountName,
                CounterAccountNumber = source.Data.CounterAccountNumber,
                VirtualAccountName = source.Data.VirtualAccountName,
                VirtualAccountNumber = source.Data.VirtualAccountNumber
            }
    };
}
