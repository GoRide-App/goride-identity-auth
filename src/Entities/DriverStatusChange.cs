using SRC.Enums;

namespace SRC.Entities;

/// <summary>
/// Audit row for every verification-state change: who moved the driver from which status to
/// which, why, and when. The latest row's reason is what the driver and admins see.
/// </summary>
public class DriverStatusChange
{
    public long Id { get; set; }
    public string DriverId { get; set; } = null!;
    public DriverStatus FromStatus { get; set; }
    public DriverStatus ToStatus { get; set; }
    public string Reason { get; set; } = null!;
    /// <summary>Asgardeo user id of the admin (or of the driver for document-triggered moves).</summary>
    public string ChangedBy { get; set; } = null!;
    public DateTime ChangedAt { get; set; }
}
