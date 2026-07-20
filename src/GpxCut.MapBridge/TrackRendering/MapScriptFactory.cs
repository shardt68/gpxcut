using System.Text.Json;
using GpxCut.Core.Domain;

namespace GpxCut.MapBridge.TrackRendering;

public static class MapScriptFactory
{
    public static IEnumerable<string> BuildRenderScripts(TrackDocument document, bool includeFitBounds = true, int chunkSize = 10_000)
    {
        yield return "window.gpxcutMap.clearTrack();";
        yield return "window.gpxcutMap.clearEndpoints();";

        foreach (var chunk in ChunkTrack(document, chunkSize))
        {
            var coordinatesJson = JsonSerializer.Serialize(chunk);
            yield return $"window.gpxcutMap.addTrackChunk({coordinatesJson});";
        }

        // Render the track once after all chunks have been accumulated
        yield return "window.gpxcutMap.flushTrackChunks();";

        var (startPoint, endPoint) = GetStartAndEndPoints(document);
        if (startPoint is not null && endPoint is not null)
        {
            var endpointsJson = JsonSerializer.Serialize(new
            {
                start = new[] { startPoint.Longitude, startPoint.Latitude },
                end = new[] { endPoint.Longitude, endPoint.Latitude }
            });
            yield return $"window.gpxcutMap.setEndpoints({endpointsJson});";
        }

        if (includeFitBounds && document.Metadata.HasBounds)
        {
            var boundsJson = JsonSerializer.Serialize(new
            {
                minLon = document.Metadata.MinLongitude,
                minLat = document.Metadata.MinLatitude,
                maxLon = document.Metadata.MaxLongitude,
                maxLat = document.Metadata.MaxLatitude
            });
            yield return $"window.gpxcutMap.fitBounds({boundsJson});";
        }
    }

    public static IEnumerable<string> BuildClearSelectionScripts()
    {
        yield return "window.gpxcutMap.clearSelection();";
    }

    public static IEnumerable<string> BuildHoverInfoScripts(double[] coordinate, string title, IReadOnlyList<string> lines)
    {
        var payloadJson = JsonSerializer.Serialize(new
        {
            coordinate,
            title,
            lines
        });

        yield return $"window.gpxcutMap.setHoverInfo({payloadJson});";
    }

    public static IEnumerable<string> BuildClearHoverInfoScripts()
    {
        yield return "window.gpxcutMap.clearHoverInfo();";
    }

    public static IEnumerable<string> BuildSelectionScripts(
        IReadOnlyList<double[]> selectionCoordinates,
        double[]? startCoordinate,
        double[]? endCoordinate)
    {
        var coordinatesJson = JsonSerializer.Serialize(selectionCoordinates);
        yield return $"window.gpxcutMap.setSelectionCoordinates({coordinatesJson});";

        var markersJson = JsonSerializer.Serialize(new
        {
            start = startCoordinate,
            end = endCoordinate
        });
        yield return $"window.gpxcutMap.setSelectionMarkers({markersJson});";
    }

    public static IEnumerable<string> BuildProfileVisibilityScripts(bool visible)
    {
        yield return $"window.gpxcutMap.setProfileVisible({(visible ? "true" : "false")});";
    }

    public static IEnumerable<string> BuildSetProfileDataScripts(string payloadJson)
    {
        yield return $"window.gpxcutMap.setProfileData({payloadJson});";
    }

    public static IEnumerable<string> BuildProfileSelectionScripts(int? startIndex, int? endIndex)
    {
        var payloadJson = JsonSerializer.Serialize(new
        {
            startIndex,
            endIndex
        });

        yield return $"window.gpxcutMap.setProfileSelection({payloadJson});";
    }

    public static IEnumerable<string> BuildClearProfileScripts()
    {
        yield return "window.gpxcutMap.clearProfile();";
    }

    private static IEnumerable<List<double[]>> ChunkTrack(TrackDocument document, int chunkSize)
    {
        if (chunkSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(chunkSize));
        }

        var currentChunk = new List<double[]>(chunkSize);

        foreach (var segment in document.Segments)
        {
            foreach (var point in segment.Points)
            {
                currentChunk.Add([point.Longitude, point.Latitude]);

                if (currentChunk.Count >= chunkSize)
                {
                    yield return currentChunk;
                    currentChunk = new List<double[]>(chunkSize);
                }
            }
        }

        if (currentChunk.Count > 0)
        {
            yield return currentChunk;
        }
    }

    private static (TrackPoint? Start, TrackPoint? End) GetStartAndEndPoints(TrackDocument document)
    {
        TrackPoint? start = null;
        TrackPoint? end = null;

        foreach (var segment in document.Segments)
        {
            if (segment.Points.Count == 0)
            {
                continue;
            }

            start ??= segment.Points[0];
            end = segment.Points[^1];
        }

        return (start, end);
    }
}
