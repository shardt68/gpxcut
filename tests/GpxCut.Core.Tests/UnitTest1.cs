using GpxCut.Core.Domain;
using GpxCut.Core.Editing;

namespace GpxCut.Core.Tests;

public class UnitTest1
{
    [Fact]
    public void NormalizeSelection_UsesLowerIndexAsStart()
    {
        var normalized = TrackRangeOperations.NormalizeSelection(8, 3);

        Assert.Equal(3, normalized.StartIndex);
        Assert.Equal(8, normalized.EndIndexExclusive);
        Assert.Equal(5, normalized.Length);
    }

    [Fact]
    public void DeleteRange_RemovesPointsAcrossSegments_AndPreservesOthers()
    {
        var source = BuildSampleTrack();

        var result = TrackRangeOperations.DeleteRange(source, 1, 4);

        Assert.Equal(3, result.DeletedPoints);
        Assert.Equal(2, result.ModifiedTrack.Segments.Count);
        Assert.Equal(2, result.ModifiedTrack.TotalPoints);
        Assert.Equal(10.0, result.ModifiedTrack.Segments[0].Points[0].Latitude);
        Assert.Equal(14.0, result.ModifiedTrack.Segments[1].Points[0].Latitude);
    }

    [Fact]
    public void ExtractRange_UsesStartInclusiveEndExclusive()
    {
        var source = BuildSampleTrack();

        var extracted = TrackRangeOperations.ExtractRange(source, 1, 4);

        Assert.Equal(3, extracted.TotalPoints);
        Assert.Equal(2, extracted.Segments.Count);
        Assert.Equal(11.0, extracted.Segments[0].Points[0].Latitude);
        Assert.Equal(12.0, extracted.Segments[0].Points[1].Latitude);
        Assert.Equal(13.0, extracted.Segments[1].Points[0].Latitude);
    }

    [Fact]
    public void ExtractRange_PreservesPointMetadata()
    {
        var source = BuildSampleTrack();

        var extracted = TrackRangeOperations.ExtractRange(source, 3, 5);

        Assert.Equal(2, extracted.TotalPoints);
        Assert.All(
            extracted.Segments.SelectMany(segment => segment.Points),
            point => Assert.False(string.IsNullOrWhiteSpace(point.ExtensionsRawXml)));
    }

    [Fact]
    public void SplitAtIndex_SplitsIntoTwoTracks_WithExpectedPointCounts()
    {
        var source = BuildSampleTrack();

        var result = TrackRangeOperations.SplitAtIndex(source, 3);

        Assert.Equal(3, result.FirstPart.TotalPoints);
        Assert.Equal(2, result.SecondPart.TotalPoints);
        Assert.Equal(10.0, result.FirstPart.Segments[0].Points[0].Latitude);
        Assert.Equal(13.0, result.SecondPart.Segments[0].Points[0].Latitude);
    }

    [Fact]
    public void SplitAtIndex_PreservesExtensionsInBothParts()
    {
        var source = BuildSampleTrack();

        var result = TrackRangeOperations.SplitAtIndex(source, 2);

        Assert.All(
            result.FirstPart.Segments.SelectMany(segment => segment.Points),
            point => Assert.False(string.IsNullOrWhiteSpace(point.ExtensionsRawXml)));
        Assert.All(
            result.SecondPart.Segments.SelectMany(segment => segment.Points),
            point => Assert.False(string.IsNullOrWhiteSpace(point.ExtensionsRawXml)));
    }

    [Fact]
    public void SplitAtIndex_Throws_WhenIndexIsOutOfRange()
    {
        var source = BuildSampleTrack();

        Assert.Throws<ArgumentOutOfRangeException>(() => TrackRangeOperations.SplitAtIndex(source, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => TrackRangeOperations.SplitAtIndex(source, source.TotalPoints));
    }

    [Fact]
    public void DeleteRange_Throws_WhenSelectionOutsideBounds()
    {
        var source = BuildSampleTrack();

        Assert.Throws<ArgumentOutOfRangeException>(() => TrackRangeOperations.DeleteRange(source, -1, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => TrackRangeOperations.DeleteRange(source, 1, 99));
    }

    private static TrackDocument BuildSampleTrack()
    {
        var baseTime = DateTimeOffset.Parse("2026-01-01T00:00:00Z");

        return new TrackDocument
        {
            Name = "sample",
            Segments =
            [
                new TrackSegment
                {
                    Points =
                    [
                        BuildPoint(10, 20, baseTime.AddMinutes(0)),
                        BuildPoint(11, 21, baseTime.AddMinutes(1)),
                        BuildPoint(12, 22, baseTime.AddMinutes(2))
                    ]
                },
                new TrackSegment
                {
                    Points =
                    [
                        BuildPoint(13, 23, baseTime.AddMinutes(3)),
                        BuildPoint(14, 24, baseTime.AddMinutes(4))
                    ]
                }
            ]
        };
    }

    private static TrackPoint BuildPoint(double latitude, double longitude, DateTimeOffset time)
    {
        return new TrackPoint
        {
            Latitude = latitude,
            Longitude = longitude,
            Time = time,
            Elevation = 100,
            ExtensionsRawXml = "<extensions><test>1</test></extensions>"
        };
    }
}