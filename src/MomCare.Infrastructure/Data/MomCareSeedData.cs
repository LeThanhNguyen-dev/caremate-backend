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

        var postpartum = await EnsureServiceAsync(context, "Postpartum Care", "Home postpartum support for mothers", 500_000m, 120, "active");
        var babyCare = await EnsureServiceAsync(context, "Baby Care", "Newborn and infant care at home", 450_000m, 120, "active");
        var overnight = await EnsureServiceAsync(context, "Overnight Baby Care", "Night shift baby care support", 900_000m, 480, "active");
        var lactation = await EnsureServiceAsync(context, "Lactation Support", "Breastfeeding consultation and support", 600_000m, 90, "active");
        var massage = await EnsureServiceAsync(context, "Mother Recovery Massage", "Postpartum massage for recovery", 700_000m, 90, "active");

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

        await EnsureDocumentAsync(context, nurseProfileA.Id, "id_card", "https://seed.local/docs/huong-id.pdf", "approved");
        await EnsureDocumentAsync(context, nurseProfileA.Id, "hospital_certificate", "https://seed.local/docs/huong-hospital.pdf", "approved");
        await EnsureDocumentAsync(context, nurseProfileB.Id, "id_card", "https://seed.local/docs/mai-id.pdf", "approved");
        await EnsureDocumentAsync(context, nurseProfilePending.Id, "id_card", "https://seed.local/docs/ngoc-id.pdf", "pending_review");

        await EnsureNurseServiceAsync(context, nurseProfileA.Id, postpartum.Id, 550_000m, "fixed", "enabled");
        await EnsureNurseServiceAsync(context, nurseProfileA.Id, babyCare.Id, 500_000m, "fixed", "enabled");
        await EnsureNurseServiceAsync(context, nurseProfileA.Id, lactation.Id, 650_000m, "fixed", "enabled");
        await EnsureNurseServiceAsync(context, nurseProfileB.Id, babyCare.Id, 470_000m, "fixed", "enabled");
        await EnsureNurseServiceAsync(context, nurseProfileB.Id, overnight.Id, 250_000m, "hourly", "enabled");
        await EnsureNurseServiceAsync(context, nurseProfileB.Id, massage.Id, 750_000m, "fixed", "enabled");

        var today = DateTime.UtcNow.Date;

        await EnsureAvailabilitySlotAsync(context, nurseProfileA.Id, today.AddDays(-2).AddHours(8), today.AddDays(-2).AddHours(12), true);
        await EnsureAvailabilitySlotAsync(context, nurseProfileA.Id, today.AddDays(1).AddHours(8), today.AddDays(1).AddHours(12), false);
        await EnsureAvailabilitySlotAsync(context, nurseProfileA.Id, today.AddDays(2).AddHours(13), today.AddDays(2).AddHours(17), false);

        await EnsureAvailabilitySlotAsync(context, nurseProfileB.Id, today.AddDays(-1).AddHours(20), today.AddDays(0).AddHours(4), true);
        await EnsureAvailabilitySlotAsync(context, nurseProfileB.Id, today.AddDays(1).AddHours(20), today.AddDays(2).AddHours(4), false);
        await EnsureAvailabilitySlotAsync(context, nurseProfileB.Id, today.AddDays(3).AddHours(8), today.AddDays(3).AddHours(12), false);

        await context.SaveChangesAsync();

        var completedBooking = await EnsureBookingAsync(
            context,
            "seed:booking:completed",
            customerA.Id,
            nurseA.Id,
            postpartum.Id,
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
            babyCare.Id,
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
            overnight.Id,
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
        await EnsureChatMessageAsync(context, conversation.Id, customerA.Id, "Ch? d?n gi�p em l�c 9h nh�.", false, today.AddDays(-2).AddHours(8));
        await EnsureChatMessageAsync(context, conversation.Id, nurseA.Id, "D? em d?n d�ng gi? ?.", true, today.AddDays(-2).AddHours(8).AddMinutes(5));

        await EnsureNotificationAsync(context, customerA.Id, "Booking completed", "Your booking has been completed successfully.", "booking");
        await EnsureNotificationAsync(context, nurseA.Id, "New review", "You received a new 5-star review.", "review");
        await EnsureNotificationAsync(context, admin.Id, "Open dispute", "There is an open dispute that needs review.", "system");

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
        decimal basePrice,
        int durationMinutes,
        string status)
    {
        var service = await context.Services.FirstOrDefaultAsync(s => s.Name == name);
        if (service == null)
        {
            service = new Service
            {
                Name = name,
                Description = description,
                BasePrice = basePrice,
                EstimatedDurationMinutes = durationMinutes,
                Status = status,
                CreatedAt = DateTime.UtcNow
            };
            context.Services.Add(service);
            return service;
        }

        service.Description = description;
        service.BasePrice = basePrice;
        service.EstimatedDurationMinutes = durationMinutes;
        service.Status = status;

        return service;
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
        string fileUrl,
        string status)
    {
        var document = await context.Documents.FirstOrDefaultAsync(d => d.NurseProfileId == nurseProfileId && d.Type == type);
        if (document == null)
        {
            context.Documents.Add(new Document
            {
                NurseProfileId = nurseProfileId,
                Type = type,
                FileUrl = fileUrl,
                Status = status,
                CreatedAt = DateTime.UtcNow
            });
            return;
        }

        document.FileUrl = fileUrl;
        document.Status = status;
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
        DateTime end,
        bool isBooked)
    {
        var slot = await context.AvailabilitySlots
            .FirstOrDefaultAsync(s => s.NurseProfileId == nurseProfileId && s.StartTime == start && s.EndTime == end);

        if (slot == null)
        {
            context.AvailabilitySlots.Add(new AvailabilitySlot
            {
                NurseProfileId = nurseProfileId,
                StartTime = start,
                EndTime = end,
                IsBooked = isBooked
            });
            return;
        }

        slot.IsBooked = isBooked;
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
