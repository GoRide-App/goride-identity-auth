using SRC.Enums;

namespace SRC.Dtos;

public class DriverDocumentDto
{
    public long Id { get; set; }
    public DriverDocumentType Type { get; set; }
    public string FileName { get; set; } = null!;
    public string ContentType { get; set; } = null!;
    public int SizeBytes { get; set; }
    public string? DocumentNumber { get; set; }
    public DateOnly? ExpiresOn { get; set; }
    public DateTime UploadedAt { get; set; }
}
