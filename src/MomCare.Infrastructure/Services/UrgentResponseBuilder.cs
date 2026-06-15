using MomCare.Dto;

namespace MomCare.Services;

/// <summary>
/// Builds a user-facing urgent care plan response after the safety guardrail flags a check-in.
/// </summary>
public class UrgentResponseBuilder
{
    /// <summary>
    /// Builds an urgent response with clear next actions and no service or session recommendations.
    /// </summary>
    public CarePlanResponse Build(SafetyEvaluationDto safetyResult)
    {
        var risk = safetyResult.Triggers.FirstOrDefault() ?? "dấu hiệu cần được nhân viên y tế đánh giá";
        return new CarePlanResponse
        {
            CarePlanId = Guid.NewGuid(),
            PlanType = "urgent",
            Status = "urgent",
            SafetyLevel = "urgent",
            SafetyNotice = safetyResult.Notice,
            Summary = $"CareMate nhận thấy {risk}. Vui lòng liên hệ y tế ngay. Đây không phải chẩn đoán y khoa.",
            RecommendedServices = [],
            PlanItems = [],
            RecommendedNurses = [],
            Disclaimer = "CareMate AI cung cấp thông tin tham khảo, không thay thế tư vấn, chẩn đoán hoặc điều trị từ bác sĩ.",
            FallbackMode = true,
            IsAiReasoned = false,
            UrgentActions =
            [
                new() { Priority = 1, Type = "call", Label = "Gọi hotline CareMate", Value = "1900-xxxx" },
                new() { Priority = 2, Type = "navigate", Label = "Tìm cơ sở y tế gần nhất", Value = "/find-clinic" },
                new() { Priority = 3, Type = "chat", Label = "Nhắn tin y tá trực", Value = "/chat/urgent" }
            ],
            CreatedAt = DateTime.UtcNow
        };
    }
}
