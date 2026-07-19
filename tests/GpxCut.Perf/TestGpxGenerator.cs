using System.Globalization;
using GpxCut.Core.Domain;
using GpxCut.Core.IO;

namespace GpxCut.Perf;

/// <summary>
/// Generates test GPX files with controllable point counts for performance testing.
/// </summary>
public sealed class TestGpxGenerator
{
    private readonly GpxWriter _writer = new();

    /// <summary>
    /// Creates a test GPX file with the specified number of points.
    /// Points are evenly distributed across segments.
    /// </summary>
    public async Task GenerateAsync(
        string outputPath,
        int totalPoints,
        int segmentCount = 1,
        CancellationToken cancellationToken = default)
    {
        if (totalPoints <= 0)
            throw new ArgumentException("Total points must be positive.", nameof(totalPoints));

        if (segmentCount <= 0)
            throw new ArgumentException("Segment count must be positive.", nameof(segmentCount));

        var random = new Random(42); // Deterministic for reproducibility
        var segments = new List<TrackSegment>();
        var pointsPerSegment = totalPoints / segmentCount;
        var remainder = totalPoints % segmentCount;

        var startTime = DateTimeOffset.UtcNow;
        var bounds = new BoundingBox(50.0, 10.0, 0.5); // Center Germany, variation ±0.5°
        var allPoints = new List<TrackPoint>();

        // Generate all points
        for (int i = 0; i < totalPoints; i++)
        {
            var (lat, lon) = bounds.RandomPoint(random);
            var ele = 100 + random.Next(500);
            var timestamp = startTime.AddSeconds(i * 10);

            allPoints.Add(new TrackPoint
            {
                Latitude = lat,
                Longitude = lon,
                Elevation = ele,
                Time = timestamp
            });
        }

        // Distribute points into segments
        int pointIndex = 0;
        for (int s = 0; s < segmentCount; s++)
        {
            var countForThisSegment = pointsPerSegment + (s < remainder ? 1 : 0);
            var segmentPoints = allPoints
                .Skip(pointIndex)
                .Take(countForThisSegment)
                .ToList();

            segments.Add(new TrackSegment { Points = segmentPoints });
            pointIndex += countForThisSegment;
        }

        // Calculate metadata
        var metadata = CalculateMetadata(allPoints);

        var document = new TrackDocument
        {
            Name = $"Performance Test Track ({totalPoints:N0} points, {segmentCount} segment{(segmentCount > 1 ? "s" : "")})",
            Description = $"Auto-generated test file. Segments: {segmentCount}, Points: {totalPoints:N0}",
            Segments = segments,
            Metadata = metadata
        };

        await _writer.WriteAsync(document, outputPath, cancellationToken);
    }

    private static TrackMetadata CalculateMetadata(IReadOnlyList<TrackPoint> points)
    {
        if (points.Count == 0)
            throw new InvalidOperationException("No points to calculate metadata from.");

        var firstTime = points[0].Time;
        var lastTime = points[^1].Time;

        var minLat = points.Min(p => p.Latitude);
        var maxLat = points.Max(p => p.Latitude);
        var minLon = points.Min(p => p.Longitude);
        var maxLon = points.Max(p => p.Longitude);

        return new TrackMetadata
        {
            StartTime = firstTime,
            EndTime = lastTime,
            MinLatitude = minLat,
            MaxLatitude = maxLat,
            MinLongitude = minLon,
            MaxLongitude = maxLon
        };
    }

    /// <summary>
    /// Simple bounding box for random point generation.
    /// </summary>
    private sealed class BoundingBox
    {
        private readonly double _centerLat;
        private readonly double _centerLon;
        private readonly double _variationDegrees;

        public BoundingBox(double centerLat, double centerLon, double variationDegrees)
        {
            _centerLat = centerLat;
            _centerLon = centerLon;
            _variationDegrees = variationDegrees;
        }

        public (double Latitude, double Longitude) RandomPoint(Random random)
        {
            var lat = _centerLat + (random.NextDouble() - 0.5) * 2 * _variationDegrees;
            var lon = _centerLon + (random.NextDouble() - 0.5) * 2 * _variationDegrees;
            return (lat, lon);
        }
    }
}
