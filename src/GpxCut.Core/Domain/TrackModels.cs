namespace GpxCut.Core.Domain;

public sealed class TrackDocument
{
    public string? Name { get; init; }

    public string? Description { get; init; }

    public IReadOnlyList<TrackSegment> Segments { get; init; } = Array.Empty<TrackSegment>();

    public TrackMetadata Metadata { get; init; } = new();

    public int TotalPoints => Segments.Sum(segment => segment.Points.Count);
}

public sealed class TrackSegment
{
    public IReadOnlyList<TrackPoint> Points { get; init; } = Array.Empty<TrackPoint>();
}

public sealed class TrackPoint
{
    public required double Latitude { get; init; }

    public required double Longitude { get; init; }

    public double? Elevation { get; init; }

    public DateTimeOffset? Time { get; init; }

    public string? ExtensionsRawXml { get; init; }
}

public sealed class TrackMetadata
{
    public DateTimeOffset? StartTime { get; set; }

    public DateTimeOffset? EndTime { get; set; }

    public double? MinLatitude { get; set; }

    public double? MinLongitude { get; set; }

    public double? MaxLatitude { get; set; }

    public double? MaxLongitude { get; set; }

    public bool HasBounds =>
        MinLatitude is not null &&
        MinLongitude is not null &&
        MaxLatitude is not null &&
        MaxLongitude is not null;

    public void IncludePoint(double latitude, double longitude, DateTimeOffset? timestamp)
    {
        MinLatitude = MinLatitude is null ? latitude : Math.Min(MinLatitude.Value, latitude);
        MinLongitude = MinLongitude is null ? longitude : Math.Min(MinLongitude.Value, longitude);
        MaxLatitude = MaxLatitude is null ? latitude : Math.Max(MaxLatitude.Value, latitude);
        MaxLongitude = MaxLongitude is null ? longitude : Math.Max(MaxLongitude.Value, longitude);

        if (timestamp is null)
        {
            return;
        }

        StartTime = StartTime is null || timestamp < StartTime ? timestamp : StartTime;
        EndTime = EndTime is null || timestamp > EndTime ? timestamp : EndTime;
    }
}
