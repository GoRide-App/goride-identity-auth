namespace SRC.Services.Interfaces;

public interface IDriverStatusAuditStore
{
    void RecordReason(string driverId, string reason);
    string? GetLatestReason(string driverId);
}
