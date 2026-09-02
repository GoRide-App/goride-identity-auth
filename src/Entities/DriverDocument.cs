using SRC.Enums;

namespace SRC.Entities;

/// <summary>
/// One uploaded verification document per (driver, type). Re-uploading the same type replaces
/// the previous file. Content lives in the database because the service has no blob store.
/// </summary>
public class DriverDocument
{
    public long Id { get; set; }
    public string DriverId { get; set; } = null!;
    public DriverDocumentType Type { get; set; }
    public string FileName { get; set; } = null!;
    public string ContentType { get; set; } = null!;
    public int SizeBytes { get; set; }
    public byte[] Content { get; set; } = [];
    public string? DocumentNumber { get; set; }
    public DateOnly? ExpiresOn { get; set; }
    public DateTime UploadedAt { get; set; }
}
