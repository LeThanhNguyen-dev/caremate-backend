using System.Text;
using System.Text.Json;
using MomCare.Dto;

namespace MomCare.Services;

/// <summary>
/// Builds versioned prompts for Gemini care plan reasoning.
/// </summary>
public class GeminiPromptBuilder
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Builds a prompt that asks Gemini to return service scores and structured plan items as JSON.
    /// </summary>
    public string BuildReasoningPrompt(SymptomTagResult tags, List<ServiceSummaryForAi> services, BookingContextForAi? booking)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Bạn là hệ thống gợi ý chăm sóc sau sinh của CareMate.");
        builder.AppendLine("KHÔNG chẩn đoán bệnh. KHÔNG kê đơn thuốc. Chỉ gợi ý dịch vụ chăm sóc.");
        builder.AppendLine();
        builder.AppendLine("## Thông tin khách hàng");
        builder.AppendLine($"- Giai đoạn hậu sản: {tags.PostpartumStage}");
        builder.AppendLine($"- Kiểu sinh: {tags.DeliveryType}");
        builder.AppendLine($"- Tags triệu chứng: {string.Join(", ", tags.Tags)}");
        builder.AppendLine($"- Nhu cầu chính: {string.Join(", ", tags.PrimaryNeeds)}");
        builder.AppendLine();
        builder.AppendLine("## Dịch vụ khả dụng");
        builder.AppendLine(JsonSerializer.Serialize(services, JsonOptions));

        if (booking is not null)
        {
            builder.AppendLine();
            builder.AppendLine("## Booking hiện tại");
            builder.AppendLine($"- Gói: {booking.ServiceName}");
            builder.AppendLine($"- Còn {booking.RemainingSessionCount} buổi");
            builder.AppendLine($"- Buổi tiếp theo: {booking.NextSessionDate:dd/MM/yyyy}");
        }

        builder.AppendLine();
        builder.AppendLine("## Yêu cầu");
        builder.AppendLine("Trả về JSON hợp lệ theo schema sau, KHÔNG có markdown, KHÔNG có text ngoài JSON:");
        builder.AppendLine("""
{
  "serviceScores": [
    {
      "serviceId": "1",
      "score": 85,
      "reason": "Lý do cụ thể 1-2 câu tiếng Việt tự nhiên theo triệu chứng của khách",
      "matchedNeeds": ["need_tag"]
    }
  ],
  "planItems": [
    {
      "sessionNumber": 1,
      "suggestedDate": "D+1",
      "focus": "Tiêu đề buổi",
      "activities": ["Hoạt động 1", "Hoạt động 2"],
      "note": "Lưu ý",
      "estimatedDurationMinutes": 90
    }
  ],
  "reasoning": "Tóm tắt 2-3 câu tại sao plan này phù hợp"
}
""");
        builder.AppendLine();
        builder.AppendLine("## Ví dụ output tốt");
        builder.AppendLine("""
{
  "serviceScores": [
    {
      "serviceId": "1",
      "score": 92,
      "reason": "Khách sinh mổ ngày 3, vết mổ cần được theo dõi và chăm sóc đúng cách để tránh nhiễm trùng.",
      "matchedNeeds": ["cham_soc_vet_mo"]
    }
  ],
  "planItems": [
    {
      "sessionNumber": 1,
      "suggestedDate": "D+1",
      "focus": "Chăm sóc vết mổ và hỗ trợ tắm bé",
      "activities": ["Kiểm tra và vệ sinh vết mổ", "Hướng dẫn tư thế cho bú đúng", "Tắm bé lần đầu"],
      "note": "Ưu tiên vết mổ trước khi làm bất kỳ việc nào khác",
      "estimatedDurationMinutes": 90
    }
  ],
  "reasoning": "Khách sinh mổ ngày 3 với triệu chứng đau vừa và sữa ít. Plan ưu tiên vết mổ và hỗ trợ cho bú trong tuần đầu."
}
""");

        return builder.ToString();
    }
}
