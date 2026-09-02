using Microsoft.EntityFrameworkCore;
using SRC.Entities;
using SRC.Enums;

namespace SRC.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<DriverProfile> DriverProfile { get; set; }
    public DbSet<UserAccount> UserAccounts { get; set; }
    public DbSet<DriverDocument> DriverDocuments { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<DriverProfile>(entity =>
        {
            entity.ToTable("driverprofiles");

            entity.Property(d => d.DriverId).HasColumnName("driver_id");
            entity.Property(d => d.VehicleMake).HasColumnName("vehicle_make");
            entity.Property(d => d.VehicleModel).HasColumnName("vehicle_model");
            entity.Property(d => d.VehiclePlate).HasColumnName("vehicle_plate");
            entity.Property(d => d.VehicleTypeCode).HasColumnName("vehicle_type_code");
            entity.Property(d => d.LicenseNumber).HasColumnName("license_number");
            entity.Property(d => d.LicenseExpiry).HasColumnName("license_expiry");

            entity.Property(d => d.Status)
                .HasColumnName("status")
                .HasConversion(
                    v => v.ToString().ToLower(),
                    v => (DriverStatus)Enum.Parse(typeof(DriverStatus), v, true));

            entity.Property(d => d.VerifiedAt).HasColumnName("verified_at");

            entity.Property(d => d.CreatedAt)
                .HasColumnName("created_at")
                .HasColumnType("timestamp")
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .ValueGeneratedOnAdd();

            entity.Property(d => d.UpdatedAt)
                .HasColumnName("updated_at")
                .HasColumnType("timestamp")
                // ON UPDATE is what makes MySQL bump the value; a bare default never changes after insert.
                .HasDefaultValueSql("CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP")
                .ValueGeneratedOnAddOrUpdate();
        });

        modelBuilder.Entity<UserAccount>(entity =>
        {
            entity.ToTable("user_accounts");

            entity.Property(u => u.UserId)
                .HasColumnName("user_id")
                .HasMaxLength(64);

            entity.Property(u => u.Status)
                .HasColumnName("status")
                .HasMaxLength(32)
                .HasConversion(
                    v => v.ToString().ToLower(),
                    v => (AccountStatus)Enum.Parse(typeof(AccountStatus), v, true));

            entity.Property(u => u.DeactivatedAt).HasColumnName("deactivated_at");

            entity.Property(u => u.CreatedAt)
                .HasColumnName("created_at")
                .HasColumnType("timestamp")
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .ValueGeneratedOnAdd();

            entity.Property(u => u.UpdatedAt)
                .HasColumnName("updated_at")
                .HasColumnType("timestamp")
                // ON UPDATE is what makes MySQL bump the value; a bare default never changes after insert.
                .HasDefaultValueSql("CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP")
                .ValueGeneratedOnAddOrUpdate();

            entity.HasIndex(u => u.Status).HasDatabaseName("ix_user_accounts_status");
        });

        modelBuilder.Entity<DriverDocument>(entity =>
        {
            entity.ToTable("driver_documents");

            entity.HasKey(d => d.Id);
            entity.Property(d => d.Id).HasColumnName("id");

            // Same type as driverprofiles.driver_id so MySQL accepts the foreign key.
            entity.Property(d => d.DriverId).HasColumnName("driver_id").HasMaxLength(255);

            entity.Property(d => d.Type)
                .HasColumnName("document_type")
                .HasMaxLength(32)
                .HasConversion(
                    v => v.ToString().ToLower(),
                    v => (DriverDocumentType)Enum.Parse(typeof(DriverDocumentType), v, true));

            entity.Property(d => d.FileName).HasColumnName("file_name").HasMaxLength(255);
            entity.Property(d => d.ContentType).HasColumnName("content_type").HasMaxLength(100);
            entity.Property(d => d.SizeBytes).HasColumnName("size_bytes");
            entity.Property(d => d.Content).HasColumnName("content").HasColumnType("longblob");
            entity.Property(d => d.DocumentNumber).HasColumnName("document_number").HasMaxLength(64);
            entity.Property(d => d.ExpiresOn).HasColumnName("expires_on");
            entity.Property(d => d.UploadedAt).HasColumnName("uploaded_at");

            entity.HasIndex(d => new { d.DriverId, d.Type })
                .IsUnique()
                .HasDatabaseName("ux_driver_documents_driver_type");

            entity.HasOne<DriverProfile>()
                .WithMany()
                .HasForeignKey(d => d.DriverId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
