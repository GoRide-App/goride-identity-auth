using Microsoft.EntityFrameworkCore;
using SRC.Data;
using SRC.Dtos;
using SRC.Entities;
using SRC.Enums;
using SRC.Services.Interfaces;

namespace SRC.Services.Impl;

public sealed class DriverVerificationService : IDriverVerificationService
{
    private const string DocumentsCompleteReason = "Required documents uploaded; awaiting admin review.";
    private const string DefaultApprovalReason = "Documents verified and registration approved.";

    private static readonly DriverStatus[] ApprovableFrom =
        [DriverStatus.PendingVerification, DriverStatus.DocumentReview, DriverStatus.Rejected];

    private readonly AppDbContext _db;
    private readonly ILogger<DriverVerificationService> _logger;
    private readonly TimeProvider _clock;

    public DriverVerificationService(AppDbContext db, ILogger<DriverVerificationService> logger, TimeProvider? clock = null)
    {
        _db = db;
        _logger = logger;
        _clock = clock ?? TimeProvider.System;
    }

    // ------------------------------------------------------------------ profile

    public async Task<DriverProfileDto?> GetProfileAsync(string driverId, CancellationToken cancellationToken = default)
    {
        var driver = await _db.DriverProfile.AsNoTracking()
            .SingleOrDefaultAsync(d => d.DriverId == driverId, cancellationToken);
        if (driver is null) return null;

        return await BuildProfileAsync(driver, cancellationToken);
    }

    // ------------------------------------------------------------------ SCRUM-42: documents

    public async Task<DocumentUploadResult> UploadDocumentAsync(string driverId, DriverDocumentUpload upload, CancellationToken cancellationToken = default)
    {
        // Every check runs before the first write, so an invalid submission changes nothing.
        var (contentType, error) = DriverDocumentValidator.Validate(upload.Content, upload.FileName);
        if (error is not null)
            return DocumentUploadResult.InvalidFile(error);

        var driver = await _db.DriverProfile.SingleOrDefaultAsync(d => d.DriverId == driverId, cancellationToken);
        if (driver is null)
            return DocumentUploadResult.NoDriverProfile();

        var now = _clock.GetUtcNow().UtcDateTime;
        var document = await _db.DriverDocuments
            .SingleOrDefaultAsync(d => d.DriverId == driverId && d.Type == upload.Type, cancellationToken);

        if (document is null)
        {
            document = new DriverDocument { DriverId = driverId, Type = upload.Type };
            _db.DriverDocuments.Add(document);
        }

        document.FileName = Path.GetFileName(upload.FileName);
        document.ContentType = contentType!;
        document.SizeBytes = upload.Content.Length;
        document.Content = upload.Content;
        document.DocumentNumber = string.IsNullOrWhiteSpace(upload.DocumentNumber) ? null : upload.DocumentNumber.Trim();
        document.ExpiresOn = upload.ExpiresOn;
        document.UploadedAt = now;

        // A driver waiting on documents (or fixing a rejection) moves to review once the
        // required set is complete. Approved, suspended or deactivated drivers keep their state.
        if (driver.Status is DriverStatus.PendingVerification or DriverStatus.Rejected)
        {
            var uploadedTypes = await _db.DriverDocuments
                .Where(d => d.DriverId == driverId)
                .Select(d => d.Type)
                .ToListAsync(cancellationToken);
            uploadedTypes.Add(upload.Type);

            if (DriverDocumentTypes.RequiredForApproval.All(uploadedTypes.Contains))
                Transition(driver, DriverStatus.DocumentReview, DocumentsCompleteReason, driverId, now);
        }

        await _db.SaveChangesAsync(cancellationToken); // one transaction: document + status together

        _logger.LogInformation("Driver {DriverId} uploaded {DocumentType} ({Bytes} bytes); status now {Status}.",
            driverId, upload.Type, document.SizeBytes, driver.Status);

        return DocumentUploadResult.Stored(ToDto(document));
    }

    public async Task<DriverDocumentFile?> GetDocumentAsync(string driverId, DriverDocumentType type, CancellationToken cancellationToken = default)
    {
        return await _db.DriverDocuments.AsNoTracking()
            .Where(d => d.DriverId == driverId && d.Type == type)
            .Select(d => new DriverDocumentFile(d.FileName, d.ContentType, d.Content))
            .SingleOrDefaultAsync(cancellationToken);
    }

    // ------------------------------------------------------------------ SCRUM-43: admin decisions

