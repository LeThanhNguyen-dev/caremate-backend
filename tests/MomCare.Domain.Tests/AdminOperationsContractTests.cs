using MomCare.Dto;

namespace MomCare.Domain.Tests;

public class AdminOperationsContractTests
{
    [Fact]
    public void AdminFinanceAnalyticsDto_ShouldInitializeCollections()
    {
        var dto = new AdminFinanceAnalyticsDto();

        Assert.NotNull(dto.DailyMetrics);
        Assert.NotNull(dto.NursePerformance);
        Assert.Empty(dto.DailyMetrics);
        Assert.Empty(dto.NursePerformance);
    }

    [Fact]
    public void TransactionHistoryItemDto_ShouldExposeOperationalFields()
    {
        var dto = new TransactionHistoryItemDto
        {
            Id = "payment-1",
            Type = "payment",
            BookingId = 10,
            Amount = 250000m,
            Status = "paid",
            CreatedAt = DateTime.UtcNow
        };

        Assert.Equal("payment-1", dto.Id);
        Assert.Equal("payment", dto.Type);
        Assert.Equal(10, dto.BookingId);
        Assert.Equal(250000m, dto.Amount);
        Assert.Equal("paid", dto.Status);
    }

    [Fact]
    public void AuditLogDto_ShouldExposeActorAndRequestMetadata()
    {
        var dto = new AuditLogDto
        {
            ActorUserId = 7,
            Method = "POST",
            Path = "/api/admin/refunds/1/complete",
            StatusCode = 200
        };

        Assert.Equal(7, dto.ActorUserId);
        Assert.Equal("POST", dto.Method);
        Assert.StartsWith("/api/admin", dto.Path);
        Assert.Equal(200, dto.StatusCode);
    }

    [Fact]
    public void PayOsWebhookLogDto_ShouldTrackRetryAndProcessingState()
    {
        var dto = new PayOsWebhookLogDto
        {
            Id = Guid.NewGuid(),
            OrderCode = "123",
            IsVerified = true,
            IsProcessed = false,
            RetryCount = 2,
            ProcessingError = "Payment not found"
        };

        Assert.True(dto.IsVerified);
        Assert.False(dto.IsProcessed);
        Assert.Equal(2, dto.RetryCount);
        Assert.Contains("Payment", dto.ProcessingError);
    }
}
