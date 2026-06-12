namespace MomCare.Dto;

public class AdminFinanceAnalyticsDto
{
    public decimal GrossRevenue { get; set; }
    public decimal RefundAmount { get; set; }
    public decimal PayoutAmount { get; set; }
    public decimal PlatformFeeAmount { get; set; }
    public int PaidPaymentCount { get; set; }
    public int RefundCount { get; set; }
    public int PendingPayoutCount { get; set; }
    public int FailedWebhookCount { get; set; }
    public decimal RefundRatePercent { get; set; }
    public decimal BookingCompletionRatePercent { get; set; }
    public List<FinanceDailyMetricDto> DailyMetrics { get; set; } = [];
    public List<NursePerformanceMetricDto> NursePerformance { get; set; } = [];
}

public class FinanceDailyMetricDto
{
    public DateTime Date { get; set; }
    public decimal Revenue { get; set; }
    public decimal Refunds { get; set; }
    public decimal Payouts { get; set; }
    public int BookingCount { get; set; }
}

public class NursePerformanceMetricDto
{
    public int NurseId { get; set; }
    public string NurseName { get; set; } = string.Empty;
    public int CompletedBookingCount { get; set; }
    public decimal Revenue { get; set; }
    public decimal PayoutAmount { get; set; }
}