    public async Task<DriverDecisionResult> ApproveAsync(string driverId, string adminId, string? reason, CancellationToken cancellationToken = default)
    {
        var driver = await _db.DriverProfile.SingleOrDefaultAsync(d => d.DriverId == driverId, cancellationToken);
        if (driver is null) return DriverDecisionResult.NotFound();

        if (driver.Status is DriverStatus.Active or DriverStatus.Offline)
            return DriverDecisionResult.InvalidTransition("This driver is already approved.");
        if (!ApprovableFrom.Contains(driver.Status))
            return DriverDecisionResult.InvalidTransition($"Cannot approve a driver whose status is {driver.Status}.");

        var uploaded = await _db.DriverDocuments
            .Where(d => d.DriverId == driverId)
            .Select(d => d.Type)
            .ToListAsync(cancellationToken);
        var missing = DriverDocumentTypes.RequiredForApproval.Where(t => !uploaded.Contains(t)).ToList();
        if (missing.Count > 0)
            return DriverDecisionResult.MissingDocuments(missing);

        var now = _clock.GetUtcNow().UtcDateTime;
        Transition(driver, DriverStatus.Active, string.IsNullOrWhiteSpace(reason) ? DefaultApprovalReason : reason.Trim(), adminId, now);
        driver.VerifiedAt = now;
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Admin {AdminId} approved driver {DriverId}.", adminId, driverId);
        return DriverDecisionResult.Applied(await BuildProfileAsync(driver, cancellationToken));
    }

    public async Task<DriverDecisionResult> RejectAsync(string driverId, string adminId, string reason, CancellationToken cancellationToken = default)
    {
        var driver = await _db.DriverProfile.SingleOrDefaultAsync(d => d.DriverId == driverId, cancellationToken);
        if (driver is null) return DriverDecisionResult.NotFound();

        if (driver.Status == DriverStatus.Rejected)
            return DriverDecisionResult.InvalidTransition("This driver's registration is already rejected.");
        if (driver.Status == DriverStatus.Deactivated)
            return DriverDecisionResult.InvalidTransition("This account is deactivated; there is no registration to reject.");

        var now = _clock.GetUtcNow().UtcDateTime;
        Transition(driver, DriverStatus.Rejected, reason.Trim(), adminId, now);
        driver.VerifiedAt = null; // a previously approved driver loses verification immediately
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Admin {AdminId} rejected driver {DriverId}: {Reason}", adminId, driverId, reason);
        return DriverDecisionResult.Applied(await BuildProfileAsync(driver, cancellationToken));
    }

    // ------------------------------------------------------------------ SCRUM-43: enforcement

    public async Task<DriverStateEnforcementResponseDto?> GetEnforcementStateAsync(string driverId, CancellationToken cancellationToken = default)
    {
        var driver = await _db.DriverProfile.AsNoTracking()
            .SingleOrDefaultAsync(d => d.DriverId == driverId, cancellationToken);
        if (driver is null) return null;

        return ToEnforcement(driver, await LatestReasonAsync(driverId, cancellationToken));
    }

    public async Task<DriverOnlineResult?> GoOnlineAsync(string driverId, CancellationToken cancellationToken = default)
    {
        var driver = await _db.DriverProfile.SingleOrDefaultAsync(d => d.DriverId == driverId, cancellationToken);
        if (driver is null) return null;

        var reason = await LatestReasonAsync(driverId, cancellationToken);

        // Scenario 2: the state saved by the admin is what decides, every time.
        if (driver.Status is not (DriverStatus.Active or DriverStatus.Offline))
            return new DriverOnlineResult(Allowed: false, ToEnforcement(driver, reason));

        if (driver.Status == DriverStatus.Offline)
        {
            driver.Status = DriverStatus.Active;
            await _db.SaveChangesAsync(cancellationToken);
        }

        return new DriverOnlineResult(Allowed: true, ToEnforcement(driver, reason));
    }

    public async Task<DriverOnlineResult?> GoOfflineAsync(string driverId, CancellationToken cancellationToken = default)
    {
        var driver = await _db.DriverProfile.SingleOrDefaultAsync(d => d.DriverId == driverId, cancellationToken);
        if (driver is null) return null;

        if (driver.Status == DriverStatus.Active)
        {
            driver.Status = DriverStatus.Offline;
            await _db.SaveChangesAsync(cancellationToken);
        }

        return new DriverOnlineResult(Allowed: true, ToEnforcement(driver, await LatestReasonAsync(driverId, cancellationToken)));
    }

