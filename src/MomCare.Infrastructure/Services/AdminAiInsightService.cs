using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MomCare.Data;
using MomCare.Dto;
using MomCare.Interfaces;
using MomCare.Models;

namespace MomCare.Services;

public class AdminAiInsightService : IAdminAiInsightService
{
    private const string Disclaimer = "CareMate AI cung cap thong tin tham khao cho van hanh, khong thay the danh gia chuyen mon y te hoac quyet dinh dieu phoI truc tiep.";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly MomCareContext _context;
    private readonly ILlmService _llmService;
    private readonly ILogger<AdminAiInsightService> _logger;

    public AdminAiInsightService(MomCareContext context, ILlmService llmService, ILogger<AdminAiInsightService> logger)
    {
        _context = context;
        _llmService = llmService;
        _logger = logger;
    }

    public async Task<ServiceResult<AdminAiInsightResponse>> GenerateAsync(AdminAiInsightRequest request, CancellationToken cancellationToken)
    {
        var normalizedUseCase = NormalizeUseCase(request.UseCase);
        var validationError = Validate(request, normalizedUseCase);
        if (validationError is not null)
        {
            return ServiceResult<AdminAiInsightResponse>.Fail(validationError);
        }

        var context = await BuildContextAsync(request, normalizedUseCase, cancellationToken);

        try
        {
            var response = await _llmService.GenerateAsync(new GeminiGenerateRequest
            {
                Prompt = request.Prompt.Trim(),
                SystemInstruction = BuildSystemInstruction(normalizedUseCase),
                Temperature = 0.2,
                MaxOutputTokens = 700,
                TimeoutSeconds = 25,
                BypassCache = true,
                CallType = "admin_ai_insight",
                PromptVersion = $"{normalizedUseCase}_v1",
                Contents =
                [
                    new GeminiContentDto
                    {
                        Role = "user",
                        Parts =
                        [
                            new GeminiPartDto
                            {
                                Text = BuildUserPrompt(request.Prompt, normalizedUseCase, context)
                            }
                        ]
                    }
                ]
            }, cancellationToken);

            var parsed = TryParseResponse(response.Text, normalizedUseCase);
            if (parsed is null)
            {
                _logger.LogWarning("Admin AI insight response could not be parsed for use case {UseCase}. Falling back.", normalizedUseCase);
                return ServiceResult<AdminAiInsightResponse>.Ok(BuildFallback(normalizedUseCase, request, context, response.Model));
            }

            parsed.UseCase = normalizedUseCase;
            parsed.AiModel = string.IsNullOrWhiteSpace(parsed.AiModel) ? response.Model : parsed.AiModel;
            parsed.Disclaimer = string.IsNullOrWhiteSpace(parsed.Disclaimer) ? Disclaimer : parsed.Disclaimer;
            parsed.CreatedAt = parsed.CreatedAt == default ? DateTime.UtcNow : parsed.CreatedAt;
            parsed.FallbackMode = false;
            NormalizeResponse(parsed);
            return ServiceResult<AdminAiInsightResponse>.Ok(parsed);
        }
        catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException or TaskCanceledException or JsonException)
        {
            _logger.LogWarning(ex, "Admin AI insight generation failed for use case {UseCase}.", normalizedUseCase);
            return ServiceResult<AdminAiInsightResponse>.Ok(BuildFallback(normalizedUseCase, request, context, null));
        }
    }

    private static string NormalizeUseCase(string? useCase)
    {
        var normalized = (useCase ?? string.Empty).Trim().ToLowerInvariant();
        return normalized;
    }

    private static string? Validate(AdminAiInsightRequest request, string useCase)
    {
        if (Array.IndexOf(AdminAiInsightUseCases.All, useCase) < 0)
        {
            return "Use case khong hop le.";
        }

        if (string.IsNullOrWhiteSpace(request.Prompt))
        {
            return "Prompt khong duoc de trong.";
        }

        return useCase switch
        {
            var value when value == AdminAiInsightUseCases.PersonalizedCarePlan &&
                request.CustomerId is null && request.BookingId is null && request.HealthCheckInId is null =>
                "Use case personalized_care_plan can it nhat customerId, bookingId hoac healthCheckInId.",
            var value when value == AdminAiInsightUseCases.HealthSummary &&
                request.CustomerId is null && request.HealthCheckInId is null =>
                "Use case health_summary can it nhat customerId hoac healthCheckInId.",
            _ => null
        };
    }

    private async Task<AdminInsightContext> BuildContextAsync(AdminAiInsightRequest request, string useCase, CancellationToken cancellationToken)
    {
        var context = new AdminInsightContext
        {
            UseCase = useCase,
            Prompt = request.Prompt.Trim(),
            DateRange = NormalizeDateRange(request.DateRange)
        };

        if (request.BookingId.HasValue)
        {
            context.Booking = await _context.Bookings
                .AsNoTracking()
                .Include(x => x.Customer)
                .Include(x => x.Nurse)
                .Include(x => x.Service)
                .Include(x => x.SessionLogs.OrderBy(s => s.SessionNumber))
                .Include(x => x.Review)
                .FirstOrDefaultAsync(x => x.Id == request.BookingId.Value, cancellationToken);
        }

        var resolvedCustomerId = request.CustomerId ?? context.Booking?.CustomerId;
        if (resolvedCustomerId.HasValue)
        {
            context.Customer = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == resolvedCustomerId.Value, cancellationToken);

            context.RecentCheckIns = await _context.HealthCheckIns
                .AsNoTracking()
                .Include(x => x.Analysis)
                .Where(x => x.UserId == resolvedCustomerId.Value)
                .OrderByDescending(x => x.CreatedAt)
                .Take(5)
                .ToListAsync(cancellationToken);

            context.CustomerBookings = await _context.Bookings
                .AsNoTracking()
                .Include(x => x.Service)
                .Include(x => x.Nurse)
                .Include(x => x.Review)
                .Where(x => x.CustomerId == resolvedCustomerId.Value)
                .OrderByDescending(x => x.StartTime)
                .Take(8)
                .ToListAsync(cancellationToken);
        }

        if (request.HealthCheckInId.HasValue)
        {
            context.SelectedCheckIn = await _context.HealthCheckIns
                .AsNoTracking()
                .Include(x => x.Analysis)
                .FirstOrDefaultAsync(x => x.Id == request.HealthCheckInId.Value, cancellationToken);
        }

        context.SelectedCheckIn ??= context.RecentCheckIns.FirstOrDefault();

        if (useCase == AdminAiInsightUseCases.ServiceOptimization)
        {
            var from = context.DateRange.From ?? DateTime.UtcNow.Date.AddDays(-30);
            var toExclusive = (context.DateRange.To ?? DateTime.UtcNow).AddDays(1);

            context.OperationalBookings = await _context.Bookings
                .AsNoTracking()
                .Include(x => x.Service)
                .Include(x => x.Nurse)
                .Include(x => x.Review)
                .Where(x => x.CreatedAt >= from && x.CreatedAt < toExclusive)
                .OrderByDescending(x => x.CreatedAt)
                .Take(200)
                .ToListAsync(cancellationToken);

            context.ActiveServices = await _context.Services
                .AsNoTracking()
                .Where(x => x.Status == "active")
                .OrderBy(x => x.Name)
                .Take(20)
                .ToListAsync(cancellationToken);
        }

        return context;
    }

    private static AdminAiInsightDateRangeDto NormalizeDateRange(AdminAiInsightDateRangeDto? value)
    {
        if (value is null)
        {
            return new AdminAiInsightDateRangeDto();
        }

        if (value.From.HasValue && value.To.HasValue && value.From > value.To)
        {
            return new AdminAiInsightDateRangeDto
            {
                From = value.To,
                To = value.From
            };
        }

        return value;
    }

    private static string BuildSystemInstruction(string useCase)
    {
        var scenario = useCase switch
        {
            var value when value == AdminAiInsightUseCases.PersonalizedCarePlan =>
                "Ban la tro ly admin CareMate. Hay de xuat lo trinh cham soc ca nhan hoa dua tren booking, health check-in va lich su cham soc.",
            var value when value == AdminAiInsightUseCases.HealthSummary =>
                "Ban la tro ly admin CareMate. Hay tom tat tinh hinh suc khoe me va be dua tren health check-in va lich su lien quan.",
            _ =>
                "Ban la tro ly admin CareMate. Hay phan tich du lieu van hanh de de xuat cach toi uu hoa dich vu."
        };

        return $$"""
{{scenario}}

Tra ve DUY NHAT JSON hop le, khong them markdown, khong them giai thich.
Schema JSON:
{
  "useCase": "string",
  "title": "string",
  "summary": "string",
  "insights": ["string"],
  "recommendedActions": [
    { "label": "string", "reason": "string", "priority": 1 }
  ],
  "metrics": [
    { "label": "string", "value": "string", "trend": "string|null", "note": "string|null" }
  ],
  "relatedEntities": [
    { "type": "customer|booking|health_checkin|service|nurse", "id": "string", "label": "string" }
  ],
  "disclaimer": "{{Disclaimer}}",
  "aiModel": null,
  "fallbackMode": false,
  "createdAt": "{{DateTime.UtcNow:O}}"
}

Yeu cau:
- Viet tieng Viet co dau, giong dieu ro rang, huu ich cho admin.
- Summary ngan gon 2-4 cau.
- Insights toi da 5 dong, uu tien co the hanh dong.
- RecommendedActions toi da 4 muc, priority tu 1 den 3.
- Metrics chi dua tren du lieu co trong context, khong tu bo sung so lieu.
- Neu du lieu thieu, phai noi ro han che trong summary hoac note.
""";
    }

    private static string BuildUserPrompt(string prompt, string useCase, AdminInsightContext context)
    {
        var serializedContext = JsonSerializer.Serialize(new
        {
            useCase,
            inputPrompt = prompt.Trim(),
            dateRange = context.DateRange,
            customer = context.Customer is null ? null : new
            {
                context.Customer.Id,
                context.Customer.FullName,
                context.Customer.Email,
                Phone = context.Customer.PhoneNumber,
                context.Customer.Status,
                context.Customer.CreatedAt
            },
            booking = context.Booking is null ? null : new
            {
                context.Booking.Id,
                context.Booking.Status,
                context.Booking.TotalPrice,
                context.Booking.Address,
                context.Booking.StartTime,
                context.Booking.EndTime,
                Service = context.Booking.Service is null ? null : new
                {
                    context.Booking.Service.Id,
                    context.Booking.Service.Name,
                    context.Booking.Service.Category,
                    context.Booking.Service.ServiceKind,
                    context.Booking.Service.PackageDays
                },
                Customer = context.Booking.Customer is null ? null : new
                {
                    context.Booking.Customer.Id,
                    context.Booking.Customer.FullName
                },
                Nurse = context.Booking.Nurse is null ? null : new
                {
                    context.Booking.Nurse.Id,
                    context.Booking.Nurse.FullName
                },
                Sessions = context.Booking.SessionLogs.Select(x => new
                {
                    x.Id,
                    x.SessionNumber,
                    x.SessionDate,
                    x.Status,
                    x.Title,
                    x.CustomerRating,
                    x.CustomerNote
                }).ToList(),
                Review = context.Booking.Review is null ? null : new
                {
                    context.Booking.Review.Rating,
                    context.Booking.Review.Comment,
                    context.Booking.Review.CreatedAt
                }
            },
            selectedCheckIn = context.SelectedCheckIn is null ? null : ProjectCheckIn(context.SelectedCheckIn),
            recentCheckIns = context.RecentCheckIns.Select(ProjectCheckIn).ToList(),
            customerBookings = context.CustomerBookings.Select(x => new
            {
                x.Id,
                x.Status,
                x.StartTime,
                x.TotalPrice,
                Service = x.Service is null ? null : x.Service.Name,
                Nurse = x.Nurse is null ? null : x.Nurse.FullName,
                Rating = x.Review?.Rating
            }).ToList(),
            operationalSnapshot = context.OperationalBookings.Count == 0 ? null : new
            {
                totalBookings = context.OperationalBookings.Count,
                statusBreakdown = context.OperationalBookings
                    .GroupBy(x => x.Status)
                    .Select(g => new { status = g.Key, count = g.Count() })
                    .OrderByDescending(x => x.count)
                    .ToList(),
                topServices = context.OperationalBookings
                    .Where(x => x.Service is not null)
                    .GroupBy(x => x.Service.Name)
                    .Select(g => new
                    {
                        name = g.Key,
                        count = g.Count(),
                        avgRating = g.Where(x => x.Review != null).Select(x => (double?)x.Review!.Rating).Average()
                    })
                    .OrderByDescending(x => x.count)
                    .Take(5)
                    .ToList(),
                topNursesByLoad = context.OperationalBookings
                    .Where(x => x.Nurse is not null)
                    .GroupBy(x => new { x.NurseId, x.Nurse.FullName })
                    .Select(g => new
                    {
                        g.Key.NurseId,
                        g.Key.FullName,
                        count = g.Count(),
                        avgRating = g.Where(x => x.Review != null).Select(x => (double?)x.Review!.Rating).Average()
                    })
                    .OrderByDescending(x => x.count)
                    .Take(5)
                    .ToList(),
                activeServices = context.ActiveServices.Select(x => new
                {
                    x.Id,
                    x.Name,
                    x.Category,
                    x.BasePrice,
                    x.ServiceKind
                }).ToList()
            }
        }, JsonOptions);

        return $"""
Phan tich yeu cau admin sau va dua ra insight co cau truc.

Yeu cau tu admin:
{prompt.Trim()}

Context he thong:
{serializedContext}
""";
    }

    private static object ProjectCheckIn(HealthCheckIn checkIn)
    {
        return new
        {
            checkIn.Id,
            checkIn.UserId,
            checkIn.CreatedAt,
            checkIn.SleepHours,
            checkIn.PainLevel,
            checkIn.PainLocation,
            checkIn.PainType,
            checkIn.PainDuration,
            checkIn.PainTrend,
            checkIn.MotherAge,
            checkIn.SystolicBloodPressure,
            checkIn.DiastolicBloodPressure,
            checkIn.TemperatureCelsius,
            checkIn.TookMedicationToday,
            checkIn.MedicationNote,
            checkIn.Mood,
            checkIn.MilkStatus,
            checkIn.BabyFeeding,
            checkIn.BabySleep,
            checkIn.Note,
            Analysis = checkIn.Analysis is null ? null : new
            {
                checkIn.Analysis.Summary,
                checkIn.Analysis.WarningLevel,
                checkIn.Analysis.RiskScore,
                checkIn.Analysis.ConfidenceScore,
                checkIn.Analysis.TrendSummary,
                checkIn.Analysis.UrgencyAction
            }
        };
    }

    private static AdminAiInsightResponse? TryParseResponse(string rawText, string useCase)
    {
        if (string.IsNullOrWhiteSpace(rawText))
        {
            return null;
        }

        var json = rawText.Trim();
        if (json.StartsWith("```", StringComparison.Ordinal))
        {
            var firstNewline = json.IndexOf('\n');
            if (firstNewline >= 0)
            {
                json = json[(firstNewline + 1)..];
            }

            var closingFence = json.LastIndexOf("```", StringComparison.Ordinal);
            if (closingFence >= 0)
            {
                json = json[..closingFence];
            }
        }

        var parsed = JsonSerializer.Deserialize<AdminAiInsightResponse>(json.Trim(), JsonOptions);
        if (parsed is null)
        {
            return null;
        }

        parsed.UseCase = string.IsNullOrWhiteSpace(parsed.UseCase) ? useCase : parsed.UseCase.Trim().ToLowerInvariant();
        return parsed;
    }

    private static void NormalizeResponse(AdminAiInsightResponse response)
    {
        response.Title = CleanText(response.Title, "Tong hop AI");
        response.Summary = CleanText(response.Summary, "Chua co du lieu du de tao tong hop.");
        response.Insights = response.Insights
            .Select(x => CleanText(x, string.Empty))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Take(5)
            .ToList();
        response.RecommendedActions = response.RecommendedActions
            .Where(x => !string.IsNullOrWhiteSpace(x.Label))
            .Select(x => new AdminAiInsightActionDto
            {
                Label = CleanText(x.Label, "Hanh dong"),
                Reason = CleanText(x.Reason, "Theo doi them du lieu lien quan."),
                Priority = Math.Clamp(x.Priority <= 0 ? 2 : x.Priority, 1, 3)
            })
            .Take(4)
            .ToList();
        response.Metrics = response.Metrics
            .Where(x => !string.IsNullOrWhiteSpace(x.Label))
            .Select(x => new AdminAiInsightMetricDto
            {
                Label = CleanText(x.Label, "Chi so"),
                Value = CleanText(x.Value, "N/A"),
                Trend = string.IsNullOrWhiteSpace(x.Trend) ? null : x.Trend.Trim(),
                Note = string.IsNullOrWhiteSpace(x.Note) ? null : x.Note.Trim()
            })
            .Take(6)
            .ToList();
        response.RelatedEntities = response.RelatedEntities
            .Where(x => !string.IsNullOrWhiteSpace(x.Type) && !string.IsNullOrWhiteSpace(x.Id))
            .Select(x => new AdminAiInsightEntityDto
            {
                Type = x.Type.Trim().ToLowerInvariant(),
                Id = x.Id.Trim(),
                Label = CleanText(x.Label, $"{x.Type} #{x.Id}")
            })
            .Take(8)
            .ToList();
    }

    private static string CleanText(string? value, string fallback)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? fallback : trimmed;
    }

    private static AdminAiInsightResponse BuildFallback(string useCase, AdminAiInsightRequest request, AdminInsightContext context, string? model)
    {
        var response = useCase switch
        {
            var value when value == AdminAiInsightUseCases.PersonalizedCarePlan => BuildCarePlanFallback(context),
            var value when value == AdminAiInsightUseCases.HealthSummary => BuildHealthSummaryFallback(context),
            _ => BuildServiceOptimizationFallback(context)
        };

        response.UseCase = useCase;
        response.AiModel = model ?? "rule_engine";
        response.FallbackMode = true;
        response.Disclaimer = Disclaimer;
        response.CreatedAt = DateTime.UtcNow;

        if (response.RelatedEntities.Count == 0)
        {
            if (request.CustomerId.HasValue)
            {
                response.RelatedEntities.Add(new AdminAiInsightEntityDto
                {
                    Type = "customer",
                    Id = request.CustomerId.Value.ToString(),
                    Label = $"Khach hang #{request.CustomerId.Value}"
                });
            }

            if (request.BookingId.HasValue)
            {
                response.RelatedEntities.Add(new AdminAiInsightEntityDto
                {
                    Type = "booking",
                    Id = request.BookingId.Value.ToString(),
                    Label = $"Booking #{request.BookingId.Value}"
                });
            }
        }

        return response;
    }

    private static AdminAiInsightResponse BuildCarePlanFallback(AdminInsightContext context)
    {
        var booking = context.Booking;
        var latestCheckIn = context.SelectedCheckIn;
        var sessionCount = booking?.SessionLogs.Count ?? 0;
        var completedSessions = booking?.SessionLogs.Count(x => x.Status == "completed") ?? 0;

        var insights = new List<string>();
        if (booking?.Service is not null)
        {
            insights.Add($"Dang theo doi goi {booking.Service.Name} voi trang thai booking {booking.Status}.");
        }
        if (latestCheckIn is not null)
        {
            insights.Add($"Check-in gan nhat ghi nhan muc dau {latestCheckIn.PainLevel}/10, tam trang {latestCheckIn.Mood}, tinh trang sua {latestCheckIn.MilkStatus}.");
        }
        if (sessionCount > 0)
        {
            insights.Add($"Tien do buoi cham soc: {completedSessions}/{sessionCount} buoi da hoan tat.");
        }
        if (insights.Count == 0)
        {
            insights.Add("Chua co du lieu booking hoac check-in du de ca nhan hoa lo trinh sau sinh.");
        }

        return new AdminAiInsightResponse
        {
            Title = "De xuat lo trinh cham soc",
            Summary = latestCheckIn is null && booking is null
                ? "He thong chua co du du lieu de tao lo trinh chi tiet. Nen bo sung booking hoac health check-in moi nhat."
                : "Day la goi y van hanh an toan de admin dieu chinh lo trinh cham soc cho me va be dua tren du lieu hien co.",
            Insights = insights,
            RecommendedActions =
            [
                new AdminAiInsightActionDto
                {
                    Label = "Rasoat muc tieu buoi tiep theo",
                    Reason = booking?.Service is not null ? $"Can doi chieu voi pham vi goi {booking.Service.Name}." : "Can chot ro muc tieu cham soc cho dot tiep theo.",
                    Priority = 1
                },
                new AdminAiInsightActionDto
                {
                    Label = "Cap nhat check-in moi nhat",
                    Reason = latestCheckIn is null ? "Hien chua co check-in de danh gia tinh trang me va be." : "Can bo sung them trieu chung neu tinh trang da thay doi.",
                    Priority = 1
                }
            ],
            Metrics =
            [
                new AdminAiInsightMetricDto { Label = "So booking da gan", Value = booking is null ? "0" : "1", Note = "Booking lien quan den de xuat hien tai." },
                new AdminAiInsightMetricDto { Label = "Tien do buoi", Value = sessionCount == 0 ? "N/A" : $"{completedSessions}/{sessionCount}", Note = "So buoi hoan tat tren tong lo trinh." },
                new AdminAiInsightMetricDto { Label = "Check-in gan nhat", Value = latestCheckIn?.CreatedAt.ToString("dd/MM/yyyy HH:mm") ?? "Chua co", Note = "Moc cap nhat suc khoe me va be gan nhat." }
            ],
            RelatedEntities = BuildRelatedEntities(context)
        };
    }

    private static AdminAiInsightResponse BuildHealthSummaryFallback(AdminInsightContext context)
    {
        var latestCheckIn = context.SelectedCheckIn;
        var trendSource = context.RecentCheckIns.OrderByDescending(x => x.CreatedAt).Take(3).ToList();
        var avgPain = trendSource.Count == 0 ? (double?)null : trendSource.Average(x => x.PainLevel);

        var insights = new List<string>();
        if (latestCheckIn is not null)
        {
            insights.Add($"Lan check-in gan nhat cho thay me dang o tam trang {latestCheckIn.Mood}, be {latestCheckIn.BabyFeeding.ToLowerInvariant()} va ngu {latestCheckIn.BabySleep.ToLowerInvariant()}.");
            if (latestCheckIn.Analysis is not null)
            {
                insights.Add($"AI health analysis gan nhat danh gia muc canh bao {latestCheckIn.Analysis.WarningLevel} voi risk score {latestCheckIn.Analysis.RiskScore}.");
            }
        }
        if (avgPain.HasValue)
        {
            insights.Add($"Muc dau trung binh tren 3 lan check-in gan nhat la {avgPain.Value:F1}/10.");
        }
        if (insights.Count == 0)
        {
            insights.Add("Chua co du lieu check-in de tom tat tinh hinh suc khoe me va be.");
        }

        return new AdminAiInsightResponse
        {
            Title = "Tom tat tinh hinh me va be",
            Summary = latestCheckIn is null
                ? "Chua co du lieu health check-in de tong hop tinh hinh me va be. Nen yeu cau cap nhat check-in moi."
                : "Ban tom tat nay giup admin nhanh chong nhan ra dau hieu can theo doi them trong hanh trinh cham soc.",
            Insights = insights,
            RecommendedActions =
            [
                new AdminAiInsightActionDto
                {
                    Label = "Theo doi check-in moi trong 24h",
                    Reason = "Can xac nhan xu huong trieu chung va cap nhat muc canh bao neu co.",
                    Priority = 1
                },
                new AdminAiInsightActionDto
                {
                    Label = "Lien he dieu duong phu trach neu co dau hieu bat thuong",
                    Reason = latestCheckIn?.Analysis?.UrgencyAction ?? "Can co danh gia truc tiep neu trieu chung tang len.",
                    Priority = 2
                }
            ],
            Metrics =
            [
                new AdminAiInsightMetricDto { Label = "So check-in da phan tich", Value = context.RecentCheckIns.Count.ToString(), Note = "Lay toi da 5 ban ghi gan nhat." },
                new AdminAiInsightMetricDto { Label = "Pain level TB", Value = avgPain.HasValue ? avgPain.Value.ToString("F1") : "N/A", Note = "Trung binh 3 check-in gan nhat." },
                new AdminAiInsightMetricDto { Label = "Canh bao hien tai", Value = latestCheckIn?.Analysis?.WarningLevel ?? "Chua co", Note = "Muc canh bao tu health analysis neu da co." }
            ],
            RelatedEntities = BuildRelatedEntities(context)
        };
    }

    private static AdminAiInsightResponse BuildServiceOptimizationFallback(AdminInsightContext context)
    {
        var bookings = context.OperationalBookings;
        var total = bookings.Count;
        var completed = bookings.Count(x => x.Status == "completed");
        var cancelled = bookings.Count(x => x.Status == "cancelled");
        var avgRating = bookings.Where(x => x.Review != null).Select(x => (double?)x.Review!.Rating).Average();
        var topService = bookings
            .Where(x => x.Service is not null)
            .GroupBy(x => x.Service.Name)
            .OrderByDescending(g => g.Count())
            .Select(g => $"{g.Key} ({g.Count()} booking)")
            .FirstOrDefault();

        var insights = new List<string>
        {
            total == 0
                ? "Chua co du lieu booking trong khoang thoi gian da chon."
                : $"Tong quan van hanh ghi nhan {total} booking, {completed} hoan tat va {cancelled} huy."
        };
        if (!string.IsNullOrWhiteSpace(topService))
        {
            insights.Add($"Dich vu noi bat theo luong booking la {topService}.");
        }
        if (avgRating.HasValue)
        {
            insights.Add($"Danh gia trung binh tu cac booking da review la {avgRating.Value:F1}/5.");
        }

        return new AdminAiInsightResponse
        {
            Title = "Goi y toi uu van hanh dich vu",
            Summary = total == 0
                ? "Khong co du lieu booking trong khoang thoi gian nay, nen insight van hanh dang o muc co ban."
                : "Ban tong hop nay uu tien cac chi so booking, chat luong phuc vu va phan bo tai de admin toi uu van hanh.",
            Insights = insights,
            RecommendedActions =
            [
                new AdminAiInsightActionDto
                {
                    Label = "Rasoat nhom booking huy hoac chua xac nhan",
                    Reason = "Day la nhom de gay roi mach doanh thu va trai nghiem khach hang.",
                    Priority = 1
                },
                new AdminAiInsightActionDto
                {
                    Label = "Dieu phoi lai tai nguyen o dich vu co nhu cau cao",
                    Reason = string.IsNullOrWhiteSpace(topService) ? "Can them du lieu de xac dinh nhom dich vu uu tien." : $"Nhu cau dang tap trung o {topService}.",
                    Priority = 2
                }
            ],
            Metrics =
            [
                new AdminAiInsightMetricDto { Label = "Tong booking", Value = total.ToString(), Note = "So booking trong khoang thoi gian duoc chon." },
                new AdminAiInsightMetricDto { Label = "Ty le hoan tat", Value = total == 0 ? "N/A" : $"{(completed * 100.0 / total):F0}%", Note = "Booking completed / tong booking." },
                new AdminAiInsightMetricDto { Label = "Danh gia TB", Value = avgRating.HasValue ? avgRating.Value.ToString("F1") : "N/A", Note = "Tinh tren cac booking co review." }
            ],
            RelatedEntities = BuildRelatedEntities(context)
        };
    }

    private static List<AdminAiInsightEntityDto> BuildRelatedEntities(AdminInsightContext context)
    {
        var entities = new List<AdminAiInsightEntityDto>();

        if (context.Customer is not null)
        {
            entities.Add(new AdminAiInsightEntityDto
            {
                Type = "customer",
                Id = context.Customer.Id.ToString(),
                Label = context.Customer.FullName
            });
        }

        if (context.Booking is not null)
        {
            entities.Add(new AdminAiInsightEntityDto
            {
                Type = "booking",
                Id = context.Booking.Id.ToString(),
                Label = $"{context.Booking.Service?.Name ?? "Booking"} - {context.Booking.Status}"
            });
        }

        if (context.SelectedCheckIn is not null)
        {
            entities.Add(new AdminAiInsightEntityDto
            {
                Type = "health_checkin",
                Id = context.SelectedCheckIn.Id.ToString(),
                Label = $"Check-in {context.SelectedCheckIn.CreatedAt:dd/MM/yyyy HH:mm}"
            });
        }

        if (context.Booking?.Nurse is not null)
        {
            entities.Add(new AdminAiInsightEntityDto
            {
                Type = "nurse",
                Id = context.Booking.Nurse.Id.ToString(),
                Label = context.Booking.Nurse.FullName
            });
        }

        if (context.Booking?.Service is not null)
        {
            entities.Add(new AdminAiInsightEntityDto
            {
                Type = "service",
                Id = context.Booking.Service.Id.ToString(),
                Label = context.Booking.Service.Name
            });
        }

        return entities;
    }

    private sealed class AdminInsightContext
    {
        public string UseCase { get; set; } = string.Empty;
        public string Prompt { get; set; } = string.Empty;
        public AdminAiInsightDateRangeDto DateRange { get; set; } = new();
        public ApplicationUser? Customer { get; set; }
        public Booking? Booking { get; set; }
        public HealthCheckIn? SelectedCheckIn { get; set; }
        public List<HealthCheckIn> RecentCheckIns { get; set; } = [];
        public List<Booking> CustomerBookings { get; set; } = [];
        public List<Booking> OperationalBookings { get; set; } = [];
        public List<Service> ActiveServices { get; set; } = [];
    }
}
