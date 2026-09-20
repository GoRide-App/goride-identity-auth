namespace SRC.Dtos;

public class DriverProfileRequestDto
{
    /// <summary>Filter to one vehicle type (e.g. "TUK"); leave empty for drivers of every type.</summary>
    public string? VehicleType { get; set; }
}
