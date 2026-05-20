using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MomCare.Enums;
using MomCare.Models;

namespace MomCare.Data;

public static class MomCareSeedData
{
    private const string DefaultPassword = "MomCare@123";

    public static async Task SeedAsync(IServiceProvider services)
    {
        var context = services.GetRequiredService<MomCareContext>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = services.GetRequiredService<RoleManager<ApplicationRole>>();
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("MomCareSeedData");

        await context.Database.MigrateAsync();

        await EnsureRolesAsync(roleManager);

        var admin = await EnsureUserAsync(userManager, "admin@momcare.local", "System Admin", "0900000001", [AppRoles.Admin]);
        var customerA = await EnsureUserAsync(userManager, "lan.customer@momcare.local", "Lan Nguyen", "0900000002", [AppRoles.Customer]);
        var customerB = await EnsureUserAsync(userManager, "thu.customer@momcare.local", "Thu Tran", "0900000003", [AppRoles.Customer]);

        var nurseA = await EnsureUserAsync(userManager, "huong.nurse@momcare.local", "Huong Le", "0900000004", [AppRoles.NurseConfirmed]);
        var nurseB = await EnsureUserAsync(userManager, "mai.nurse@momcare.local", "Mai Pham", "0900000005", [AppRoles.NurseConfirmed]);
        var nursePending = await EnsureUserAsync(userManager, "pending.nurse@momcare.local", "Ngoc Do", "0900000006", [AppRoles.NurseUnconfirmed]);

        await context.SaveChangesAsync();

        await EnsureAddressAsync(context, customerA.Id, "25 Nguyen Hue, District 1, HCMC", "Ben Nghe", "District 1", true, "customer_home");
        await EnsureAddressAsync(context, customerB.Id, "120 Vo Van Tan, District 3, HCMC", "Ward 6", "District 3", true, "customer_home");
        await EnsureAddressAsync(context, nurseA.Id, "5 Le Van Sy, Phu Nhuan, HCMC", "Ward 12", "Phu Nhuan", true, "nurse_base");
        await EnsureAddressAsync(context, nurseB.Id, "88 Dien Bien Phu, Binh Thanh, HCMC", "Ward 15", "Binh Thanh", true, "nurse_base");

        var babyBath = await EnsureServiceAsync(context, "Tắm bé", "Hỗ trợ tắm bé, vệ sinh cơ bản và hướng dẫn gia đình chăm bé an toàn.", "cham-be-so-sinh", 320_000m, 45, "active", "single");
        var motherHealth = await EnsureServiceAsync(context, "Theo dõi sức khỏe mẹ", "Theo dõi hồi phục, dấu hiệu bất thường và nhu cầu nghỉ ngơi của mẹ sau sinh.", "cham-me-sau-sinh", 500_000m, 60, "active", "single");
        var babyHealth = await EnsureServiceAsync(context, "Theo dõi sức khỏe bé", "Theo dõi nhịp sinh hoạt, thân nhiệt, bú ngủ và các dấu hiệu cần lưu ý của bé.", "cham-be-so-sinh", 450_000m, 60, "active", "single");
        var lactation = await EnsureServiceAsync(context, "Hỗ trợ cho bú", "Tư vấn tư thế bú, lịch bú và hỗ trợ các vấn đề thường gặp khi cho bé bú.", "tu-van-tai-nha", 600_000m, 90, "active", "single");
        var massage = await EnsureServiceAsync(context, "Massage sau sinh", "Liệu trình massage hỗ trợ mẹ thư giãn và phục hồi sau sinh.", "phuc-hoi-suc-khoe", 700_000m, 90, "active", "single");
        var nutrition = await EnsureServiceAsync(context, "Tư vấn dinh dưỡng", "Gợi ý chế độ ăn phù hợp cho mẹ sau sinh và trong giai đoạn cho con bú.", "tu-van-tai-nha", 550_000m, 60, "active", "single");
        var nightCare = await EnsureServiceAsync(context, "Chăm bé ban đêm", "Điều dưỡng hỗ trợ chăm bé vào ban đêm để mẹ có thêm thời gian nghỉ ngơi.", "cham-be-so-sinh", 900_000m, 480, "active", "single");
        var houseSupport = await EnsureServiceAsync(context, "Hỗ trợ việc nhà", "Hỗ trợ các việc nhẹ trong không gian chăm sóc mẹ và bé để gia đình giảm tải.", "ho-tro-gia-dinh", 400_000m, 120, "active", "single");
        var mentalWellness = await EnsureServiceAsync(context, "Hỗ trợ tâm lý", "Lắng nghe, đồng hành và hỗ trợ mẹ giảm căng thẳng trong giai đoạn đầu sau sinh.", "ho-tro-tinh-than", 500_000m, 60, "active", "single");
        var emergencyConsultation = await EnsureServiceAsync(context, "Tư vấn khẩn", "Tư vấn nhanh khi gia đình cần định hướng xử lý tình huống chăm sóc mẹ và bé.", "tu-van-tai-nha", 300_000m, 30, "active", "single");
        var miniConsultation = await EnsureServiceAsync(context, "Gói Mini Tư Vấn Nhanh", "Buổi tư vấn ngắn 15 phút để gia đình hỏi nhanh về các lưu ý cơ bản khi chăm mẹ và bé tại nhà.", "tu-van-tai-nha", 29_000m, 15, "active", "single");

        var pkgThongTuyenSua = await EnsureServiceAsync(context, "Gói Chăm Sóc Thông Tuyến Sữa", "Gói 3 buổi hỗ trợ thông tuyến sữa, tư vấn tư thế cho bú và xử lý tắc sữa.", "goi-dich-vu", 2_400_000m, 60, "active", "package", 3, "breastfeeding-support", GeneratePackageScheduleJson(3, "breastfeeding-support"));
        var pkgGiamNhucMoi = await EnsureServiceAsync(context, "Gói Chăm Sóc Bầu Giảm Nhức Mỏi", "Gói 6 buổi massage giảm nhức mỏi và theo dõi sức khỏe mẹ sau sinh.", "goi-dich-vu", 2_500_000m, 60, "active", "package", 6, "postpartum-massage,mother-health-monitoring", GeneratePackageScheduleJson(6, "postpartum-massage,mother-health-monitoring"));
        var pkgTreSoSinh = await EnsureServiceAsync(context, "Gói Chăm Sóc Trẻ Sơ Sinh", "Gói 7 buổi chăm sóc toàn diện cho trẻ sơ sinh: tắm bé, theo dõi sức khỏe bé.", "goi-dich-vu", 8_300_000m, 90, "active", "package", 7, "baby-bathing,baby-health-monitoring", GeneratePackageScheduleJson(7, "baby-bathing,baby-health-monitoring"));
        var pkgMassageTamBe = await EnsureServiceAsync(context, "Gói Massage & Tắm Bé", "Gói 10 buổi kết hợp massage sau sinh cho mẹ và tắm bé.", "goi-dich-vu", 3_200_000m, 90, "active", "package", 10, "postpartum-massage,baby-bathing", GeneratePackageScheduleJson(10, "postpartum-massage,baby-bathing"));
        var pkgPhucHoi = await EnsureServiceAsync(context, "Gói Chăm Sóc Phục Hồi", "Gói 12 buổi phục hồi sức khỏe mẹ sau sinh: massage, dinh dưỡng và theo dõi sức khỏe.", "goi-dich-vu", 8_900_000m, 90, "active", "package", 12, "postpartum-massage,nutrition-consultation,mother-health-monitoring", GeneratePackageScheduleJson(12, "postpartum-massage,nutrition-consultation,mother-health-monitoring"));
        var pkgVipSauSinh = await EnsureServiceAsync(context, "Gói Chăm Sóc VIP Sau Sinh", "Gói 15 buổi chăm sóc cao cấp: massage, tắm bé, theo dõi mẹ & bé, tư vấn dinh dưỡng và hỗ trợ tâm lý.", "goi-dich-vu", 16_700_000m, 120, "active", "package", 15, "postpartum-massage,baby-bathing,mother-health-monitoring,baby-health-monitoring,nutrition-consultation,mental-wellness", GeneratePackageScheduleJson(15, "postpartum-massage,baby-bathing,mother-health-monitoring,baby-health-monitoring,nutrition-consultation,mental-wellness"));
        var pkgChuyenSau = await EnsureServiceAsync(context, "Gói Chăm Sóc Chuyên Sâu", "Gói 18 buổi chăm sóc chuyên sâu toàn diện cho mẹ và bé sau sinh.", "goi-dich-vu", 15_800_000m, 120, "active", "package", 18, "postpartum-massage,baby-bathing,mother-health-monitoring,baby-health-monitoring,nutrition-consultation,breastfeeding-support", GeneratePackageScheduleJson(18, "postpartum-massage,baby-bathing,mother-health-monitoring,baby-health-monitoring,nutrition-consultation,breastfeeding-support"));
        var pkgChuyenSauBung = await EnsureServiceAsync(context, "Gói Chăm Sóc Chuyên Sâu Bụng & Chăm Sóc Da", "Gói 20 buổi chuyên sâu phục hồi vùng bụng và chăm sóc da cho mẹ sau sinh.", "goi-dich-vu", 17_800_000m, 90, "active", "package", 20, "postpartum-massage,mother-health-monitoring", GeneratePackageScheduleJson(20, "postpartum-massage,mother-health-monitoring"));
        await DeactivateLegacyServicesAsync(context);

        await context.SaveChangesAsync();

        var nurseProfileA = await EnsureNurseProfileAsync(
            context,
            nurseA.Id,
            "7 years in maternity ward and home care.",
            "Postpartum, Newborn",
            "BSc Nursing; Neonatal Care Certificate",
            7,
            15,
            4.8m,
            true,
            "verified");

        var nurseProfileB = await EnsureNurseProfileAsync(
            context,
            nurseB.Id,
            "5 years of infant and overnight care.",
            "Infant Night Care",
            "RN License; Infant CPR",
            5,
            12,
            4.6m,
            true,
            "verified");

        var nurseProfilePending = await EnsureNurseProfileAsync(
            context,
            nursePending.Id,
            "New caregiver pending document verification.",
            "Postpartum",
            "RN Candidate",
            2,
            10,
            0m,
            true,
            "unverified");

        // Persist nurse profiles first so dependent rows use valid nurse_profile_id values.
        await context.SaveChangesAsync();

        await EnsureDocumentAsync(context, nurseProfileA.Id, DocumentTypes.IdCardFront, "seed_huong_id", "approved");
        await EnsureDocumentAsync(context, nurseProfileA.Id, DocumentTypes.Certificate, "seed_huong_hospital", "approved");
        await EnsureDocumentAsync(context, nurseProfileB.Id, DocumentTypes.IdCardFront, "seed_mai_id", "approved");
        await EnsureDocumentAsync(context, nurseProfilePending.Id, DocumentTypes.IdCardFront, "seed_ngoc_id", "pending_review");

        await EnsureNurseServiceAsync(context, nurseProfileA.Id, motherHealth.Id, 550_000m, "fixed", "enabled");
        await EnsureNurseServiceAsync(context, nurseProfileA.Id, babyHealth.Id, 500_000m, "fixed", "enabled");
        await EnsureNurseServiceAsync(context, nurseProfileA.Id, lactation.Id, 650_000m, "fixed", "enabled");
        await EnsureNurseServiceAsync(context, nurseProfileA.Id, massage.Id, 700_000m, "fixed", "enabled");
        await EnsureNurseServiceAsync(context, nurseProfileA.Id, nutrition.Id, 580_000m, "fixed", "enabled");
        await EnsureNurseServiceAsync(context, nurseProfileA.Id, emergencyConsultation.Id, 320_000m, "fixed", "enabled");
        await EnsureNurseServiceAsync(context, nurseProfileA.Id, miniConsultation.Id, 29_000m, "fixed", "enabled");
        await EnsureNurseServiceAsync(context, nurseProfileA.Id, pkgThongTuyenSua.Id, 2_500_000m, "fixed", "enabled");
        await EnsureNurseServiceAsync(context, nurseProfileA.Id, pkgGiamNhucMoi.Id, 2_700_000m, "fixed", "enabled");
        await EnsureNurseServiceAsync(context, nurseProfileA.Id, pkgPhucHoi.Id, 9_200_000m, "fixed", "enabled");
        await EnsureNurseServiceAsync(context, nurseProfileA.Id, pkgChuyenSau.Id, 16_000_000m, "fixed", "enabled");
        await EnsureNurseServiceAsync(context, nurseProfileB.Id, babyHealth.Id, 470_000m, "fixed", "enabled");
        await EnsureNurseServiceAsync(context, nurseProfileB.Id, nightCare.Id, 250_000m, "hourly", "enabled");
        await EnsureNurseServiceAsync(context, nurseProfileB.Id, massage.Id, 750_000m, "fixed", "enabled");
        await EnsureNurseServiceAsync(context, nurseProfileB.Id, babyBath.Id, 350_000m, "fixed", "enabled");
        await EnsureNurseServiceAsync(context, nurseProfileB.Id, mentalWellness.Id, 520_000m, "fixed", "enabled");
        await EnsureNurseServiceAsync(context, nurseProfileB.Id, houseSupport.Id, 420_000m, "fixed", "enabled");
        await EnsureNurseServiceAsync(context, nurseProfileB.Id, miniConsultation.Id, 29_000m, "fixed", "enabled");
        await EnsureNurseServiceAsync(context, nurseProfileB.Id, pkgMassageTamBe.Id, 3_400_000m, "fixed", "enabled");
        await EnsureNurseServiceAsync(context, nurseProfileB.Id, pkgTreSoSinh.Id, 8_500_000m, "fixed", "enabled");
        await EnsureNurseServiceAsync(context, nurseProfileB.Id, pkgVipSauSinh.Id, 17_000_000m, "fixed", "enabled");
        await EnsureNurseServiceAsync(context, nurseProfileB.Id, pkgChuyenSauBung.Id, 18_000_000m, "fixed", "enabled");

        // Use a stable reference date so re-running seed doesn't create duplicate slots.
        // "today" is always the current UTC date; past slots are fixed, future slots shift forward.
        var today = DateTime.UtcNow.Date;

        // Clean up old seed availability slots (past) to avoid unbounded growth.
        var oldSlots = context.AvailabilitySlots
            .Where(s => s.EndTime < today && !context.Bookings.Any(b => b.AvailabilitySlotId == s.Id))
            .ToList();
        if (oldSlots.Count > 0)
        {
            context.AvailabilitySlots.RemoveRange(oldSlots);
        }

        // Nurse A: one past + two future
        await EnsureAvailabilitySlotAsync(context, nurseProfileA.Id, today.AddDays(-2).AddHours(8), today.AddDays(-2).AddHours(12));
        await EnsureAvailabilitySlotAsync(context, nurseProfileA.Id, today.AddDays(1).AddHours(8), today.AddDays(1).AddHours(12)); 
        await EnsureAvailabilitySlotAsync(context, nurseProfileA.Id, today.AddDays(2).AddHours(13), today.AddDays(2).AddHours(17));

        // Nurse B: one past + two future
        await EnsureAvailabilitySlotAsync(context, nurseProfileB.Id, today.AddDays(-1).AddHours(20), today.AddDays(0).AddHours(4));
        await EnsureAvailabilitySlotAsync(context, nurseProfileB.Id, today.AddDays(1).AddHours(20), today.AddDays(2).AddHours(4));
        await EnsureAvailabilitySlotAsync(context, nurseProfileB.Id, today.AddDays(3).AddHours(8), today.AddDays(3).AddHours(12));

        await context.SaveChangesAsync();

        var completedBooking = await EnsureBookingAsync(
            context,
            "seed:booking:completed",
            customerA.Id,
            nurseA.Id,
            motherHealth.Id,
            BookingStatuses.Completed,
            550_000m,
            "25 Nguyen Hue, District 1, HCMC",
            today.AddDays(-2).AddHours(9),
            today.AddDays(-2).AddHours(11));

        var pendingBooking = await EnsureBookingAsync(
            context,
            "seed:booking:pending",
            customerB.Id,
            nurseA.Id,
            babyHealth.Id,
            BookingStatuses.PendingConfirm,
            500_000m,
            "120 Vo Van Tan, District 3, HCMC",
            today.AddDays(1).AddHours(9),
            today.AddDays(1).AddHours(11));

        var inProgressBooking = await EnsureBookingAsync(
            context,
            "seed:booking:inprogress",
            customerA.Id,
            nurseB.Id,
            nightCare.Id,
            BookingStatuses.InProgress,
            2_000_000m,
            "25 Nguyen Hue, District 1, HCMC",
            today.AddDays(-1).AddHours(21),
            today.AddDays(0).AddHours(3));

        await context.SaveChangesAsync();

        await EnsureBookingStatusHistoryAsync(context, completedBooking.Id, BookingStatuses.PendingConfirm, customerA.Id, "seed-created", today.AddDays(-3).AddHours(8));
        await EnsureBookingStatusHistoryAsync(context, completedBooking.Id, BookingStatuses.Confirmed, nurseA.Id, "seed-confirmed", today.AddDays(-3).AddHours(9));
        await EnsureBookingStatusHistoryAsync(context, completedBooking.Id, BookingStatuses.InProgress, nurseA.Id, "seed-started", today.AddDays(-2).AddHours(9));
        await EnsureBookingStatusHistoryAsync(context, completedBooking.Id, BookingStatuses.Completed, nurseA.Id, "seed-completed", today.AddDays(-2).AddHours(11));

        await EnsureBookingStatusHistoryAsync(context, pendingBooking.Id, BookingStatuses.PendingConfirm, customerB.Id, "seed-created", today.AddHours(8));

        await EnsureBookingStatusHistoryAsync(context, inProgressBooking.Id, BookingStatuses.PendingConfirm, customerA.Id, "seed-created", today.AddDays(-2).AddHours(18));
        await EnsureBookingStatusHistoryAsync(context, inProgressBooking.Id, BookingStatuses.Confirmed, nurseB.Id, "seed-confirmed", today.AddDays(-2).AddHours(19));
        await EnsureBookingStatusHistoryAsync(context, inProgressBooking.Id, BookingStatuses.InProgress, nurseB.Id, "seed-started", today.AddDays(-1).AddHours(21));

        await EnsurePaymentAsync(context, completedBooking.Id, 550_000m, "bank_transfer", PaymentStatuses.Paid, "SEED-TXN-PAID-001");
        await EnsurePaymentAsync(context, pendingBooking.Id, 500_000m, "bank_transfer", PaymentStatuses.Initiated, "SEED-TXN-INIT-002");
        await EnsurePaymentAsync(context, inProgressBooking.Id, 2_000_000m, "bank_transfer", PaymentStatuses.Paid, "SEED-TXN-PAID-003");

        await EnsurePayoutAsync(context, completedBooking.Id, nurseA.Id, 500_000m, 50_000m, "released", today.AddDays(-1));
        await EnsurePayoutAsync(context, inProgressBooking.Id, nurseB.Id, 1_800_000m, 200_000m, "on_hold", null);

        await EnsureReviewAsync(context, completedBooking.Id, customerA.Id, nurseA.Id, 5, "Great support and very caring.", today.AddDays(-2).AddHours(12));

        await EnsureDisputeAsync(context, inProgressBooking.Id, "Need clarification on overtime pricing", "open", "Pending admin review");

        var conversation = await EnsureConversationAsync(context, completedBooking.Id, customerA.Id, nurseA.Id);
        await context.SaveChangesAsync();
        await EnsureChatMessageAsync(context, conversation.Id, customerA.Id, "Chị đến giúp em lúc 9h nhé.", false, today.AddDays(-2).AddHours(8));
        await EnsureChatMessageAsync(context, conversation.Id, nurseA.Id, "Dạ em đến đúng giờ ạ.", true, today.AddDays(-2).AddHours(8).AddMinutes(5));

        await EnsureNotificationAsync(context, customerA.Id, "Lịch hẹn đã hoàn thành", "Lịch hẹn của bạn đã được hoàn thành thành công.", "booking");
        await EnsureNotificationAsync(context, nurseA.Id, "Đánh giá mới", "Bạn vừa nhận được một đánh giá 5 sao.", "review");
        await EnsureNotificationAsync(context, admin.Id, "Khiếu nại đang mở", "Có một khiếu nại đang chờ quản trị viên xem xét.", "system");

        await context.SaveChangesAsync();

        logger.LogInformation("Seed data completed successfully.");
    }

