using GpxCut.Core.IO;
using GpxCut.MapBridge.TrackRendering;

if (args.Length == 0)
{
	Console.WriteLine("Usage: dotnet run --project tests/GpxCut.Perf -- <path-to-gpx>");
	return;
}

var path = args[0];
var reader = new GpxReader();

try
{
	var document = await reader.ReadAsync(path, CancellationToken.None);
	var scriptCount = 0;
	var chunkCalls = 0;

	foreach (var script in MapScriptFactory.BuildRenderScripts(document))
	{
		scriptCount++;
		if (script.Contains("addTrackChunk", StringComparison.Ordinal))
		{
			chunkCalls++;
		}
	}

	Console.WriteLine($"File: {path}");
	Console.WriteLine($"Segments: {document.Segments.Count}");
	Console.WriteLine($"Points: {document.TotalPoints}");
	Console.WriteLine($"Bounds: {document.Metadata.MinLatitude},{document.Metadata.MinLongitude} -> {document.Metadata.MaxLatitude},{document.Metadata.MaxLongitude}");
	Console.WriteLine($"Map scripts: {scriptCount} (chunks={chunkCalls})");
}
catch (Exception ex)
{
	Console.WriteLine($"ERROR: {ex.GetType().Name}: {ex.Message}");
}
