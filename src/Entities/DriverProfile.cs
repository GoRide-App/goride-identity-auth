using System.ComponentModel.DataAnnotations;
using SRC.Enums;

namespace SRC.Entities;

public class DriverProfile
{
    [Key]
    public string DriverId { get; set; } = null!;      // char(36) — often a GUID
    public string VehicleMake { get; set; } = null!;
    public string VehicleModel { get; set; } = null!;
    public string VehiclePlate { get; set; } = null!;
    public string VehicleTypeCode { get; set; } = null!;
    public string LicenseNumber { get; set; } = null!;
    public DateOnly LicenseExpiry { get; set; }
    public DriverStatus Status { get; set; }
    public DateTime? VerifiedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}