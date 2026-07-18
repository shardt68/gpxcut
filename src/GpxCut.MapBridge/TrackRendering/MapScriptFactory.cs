using System.Text.Json;
using GpxCut.Core.Domain;

namespace GpxCut.MapBridge.TrackRendering;

public static class MapScriptFactory
{
    public static IEnumerable<string> BuildRenderScripts(TrackDocument document, int chunkSize = 10_000)
    {
        yield return "window.gpxcutMap.clearTrack();";

        foreach (var chunk in ChunkTrack(document, chunkSize))
        {
            var coordinatesJson = JsonSerializer.Serialize(chunk);
            yield return $"window.gpxcutMap.addTrackChunk({coordinatesJson});";
        }

        if (document.Metadata.HasBounds)
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
}