    private static async Task EnsureRolesAsync(RoleManager<ApplicationRole> roleManager)
    {
        await EnsureRoleAsync(roleManager, AppRoles.Admin, "Administrator");
        await EnsureRoleAsync(roleManager, AppRoles.Customer, "Customer");
        await EnsureRoleAsync(roleManager, AppRoles.Nurse, "Nurse");
        await EnsureRoleAsync(roleManager, AppRoles.NurseUnconfirmed, "Nurse (Unconfirmed)");
        await EnsureRoleAsync(roleManager, AppRoles.NurseConfirmed, "Nurse (Confirmed)");
    }

    private static async Task EnsureRoleAsync(RoleManager<ApplicationRole> roleManager, string code, string displayName)
    {
        var normalizedCode = roleManager.NormalizeKey(code);
        var role = await roleManager.FindByNameAsync(code);
        role ??= await roleManager.Roles.FirstOrDefaultAsync(r => r.Name == code);

        if (role == null)
        {
            var createResult = await roleManager.CreateAsync(new ApplicationRole { Name = code, DisplayName = displayName });
            if (createResult.Succeeded)
            {
                return;
            }

            role = await roleManager.Roles.FirstOrDefaultAsync(r => r.Name == code);
            if (role == null)
            {
                throw new InvalidOperationException(
                    $"Unable to create seed role '{code}': {string.Join(", ", createResult.Errors.Select(e => e.Description))}");
            }
        }

        var changed = false;
        if (role.Name != code)
        {
            role.Name = code;
            changed = true;
        }

        if (role.DisplayName != displayName)
        {
            role.DisplayName = displayName;
            changed = true;
        }

        if (role.NormalizedName != normalizedCode)
        {
            role.NormalizedName = normalizedCode;
            changed = true;
        }

        if (changed)
        {
            var updateResult = await roleManager.UpdateAsync(role);
            if (!updateResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Unable to update seed role '{code}': {string.Join(", ", updateResult.Errors.Select(e => e.Description))}");
            }
        }
    }

