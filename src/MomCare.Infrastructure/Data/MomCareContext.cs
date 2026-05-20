using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MomCare.Models;

namespace MomCare.Data;

public class MomCareContext : IdentityDbContext<
    ApplicationUser,
    ApplicationRole,
    int,
    IdentityUserClaim<int>,
    ApplicationUserRole,
    IdentityUserLogin<int>,
    IdentityRoleClaim<int>,
    IdentityUserToken<int>>
{
    public MomCareContext(DbContextOptions<MomCareContext> options) : base(options)
    {
    }

    // Profiles & Locations
    public DbSet<Address> Addresses => Set<Address>();
    public DbSet<NurseProfile> NurseProfiles => Set<NurseProfile>();
    public DbSet<Document> Documents => Set<Document>();

    // Services & Operations
    public DbSet<Service> Services => Set<Service>();
    public DbSet<NurseService> NurseServices => Set<NurseService>();
    public DbSet<AvailabilitySlot> AvailabilitySlots => Set<AvailabilitySlot>();

    // Booking Core
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<BookingStatusHistory> BookingStatusHistories => Set<BookingStatusHistory>();
    public DbSet<PackageSessionLog> PackageSessionLogs => Set<PackageSessionLog>();

    // Finance
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Payout> Payouts => Set<Payout>();

    // Feedback & Review
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<Dispute> Disputes => Set<Dispute>();
    public DbSet<HealthCheckIn> HealthCheckIns => Set<HealthCheckIn>();
    public DbSet<AiHealthAnalysis> AiHealthAnalyses => Set<AiHealthAnalysis>();

    // Communication
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();


    // Auth - Refresh Tokens
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // --- Identity table mapping ---
        modelBuilder.Entity<ApplicationUser>(entity =>
        {
            entity.ToTable("users");
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.FullName).HasColumnName("full_name");
            entity.Property(e => e.Avatar).HasColumnName("avatar");
            entity.Property(e => e.BankBin).HasColumnName("bank_bin").HasMaxLength(20);
            entity.Property(e => e.BankAccountNumber).HasColumnName("bank_account_number").HasMaxLength(50);
            entity.Property(e => e.BankAccountName).HasColumnName("bank_account_name").HasMaxLength(255);
            entity.Property(e => e.Status).HasColumnName("status").HasDefaultValue("active");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("SYSUTCDATETIME()");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("SYSUTCDATETIME()");

            entity.Property(e => e.UserName).HasColumnName("user_name").HasMaxLength(256);
            entity.Property(e => e.NormalizedUserName).HasColumnName("normalized_user_name").HasMaxLength(256);
            entity.Property(e => e.Email).HasColumnName("email").HasMaxLength(256);
            entity.Property(e => e.NormalizedEmail).HasColumnName("normalized_email").HasMaxLength(256);
            entity.Property(e => e.EmailConfirmed).HasColumnName("email_confirmed");
            entity.Property(e => e.PasswordHash).HasColumnName("password_hash");
            entity.Property(e => e.SecurityStamp).HasColumnName("security_stamp");
            entity.Property(e => e.ConcurrencyStamp).HasColumnName("concurrency_stamp");
            entity.Property(e => e.PhoneNumber).HasColumnName("phone").HasMaxLength(50);
            entity.Property(e => e.PhoneNumberConfirmed).HasColumnName("phone_confirmed");
            entity.Property(e => e.TwoFactorEnabled).HasColumnName("two_factor_enabled");
            entity.Property(e => e.LockoutEnd).HasColumnName("lockout_end");
            entity.Property(e => e.LockoutEnabled).HasColumnName("lockout_enabled");
            entity.Property(e => e.AccessFailedCount).HasColumnName("access_failed_count");

            entity.HasIndex(e => e.NormalizedUserName).IsUnique();
            entity.HasIndex(e => e.NormalizedEmail);
            entity.HasIndex(e => e.PhoneNumber).IsUnique().HasFilter("[phone] IS NOT NULL");
            entity.HasIndex(e => e.Email).IsUnique().HasFilter("[email] IS NOT NULL");
        });

        modelBuilder.Entity<ApplicationRole>(entity =>
        {
            entity.ToTable("roles");
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Name).HasColumnName("code").HasMaxLength(256);
            entity.Property(e => e.NormalizedName).HasColumnName("normalized_code").HasMaxLength(256);
            entity.Property(e => e.ConcurrencyStamp).HasColumnName("concurrency_stamp");
            entity.Property(e => e.DisplayName).HasColumnName("name").HasMaxLength(256);
            entity.HasIndex(e => e.Name).IsUnique();
        });

        modelBuilder.Entity<ApplicationUserRole>(entity =>
        {
            entity.ToTable("user_roles");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.RoleId).HasColumnName("role_id");
        });

        modelBuilder.Entity<IdentityUserClaim<int>>(entity =>
        {
            entity.ToTable("user_claims");
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.ClaimType).HasColumnName("claim_type");
            entity.Property(e => e.ClaimValue).HasColumnName("claim_value");
        });

        modelBuilder.Entity<IdentityRoleClaim<int>>(entity =>
        {
            entity.ToTable("role_claims");
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.RoleId).HasColumnName("role_id");
            entity.Property(e => e.ClaimType).HasColumnName("claim_type");
            entity.Property(e => e.ClaimValue).HasColumnName("claim_value");
        });

        modelBuilder.Entity<IdentityUserLogin<int>>(entity =>
        {
            entity.ToTable("user_logins");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.LoginProvider).HasColumnName("login_provider");
            entity.Property(e => e.ProviderKey).HasColumnName("provider_key");
            entity.Property(e => e.ProviderDisplayName).HasColumnName("provider_display_name");
        });

        modelBuilder.Entity<IdentityUserToken<int>>(entity =>
        {
            entity.ToTable("user_tokens");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.LoginProvider).HasColumnName("login_provider");
            entity.Property(e => e.Name).HasColumnName("name");
            entity.Property(e => e.Value).HasColumnName("value");
        });

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.HasIndex(e => e.Token).IsUnique();
            entity.HasOne(d => d.User)
                  .WithMany()
                  .HasForeignKey(d => d.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // --- Profile Relationship ---
        modelBuilder.Entity<Address>()
            .HasOne(a => a.User)
            .WithMany()
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<NurseProfile>()
            .HasOne(n => n.User)
            .WithOne() // Assuming 1-to-1. User can have navigation if added.
            .HasForeignKey<NurseProfile>(n => n.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // --- Booking Relationships ---
        modelBuilder.Entity<Booking>(entity =>
        {
            entity.HasOne(b => b.Customer)
                .WithMany()
                .HasForeignKey(b => b.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(b => b.Nurse)
                .WithMany()
                .HasForeignKey(b => b.NurseId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(b => b.Service)
                .WithMany()
                .HasForeignKey(b => b.ServiceId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(b => new { b.CustomerId, b.Status, b.StartTime });
            entity.HasIndex(b => new { b.NurseId, b.Status, b.StartTime });
            entity.HasIndex(b => b.AvailabilitySlotId);
        });

        // 1-to-1 relationships for Booking components
        modelBuilder.Entity<Booking>()
            .HasOne(b => b.Payment)
            .WithOne(p => p.Booking)
            .HasForeignKey<Payment>(p => p.BookingId);

        modelBuilder.Entity<Booking>()
            .HasOne(b => b.Review)
            .WithOne(r => r.Booking)
            .HasForeignKey<Review>(r => r.BookingId);

        modelBuilder.Entity<Booking>()
            .HasOne(b => b.Dispute)
            .WithOne(d => d.Booking)
            .HasForeignKey<Dispute>(d => d.BookingId);

        modelBuilder.Entity<Booking>()
            .HasOne(b => b.Conversation)
            .WithOne(c => c.Booking)
            .HasForeignKey<Conversation>(c => c.BookingId);
        
        // --- Conversation ---
        modelBuilder.Entity<Conversation>(entity => 
        {
             entity.HasOne(c => c.User1).WithMany().HasForeignKey(c => c.User1Id).OnDelete(DeleteBehavior.Restrict);
             entity.HasOne(c => c.User2).WithMany().HasForeignKey(c => c.User2Id).OnDelete(DeleteBehavior.Restrict);
        });

        // --- Review Configuration ---
        modelBuilder.Entity<Review>(entity =>
        {
            entity.HasOne(r => r.Customer)
                .WithMany()
                .HasForeignKey(r => r.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(r => r.Nurse)
                .WithMany()
                .HasForeignKey(r => r.NurseId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(r => r.BookingId).IsUnique();
        });

        // --- Domain indexes for frequent queries ---
        modelBuilder.Entity<Service>(entity =>
        {
            entity.Property(s => s.BasePrice).HasColumnType("decimal(18,2)");
            entity.Property(s => s.ServiceKind).HasDefaultValue("single");
            entity.HasIndex(s => s.Name);
            entity.HasIndex(s => s.Status);
            entity.HasIndex(s => s.ServiceKind);
        });

        modelBuilder.Entity<NurseService>(entity =>
        {
            entity.Property(ns => ns.Price).HasColumnType("decimal(18,2)");
            entity.HasIndex(ns => new { ns.NurseProfileId, ns.ServiceId }).IsUnique();
            entity.HasIndex(ns => new { ns.ServiceId, ns.Status, ns.Price });
        });

        modelBuilder.Entity<NurseProfile>(entity =>
        {
            entity.Property(n => n.AverageRating).HasColumnType("decimal(3,2)").HasDefaultValue(0m);
            entity.Property(n => n.IsActive).HasDefaultValue(true);
            entity.HasIndex(n => new { n.IsActive, n.AverageRating });
        });

        modelBuilder.Entity<AvailabilitySlot>(entity =>
        {
            entity.HasIndex(a => new { a.NurseProfileId, a.StartTime, a.EndTime });
        });

        modelBuilder.Entity<Booking>(entity =>
        {
            entity.Property(b => b.TotalPrice).HasColumnType("decimal(18,2)");
        });

        modelBuilder.Entity<Review>(entity =>
        {
            entity.ToTable(t => t.HasCheckConstraint("CK_reviews_rating", "[rating] >= 1 AND [rating] <= 5"));
            entity.HasIndex(r => new { r.NurseId, r.CreatedAt });
        });

        modelBuilder.Entity<HealthCheckIn>(entity =>
        {
            entity.HasOne(h => h.User)
                .WithMany()
                .HasForeignKey(h => h.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(h => new { h.UserId, h.CreatedAt });
        });

        modelBuilder.Entity<AiHealthAnalysis>(entity =>
        {
            entity.HasOne(a => a.HealthCheckIn)
                .WithOne(h => h.Analysis)
                .HasForeignKey<AiHealthAnalysis>(a => a.HealthCheckInId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(a => a.HealthCheckInId).IsUnique();
        });

        modelBuilder.Entity<ChatMessage>(entity =>
        {
            entity.HasIndex(c => new { c.ConversationId, c.CreatedAt });
        });

        modelBuilder.Entity<PackageSessionLog>(entity =>
        {
            entity.HasOne(p => p.Booking)
                .WithMany(b => b.SessionLogs)
                .HasForeignKey(p => p.BookingId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(p => new { p.BookingId, p.SessionNumber }).IsUnique();
            entity.HasIndex(p => new { p.BookingId, p.SessionDate });
        });

        modelBuilder.Entity<Payment>(entity =>
        {
            entity.Property(p => p.Amount).HasColumnType("decimal(18,2)");
            entity.Property(p => p.RefundAmount).HasColumnType("decimal(18,2)");
        });

        modelBuilder.Entity<Payout>(entity =>
        {
            entity.Property(p => p.Amount).HasColumnType("decimal(18,2)");
            entity.Property(p => p.PlatformFee).HasColumnType("decimal(18,2)");
        });
    }
}
