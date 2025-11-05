using Microsoft.EntityFrameworkCore;
using SafeSignal.Cloud.Core.Entities;

namespace SafeSignal.Cloud.Infrastructure.Data;

public class SafeSignalDbContext : DbContext
{
    public SafeSignalDbContext(DbContextOptions<SafeSignalDbContext> options) : base(options)
    {
    }

    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<Site> Sites => Set<Site>();
    public DbSet<Building> Buildings => Set<Building>();
    public DbSet<Floor> Floors => Set<Floor>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<DeviceMetric> DeviceMetrics => Set<DeviceMetric>();
    public DbSet<Alert> Alerts => Set<Alert>();
    public DbSet<User> Users => Set<User>();
    public DbSet<UserOrganization> UserOrganizations => Set<UserOrganization>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<PushToken> PushTokens => Set<PushToken>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<AlertClearance> AlertClearances => Set<AlertClearance>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Organization
        modelBuilder.Entity<Organization>(entity =>
        {
            entity.ToTable("organizations");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Slug).IsRequired().HasMaxLength(100);
            entity.HasIndex(e => e.Slug).IsUnique();
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.Metadata).HasColumnType("jsonb");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("NOW()");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("NOW()");
        });

        // Site
        modelBuilder.Entity<Site>(entity =>
        {
            entity.ToTable("sites");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Latitude).HasPrecision(10, 8);
            entity.Property(e => e.Longitude).HasPrecision(11, 8);
            entity.Property(e => e.Timezone).HasMaxLength(50).HasDefaultValue("UTC");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("NOW()");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("NOW()");

            entity.HasOne(e => e.Organization)
                .WithMany(o => o.Sites)
                .HasForeignKey(e => e.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Building
        modelBuilder.Entity<Building>(entity =>
        {
            entity.ToTable("buildings");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.FloorCount).HasDefaultValue(1);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("NOW()");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("NOW()");

            entity.HasOne(e => e.Site)
                .WithMany(s => s.Buildings)
                .HasForeignKey(e => e.SiteId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Floor
        modelBuilder.Entity<Floor>(entity =>
        {
            entity.ToTable("floors");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(100);
            entity.HasIndex(e => new { e.BuildingId, e.FloorNumber }).IsUnique();

            entity.HasOne(e => e.Building)
                .WithMany(b => b.Floors)
                .HasForeignKey(e => e.BuildingId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Room
        modelBuilder.Entity<Room>(entity =>
        {
            entity.ToTable("rooms");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.RoomNumber).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Name).HasMaxLength(200);
            entity.Property(e => e.RoomType).HasMaxLength(50);
            entity.HasIndex(e => new { e.FloorId, e.RoomNumber }).IsUnique();

            entity.HasOne(e => e.Floor)
                .WithMany(f => f.Rooms)
                .HasForeignKey(e => e.FloorId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Device
        modelBuilder.Entity<Device>(entity =>
        {
            entity.ToTable("devices");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.DeviceId).IsRequired().HasMaxLength(100);
            entity.HasIndex(e => e.DeviceId).IsUnique();
            entity.Property(e => e.DeviceType).HasConversion<string>().HasMaxLength(50);
            entity.Property(e => e.FirmwareVersion).HasMaxLength(50);
            entity.Property(e => e.HardwareVersion).HasMaxLength(50);
            entity.Property(e => e.SerialNumber).HasMaxLength(100);
            entity.HasIndex(e => e.SerialNumber).IsUnique();
            entity.Property(e => e.MacAddress).HasMaxLength(17);
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(20).HasDefaultValue(DeviceStatus.Inactive);
            entity.Property(e => e.Metadata).HasColumnType("jsonb");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("NOW()");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("NOW()");

            entity.HasOne(e => e.Organization)
                .WithMany(o => o.Devices)
                .HasForeignKey(e => e.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Room)
                .WithMany(r => r.Devices)
                .HasForeignKey(e => e.RoomId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(e => e.OrganizationId);
            entity.HasIndex(e => e.RoomId);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.LastSeenAt);
        });

        // DeviceMetric
        modelBuilder.Entity<DeviceMetric>(entity =>
        {
            entity.ToTable("device_metrics");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Timestamp).HasDefaultValueSql("NOW()");
            entity.Property(e => e.BatteryVoltage).HasPrecision(4, 2);
            entity.Property(e => e.AlertCount).HasDefaultValue(0);
            entity.Property(e => e.Metadata).HasColumnType("jsonb");

            entity.HasOne(e => e.Device)
                .WithMany(d => d.DeviceMetrics)
                .HasForeignKey(e => e.DeviceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Alert
        modelBuilder.Entity<Alert>(entity =>
        {
            entity.ToTable("alert_history");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.AlertId).IsRequired().HasMaxLength(100);
            entity.HasIndex(e => e.AlertId).IsUnique();
            entity.Property(e => e.Severity).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.AlertType).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.Source).HasConversion<string>().HasMaxLength(50).HasDefaultValue(AlertSource.Button);
            entity.Property(e => e.Metadata).HasColumnType("jsonb");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("NOW()");

            entity.HasOne(e => e.Organization)
                .WithMany()
                .HasForeignKey(e => e.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Building)
                .WithMany(b => b.Alerts)
                .HasForeignKey(e => e.BuildingId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.Device)
                .WithMany(d => d.Alerts)
                .HasForeignKey(e => e.DeviceId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.Room)
                .WithMany(r => r.Alerts)
                .HasForeignKey(e => e.RoomId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.FirstClearanceUser)
                .WithMany()
                .HasForeignKey(e => e.FirstClearanceUserId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.SecondClearanceUser)
                .WithMany()
                .HasForeignKey(e => e.SecondClearanceUserId)
                .OnDelete(DeleteBehavior.SetNull);

            // Two-person clearance fields
            entity.Property(e => e.FirstClearanceAt).HasColumnName("first_clearance_at");
            entity.Property(e => e.FirstClearanceUserId).HasColumnName("first_clearance_user_id");
            entity.Property(e => e.SecondClearanceAt).HasColumnName("second_clearance_at");
            entity.Property(e => e.SecondClearanceUserId).HasColumnName("second_clearance_user_id");
            entity.Property(e => e.FullyClearedAt).HasColumnName("fully_cleared_at");

            entity.HasIndex(e => new { e.OrganizationId, e.TriggeredAt });
            entity.HasIndex(e => e.DeviceId);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => new { e.OrganizationId, e.Status })
                .HasFilter("\"Status\" = 'PendingClearance'");
            entity.HasIndex(e => new { e.FirstClearanceUserId, e.FirstClearanceAt })
                .HasFilter("first_clearance_user_id IS NOT NULL");
        });

        // User
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Email).IsRequired().HasMaxLength(255);
            entity.HasIndex(e => e.Email).IsUnique();
            entity.Property(e => e.Phone).HasMaxLength(20);
            entity.Property(e => e.PasswordHash).IsRequired().HasMaxLength(255);
            entity.Property(e => e.FirstName).HasMaxLength(100);
            entity.Property(e => e.LastName).HasMaxLength(100);
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(20).HasDefaultValue(UserStatus.Active);
            entity.Property(e => e.EmailVerified).HasDefaultValue(false);
            entity.Property(e => e.PhoneVerified).HasDefaultValue(false);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("NOW()");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("NOW()");
        });

        // UserOrganization (Many-to-Many with Role)
        modelBuilder.Entity<UserOrganization>(entity =>
        {
            entity.ToTable("user_organizations");
            entity.HasKey(e => new { e.UserId, e.OrganizationId });
            entity.Property(e => e.Role).HasConversion<string>().HasMaxLength(50);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("NOW()");

            entity.HasOne(e => e.User)
                .WithMany(u => u.UserOrganizations)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Organization)
                .WithMany(o => o.UserOrganizations)
                .HasForeignKey(e => e.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.OrganizationId);
        });

        // Permission
        modelBuilder.Entity<Permission>(entity =>
        {
            entity.ToTable("permissions");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Role).HasConversion<string>().HasMaxLength(50);
            entity.Property(e => e.Resource).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Action).HasConversion<string>().HasMaxLength(50);
            entity.HasIndex(e => new { e.Role, e.Resource, e.Action }).IsUnique();
        });

        // RefreshToken
        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.ToTable("refresh_tokens");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Token).IsRequired().HasMaxLength(500);
            entity.HasIndex(e => e.Token).IsUnique();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("NOW()");

            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => new { e.UserId, e.ExpiresAt });
        });

        // PushToken
        modelBuilder.Entity<PushToken>(entity =>
        {
            entity.ToTable("push_tokens");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Token).IsRequired().HasMaxLength(500);
            entity.Property(e => e.Platform).IsRequired().HasMaxLength(20);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("NOW()");
            entity.HasIndex(e => new { e.UserId, e.Token }).IsUnique();

            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // AuditLog
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.ToTable("audit_logs");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Action).IsRequired().HasMaxLength(100);
            entity.Property(e => e.EntityType).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Category).HasConversion<string>().HasMaxLength(50);
            entity.Property(e => e.IpAddress).HasMaxLength(45); // IPv6 max length
            entity.Property(e => e.UserAgent).HasMaxLength(500);
            entity.Property(e => e.HttpMethod).HasMaxLength(10);
            entity.Property(e => e.RequestPath).HasMaxLength(500);
            entity.Property(e => e.UserEmail).HasMaxLength(255);
            entity.Property(e => e.OldValues).HasColumnType("jsonb");
            entity.Property(e => e.NewValues).HasColumnType("jsonb");
            entity.Property(e => e.AdditionalInfo).HasColumnType("jsonb");
            entity.Property(e => e.Timestamp).HasDefaultValueSql("NOW()");
            entity.Property(e => e.Success).HasDefaultValue(true);

            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.Organization)
                .WithMany()
                .HasForeignKey(e => e.OrganizationId)
                .OnDelete(DeleteBehavior.SetNull);

            // Indexes for efficient querying
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.OrganizationId);
            entity.HasIndex(e => e.Category);
            entity.HasIndex(e => e.Timestamp);
            entity.HasIndex(e => new { e.EntityType, e.EntityId });
            entity.HasIndex(e => new { e.OrganizationId, e.Timestamp });
        });

        // AlertClearance
        modelBuilder.Entity<AlertClearance>(entity =>
        {
            entity.ToTable("alert_clearances");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ClearanceStep).IsRequired();
            entity.Property(e => e.Notes).HasMaxLength(1000);
            entity.Property(e => e.Location).HasColumnType("jsonb");
            entity.Property(e => e.DeviceInfo).HasMaxLength(500);
            entity.Property(e => e.ClearedAt).HasDefaultValueSql("NOW()");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("NOW()");

            entity.HasOne(e => e.Alert)
                .WithMany(a => a.Clearances)
                .HasForeignKey(e => e.AlertId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Organization)
                .WithMany()
                .HasForeignKey(e => e.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade);

            // Indexes
            entity.HasIndex(e => e.AlertId);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => new { e.OrganizationId, e.ClearedAt });
            entity.HasIndex(e => new { e.AlertId, e.ClearanceStep }).IsUnique();
        });
    }
}
