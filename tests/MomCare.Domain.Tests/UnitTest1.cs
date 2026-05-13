using MomCare.Enums;

namespace MomCare.Domain.Tests;

public class DomainConstantsTests
{
    [Fact]
    public void BookingStatuses_ShouldBeUnique_AndLowercase()
    {
        var statuses = new[]
        {
            BookingStatuses.PendingConfirm,
            BookingStatuses.Confirmed,
            BookingStatuses.InProgress,
            BookingStatuses.Completed,
            BookingStatuses.Cancelled,
            BookingStatuses.Rejected
        };

        Assert.Equal(statuses.Length, statuses.Distinct().Count());
        Assert.All(statuses, status => Assert.Equal(status.ToLowerInvariant(), status));
    }

    [Fact]
    public void PaymentStatuses_ShouldBeUnique_AndLowercase()
    {
        var statuses = new[]
        {
            PaymentStatuses.Initiated,
            PaymentStatuses.Paid,
            PaymentStatuses.Refunded,
            PaymentStatuses.Failed
        };

        Assert.Equal(statuses.Length, statuses.Distinct().Count());
        Assert.All(statuses, status => Assert.Equal(status.ToLowerInvariant(), status));
    }

    [Fact]
    public void AppRoles_ShouldContainRequiredRoles()
    {
        var roles = new[]
        {
            AppRoles.Customer,
            AppRoles.Nurse,
            AppRoles.NurseUnconfirmed,
            AppRoles.NurseConfirmed,
            AppRoles.Admin
        };

        Assert.Equal(roles.Length, roles.Distinct().Count());
        Assert.Contains(AppRoles.Admin, roles);
        Assert.Contains(AppRoles.Customer, roles);
        Assert.All(roles, role => Assert.Equal(role.ToLowerInvariant(), role));
    }
}
