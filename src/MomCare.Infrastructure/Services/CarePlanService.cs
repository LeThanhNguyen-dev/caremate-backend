using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MomCare.Data;
using MomCare.Dto;
using MomCare.Enums;
using MomCare.Interfaces;
using MomCare.Models;

namespace MomCare.Services;

public class CarePlanService : ICarePlanService
{
    private const string Disclaimer = "CareMate AI cung cấp thông tin tham khảo, không thay thế tư vấn, chẩn đoán hoặc điều trị từ bác sĩ.";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly MomCareContext _context;
    private readonly IGeminiService _geminiService;
    private readonly INurseDiscoveryService _nurseDiscoveryService;
    private readonly ILogger<CarePlanService> _logger;

    public CarePlanService(
        MomCareContext context,
        IGeminiService geminiService,
        INurseDiscoveryService nurseDiscoveryService,
        ILogger<CarePlanService> logger)
    {
        _context = context;
        _geminiService = geminiService;
        _nurseDiscoveryService = nurseDiscoveryService;
        _logger = logger;
    }

    public async Task<ServiceResult<CarePlanResponse>> RecommendAsync(int userId, CarePlanRecommendRequest request, CancellationToken cancellationToken)
    {
        var checkIn = await ResolveCheckInAsync(userId, request, cancellationToken);
        if (checkIn is null)
        {
            return ServiceResult<CarePlanResponse>.Fail("Không tìm thấy dữ liệu check-in để tạo lộ trình.");
        }

        var activeBooking = await FindActiveBookingAsync(userId, cancellationToken);
        if (activeBooking is not null)
        {
            return await GenerateForBookingInternalAsync(userId, false, activeBooking.Id, checkIn, request.UserLocation, cancellationToken);
        }

        await SupersedeOpenPlansAsync(userId, null, cancellationToken);
        var safety = SafetyGuardrailEngine.Evaluate(checkIn);
        var services = safety.SafetyLevel == "urgent"
            ? []
            : await RecommendServicesAsync(checkIn, cancellationToken);
        var firstServiceId = services.FirstOrDefault()?.ServiceId;
        var nurses = firstServiceId.HasValue
            ? await GetRecommendedNursesAsync(firstServiceId, request.UserLocation, cancellationToken)
            : [];
        var ai = safety.SafetyLevel == "urgent"
            ? AiText.Fallback(BuildUrgentSummary(safety))
            : await TryWriteCarePlanSummaryAsync(checkIn, services, [], cancellationToken);

        var plan = new AiCarePlan
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            HealthCheckInId = checkIn.Id,
            Status = "active",
            PlanType = "recommend_package",
            SafetyLevel = safety.SafetyLevel,
            SafetyNotice = safety.Notice,
            Summary = ai.Summary,
            RecommendedServicesJson = JsonSerializer.Serialize(services, JsonOptions),
            PlanItemsJson = "[]",
            RecommendedNursesJson = JsonSerializer.Serialize(nurses, JsonOptions),
            Disclaimer = Disclaimer,
            AiModel = ai.Model,
            FallbackMode = ai.FallbackMode,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.AiCarePlans.Add(plan);
        await _context.SaveChangesAsync(cancellationToken);
        return ServiceResult<CarePlanResponse>.Ok(Map(plan));
    }

    public async Task<ServiceResult<CarePlanResponse>> GenerateForBookingAsync(int actorUserId, bool isAdmin, int bookingId, CancellationToken cancellationToken)
    {
        return await GenerateForBookingInternalAsync(actorUserId, isAdmin, bookingId, null, null, cancellationToken);
    }

    public async Task<ServiceResult<CarePlanResponse>> GetForBookingAsync(int actorUserId, bool isAdmin, int bookingId, CancellationToken cancellationToken)
    {
        var booking = await _context.Bookings.AsNoTracking().FirstOrDefaultAsync(x => x.Id == bookingId, cancellationToken);
        if (booking is null)
        {
            return ServiceResult<CarePlanResponse>.Fail("Không tìm thấy lịch hẹn.");
        }

        if (!CanAccessBooking(actorUserId, isAdmin, booking))
        {
            return ServiceResult<CarePlanResponse>.Fail("Bạn không có quyền xem lộ trình này.");
        }

        var plan = await _context.AiCarePlans
            .AsNoTracking()
            .Where(x => x.BookingId == bookingId && x.Status != "superseded")
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        return plan is null
            ? ServiceResult<CarePlanResponse>.Fail("Chưa có lộ trình cho lịch hẹn này.")
            : ServiceResult<CarePlanResponse>.Ok(Map(plan));
    }

