using Microsoft.EntityFrameworkCore;
using MomCare.Models;

namespace MomCare.Data;

public class MomCareContext : DbContext
{
    public MomCareContext(DbContextOptions<MomCareContext> options) : base(options)
    {
    }

    // Auth
    public DbSet<User> Users { get; set; }
    public DbSet<Role> Roles { get; set; }
    public DbSet<UserRole> UserRoles { get; set; }

    // Profiles & Locations
    public DbSet<Address> Addresses { get; set; }
    public DbSet<NurseProfile> NurseProfiles { get; set; }
    public DbSet<Document> Documents { get; set; }

    // Services & Operations
    public DbSet<Service> Services { get; set; }
    public DbSet<NurseService> NurseServices { get; set; }
    public DbSet<AvailabilitySlot> AvailabilitySlots { get; set; }

    // Booking Core
    public DbSet<Booking> Bookings { get; set; }
    public DbSet<BookingStatusHistory> BookingStatusHistories { get; set; }

    // Finance
    public DbSet<Payment> Payments { get; set; }
    public DbSet<Payout> Payouts { get; set; }

    // Feedback & Review
    public DbSet<Review> Reviews { get; set; }
    public DbSet<Dispute> Disputes { get; set; }

    // Communication
    public DbSet<Notification> Notifications { get; set; }
    public DbSet<Conversation> Conversations { get; set; }
    public DbSet<ChatMessage> ChatMessages { get; set; }


    // Auth - Refresh Tokens
    public DbSet<RefreshToken> RefreshTokens { get; set; }
    public DbSet<OAuthProvider> OAuthProviders { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // --- Auth Configuration ---
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(e => e.Phone).IsUnique();
            entity.HasIndex(e => e.Email).IsUnique();
            entity.Property(e => e.Status).HasDefaultValue("active");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        });

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.HasIndex(e => e.Token).IsUnique();
            entity.HasOne(d => d.User)
                  .WithMany() // Can add collection to User if needed, keeping it uni-directional for now
                  .HasForeignKey(d => d.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });


        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasIndex(e => e.Code).IsUnique();
        });

        modelBuilder.Entity<UserRole>(entity =>
        {
            entity.HasKey(e => new { e.UserId, e.RoleId });
            entity.HasOne(d => d.User).WithMany(p => p.UserRoles).HasForeignKey(d => d.UserId);
            entity.HasOne(d => d.Role).WithMany().HasForeignKey(d => d.RoleId);
        });

        // --- Profile Relationship ---
        modelBuilder.Entity<Address>()
            .HasOne(a => a.User)
            .WithMany() // Or user could have collection
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
                .WithMany() // or CustomerBookings
                .HasForeignKey(b => b.CustomerId)
                .OnDelete(DeleteBehavior.Restrict); // Prevent deleting user if booking exists

            entity.HasOne(b => b.Nurse)
                .WithMany() // or NurseBookings
                .HasForeignKey(b => b.NurseId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(b => b.Service)
                .WithMany()
                .HasForeignKey(b => b.ServiceId)
                .OnDelete(DeleteBehavior.Restrict);
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
        });
    }
}
