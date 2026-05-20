using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MomCare.Data;
using MomCare.Infrastructure.Configurations;
using MomCare.Interfaces;
using MomCare.Repositories;
using MomCare.Services;

namespace MomCare.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<PayOSOptions>(configuration.GetSection(PayOSOptions.SectionName));
        services.AddScoped<IJwtService, JwtService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<INurseService, NurseService>();
        services.AddScoped<IAdminService, AdminService>();
        services.AddScoped<IServiceCatalogService, ServiceCatalogService>();
        services.AddScoped<INurseDiscoveryService, NurseDiscoveryService>();
        services.AddScoped<IAvailabilityService, AvailabilityService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<INotificationQueryService, NotificationQueryService>();
        services.AddScoped<IBookingService, BookingService>();
        services.AddScoped<IReviewService, ReviewService>();
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<IChatService, ChatService>();
        services.AddScoped<IDisputeService, DisputeService>();
        services.AddScoped<INurseServiceManagementService, NurseServiceManagementService>();
        services.AddScoped<ICloudinaryService, CloudinaryService>();
        services.AddScoped<IHealthCheckInService, HealthCheckInService>();
        services.AddScoped<IPackageSessionService, PackageSessionService>();
        services.AddHttpClient<IOpenAiHealthAnalysisService, OpenAiHealthAnalysisService>();

        services.AddDbContext<MomCareContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

        services.AddIdentityCore<MomCare.Models.ApplicationUser>(options =>
        {
            options.User.RequireUniqueEmail = true;
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = false;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequiredLength = 6;
        })
        .AddRoles<MomCare.Models.ApplicationRole>()
        .AddEntityFrameworkStores<MomCareContext>()
        .AddDefaultTokenProviders();

        return services;
    }
}