    private async Task<ServiceResult<CarePlanResponse>> GenerateForBookingInternalAsync(
        int actorUserId,
        bool isAdmin,
        int bookingId,
        HealthCheckIn? suppliedCheckIn,
        GeoPointDto? location,
        CancellationToken cancellationToken)
    {
        var booking = await _context.Bookings
            .Include(x => x.Service)
            .Include(x => x.SessionLogs.OrderBy(s => s.SessionNumber))
            .FirstOrDefaultAsync(x => x.Id == bookingId, cancellationToken);

        if (booking is null)
        {
            return ServiceResult<CarePlanResponse>.Fail("Không tìm thấy lịch hẹn.");
        }

        if (!CanAccessBooking(actorUserId, isAdmin, booking))
        {
            return ServiceResult<CarePlanResponse>.Fail("Bạn không có quyền tạo lộ trình này.");
        }

        var checkIn = suppliedCheckIn ?? await GetLatestCheckInAsync(booking.CustomerId, cancellationToken);
        await SupersedeOpenPlansAsync(booking.CustomerId, booking.Id, cancellationToken);
        var safety = checkIn is null
            ? new SafetyEvaluationDto { SafetyLevel = "normal" }
            : SafetyGuardrailEngine.Evaluate(checkIn);
        var items = safety.SafetyLevel == "urgent"
            ? []
            : BuildPlanItems(booking);
        var nurses = await GetRecommendedNursesAsync(booking.ServiceId, location, cancellationToken);
        var ai = safety.SafetyLevel == "urgent"
            ? AiText.Fallback(BuildUrgentSummary(safety))
            : await TryWriteCarePlanSummaryAsync(checkIn, [], items, cancellationToken);

        var plan = new AiCarePlan
        {
            Id = Guid.NewGuid(),
            UserId = booking.CustomerId,
            BookingId = booking.Id,
            HealthCheckInId = checkIn?.Id,
            Status = "active",
            PlanType = "by_booking",
            SafetyLevel = safety.SafetyLevel,
            SafetyNotice = safety.Notice,
            Summary = ai.Summary,
            RecommendedServicesJson = "[]",
            PlanItemsJson = JsonSerializer.Serialize(items, JsonOptions),
            RecommendedNursesJson = JsonSerializer.Serialize(nurses, JsonOptions),
            Disclaimer = Disclaimer,
            AiModel = ai.Model,
            FallbackMode = ai.FallbackMode,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.AiCarePlans.Add(plan);
        await _context.SaveChangesAsync(cancellationToken);
        return ServiceResult<CarePlanResponse>.Ok(Map(plan));
    }

    private async Task<HealthCheckIn?> ResolveCheckInAsync(int userId, CarePlanRecommendRequest request, CancellationToken cancellationToken)
    {
        if (request.HealthCheckInId.HasValue)
        {
            return await _context.HealthCheckIns.FirstOrDefaultAsync(x => x.Id == request.HealthCheckInId && x.UserId == userId, cancellationToken);
        }

        if (request.CheckIn is not null)
        {
            var checkIn = BuildCheckIn(userId, request.CheckIn);
            _context.HealthCheckIns.Add(checkIn);
            await _context.SaveChangesAsync(cancellationToken);
            return checkIn;
        }

        return await GetLatestCheckInAsync(userId, cancellationToken);
    }

    private static HealthCheckIn BuildCheckIn(int userId, AnalyzeHealthCheckInRequest request)
    {
        return new HealthCheckIn
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            SleepHours = request.SleepHours,
            PainLevel = request.PainLevel ?? 0,
            PainLocation = Clean(request.PainLocation),
            PainType = Clean(request.PainType),
            PainDuration = Clean(request.PainDuration),
            PainTrend = Clean(request.PainTrend),
            SymptomsJson = JsonSerializer.Serialize(CleanList(request.Symptoms), JsonOptions),
            MedicalHistoryJson = JsonSerializer.Serialize(CleanList(request.MedicalHistory), JsonOptions),
            ContextDataJson = JsonSerializer.Serialize(request.ContextData ?? [], JsonOptions),
            MotherAge = request.MotherAge,
            SystolicBloodPressure = request.SystolicBloodPressure,
            DiastolicBloodPressure = request.DiastolicBloodPressure,
            TemperatureCelsius = request.TemperatureCelsius,
            TookMedicationToday = request.TookMedicationToday,
            MedicationNote = Clean(request.MedicationNote),
            Mood = request.Mood.Trim(),
            MilkStatus = request.MilkStatus.Trim(),
            BabyFeeding = request.BabyFeeding.Trim(),
            BabySleep = request.BabySleep.Trim(),
            Note = Clean(request.Note),
            CreatedAt = DateTime.UtcNow
        };
    }

