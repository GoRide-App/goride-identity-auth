using System.Collections.Concurrent;
using SRC.Services.Interfaces;

namespace SRC.Services.Impl;

public class DriverStatusAuditStoreImpl : IDriverStatusAuditStore
{
    private readonly ConcurrentDictionary<string, string> _reasons = new();

    public void RecordReason(string driverId, string reason)
    {
        if (string.IsNullOrWhiteSpace(driverId)) return;
        _reasons[driverId] = reason;
    }

    public string? GetLatestReason(string driverId)
    {
        if (string.IsNullOrWhiteSpace(driverId)) return null;
        _reasons.TryGetValue(driverId, out var reason);
        return reason;
    }
}
