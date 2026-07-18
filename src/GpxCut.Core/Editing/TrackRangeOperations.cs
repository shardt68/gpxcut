using System.Globalization;
using GpxCut.Core.Domain;

namespace GpxCut.Core.Editing;

public sealed record RangeSelection(int StartIndex, int EndIndexExclusive)
{
    public int Length => EndIndexExclusive - StartIndex;
}

public sealed record DeleteRangeResult(TrackDocument ModifiedTrack, int DeletedPoints);

public static class TrackRangeOperations
{
    public static RangeSelection NormalizeSelection(int firstIndex, int secondIndex)
    {
        var start = Math.Min(firstIndex, secondIndex);
        var endExclusive = Math.Max(firstIndex, secondIndex);
        return new RangeSelection(start, endExclusive);
    }

    public static DeleteRangeResult DeleteRange(TrackDocument source, int firstIndex, int secondIndex)
    {
        var selection = NormalizeSelection(firstIndex, secondIndex);
        ValidateSelection(source, selection);

        var segments = new List<TrackSegment>(source.Segments.Count);
        var metadata = new TrackMetadata();
        var deleted = 0;
        var globalIndex = 0;

        foreach (var segment in source.Segments)
        {
            var keptPoints = new List<TrackPoint>(segment.Points.Count);

            foreach (var point in segment.Points)
            {
                var isDeleted = globalIndex >= selection.StartIndex && globalIndex < selection.EndIndexExclusive;
                if (isDeleted)
                {
                    deleted++;
                }
                else
                {
                    keptPoints.Add(point);
                    metadata.IncludePoint(point.Latitude, point.Longitude, point.Time);
                }

                globalIndex++;
            }

            if (keptPoints.Count > 0)
            {
                segments.Add(new TrackSegment { Points = keptPoints });
            }
        }

        if (deleted <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(firstIndex), "The selected range does not contain any points.");
        }

        if (segments.Count == 0)
        {
            throw new InvalidOperationException("Deleting the selected range would remove the entire track.");
        }

        var modified = new TrackDocument
        {
            Name = source.Name,
            Description = source.Description,
            Segments = segments,
            Metadata = metadata
        };

        return new DeleteRangeResult(modified, deleted);
    }

    public static TrackDocument ExtractRange(TrackDocument source, int firstIndex, int secondIndex)
    {
        var selection = NormalizeSelection(firstIndex, secondIndex);
        ValidateSelection(source, selection);

        var segments = new List<TrackSegment>();
        var metadata = new TrackMetadata();
        var globalIndex = 0;

        foreach (var segment in source.Segments)
        {
            var selectedPoints = new List<TrackPoint>();

            foreach (var point in segment.Points)
            {
                var isSelected = globalIndex >= selection.StartIndex && globalIndex < selection.EndIndexExclusive;
                if (isSelected)
                {
                    selectedPoints.Add(point);
                    metadata.IncludePoint(point.Latitude, point.Longitude, point.Time);
                }

                globalIndex++;
            }

            if (selectedPoints.Count > 0)
            {
                segments.Add(new TrackSegment { Points = selectedPoints });
            }
        }

        if (segments.Count == 0)
        {
            throw new InvalidOperationException("The selected range does not contain any points.");
        }

        return new TrackDocument
        {
            Name = BuildExportName(source.Name, selection),
            Description = source.Description,
            Segments = segments,
            Metadata = metadata
        };
    }

    private static string BuildExportName(string? sourceName, RangeSelection selection)
    {
        var baseName = string.IsNullOrWhiteSpace(sourceName) ? "track" : sourceName.Trim();
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{baseName} [{selection.StartIndex}..{selection.EndIndexExclusive})");
    }

    private static void ValidateSelection(TrackDocument source, RangeSelection selection)
    {
        if (source.TotalPoints <= 0)
        {
            throw new InvalidOperationException("The track does not contain any points.");
        }

        if (selection.StartIndex < 0 || selection.EndIndexExclusive > source.TotalPoints)
        {
            throw new ArgumentOutOfRangeException(nameof(selection), "The selected range is outside of the track bounds.");
        }

        if (selection.EndIndexExclusive <= selection.StartIndex)
        {
            throw new ArgumentOutOfRangeException(nameof(selection), "The selected range must contain at least one point.");
        }
    }
}