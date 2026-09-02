using System.ComponentModel.DataAnnotations;
using SRC.Enums;
using SRC.Validation;

namespace SRC.Dtos;

/// <summary>multipart/form-data body for POST /api/driver/documents.</summary>
public class UploadDriverDocumentRequestDto
{
    /// <summary>DrivingLicence, VehicleRegistration, VehicleInsurance or VehicleRevenueLicence.</summary>
    [Required]
    public DriverDocumentType? Type { get; set; }

    /// <summary>JPEG, PNG or PDF, at most 5 MB.</summary>
    [Required]
    public IFormFile? File { get; set; }

    /// <summary>Number printed on the document (licence number, registration number, policy number).</summary>
    [StringLength(64)]
    public string? DocumentNumber { get; set; }

    /// <summary>Expiry printed on the document; must be in the future when supplied.</summary>
    [FutureDate]
    public DateOnly? ExpiresOn { get; set; }
}
