using SRC.Enums;

namespace SRC.Dtos;

/// <summary>What the driver app needs to enforce the current verification state (SCRUM-43).</summary>
public class DriverStateEnforcementResponseDto
{
    public string DriverId { get; set; } = null!;
    public DriverStatus Status { get; set; }
    /// <summary>Reason recorded with the most recent status change, if any.</summary>
    public string? StatusReason { get; set; }
    /// <summary>True only while approved and online.</summary>
    public bool CanAcceptTrips { get; set; }
    public string Message { get; set; } = null!;
}
