namespace ServerFurniture.Services;

public record RouteCoordinate(double Latitude, double Longitude);

public record RouteResult
{
    public bool Success { get; init; }
    public string? Message { get; init; }
    public double DistanceMeters { get; init; }
    public double DistanceKm { get; init; }
    public double DurationSeconds { get; init; }
    public double DurationMinutes { get; init; }
    public string? FormattedDistance { get; init; }
    public string? FormattedDuration { get; init; }
    public RouteCoordinate? Origin { get; init; }
    public RouteCoordinate? Destination { get; init; }
    public string? OriginName { get; init; }
    public string? DestinationName { get; init; }
    public string? Profile { get; init; }
}

public interface IOSRMService
{
    Task<RouteResult> GetDistanceAsync(
        double originLat,
        double originLng,
        double destLat,
        double destLng,
        string profile = "driving",
        CancellationToken cancellationToken = default);
}

