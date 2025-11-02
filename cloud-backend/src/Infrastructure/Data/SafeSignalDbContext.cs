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

            entity.HasOne(e => e.Device)
                .WithMany(d => d.Alerts)
                .HasForeignKey(e => e.DeviceId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.Room)
                .WithMany(r => r.Alerts)
                .HasForeignKey(e => e.RoomId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(e => new { e.OrganizationId, e.TriggeredAt });
            entity.HasIndex(e => e.DeviceId);
            entity.HasIndex(e => e.Status);
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
    }
}
