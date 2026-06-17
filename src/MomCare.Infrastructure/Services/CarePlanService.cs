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
    private readonly ILlmService _llmService;
    private readonly INurseDiscoveryService _nurseDiscoveryService;
    private readonly ILogger<CarePlanService> _logger;
    private readonly SymptomTagEngine _symptomTagEngine;
    private readonly ServiceMatcher _serviceMatcher;
    private readonly GeminiReasoningService _geminiReasoningService;
    private readonly PlanValidatorEngine _planValidatorEngine;
    private readonly UrgentResponseBuilder _urgentResponseBuilder;

    public CarePlanService(
        MomCareContext context,
        ILlmService llmService,
        INurseDiscoveryService nurseDiscoveryService,
        ILogger<CarePlanService> logger,
        SymptomTagEngine symptomTagEngine,
        ServiceMatcher serviceMatcher,
        GeminiReasoningService geminiReasoningService,
        PlanValidatorEngine planValidatorEngine,
        UrgentResponseBuilder urgentResponseBuilder)
    {
        _context = context;
        _llmService = llmService;
        _nurseDiscoveryService = nurseDiscoveryService;
        _logger = logger;
        _symptomTagEngine = symptomTagEngine;
        _serviceMatcher = serviceMatcher;
        _geminiReasoningService = geminiReasoningService;
        _planValidatorEngine = planValidatorEngine;
        _urgentResponseBuilder = urgentResponseBuilder;
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
            return await GenerateForBookingInternalAsync(userId, false, activeBooking.Id, checkIn, MapLocation(request.UserLocation), cancellationToken);
        }

        await SupersedeOpenPlansAsync(userId, null, cancellationToken);
        var safety = SafetyGuardrailEngine.Evaluate(checkIn);
        if (safety.SafetyLevel == "urgent")
        {
            var urgent = _urgentResponseBuilder.Build(safety);
            var urgentPlan = new AiCarePlan
            {
                Id = urgent.CarePlanId,
                UserId = userId,
                HealthCheckInId = checkIn.Id,
                Status = "urgent",
                PlanType = "recommend_package",
                SafetyLevel = "urgent",
                SafetyNotice = safety.Notice,
                Summary = urgent.Summary,
                RecommendedServicesJson = "[]",
                PlanItemsJson = "[]",
                RecommendedNursesJson = "[]",
                Disclaimer = Disclaimer,
                AiModel = "guardrail",
                FallbackMode = true,
                IsAiReasoned = false,
                SymptomTagsJson = "{}",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _context.AiCarePlans.Add(urgentPlan);
            await _context.SaveChangesAsync(cancellationToken);
            return ServiceResult<CarePlanResponse>.Ok(Map(urgentPlan));
        }

        // Run AI reasoning pipeline with rule-based matching as primary
        var tags = _symptomTagEngine.Extract(checkIn);
        var activeServices = await _context.Services
            .AsNoTracking()
            .Where(x => x.Status == "active")
            .ToListAsync(cancellationToken);

        var servicesForAi = activeServices.Select(x => new ServiceSummaryForAi
        {
            Id = x.Id.ToString(),
            Name = x.Name,
            ShortDescription = x.Description ?? "",
            Tags = string.IsNullOrWhiteSpace(x.Category) ? [] : [x.Category],
            Price = x.BasePrice,
            IsPackage = x.ServiceKind == "package"
        }).ToList();

        // Rule-based matching first
        var ruleMatches = _serviceMatcher.Match(tags, servicesForAi);

        // AI reasoning as enhancement
        var reasoningResult = await _geminiReasoningService.ReasonAsync(tags, servicesForAi, null, cancellationToken);
        var validatedResult = _planValidatorEngine.Validate(reasoningResult, servicesForAi);

        // Merge: prefer rule-based service IDs but use AI reasons when available
        var aiScores = validatedResult.ServiceScores.ToDictionary(x => x.ServiceId, x => x);
        var mergedServiceIds = ruleMatches
            .Select(m => m.ServiceId)
            .Concat(validatedResult.ServiceScores.Select(x => x.ServiceId))
            .Distinct()
            .Take(6)
            .ToList();

        var recommendedServices = mergedServiceIds.Select(id =>
        {
            var s = activeServices.FirstOrDefault(x => x.Id.ToString() == id);
            var ruleMatch = ruleMatches.FirstOrDefault(m => m.ServiceId == id);
            var aiScore = aiScores.GetValueOrDefault(id);
            return new RecommendedCareServiceDto
            {
                ServiceId = s?.Id ?? int.Parse(id),
                Name = s?.Name ?? "Dịch vụ đề xuất",
                Reason = aiScore?.Reason
                    ?? (ruleMatch.MatchedNeeds.Count > 0
                        ? $"Phù hợp với nhu cầu: {string.Join(", ", ruleMatch.MatchedNeeds)}"
                        : "Phù hợp với giai đoạn hậu sản của bạn."),
                SessionCount = s?.PackageDays,
                EstimatedPrice = s?.BasePrice ?? 0
            };
        }).ToList();

        var firstServiceId = recommendedServices.FirstOrDefault()?.ServiceId;
        var nurses = firstServiceId.HasValue
            ? await GetRecommendedNursesAsync(firstServiceId, MapLocation(request.UserLocation), cancellationToken)
            : [];

        var hasAiContent = validatedResult.IsFromAi && (validatedResult.ServiceScores.Count > 0 || !string.IsNullOrWhiteSpace(validatedResult.Reasoning));
        var summary = recommendedServices.Count > 0
            ? $"CareMate đề xuất {recommendedServices.Count} dịch vụ phù hợp với tình trạng của mẹ. "
                + (string.IsNullOrWhiteSpace(validatedResult.Reasoning) ? "" : validatedResult.Reasoning)
            : "Gợi ý chăm sóc từ CareMate AI.";

        var plan = new AiCarePlan
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            HealthCheckInId = checkIn.Id,
            Status = "active",
            PlanType = "recommend_package",
            SafetyLevel = safety.SafetyLevel,
            SafetyNotice = safety.Notice,
            Summary = summary.Trim(),
            RecommendedServicesJson = JsonSerializer.Serialize(recommendedServices, JsonOptions),
            PlanItemsJson = "[]",
            RecommendedNursesJson = JsonSerializer.Serialize(nurses, JsonOptions),
            Disclaimer = Disclaimer,
            AiModel = hasAiContent ? "gemini-2.0-flash" : "rule_engine",
            FallbackMode = !hasAiContent,
            IsAiReasoned = hasAiContent,
            SymptomTagsJson = JsonSerializer.Serialize(tags, JsonOptions),
            GeminiPromptVersion = GeminiReasoningService.PromptVersion,
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

        if (safety.SafetyLevel == "urgent")
        {
            var urgent = _urgentResponseBuilder.Build(safety);
            var urgentPlan = new AiCarePlan
            {
                Id = urgent.CarePlanId,
                UserId = booking.CustomerId,
                BookingId = booking.Id,
                HealthCheckInId = checkIn?.Id,
                Status = "urgent",
                PlanType = "by_booking",
                SafetyLevel = "urgent",
                SafetyNotice = safety.Notice,
                Summary = urgent.Summary,
                RecommendedServicesJson = "[]",
                PlanItemsJson = "[]",
                RecommendedNursesJson = "[]",
                Disclaimer = Disclaimer,
                AiModel = "guardrail",
                FallbackMode = true,
                IsAiReasoned = false,
                SymptomTagsJson = "{}",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _context.AiCarePlans.Add(urgentPlan);
            await _context.SaveChangesAsync(cancellationToken);
            return ServiceResult<CarePlanResponse>.Ok(Map(urgentPlan));
        }

        // Non-urgent, run AI pipeline
        var tags = _symptomTagEngine.Extract(checkIn);
        var activeServices = await _context.Services
            .AsNoTracking()
            .Where(x => x.Status == "active")
            .ToListAsync(cancellationToken);

        var servicesForAi = activeServices.Select(x => new ServiceSummaryForAi
        {
            Id = x.Id.ToString(),
            Name = x.Name,
            ShortDescription = x.Description ?? "",
            Tags = string.IsNullOrWhiteSpace(x.Category) ? [] : [x.Category],
            Price = x.BasePrice,
            IsPackage = x.ServiceKind == "package"
        }).ToList();

        var bookingContext = new BookingContextForAi
        {
            ServiceName = booking.Service.Name,
            RemainingSessionCount = booking.SessionLogs.Count(s => s.Status == "pending" || s.Status == "checked_in"),
            NextSessionDate = booking.SessionLogs
                .Where(s => s.Status == "pending" || s.Status == "checked_in")
                .OrderBy(s => s.SessionNumber)
                .Select(s => (DateTime?)s.SessionDate)
                .FirstOrDefault()
        };

        var reasoningResult = await _geminiReasoningService.ReasonAsync(tags, servicesForAi, bookingContext, cancellationToken);
        var validatedResult = _planValidatorEngine.Validate(reasoningResult, servicesForAi);

        var items = validatedResult.PlanItems.Select(pi =>
        {
            var matchedSession = booking.SessionLogs.FirstOrDefault(s => s.SessionNumber == pi.SessionNumber);
            return new CarePlanTimelineItemDto
            {
                SessionNumber = pi.SessionNumber,
                ScheduledDate = matchedSession?.SessionDate ?? DateTime.UtcNow.AddDays(pi.SessionNumber),
                Focus = pi.Focus,
                Activities = pi.Activities,
                Notes = pi.Note,
                DurationMinutes = pi.EstimatedDurationMinutes
            };
        }).ToList();

        if (!validatedResult.IsFromAi || items.Count == 0)
        {
            items = BuildPlanItems(booking);
        }

        var nurses = await GetRecommendedNursesAsync(booking.ServiceId, location, cancellationToken);

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
            Summary = string.IsNullOrWhiteSpace(validatedResult.Reasoning)
                ? $"CareMate đã tạo lộ trình gồm {items.Count} buổi còn lại theo lịch chăm sóc của bạn."
                : validatedResult.Reasoning,
            RecommendedServicesJson = "[]",
            PlanItemsJson = JsonSerializer.Serialize(items, JsonOptions),
            RecommendedNursesJson = JsonSerializer.Serialize(nurses, JsonOptions),
            Disclaimer = Disclaimer,
            AiModel = validatedResult.IsFromAi ? "gemini-2.0-flash-stable" : "rule_engine",
            FallbackMode = !validatedResult.IsFromAi,
            IsAiReasoned = validatedResult.IsFromAi,
            SymptomTagsJson = JsonSerializer.Serialize(tags, JsonOptions),
            GeminiPromptVersion = GeminiReasoningService.PromptVersion,
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

    private static string BuildUrgentSummary(SafetyEvaluationDto safety) =>
        safety.Notice ?? "Có dấu hiệu cần được nhân viên y tế đánh giá trực tiếp. Không nên tự xử lý tại nhà.";

    private async Task SupersedeOpenPlansAsync(int userId, int? bookingId, CancellationToken cancellationToken)
    {
        var openPlans = await _context.AiCarePlans
            .Where(x => x.UserId == userId && x.Status != "completed" && x.Status != "superseded" && (bookingId == null ? x.BookingId == null : x.BookingId == bookingId))
            .ToListAsync(cancellationToken);

        foreach (var plan in openPlans)
        {
            plan.Status = "superseded";
            plan.UpdatedAt = DateTime.UtcNow;
        }
    }

    private static GeoPointDto? MapLocation(UserLocationDto? userLocation)
    {
        if (userLocation?.Lat == null || userLocation?.Lng == null)
        {
            return null;
        }
        return new GeoPointDto
        {
            Lat = userLocation.Lat.Value,
            Lng = userLocation.Lng.Value
        };
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
        IsAiReasoned = plan.IsAiReasoned,
        UrgentActions = plan.SafetyLevel == "urgent"
            ? [
                new() { Priority = 1, Type = "call", Label = "Gọi hotline CareMate", Value = "1900-xxxx" },
                new() { Priority = 2, Type = "navigate", Label = "Tìm cơ sở y tế gần nhất", Value = "/find-clinic" },
                new() { Priority = 3, Type = "chat", Label = "Nhắn tin y tá trực", Value = "/chat/urgent" }
              ]
            : null,
        CreatedAt = plan.CreatedAt
    };

    private static T Deserialize<T>(string json) where T : new()
    {
        try { return JsonSerializer.Deserialize<T>(json, JsonOptions) ?? new T(); }
        catch { return new T(); }
    }

    private static List<string> CleanList(IEnumerable<string>? values) =>
        values?.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Take(20).ToList() ?? [];

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
