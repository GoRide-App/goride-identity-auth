namespace SRC.Enums;

public enum DriverDocumentType
{
    DrivingLicence,
    VehicleRegistration,
    VehicleInsurance,
    VehicleRevenueLicence
}

public static class DriverDocumentTypes
{
    /// <summary>Documents an admin needs to see before a driver can be approved (SCRUM-43).</summary>
    public static readonly IReadOnlyList<DriverDocumentType> RequiredForApproval =
    [
        DriverDocumentType.DrivingLicence,
        DriverDocumentType.VehicleRegistration,
        DriverDocumentType.VehicleInsurance
    ];
}
