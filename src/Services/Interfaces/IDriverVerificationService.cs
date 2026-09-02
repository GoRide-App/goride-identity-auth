using SRC.Dtos;
using SRC.Enums;

namespace SRC.Services.Interfaces;

/// <summary>Driver verification: document uploads (SCRUM-42) and the resulting profile view.</summary>
public interface IDriverVerificationService
{
    /// <summary>Full profile including verification state and documents; null when no driver profile exists.</summary>
    Task<DriverProfileDto?> GetProfileAsync(string driverId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates and stores (or replaces) one document. Nothing is written unless every check passes.
    /// </summary>
    Task<DocumentUploadResult> UploadDocumentAsync(string driverId, DriverDocumentUpload upload, CancellationToken cancellationToken = default);

    /// <summary>The stored file for download; null when the driver has no such document.</summary>
    Task<DriverDocumentFile?> GetDocumentAsync(string driverId, DriverDocumentType type, CancellationToken cancellationToken = default);
}

public sealed record DriverDocumentUpload(
    DriverDocumentType Type,
    string FileName,
    string? DeclaredContentType,
    byte[] Content,
    string? DocumentNumber,
    DateOnly? ExpiresOn);

public sealed record DriverDocumentFile(string FileName, string ContentType, byte[] Content);

public enum DocumentUploadOutcome
{
    Stored,
    NoDriverProfile,
    InvalidFile
}

public sealed record DocumentUploadResult(DocumentUploadOutcome Outcome, DriverDocumentDto? Document = null, string? Error = null)
{
    public static DocumentUploadResult Stored(DriverDocumentDto document) => new(DocumentUploadOutcome.Stored, document);
    public static DocumentUploadResult NoDriverProfile() => new(DocumentUploadOutcome.NoDriverProfile,
        Error: "Create your driver profile (vehicle and licence details) before uploading documents.");
    public static DocumentUploadResult InvalidFile(string error) => new(DocumentUploadOutcome.InvalidFile, Error: error);
}
