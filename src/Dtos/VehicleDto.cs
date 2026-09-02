namespace SRC.Dtos;

public class VehicleDto
{
    public string VehicleMake { get; set; } = null!;
    public string VehicleModel { get; set; } = null!;
    public string VehiclePlate { get; set; } = null!;
    public string VehicleTypeCode { get; set; } = null!;
    public string LicenseNumber { get; set; } = null!;
    public DateOnly LicenseExpiry { get; set; }
}