    private async Task<HealthCheckIn?> GetLatestCheckInAsync(int userId, CancellationToken cancellationToken)
    {
        return await _context.HealthCheckIns
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<Booking?> FindActiveBookingAsync(int userId, CancellationToken cancellationToken)
    {
        var activeStatuses = new[] { BookingStatuses.PendingConfirm, BookingStatuses.Confirmed, BookingStatuses.InProgress };
        return await _context.Bookings
            .Include(x => x.Service)
            .Include(x => x.SessionLogs)
            .Where(x => x.CustomerId == userId && activeStatuses.Contains(x.Status) && x.Service.ServiceKind == "package")
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<List<RecommendedCareServiceDto>> RecommendServicesAsync(HealthCheckIn checkIn, CancellationToken cancellationToken)
    {
        var services = await _context.Services
            .AsNoTracking()
            .Where(x => x.Status == "active")
            .OrderByDescending(x => x.ServiceKind == "package")
            .ThenBy(x => x.BasePrice)
            .Take(6)
            .ToListAsync(cancellationToken);

        var context = ReadDictionary(checkIn.ContextDataJson);
        var postpartum = context.TryGetValue("postpartumDay", out var day) ? day : null;
        var reasonSuffix = string.IsNullOrWhiteSpace(postpartum) ? "dựa trên check-in mới nhất" : $"cho giai đoạn ngày {postpartum} sau sinh";

        return services.Select(service => new RecommendedCareServiceDto
        {
            ServiceId = service.Id,
            Name = service.Name,
            Reason = service.ServiceKind == "package"
                ? $"Gói này phù hợp để theo dõi liên tục {reasonSuffix}."
                : $"Dịch vụ này phù hợp để hỗ trợ một nhu cầu chăm sóc cụ thể {reasonSuffix}.",
            SessionCount = service.PackageDays,
            EstimatedPrice = service.BasePrice
        }).ToList();
    }

    private static List<CarePlanTimelineItemDto> BuildPlanItems(Booking booking)
    {
        if (booking.Service.ServiceKind != "package" || booking.SessionLogs.Count == 0)
        {
            return
            [
                new()
                {
                    SessionNumber = 1,
                    ScheduledDate = booking.StartTime,
                    Focus = booking.Service.Name,
                    Activities = ["Chuẩn bị câu hỏi cho y tá", "Theo dõi tình trạng mẹ và bé trước buổi chăm sóc"],
                    Notes = "Buổi chăm sóc tập trung vào nhu cầu hiện tại của mẹ và bé.",
                    DurationMinutes = Math.Max(booking.Service.EstimatedDurationMinutes, 1)
                }
            ];
        }

        return booking.SessionLogs
            .Where(session => session.Status is "pending" or "checked_in")
            .OrderBy(session => session.SessionNumber)
            .Select(session => new CarePlanTimelineItemDto
            {
                SessionNumber = session.SessionNumber,
                ScheduledDate = session.SessionDate,
                Focus = string.IsNullOrWhiteSpace(session.Title) ? $"Buổi {session.SessionNumber}" : session.Title!,
                Activities = BuildActivities(session, booking.Service),
                Notes = string.IsNullOrWhiteSpace(session.Description)
                    ? "Y tá sẽ điều chỉnh nội dung chăm sóc theo tình trạng thực tế của mẹ và bé."
                    : session.Description!,
                DurationMinutes = Math.Max(booking.Service.EstimatedDurationMinutes, 1)
            })
            .ToList();
    }

    private static List<string> BuildActivities(PackageSessionLog session, Service service)
    {
        var activities = new List<string>();
        if (!string.IsNullOrWhiteSpace(session.PlannedServiceKeys))
        {
            activities.AddRange(session.PlannedServiceKeys
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Take(3)
                .Select(key => $"Thực hiện nội dung {key}"));
        }

        if (activities.Count == 0)
        {
            activities.Add($"Theo dõi và hỗ trợ {service.Name.ToLowerInvariant()}");
            activities.Add("Ghi nhận thay đổi của mẹ và bé sau buổi chăm sóc");
        }

        return activities;
    }

    private async Task<List<NurseDiscoveryDto>> GetRecommendedNursesAsync(int? serviceId, GeoPointDto? location, CancellationToken cancellationToken)
    {
        var nurses = await _nurseDiscoveryService.SearchAsync(
            serviceId,
            null,
            null,
            null,
            null,
            location?.Lat,
            location?.Lng,
            null,
            "bestMatch");

        return nurses.Take(5).ToList();
    }

    private async Task<AiText> TryWriteCarePlanSummaryAsync(
        HealthCheckIn? checkIn,
        IReadOnlyList<RecommendedCareServiceDto> services,
        IReadOnlyList<CarePlanTimelineItemDto> items,
        CancellationToken cancellationToken)
    {
        try
        {
            var promptData = new
            {
                checkIn = checkIn is null ? null : new
                {
                    checkIn.SleepHours,
                    checkIn.PainLevel,
                    checkIn.PainLocation,
                    checkIn.Mood,
                    checkIn.MilkStatus,
                    checkIn.BabyFeeding,
                    checkIn.BabySleep,
                    checkIn.Note
                },
                recommendedServices = services,
                planItems = items
            };

            var response = await _geminiService.GenerateAsync(new GeminiGenerateRequest
            {
                SystemInstruction = "Bạn là CareMate AI. Viết tiếng Việt thân thiện, không chẩn đoán, không kê đơn, không nhắc điểm rủi ro. Tối đa 120 từ.",
                Prompt = $"Tóm tắt lộ trình chăm sóc mẹ và bé theo JSON sau:\n{JsonSerializer.Serialize(promptData, JsonOptions)}",
                Temperature = 0.2,
                MaxOutputTokens = 300
            }, cancellationToken);

            var text = response.Text.Trim();
            return string.IsNullOrWhiteSpace(text)
                ? AiText.Fallback(BuildFallbackSummary(services, items))
                : new AiText(Limit(text, 700), response.Model, false);
        }
        catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException or TaskCanceledException or JsonException)
        {
            _logger.LogWarning(ex, "Gemini care plan summary failed. Falling back to deterministic care plan text.");
            return AiText.Fallback(BuildFallbackSummary(services, items));
        }
    }

    private static string BuildFallbackSummary(IReadOnlyList<RecommendedCareServiceDto> services, IReadOnlyList<CarePlanTimelineItemDto> items)
    {
        if (items.Count > 0)
        {
            return $"CareMate đã tạo lộ trình gồm {items.Count} buổi còn lại theo lịch chăm sóc của bạn. Hãy theo dõi tình trạng mẹ và bé trước mỗi buổi để y tá hỗ trợ sát nhu cầu hơn.";
        }

        if (services.Count > 0)
        {
            return "CareMate đề xuất một số gói/dịch vụ phù hợp với thông tin check-in hiện tại. Bạn có thể xem chi tiết dịch vụ và chọn y tá phù hợp gần khu vực của mình.";
        }

        return "CareMate đã ghi nhận check-in của bạn. Hãy tiếp tục theo dõi tình trạng mẹ và bé, và liên hệ nhân viên y tế nếu có dấu hiệu bất thường.";
    }

    private static string BuildUrgentSummary(SafetyEvaluationDto safety) =>
        safety.Notice ?? "Có dấu hiệu cần được nhân viên y tế đánh giá trực tiếp. Không nên tự xử lý tại nhà.";

    private async Task SupersedeOpenPlansAsync(int userId, int? bookingId, CancellationToken cancellationToken)
    {
        var openPlans = await _context.AiCarePlans
            .Where(x => x.UserId == userId && x.Status != "completed" && x.Status != "superseded" && (bookingId == null || x.BookingId == bookingId || x.BookingId == null))
            .ToListAsync(cancellationToken);

        foreach (var plan in openPlans)
        {
            plan.Status = "superseded";
            plan.UpdatedAt = DateTime.UtcNow;
        }
    }

    private static bool CanAccessBooking(int actorUserId, bool isAdmin, Booking booking) =>
        isAdmin || booking.CustomerId == actorUserId || booking.NurseId == actorUserId;

    private static CarePlanResponse Map(AiCarePlan plan) => new()
    {
        CarePlanId = plan.Id,
        PlanType = plan.PlanType,
        Status = plan.Status,
        SafetyLevel = plan.SafetyLevel,
        SafetyNotice = plan.SafetyNotice,
        Summary = plan.Summary,
        RecommendedServices = Deserialize<List<RecommendedCareServiceDto>>(plan.RecommendedServicesJson),
        PlanItems = Deserialize<List<CarePlanTimelineItemDto>>(plan.PlanItemsJson),
        RecommendedNurses = Deserialize<List<NurseDiscoveryDto>>(plan.RecommendedNursesJson),
        Disclaimer = plan.Disclaimer,
        AiModel = plan.AiModel,
        FallbackMode = plan.FallbackMode,
        CreatedAt = plan.CreatedAt
    };

    private static T Deserialize<T>(string json) where T : new()
    {
        try { return JsonSerializer.Deserialize<T>(json, JsonOptions) ?? new T(); }
        catch { return new T(); }
    }

    private static Dictionary<string, string> ReadDictionary(string json) => Deserialize<Dictionary<string, string>>(json);

    private static List<string> CleanList(IEnumerable<string>? values) =>
        values?.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Take(20).ToList() ?? [];

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string Limit(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength].TrimEnd() + "...";

    private sealed record AiText(string Summary, string? Model, bool FallbackMode)
    {
        public static AiText Fallback(string summary) => new(summary, null, true);
    }
}
