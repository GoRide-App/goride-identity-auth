using System.ComponentModel.DataAnnotations;
using SRC.Validation;

namespace SRC.Dtos;

/// <summary>
/// Vehicle and licence details a driver may change themselves. Verification status is not
/// part of this contract: only an admin decision (SCRUM-43) moves it.
/// </summary>
public class UpdateVehicleDto
{
    [Required, StringLength(50, MinimumLength = 2)]
    public string VehicleMake { get; set; } = null!;

    [Required, StringLength(50, MinimumLength = 1)]
    public string VehicleModel { get; set; } = null!;

    [Required, StringLength(20, MinimumLength = 4)]
    public string VehiclePlate { get; set; } = null!;

    [Required, StringLength(20, MinimumLength = 2)]
    public string VehicleTypeCode { get; set; } = null!;

    [Required, StringLength(32, MinimumLength = 6)]
    public string LicenseNumber { get; set; } = null!;

    [FutureDate]
    public DateOnly LicenseExpiry { get; set; }
}
