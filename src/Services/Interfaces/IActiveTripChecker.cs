namespace SRC.Services.Interfaces;

/// <summary>Answers whether a user currently has a trip that is not in a terminal state.</summary>
public interface IActiveTripChecker
{
    /// <exception cref="TripStatusUnavailableException">
    /// The trip service is configured but could not give a definite answer.
    /// </exception>
    Task<bool> HasActiveTripAsync(string userId, CancellationToken cancellationToken = default);
}

public sealed class TripStatusUnavailableException : Exception
{
    public TripStatusUnavailableException(string message, Exception? inner = null) : base(message, inner) { }
}
