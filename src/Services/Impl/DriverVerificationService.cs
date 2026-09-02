using Microsoft.EntityFrameworkCore;
using SRC.Data;
using SRC.Dtos;
using SRC.Entities;
using SRC.Enums;
using SRC.Services.Interfaces;

namespace SRC.Services.Impl;

public sealed class DriverVerificationService : IDriverVerificationService
{
    private readonly AppDbContext _db;
    private readonly ILogger<DriverVerificationService> _logger;
    private readonly TimeProvider _clock;

    public DriverVerificationService(AppDbContext db, ILogger<DriverVerificationService> logger, TimeProvider? clock = null)
    {
        _db = db;
        _logger = logger;
        _clock = clock ?? TimeProvider.System;
    }

    public async Task<DriverProfileDto?> GetProfileAsync(string driverId, CancellationToken cancellationToken = default)
    {
        var driver = await _db.DriverProfile.AsNoTracking()
            .SingleOrDefaultAsync(d => d.DriverId == driverId, cancellationToken);
        if (driver is null) return null;

        var documents = await ListDocumentsAsync(driverId, cancellationToken);
        return ToDto(driver, documents);
    }

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
                driver.Status = DriverStatus.DocumentReview;
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

    private Task<List<DriverDocumentDto>> ListDocumentsAsync(string driverId, CancellationToken cancellationToken) =>
        _db.DriverDocuments.AsNoTracking()
            .Where(d => d.DriverId == driverId)
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

    internal static DriverProfileDto ToDto(DriverProfile driver, IReadOnlyList<DriverDocumentDto> documents)
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
            VerifiedAt = driver.VerifiedAt,
            CanAcceptTrips = driver.Status == DriverStatus.Active,
            Documents = documents,
            MissingDocuments = DriverDocumentTypes.RequiredForApproval.Where(t => !uploaded.Contains(t)).ToList(),
            CreatedAt = driver.CreatedAt,
            UpdatedAt = driver.UpdatedAt
        };
    }

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
