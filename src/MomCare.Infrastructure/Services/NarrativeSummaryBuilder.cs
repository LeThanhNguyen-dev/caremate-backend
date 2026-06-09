using MomCare.Dto;
using MomCare.Models;

namespace MomCare.Services;

public static class NarrativeSummaryBuilder
{
    public static string Build(HealthAnalysisResult result, HealthCheckIn currentCheckIn, List<HealthCheckIn> recentHistory)
    {
        var topFactor = result.RiskFactors.OrderByDescending(x => x.Points).FirstOrDefault();
        var trend = BuildTrendPhrase(result.TrendSignals);
        var coverageLabel = result.DataCoveragePercent >= 70 ? "cao" : result.DataCoveragePercent >= 40 ? "trung bình" : "thấp";
        var ppdPhrase = result.PpdScreeningScore >= 9
            ? $"Sàng lọc tâm lý ghi nhận mức {ToPpdLabel(result.PpdScreeningLevel).ToLowerInvariant()} với {result.PpdScreeningScore}/30 điểm."
            : "Sàng lọc tâm lý chưa ghi nhận tín hiệu nổi bật.";

        var opening = result.WarningLevel switch
        {
            "Emergency" => $"CareMate Engine ghi nhận điểm rủi ro {result.RiskScore}/100, thuộc nhóm đỏ khẩn cấp cần ưu tiên an toàn ngay.",
            "Red" => $"CareMate Engine ghi nhận điểm rủi ro {result.RiskScore}/100, thuộc ngưỡng đỏ cần được theo dõi sát trong ngày.",
            "Yellow" => $"CareMate Engine ghi nhận điểm rủi ro {result.RiskScore}/100, thuộc ngưỡng vàng và cần quan sát thêm.",
            _ => $"CareMate Engine ghi nhận điểm rủi ro {result.RiskScore}/100, hiện ở ngưỡng xanh."
        };

        var concern = topFactor is null
            ? "Chưa có yếu tố đơn lẻ nào nổi bật, nhưng hệ thống vẫn cần dữ liệu đều để theo dõi xu hướng."
            : $"Yếu tố cần chú ý nhất là {topFactor.Label.ToLowerInvariant()} thuộc nhóm {ToCategoryLabel(topFactor.Category)}.";

        var recommendation = result.Recommendations.Count > 0
            ? $"Khuyến nghị ưu tiên: {result.Recommendations[0]}"
            : $"Khuyến nghị ưu tiên: {result.UrgencyAction}";

        return string.Join(" ", new[]
        {
            opening,
            concern,
            trend,
            ppdPhrase,
            recommendation,
            $"Dữ liệu phân tích dựa trên {result.DataCoverageItems.Count}/20 chỉ số ({result.DataCoveragePercent}% - độ tin cậy {coverageLabel})."
        }.Where(x => !string.IsNullOrWhiteSpace(x)));
    }

    private static string BuildTrendPhrase(List<TrendSignalDto> signals)
    {
        var notable = signals.FirstOrDefault(x => x.Direction is "up" or "down");
        return notable is null
            ? "Các xu hướng gần đây chưa thay đổi rõ rệt."
            : $"Xu hướng đáng chú ý: {notable.Summary.ToLowerInvariant()}";
    }

    private static string ToPpdLabel(string level)
    {
        return level switch
        {
            "High" => "Cao",
            "Moderate" => "Trung bình",
            _ => "Thấp"
        };
    }

    private static string ToCategoryLabel(string category)
    {
        return category switch
        {
            "VitalSigns" => "chỉ số sinh tồn",
            "Pain" => "đau",
            "Baby" => "sơ sinh",
            "Mental" => "tâm lý",
            "Wound" => "vết mổ/vết khâu",
            "Feeding" => "cho bú",
            "Bleeding" => "sản dịch/ra máu",
            "Medication" => "thuốc",
            _ => "tổng quát"
        };
    }
}
