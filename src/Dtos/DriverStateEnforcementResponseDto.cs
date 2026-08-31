using SRC.Enums;

namespace SRC.Dtos;

public class DriverStateEnforcementResponseDto
{
    public string DriverId { get; set; } = null!;
    public DriverStatus Status { get; set; }
    public string? StatusReason { get; set; }
    public bool CanAcceptTrips { get; set; }
    public string Message { get; set; } = null!;
}
