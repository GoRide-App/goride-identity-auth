using Microsoft.EntityFrameworkCore;
using SRC.Data;
using SRC.Dtos;
using SRC.Entities;
using SRC.Enums;
using SRC.Services.Interfaces;

namespace SRC.Services.Impl
{
    public class DriverProfileServiceImpl : IDriverProfileService
    {
        private readonly AppDbContext _context;
        private readonly IDriverStatusAuditStore _auditStore;

        public DriverProfileServiceImpl(AppDbContext context, IDriverStatusAuditStore auditStore)
        {
            _context = context;
            _auditStore = auditStore;
        }

        async Task<DriverProfile> IDriverProfileService.AddProfile(string sub, CreateDriverProfileRequestDto request)
        {
            var exists = await _context.DriverProfile.AnyAsync(d => d.DriverId == sub);
            if (exists) throw new HttpRequestException("A profile already exists for this driver!!!");

            var profile = new DriverProfile
            {
                DriverId = sub,
                VehicleMake = request.VehicleMake,
                VehicleModel = request.VehicleModel,
                VehiclePlate = request.VehiclePlate,
                VehicleTypeCode = request.VehicleTypeCode,
                LicenseNumber = request.LicenseNumber,
                LicenseExpiry = request.LicenseExpiry,
                Status = DriverStatus.PendingVerification
            };

            _context.DriverProfile.Add(profile);
            await _context.SaveChangesAsync();
            return profile;
        }

        async Task<VehicleDto?> IDriverProfileService.GetVehicleById(string sub)
        {
            return await _context.DriverProfile
                .AsNoTracking()
                .Where(d => d.DriverId == sub)
                .Select(d => new VehicleDto
                {
                    VehicleMake = d.VehicleMake,
                    VehicleModel = d.VehicleModel,
                    VehiclePlate = d.VehiclePlate,
                    VehicleTypeCode = d.VehicleTypeCode,
                    LicenseNumber = d.LicenseNumber,
                    LicenseExpiry = d.LicenseExpiry
                }).FirstOrDefaultAsync();
        }

        async Task<DriverProfile?> IDriverProfileService.UpdateVehicle(string sub, string usrSub, UpdateVehicleDto request, bool isAdmin)
        {
            var driver = await _context.DriverProfile.FirstOrDefaultAsync(v => v.DriverId == sub);
            if (driver is null) return null;

            if (!isAdmin)
            {
                if (driver.DriverId != usrSub)
                    throw new UnauthorizedAccessException("You can only update your own vehicle.");
            }

            driver.VehicleMake = request.VehicleMake;
            driver.VehicleModel = request.VehicleModel;
            driver.VehiclePlate = request.VehiclePlate;
            driver.VehicleTypeCode = request.VehicleTypeCode;
            driver.LicenseNumber = request.LicenseNumber;
            driver.LicenseExpiry = request.LicenseExpiry;

            await _context.SaveChangesAsync();
            return driver;
        }

        public async Task<IEnumerable<DriverAccountAdminDto>> GetAllDriverAccountsAsync(DriverStatus? statusFilter = null)
        {
            var query = _context.DriverProfile.AsNoTracking();
            if (statusFilter.HasValue)
            {
                query = query.Where(d => d.Status == statusFilter.Value);
            }

            var drivers = await query.ToListAsync();

            return drivers.Select(d => new DriverAccountAdminDto
            {
                DriverId = d.DriverId,
                VehicleMake = d.VehicleMake,
                VehicleModel = d.VehicleModel,
                VehiclePlate = d.VehiclePlate,
                VehicleTypeCode = d.VehicleTypeCode,
                LicenseNumber = d.LicenseNumber,
                LicenseExpiry = d.LicenseExpiry,
                Status = d.Status,
                StatusReason = _auditStore.GetLatestReason(d.DriverId),
                VerifiedAt = d.VerifiedAt,
                CreatedAt = d.CreatedAt,
                UpdatedAt = d.UpdatedAt
            });
        }

        public async Task<DriverAccountAdminDto?> GetDriverAccountByIdAsync(string driverId)
        {
            var d = await _context.DriverProfile.AsNoTracking().FirstOrDefaultAsync(d => d.DriverId == driverId);
            if (d is null) return null;

            return new DriverAccountAdminDto
            {
                DriverId = d.DriverId,
                VehicleMake = d.VehicleMake,
                VehicleModel = d.VehicleModel,
                VehiclePlate = d.VehiclePlate,
                VehicleTypeCode = d.VehicleTypeCode,
                LicenseNumber = d.LicenseNumber,
                LicenseExpiry = d.LicenseExpiry,
                Status = d.Status,
                StatusReason = _auditStore.GetLatestReason(d.DriverId),
                VerifiedAt = d.VerifiedAt,
                CreatedAt = d.CreatedAt,
                UpdatedAt = d.UpdatedAt
            };
        }