    private static async Task<ApplicationUser> EnsureUserAsync(
        UserManager<ApplicationUser> userManager,
        string email,
        string fullName,
        string phone,
        string[] roles)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user == null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FullName = fullName,
                PhoneNumber = phone,
                Status = "active",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var result = await userManager.CreateAsync(user, DefaultPassword);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException($"Unable to create seed user '{email}': {string.Join(", ", result.Errors.Select(e => e.Description))}");
            }
        }
        else
        {
            var changed = false;

            if (user.FullName != fullName)
            {
                user.FullName = fullName;
                changed = true;
            }

            if (user.PhoneNumber != phone)
            {
                user.PhoneNumber = phone;
                changed = true;
            }

            if (user.UserName != email)
            {
                user.UserName = email;
                changed = true;
            }

            if (user.Status != "active")
            {
                user.Status = "active";
                changed = true;
            }

            if (!user.EmailConfirmed)
            {
                user.EmailConfirmed = true;
                changed = true;
            }

            if (changed)
            {
                user.UpdatedAt = DateTime.UtcNow;
                var updateResult = await userManager.UpdateAsync(user);
                if (!updateResult.Succeeded)
                {
                    throw new InvalidOperationException($"Unable to update seed user '{email}': {string.Join(", ", updateResult.Errors.Select(e => e.Description))}");
                }
            }
        }

        var existingRoles = await userManager.GetRolesAsync(user);
        foreach (var role in roles)
        {
            if (!existingRoles.Contains(role))
            {
                var addRoleResult = await userManager.AddToRoleAsync(user, role);
                if (!addRoleResult.Succeeded)
                {
                    throw new InvalidOperationException($"Unable to assign role '{role}' to '{email}'.");
                }
            }
        }

        return user;
    }

    private static async Task EnsureAddressAsync(
        MomCareContext context,
        int userId,
        string fullAddress,
        string? ward,
        string? district,
        bool isDefault,
        string type)
    {
        var address = await context.Addresses.FirstOrDefaultAsync(a => a.UserId == userId && a.Type == type && a.IsDefault == isDefault);
        if (address == null)
        {
            context.Addresses.Add(new Address
            {
                UserId = userId,
                FullAddress = fullAddress,
                Ward = ward,
                District = district,
                IsDefault = isDefault,
                Type = type
            });
            return;
        }

        address.FullAddress = fullAddress;
        address.Ward = ward;
        address.District = district;
    }

    private static async Task<Service> EnsureServiceAsync(
        MomCareContext context,
        string name,
        string description,
        string category,
        decimal basePrice,
        int durationMinutes,
        string status,
        string serviceKind,
        int? packageDays = null,
        string? includedServiceKeys = null,
        string? packageScheduleJson = null)
    {
        var service = await context.Services.FirstOrDefaultAsync(s => s.Name == name);
        if (service == null)
        {
            service = new Service
            {
                Name = name,
                Category = category,
                Description = description,
                BasePrice = basePrice,
                EstimatedDurationMinutes = durationMinutes,
                ServiceKind = serviceKind,
                PackageDays = packageDays,
                IncludedServiceKeys = includedServiceKeys,
                PackageScheduleJson = packageScheduleJson,
                Status = status,
                CreatedAt = DateTime.UtcNow
            };
            context.Services.Add(service);
            return service;
        }

        service.Category = category;
        service.Description = description;
        service.BasePrice = basePrice;
        service.EstimatedDurationMinutes = durationMinutes;
        service.ServiceKind = serviceKind;
        service.PackageDays = packageDays;
        service.IncludedServiceKeys = includedServiceKeys;
        service.PackageScheduleJson = packageScheduleJson;
        service.Status = status;

        return service;
    }

    private static string GeneratePackageScheduleJson(int days, string serviceKeys)
    {
        var list = new System.Collections.Generic.List<object>();
        var labels = serviceKeys
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(GetPackageServiceLabel)
            .ToList();
        var serviceSummary = labels.Count > 0 ? string.Join(", ", labels) : "chăm sóc tổng quát";

        for (int i = 1; i <= days; i++)
        {
            string title;
            string desc;
            
            if (i == 1) {
                title = "Khởi đầu chăm sóc";
                desc = "Khám đánh giá tổng quát ngày đầu tiên và thiết lập phác đồ chăm sóc phù hợp.";
            } else if (i == days) {
                title = "Buổi cuối – tổng kết";
                desc = "Thực hiện dịch vụ buổi cuối, hướng dẫn gia đình tự chăm sóc sau khi kết thúc gói.";
            } else if (i % 3 == 0) {
                title = "Đánh giá định kỳ";
                desc = "Chăm sóc theo liệu trình, đồng thời kiểm tra tiến độ phục hồi của mẹ và bé.";
            } else {
                title = $"Chăm sóc ngày {i}";
                desc = "Thực hiện các dịch vụ theo liệu trình hàng ngày để đảm bảo sức khỏe và sự thư giãn.";
            }

            if (i == 1)
            {
                title = "Khởi động và đánh giá ban đầu";
                desc = $"Y tá đánh giá tình trạng mẹ và bé, thống nhất mục tiêu chăm sóc, sau đó thực hiện các hạng mục chính: {serviceSummary}.";
            }
            else if (i == days)
            {
                title = "Tổng kết và bàn giao hướng dẫn";
                desc = $"Hoàn tất liệu trình {serviceSummary}, tổng kết thay đổi sau gói và hướng dẫn gia đình tiếp tục theo dõi tại nhà.";
            }
            else if (i % 3 == 0)
            {
                title = $"Ngày {i}: Theo dõi tiến triển";
                desc = $"Thực hiện {BuildDailyServiceSummary(serviceKeys, i)}, ghi nhận phản hồi của gia đình và điều chỉnh nhịp chăm sóc nếu cần.";
            }
            else
            {
                title = $"Ngày {i}: Chăm sóc theo liệu trình";
                desc = $"Tập trung vào {BuildDailyServiceSummary(serviceKeys, i)} để duy trì tiến độ chăm sóc ổn định cho mẹ và bé.";
            }

            list.Add(new {
                day = i,
                title = title,
                description = desc,
                serviceKeys = serviceKeys
            });
        }
        return System.Text.Json.JsonSerializer.Serialize(list, new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase });
    }

    private static string BuildDailyServiceSummary(string serviceKeys, int day)
    {
        var keys = serviceKeys
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        if (keys.Count == 0)
        {
            return "kiểm tra sức khỏe, vệ sinh cơ bản và tư vấn chăm sóc tại nhà";
        }

        var ordered = keys
            .Skip((day - 1) % keys.Count)
            .Concat(keys.Take((day - 1) % keys.Count))
            .Take(Math.Min(3, keys.Count))
            .Select(GetPackageServiceDetail);

        return string.Join("; ", ordered);
    }

    private static string GetPackageServiceLabel(string key)
    {
        return key switch
        {
            "baby-bathing" => "tắm bé",
            "mother-health-monitoring" => "theo dõi sức khỏe mẹ",
            "baby-health-monitoring" => "theo dõi sức khỏe bé",
            "breastfeeding-support" => "hỗ trợ cho bú",
            "postpartum-massage" => "massage phục hồi sau sinh",
            "nutrition-consultation" => "tư vấn dinh dưỡng",
            "mental-wellness" => "hỗ trợ tâm lý",
            "night-care" => "chăm bé ban đêm",
            "house-support" => "hỗ trợ việc nhà nhẹ",
            _ => key
        };
    }

    private static string GetPackageServiceDetail(string key)
    {
        return key switch
        {
            "baby-bathing" => "tắm bé, vệ sinh rốn/da và hướng dẫn thao tác an toàn",
            "mother-health-monitoring" => "kiểm tra dấu hiệu hồi phục của mẹ, vết mổ/vết khâu và mức độ đau",
            "baby-health-monitoring" => "theo dõi thân nhiệt, ăn ngủ, tiểu tiện/tiêu phân và dấu hiệu bất thường của bé",
            "breastfeeding-support" => "kiểm tra khớp ngậm, tư thế cho bú và xử lý căng/tắc tia sữa",
            "postpartum-massage" => "massage thư giãn, giảm nhức mỏi lưng vai gáy và hỗ trợ lưu thông máu",
            "nutrition-consultation" => "tư vấn thực đơn, bổ sung nước/sữa và các món phù hợp giai đoạn sau sinh",
            "mental-wellness" => "lắng nghe tình trạng cảm xúc, hướng dẫn nghỉ ngơi và dấu hiệu cần hỗ trợ thêm",
            "night-care" => "hỗ trợ bé ngủ, bú đêm và ghi nhận những thay đổi trong đêm",
            "house-support" => "sắp xếp khu chăm sóc, khu tiệt trùng và các việc nhẹ quanh mẹ và bé",
            _ => key
        };
    }

    private static async Task DeactivateLegacyServicesAsync(MomCareContext context)
    {
        var legacyNames = new[]
        {
            "Chăm sóc mẹ sau sinh tại nhà",
            "Chăm sóc bé sơ sinh tại nhà",
            "Tư vấn hỗ trợ cho bé bú",
            "Massage phục hồi sau sinh",
            "Theo dõi vết mổ sau sinh",
            "Tư vấn dinh dưỡng sau sinh",
            "Tắm bé tại nhà",
            "Hỗ trợ tinh thần sau sinh",
            "Chăm sóc bầu ngực sau sinh",
            "Hướng dẫn xây dựng nếp ngủ cho bé",
            // Legacy English packages
            "Basic Package",
            "Standard Package",
            "Premium Package",
        };

        var legacyServices = await context.Services
            .Where(service => legacyNames.Contains(service.Name))
            .ToListAsync();

        foreach (var service in legacyServices)
        {
            service.Status = "inactive";
        }
    }

    private static async Task<NurseProfile> EnsureNurseProfileAsync(
        MomCareContext context,
        int userId,
        string bio,
        string specialization,
        string certificates,
        int yearsExperience,
        int serviceRadiusKm,
        decimal averageRating,
        bool isActive,
        string verifyStatus)
    {
        var profile = await context.NurseProfiles.FirstOrDefaultAsync(n => n.UserId == userId);
        if (profile == null)
        {
            profile = new NurseProfile
            {
                UserId = userId,
                Bio = bio,
                Specialization = specialization,
                Certificates = certificates,
                YearsExperience = yearsExperience,
                ServiceRadiusKm = serviceRadiusKm,
                AverageRating = averageRating,
                IsActive = isActive,
                IsVerified = verifyStatus,
                ConfirmedAt = verifyStatus == "verified" ? DateTime.UtcNow.AddDays(-7) : null
            };

            context.NurseProfiles.Add(profile);
            return profile;
        }

        profile.Bio = bio;
        profile.Specialization = specialization;
        profile.Certificates = certificates;
        profile.YearsExperience = yearsExperience;
        profile.ServiceRadiusKm = serviceRadiusKm;
        profile.AverageRating = averageRating;
        profile.IsActive = isActive;
        profile.IsVerified = verifyStatus;
        profile.ConfirmedAt = verifyStatus == "verified" ? profile.ConfirmedAt ?? DateTime.UtcNow.AddDays(-7) : null;

        return profile;
    }

    private static async Task EnsureDocumentAsync(
        MomCareContext context,
        int nurseProfileId,
        string type,
        string publicId,
        string status)
    {
        var normalizedType = NormalizeLegacyDocumentType(type);
        var document = await context.Documents.FirstOrDefaultAsync(d => d.NurseProfileId == nurseProfileId && d.Type == normalizedType);
        if (document == null)
        {
            context.Documents.Add(new Document
            {
                NurseProfileId = nurseProfileId,
                Type = normalizedType,
                PublicId = publicId,
                Status = status,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            return;
        }

        document.Type = normalizedType;
        document.PublicId = publicId;
        document.Status = status;
        document.UpdatedAt = DateTime.UtcNow;
    }

    private static string NormalizeLegacyDocumentType(string type)
    {
        var normalized = type.Trim().ToLowerInvariant();
        return normalized switch
        {
            "id_card" => DocumentTypes.IdCardFront,
            "hospital_certificate" => DocumentTypes.Certificate,
            _ => normalized
        };
    }

    private static async Task EnsureNurseServiceAsync(
        MomCareContext context,
        int nurseProfileId,
        int serviceId,
        decimal price,
        string unit,
        string status)
    {
        var nurseService = await context.NurseServices
            .FirstOrDefaultAsync(ns => ns.NurseProfileId == nurseProfileId && ns.ServiceId == serviceId);

        if (nurseService == null)
        {
            context.NurseServices.Add(new NurseService
            {
                NurseProfileId = nurseProfileId,
                ServiceId = serviceId,
                Price = price,
                Unit = unit,
                Status = status
            });
            return;
        }

        nurseService.Price = price;
        nurseService.Unit = unit;
        nurseService.Status = status;
    }

    private static async Task EnsureAvailabilitySlotAsync(
        MomCareContext context,
        int nurseProfileId,
        DateTime start,
        DateTime end)
    {
        var slot = await context.AvailabilitySlots
            .FirstOrDefaultAsync(s => s.NurseProfileId == nurseProfileId && s.StartTime == start && s.EndTime == end);

        if (slot == null)
        {
            context.AvailabilitySlots.Add(new AvailabilitySlot
            {
                NurseProfileId = nurseProfileId,
                StartTime = start,
                EndTime = end
            });
            return;
        }
    }

    private static async Task<Booking> EnsureBookingAsync(
        MomCareContext context,
        string seedKey,
        int customerId,
        int nurseId,
        int serviceId,
        string status,
        decimal totalPrice,
        string address,
        DateTime start,
        DateTime end)
    {
        var booking = await context.Bookings.FirstOrDefaultAsync(b => b.Notes == seedKey);
        if (booking == null)
        {
            booking = new Booking
            {
                CustomerId = customerId,
                NurseId = nurseId,
                ServiceId = serviceId,
                Status = status,
                TotalPrice = totalPrice,
                Notes = seedKey,
                Address = address,
                StartTime = start,
                EndTime = end,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            context.Bookings.Add(booking);
            return booking;
        }

        booking.CustomerId = customerId;
        booking.NurseId = nurseId;
        booking.ServiceId = serviceId;
        booking.Status = status;
        booking.TotalPrice = totalPrice;
        booking.Address = address;
        booking.StartTime = start;
        booking.EndTime = end;
        booking.UpdatedAt = DateTime.UtcNow;

        return booking;
    }

    private static async Task EnsureBookingStatusHistoryAsync(
        MomCareContext context,
        int bookingId,
        string status,
        int? changedBy,
        string note,
        DateTime createdAt)
    {
        var exists = await context.BookingStatusHistories
            .AnyAsync(h => h.BookingId == bookingId && h.Status == status && h.Note == note);

        if (exists)
        {
            return;
        }

        context.BookingStatusHistories.Add(new BookingStatusHistory
        {
            BookingId = bookingId,
            Status = status,
            ChangedBy = changedBy,
            Note = note,
            CreatedAt = createdAt
        });
    }

    private static async Task EnsurePaymentAsync(
        MomCareContext context,
        int bookingId,
        decimal amount,
        string method,
        string status,
        string transactionId)
    {
        var payment = await context.Payments.FirstOrDefaultAsync(p => p.BookingId == bookingId);
        if (payment == null)
        {
            context.Payments.Add(new Payment
            {
                BookingId = bookingId,
                Amount = amount,
                Method = method,
                Status = status,
                TransactionId = transactionId,
                CreatedAt = DateTime.UtcNow
            });
            return;
        }

        payment.Amount = amount;
        payment.Method = method;
        payment.Status = status;
        payment.TransactionId = transactionId;
    }

    private static async Task EnsurePayoutAsync(
        MomCareContext context,
        int bookingId,
        int nurseId,
        decimal amount,
        decimal platformFee,
        string status,
        DateTime? releasedAt)
    {
        var payout = await context.Payouts.FirstOrDefaultAsync(p => p.BookingId == bookingId);
        if (payout == null)
        {
            context.Payouts.Add(new Payout
            {
                BookingId = bookingId,
                NurseId = nurseId,
                Amount = amount,
                PlatformFee = platformFee,
                Status = status,
                ReleasedAt = releasedAt,
                CreatedAt = DateTime.UtcNow
            });
            return;
        }

        payout.NurseId = nurseId;
        payout.Amount = amount;
        payout.PlatformFee = platformFee;
        payout.Status = status;
        payout.ReleasedAt = releasedAt;
    }

    private static async Task EnsureReviewAsync(
        MomCareContext context,
        int bookingId,
        int customerId,
        int nurseId,
        int rating,
        string comment,
        DateTime createdAt)
    {
        var review = await context.Reviews.FirstOrDefaultAsync(r => r.BookingId == bookingId);
        if (review == null)
        {
            context.Reviews.Add(new Review
            {
                BookingId = bookingId,
                CustomerId = customerId,
                NurseId = nurseId,
                Rating = rating,
                Comment = comment,
                CreatedAt = createdAt
            });
            return;
        }

        review.CustomerId = customerId;
        review.NurseId = nurseId;
        review.Rating = rating;
        review.Comment = comment;
    }

    private static async Task EnsureDisputeAsync(
        MomCareContext context,
        int bookingId,
        string reason,
        string status,
        string? adminNote)
    {
        var dispute = await context.Disputes.FirstOrDefaultAsync(d => d.BookingId == bookingId);
        if (dispute == null)
        {
            context.Disputes.Add(new Dispute
            {
                BookingId = bookingId,
                Reason = reason,
                Status = status,
                AdminNote = adminNote,
                CreatedAt = DateTime.UtcNow
            });
            return;
        }

        dispute.Reason = reason;
        dispute.Status = status;
        dispute.AdminNote = adminNote;
    }

    private static async Task<Conversation> EnsureConversationAsync(
        MomCareContext context,
        int bookingId,
        int user1Id,
        int user2Id)
    {
        var conversation = await context.Conversations.FirstOrDefaultAsync(c => c.BookingId == bookingId);
        if (conversation == null)
        {
            conversation = new Conversation
            {
                BookingId = bookingId,
                User1Id = user1Id,
                User2Id = user2Id,
                CreatedAt = DateTime.UtcNow
            };

            context.Conversations.Add(conversation);
            return conversation;
        }

        conversation.User1Id = user1Id;
        conversation.User2Id = user2Id;
        return conversation;
    }

    private static async Task EnsureChatMessageAsync(
        MomCareContext context,
        int conversationId,
        int senderId,
        string content,
        bool isRead,
        DateTime createdAt)
    {
        var exists = await context.ChatMessages
            .AnyAsync(m => m.ConversationId == conversationId && m.SenderId == senderId && m.Content == content);

        if (exists)
        {
            return;
        }

        context.ChatMessages.Add(new ChatMessage
        {
            ConversationId = conversationId,
            SenderId = senderId,
            Content = content,
            IsRead = isRead,
            CreatedAt = createdAt
        });
    }

    private static async Task EnsureNotificationAsync(
        MomCareContext context,
        int userId,
        string title,
        string content,
        string type)
    {
        var exists = await context.Notifications
            .AnyAsync(n => n.UserId == userId && n.Title == title && n.Content == content);

        if (exists)
        {
            return;
        }

        context.Notifications.Add(new Notification
        {
            UserId = userId,
            Title = title,
            Content = content,
            Type = type,
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        });
    }
}
