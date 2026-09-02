using System.ComponentModel.DataAnnotations;
using SRC.Validation;

namespace SRC.Dtos;

public class CreateDriverProfileRequestDto
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
