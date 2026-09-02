using SRC.Enums;

namespace SRC.Dtos;

/// <summary>What a driver (or an admin) sees for a driver: vehicle, licence and verification state.</summary>
public class DriverProfileDto
{
    public string DriverId { get; set; } = null!;
    public string VehicleMake { get; set; } = null!;
    public string VehicleModel { get; set; } = null!;
    public string VehiclePlate { get; set; } = null!;
    public string VehicleTypeCode { get; set; } = null!;
    public string LicenseNumber { get; set; } = null!;
    public DateOnly LicenseExpiry { get; set; }

    public DriverStatus Status { get; set; }

    /// <summary>Reason recorded with the most recent status change (admin decision or document review).</summary>
    public string? StatusReason { get; set; }

    public DateTime? VerifiedAt { get; set; }

    /// <summary>True only while the driver is approved and online.</summary>
    public bool CanAcceptTrips { get; set; }

    public IReadOnlyList<DriverDocumentDto> Documents { get; set; } = [];

    /// <summary>Required document types that have not been uploaded yet.</summary>
    public IReadOnlyList<DriverDocumentType> MissingDocuments { get; set; } = [];

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
