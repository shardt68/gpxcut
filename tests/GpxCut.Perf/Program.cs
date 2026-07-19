using System.Diagnostics;
using GpxCut.Core.IO;
using GpxCut.MapBridge.TrackRendering;
using GpxCut.Perf;

if (args.Length == 0)
{
	PrintUsage();
	return;
}

var command = args[0].ToLowerInvariant();

try
{
	switch (command)
	{
		case "generate":
			await HandleGenerate(args);
			break;

		case "benchmark":
			if (args.Length < 2)
			{
				Console.WriteLine("ERROR: benchmark requires a path to a GPX file");
				PrintUsage();
				return;
			}
			await HandleBenchmark(args[1]);
			break;

		default:
			// Assume it's a file path for backward compatibility
			await HandleBenchmark(args[0]);
			break;
	}
}
catch (Exception ex)
{
	Console.WriteLine($"ERROR: {ex.GetType().Name}: {ex.Message}");
	Environment.Exit(1);
}

async Task HandleGenerate(string[] args)
{
	if (args.Length < 3)
	{
		Console.WriteLine("Usage: dotnet run -- generate <point-count> <output-path> [segment-count]");
		Console.WriteLine("Examples:");
		Console.WriteLine("  generate 100000 test-100k.gpx");
		Console.WriteLine("  generate 500000 test-500k.gpx");
		Console.WriteLine("  generate 1000000 test-1m.gpx");
		Console.WriteLine("  generate 500000 test-500k-5seg.gpx 5");
		return;
	}

	if (!int.TryParse(args[1], out var pointCount) || pointCount <= 0)
	{
		Console.WriteLine($"ERROR: Invalid point count '{args[1]}'");
		return;
	}

	var outputPath = args[2];
	var segmentCount = args.Length > 3 && int.TryParse(args[3], out var sc) ? sc : 1;

	Console.WriteLine($"Generating test GPX with {pointCount:N0} points in {segmentCount} segment(s)...");
	var sw = Stopwatch.StartNew();

	var generator = new TestGpxGenerator();
	await generator.GenerateAsync(outputPath, pointCount, segmentCount);

	sw.Stop();
	var fileInfo = new FileInfo(outputPath);
	Console.WriteLine($"✓ Generated {outputPath}");
	Console.WriteLine($"  Size: {fileInfo.Length / 1024.0 / 1024.0:F2} MB");
	Console.WriteLine($"  Time: {sw.ElapsedMilliseconds} ms");
}

async Task HandleBenchmark(string path)
{
	var reader = new GpxReader();
	var fileInfo = new FileInfo(path);

	Console.WriteLine($"Benchmarking: {path}");
	Console.WriteLine($"File size: {fileInfo.Length / 1024.0 / 1024.0:F2} MB");
	Console.WriteLine();

	// Parse benchmark
	var sw = Stopwatch.StartNew();
	var document = await reader.ReadAsync(path, CancellationToken.None);
	sw.Stop();
	var parseTimeMs = sw.ElapsedMilliseconds;

	Console.WriteLine("=== Parse Results ===");
	Console.WriteLine($"Parse time: {parseTimeMs} ms");
	Console.WriteLine($"Segments: {document.Segments.Count}");
	Console.WriteLine($"Total points: {document.TotalPoints:N0}");
	Console.WriteLine($"Avg points/segment: {(document.TotalPoints / Math.Max(1, document.Segments.Count)):N0}");

	var bounds = document.Metadata;
	Console.WriteLine($"Bounds: ({bounds.MinLatitude:F4},{bounds.MinLongitude:F4}) -> ({bounds.MaxLatitude:F4},{bounds.MaxLongitude:F4})");
	Console.WriteLine();

	// Script generation benchmark
	sw.Restart();
	var scripts = MapScriptFactory.BuildRenderScripts(document).ToList();
	sw.Stop();
	var scriptTimeMs = sw.ElapsedMilliseconds;

	var totalScriptSize = scripts.Sum(s => s.Length);
	var chunkCalls = scripts.Count(s => s.Contains("addTrackChunk", StringComparison.Ordinal));

	Console.WriteLine("=== Script Generation Results ===");
	Console.WriteLine($"Script generation time: {scriptTimeMs} ms");
	Console.WriteLine($"Total scripts: {scripts.Count}");
	Console.WriteLine($"Chunk calls: {chunkCalls}");
	Console.WriteLine($"Total script size: {totalScriptSize / 1024.0:F2} KB");
	Console.WriteLine($"Avg script size: {(totalScriptSize / Math.Max(1, scripts.Count)) / 1024.0:F2} KB");
	Console.WriteLine();

	// Summary
	var totalTimeMs = parseTimeMs + scriptTimeMs;
	Console.WriteLine("=== Summary ===");
	Console.WriteLine($"Total time: {totalTimeMs} ms");
	Console.WriteLine($"Points/sec: {(document.TotalPoints * 1000.0 / totalTimeMs):F0}");
}

void PrintUsage()
{
	Console.WriteLine("GPX Performance Testing Tool");
	Console.WriteLine();
	Console.WriteLine("Usage:");
	Console.WriteLine("  dotnet run -- generate <point-count> <output-path> [segment-count]");
	Console.WriteLine("  dotnet run -- benchmark <path-to-gpx>");
	Console.WriteLine("  dotnet run -- <path-to-gpx>  (backward compatible)");
	Console.WriteLine();
	Console.WriteLine("Examples:");
	Console.WriteLine("  dotnet run -- generate 100000 test-100k.gpx");
	Console.WriteLine("  dotnet run -- generate 500000 test-500k.gpx");
	Console.WriteLine("  dotnet run -- generate 1000000 test-1m.gpx");
	Console.WriteLine("  dotnet run -- benchmark test-500k.gpx");
	Console.WriteLine("  dotnet run -- test-500k.gpx");
}