        public async Task<DriverProfile?> UpdateDriverStatusByAdminAsync(string driverId, DriverStatus newStatus, string reason)
        {
            var driver = await _context.DriverProfile.FirstOrDefaultAsync(d => d.DriverId == driverId);
            if (driver is null) return null;

            driver.Status = newStatus;
            driver.UpdatedAt = DateTime.UtcNow;

            if (newStatus == DriverStatus.Active)
            {
                driver.VerifiedAt = DateTime.UtcNow;
            }

            _auditStore.RecordReason(driverId, reason);

            await _context.SaveChangesAsync();
            return driver;
        }

        public async Task<DriverStateEnforcementResponseDto> CheckDriverStateEnforcementAsync(string driverId)
        {
            var driver = await _context.DriverProfile.AsNoTracking().FirstOrDefaultAsync(d => d.DriverId == driverId);
            if (driver is null)
            {
                return new DriverStateEnforcementResponseDto
                {
                    DriverId = driverId,
                    Status = DriverStatus.PendingVerification,
                    StatusReason = null,
                    CanAcceptTrips = false,
                    Message = "Driver profile not found."
                };
            }

            var reason = _auditStore.GetLatestReason(driverId);
            var canAcceptTrips = driver.Status == DriverStatus.Active;
            var message = canAcceptTrips
                ? "Driver account is active and eligible to receive trip bookings."
                : $"Driver account status is '{driver.Status}'. Action reason: {reason ?? "No reason specified."}";

            return new DriverStateEnforcementResponseDto
            {
                DriverId = driver.DriverId,
                Status = driver.Status,
                StatusReason = reason,
                CanAcceptTrips = canAcceptTrips,
                Message = message
            };
        }

        public async Task<DriverStateEnforcementResponseDto> GoOnlineAsync(string driverId)
        {
            var driver = await _context.DriverProfile.FirstOrDefaultAsync(d => d.DriverId == driverId);
            if (driver is null)
            {
                return new DriverStateEnforcementResponseDto
                {
                    DriverId = driverId,
                    Status = DriverStatus.PendingVerification,
                    StatusReason = null,
                    CanAcceptTrips = false,
                    Message = "Driver profile not found."
                };
            }

            var reason = _auditStore.GetLatestReason(driverId);

            if (driver.Status == DriverStatus.Suspended ||
                driver.Status == DriverStatus.Rejected ||
                driver.Status == DriverStatus.PendingVerification ||
                driver.Status == DriverStatus.Deactivated ||
                driver.Status == DriverStatus.DocumentReview)
            {
                return new DriverStateEnforcementResponseDto
                {
                    DriverId = driver.DriverId,
                    Status = driver.Status,
                    StatusReason = reason,
                    CanAcceptTrips = false,
                    Message = $"Cannot go online. Account status is '{driver.Status}'. Reason: {reason ?? "Verification / Account constraint"}"
                };
            }

            driver.Status = DriverStatus.Active;
            driver.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return new DriverStateEnforcementResponseDto
            {
                DriverId = driver.DriverId,
                Status = DriverStatus.Active,
                StatusReason = reason,
                CanAcceptTrips = true,
                Message = "Driver is now online and active to receive trip bookings."
            };
        }

        public async Task<DriverStateEnforcementResponseDto> GoOfflineAsync(string driverId)
        {
            var driver = await _context.DriverProfile.FirstOrDefaultAsync(d => d.DriverId == driverId);
            if (driver is null)
            {
                return new DriverStateEnforcementResponseDto
                {
                    DriverId = driverId,
                    Status = DriverStatus.Offline,
                    StatusReason = null,
                    CanAcceptTrips = false,
                    Message = "Driver profile not found."
                };
            }

            var reason = _auditStore.GetLatestReason(driverId);

            driver.Status = DriverStatus.Offline;
            driver.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return new DriverStateEnforcementResponseDto
            {
                DriverId = driver.DriverId,
                Status = DriverStatus.Offline,
                StatusReason = reason,
                CanAcceptTrips = false,
                Message = "Driver is now offline. Cannot accept trips and hidden from active driver map."
            };
        }
    }
}