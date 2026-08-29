using Microsoft.EntityFrameworkCore;
using SRC.Entities;
using SRC.Enums;

namespace SRC.Data;

public class AppDbContext: DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options): base(options){}

    public DbSet<DriverProfile> DriverProfile {get; set;}


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
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .ValueGeneratedOnAddOrUpdate();
        });
    }
}