    // ------------------------------------------------------------------ helpers

    private void Transition(DriverProfile driver, DriverStatus to, string reason, string changedBy, DateTime at)
    {
        _db.DriverStatusChanges.Add(new DriverStatusChange
        {
            DriverId = driver.DriverId,
            FromStatus = driver.Status,
            ToStatus = to,
            Reason = reason,
            ChangedBy = changedBy,
            ChangedAt = at
        });
        driver.Status = to;
    }

    private Task<string?> LatestReasonAsync(string driverId, CancellationToken cancellationToken) =>
        _db.DriverStatusChanges.AsNoTracking()
            .Where(c => c.DriverId == driverId)
            .OrderByDescending(c => c.ChangedAt)
            .ThenByDescending(c => c.Id)
            .Select(c => c.Reason)
            .FirstOrDefaultAsync(cancellationToken);

    private async Task<DriverProfileDto> BuildProfileAsync(DriverProfile driver, CancellationToken cancellationToken)
    {
        var documents = await _db.DriverDocuments.AsNoTracking()
            .Where(d => d.DriverId == driver.DriverId)
            .OrderBy(d => d.Type)
            .Select(d => new DriverDocumentDto
            {
                // Projection on purpose: never pull the blob column just to list documents.
                Id = d.Id,
                Type = d.Type,
                FileName = d.FileName,
                ContentType = d.ContentType,
                SizeBytes = d.SizeBytes,
                DocumentNumber = d.DocumentNumber,
                ExpiresOn = d.ExpiresOn,
                UploadedAt = d.UploadedAt
            })
            .ToListAsync(cancellationToken);

        return ToDto(driver, documents, await LatestReasonAsync(driver.DriverId, cancellationToken));
    }

    internal static DriverProfileDto ToDto(DriverProfile driver, IReadOnlyList<DriverDocumentDto> documents, string? statusReason)
    {
        var uploaded = documents.Select(d => d.Type).ToHashSet();
        return new DriverProfileDto
        {
            DriverId = driver.DriverId,
            VehicleMake = driver.VehicleMake,
            VehicleModel = driver.VehicleModel,
            VehiclePlate = driver.VehiclePlate,
            VehicleTypeCode = driver.VehicleTypeCode,
            LicenseNumber = driver.LicenseNumber,
            LicenseExpiry = driver.LicenseExpiry,
            Status = driver.Status,
            StatusReason = statusReason,
            VerifiedAt = driver.VerifiedAt,
            CanAcceptTrips = driver.Status == DriverStatus.Active,
            Documents = documents,
            MissingDocuments = DriverDocumentTypes.RequiredForApproval.Where(t => !uploaded.Contains(t)).ToList(),
            CreatedAt = driver.CreatedAt,
            UpdatedAt = driver.UpdatedAt
        };
    }

    internal static DriverStateEnforcementResponseDto ToEnforcement(DriverProfile driver, string? statusReason) => new()
    {
        DriverId = driver.DriverId,
        Status = driver.Status,
        StatusReason = statusReason,
        CanAcceptTrips = driver.Status == DriverStatus.Active,
        Message = driver.Status switch
        {
            DriverStatus.Active => "You are online and can accept trips.",
            DriverStatus.Offline => "You are approved and currently offline.",
            DriverStatus.PendingVerification => "Upload your driving licence, vehicle registration and insurance; an admin must approve your registration before you can go online.",
            DriverStatus.DocumentReview => "Your documents are being reviewed. You can go online once an admin approves your registration.",
            DriverStatus.Rejected => "Your registration was rejected" + (statusReason is null ? "." : $": {statusReason}") + " Fix the issue and re-upload your documents.",
            DriverStatus.Suspended => "Your driver account is suspended" + (statusReason is null ? "." : $": {statusReason}"),
            DriverStatus.Deactivated => "This account has been deactivated.",
            _ => "Your driver account is not able to accept trips."
        }
    };

    private static DriverDocumentDto ToDto(DriverDocument d) => new()
    {
        Id = d.Id,
        Type = d.Type,
        FileName = d.FileName,
        ContentType = d.ContentType,
        SizeBytes = d.SizeBytes,
        DocumentNumber = d.DocumentNumber,
        ExpiresOn = d.ExpiresOn,
        UploadedAt = d.UploadedAt
    };
}
