using SRC.Dtos;
using SRC.Enums;

namespace SRC.Services.Interfaces;

/// <summary>
/// Driver verification: document uploads (SCRUM-42), admin approve/reject decisions and
/// the enforcement of the resulting state when a driver goes online (SCRUM-43).
/// </summary>
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

    /// <summary>Admin approves the registration: status becomes Active and the decision is recorded.</summary>
    Task<DriverDecisionResult> ApproveAsync(string driverId, string adminId, string? reason, CancellationToken cancellationToken = default);

    /// <summary>Admin rejects the registration: status becomes Rejected and the reason is recorded.</summary>
    Task<DriverDecisionResult> RejectAsync(string driverId, string adminId, string reason, CancellationToken cancellationToken = default);

    /// <summary>Current state as the driver app must enforce it; null when no driver profile exists.</summary>
    Task<DriverStateEnforcementResponseDto?> GetEnforcementStateAsync(string driverId, CancellationToken cancellationToken = default);

    /// <summary>Driver asks to accept trips. Allowed only from Active or Offline.</summary>
    Task<DriverOnlineResult?> GoOnlineAsync(string driverId, CancellationToken cancellationToken = default);

    /// <summary>Driver stops accepting trips. Never refused; the state simply reflects reality.</summary>
    Task<DriverOnlineResult?> GoOfflineAsync(string driverId, CancellationToken cancellationToken = default);
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

public enum DriverDecisionOutcome
{
    Applied,
    NotFound,
    InvalidTransition,
    MissingDocuments
}

public sealed record DriverDecisionResult(DriverDecisionOutcome Outcome, DriverProfileDto? Profile = null, string? Error = null)
{
    public static DriverDecisionResult Applied(DriverProfileDto profile) => new(DriverDecisionOutcome.Applied, profile);
    public static DriverDecisionResult NotFound() => new(DriverDecisionOutcome.NotFound);
    public static DriverDecisionResult InvalidTransition(string error) => new(DriverDecisionOutcome.InvalidTransition, Error: error);
    public static DriverDecisionResult MissingDocuments(IEnumerable<DriverDocumentType> missing) => new(
        DriverDecisionOutcome.MissingDocuments,
        Error: "Cannot approve: required documents are missing (" + string.Join(", ", missing) + ").");
}

/// <summary>Outcome of a go-online/go-offline request. Allowed is false when the state forbids going online.</summary>
public sealed record DriverOnlineResult(bool Allowed, DriverStateEnforcementResponseDto State);
