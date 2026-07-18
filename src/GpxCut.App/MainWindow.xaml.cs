using System.IO;
using System.Windows;
using System.Text.Json;
using System.Globalization;
using System.Xml.Linq;
using GpxCut.Core.IO;
using GpxCut.Core.Domain;
using GpxCut.Core.Editing;
using GpxCut.MapBridge.TrackRendering;
using Microsoft.Win32;
using Microsoft.Web.WebView2.Core;
using System.Windows.Threading;
using System.Windows.Input;

namespace GpxCut.App;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly GpxReader _gpxReader = new();
    private readonly GpxWriter _gpxWriter = new();
    private readonly string? _startupFilePath;
    private bool _isMapReady;
    private bool _isBusy;
    private bool _hasUnsavedChanges;

    private TrackDocument? _currentDocument;
    private string? _currentFilePath;
    private readonly List<IndexedTrackPoint> _indexedPoints = [];
    private int? _selectionStartIndex;
    private int? _selectionEndIndex;

    private const double ClickToleranceMeters = 20.0;
    private const double HoverToleranceMeters = 35.0;
    private const int FastStepSize = 10;

    public MainWindow()
        : this(null)
    {
    }

    public MainWindow(string? startupFilePath)
    {
        _startupFilePath = startupFilePath;
        InitializeComponent();
        Loaded += OnLoaded;
        PreviewKeyDown += OnPreviewKeyDown;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await InitializeMapAsync();

        if (!string.IsNullOrWhiteSpace(_startupFilePath))
        {
            await OpenTrackFileAsync(_startupFilePath);
        }
    }

    private async Task InitializeMapAsync()
    {
        try
        {
            SetStatus("Initializing map...");

            await MapWebView.EnsureCoreWebView2Async();
            MapWebView.CoreWebView2.WebMessageReceived += CoreWebView2OnWebMessageReceived;

            var mapFilePath = Path.Combine(AppContext.BaseDirectory, "MapAssets", "map.html");
            MapWebView.Source = new Uri(mapFilePath);
        }
        catch (Exception ex)
        {
            SetStatus($"Map initialization failed: {ex.Message}");
        }
    }

    private void CoreWebView2OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        _ = HandleWebMessageAsync(e.WebMessageAsJson);
    }

    private async Task HandleWebMessageAsync(string webMessageJson)
    {
        if (string.IsNullOrWhiteSpace(webMessageJson))
        {
            return;
        }

        try
        {
            using var json = JsonDocument.Parse(webMessageJson);
            var root = json.RootElement;

            if (root.ValueKind == JsonValueKind.String)
            {
                if (string.Equals(root.GetString(), "map-ready", StringComparison.OrdinalIgnoreCase))
                {
                    _isMapReady = true;
                    SetStatus("Map ready. Open a GPX file.");
                }

                return;
            }

            if (root.ValueKind != JsonValueKind.Object)
            {
                return;
            }

            if (!root.TryGetProperty("type", out var typeNode))
            {
                return;
            }

            var messageType = typeNode.GetString();

            if (string.Equals(messageType, "map-hover-clear", StringComparison.OrdinalIgnoreCase))
            {
                await ExecuteScriptsAsync(MapScriptFactory.BuildClearHoverInfoScripts());
                return;
            }

            if (string.Equals(messageType, "map-hover", StringComparison.OrdinalIgnoreCase))
            {
                if (!root.TryGetProperty("lat", out var hoverLatNode) || !root.TryGetProperty("lng", out var hoverLonNode))
                {
                    return;
                }

                await HandleMapHoverAsync(hoverLatNode.GetDouble(), hoverLonNode.GetDouble());
                return;
            }

            if (!string.Equals(messageType, "map-click", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (!root.TryGetProperty("lat", out var latNode) || !root.TryGetProperty("lng", out var lonNode))
            {
                return;
            }

            var latitude = latNode.GetDouble();
            var longitude = lonNode.GetDouble();
            var shiftKeyFromMessage = root.TryGetProperty("shiftKey", out var shiftNode) && shiftNode.GetBoolean();
            var useEndMarkerFromMessage = root.TryGetProperty("useEndMarker", out var endNode) && endNode.GetBoolean();
            var shiftKey = shiftKeyFromMessage || Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
            var useEndMarker = useEndMarkerFromMessage || shiftKey;

            await HandleMapClickSelectionAsync(latitude, longitude, useEndMarker);
        }
        catch (Exception)
        {
            // Ignore malformed or unexpected map messages.
        }
    }

    private async void OpenFileButton_Click(object sender, RoutedEventArgs e)
    {
        var openFileDialog = new OpenFileDialog
        {
            Title = "Open GPX file",
            Filter = "GPX files (*.gpx)|*.gpx|All files (*.*)|*.*"
        };

        if (openFileDialog.ShowDialog(this) != true)
        {
            return;
        }

        await OpenTrackFileAsync(openFileDialog.FileName);
    }

    private async Task OpenTrackFileAsync(string filePath)
    {
        if (_isBusy)
        {
            return;
        }

        if (!_isMapReady)
        {
            SetStatus("Map is not ready yet.");
            return;
        }

        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            SetStatus($"GPX file not found: {filePath}");
            return;
        }

        _isBusy = true;
        UpdateActionButtons();
        SetStatus("Loading GPX...");
        TrackSummaryTextBlock.Text = "Parsing file...";

        try
        {
            var document = await _gpxReader.ReadAsync(filePath, CancellationToken.None);

            _currentDocument = document;
            _currentFilePath = filePath;
            _hasUnsavedChanges = false;
            RebuildIndex();
            ClearSelectionState();

            await RenderTrackAsync(document, includeFitBounds: true);
            await ExecuteScriptsAsync(MapScriptFactory.BuildClearSelectionScripts());

            UpdateTrackSummary();
            SetStatus($"Loaded {document.TotalPoints:N0} points across {document.Segments.Count} segment(s).");
        }
        catch (GpxReadException ex)
        {
            TrackSummaryTextBlock.Text = "Load failed.";
            SetStatus($"GPX error: {ex.Message}");
        }
        catch (Exception ex)
        {
            TrackSummaryTextBlock.Text = "Load failed.";
            SetStatus($"Unexpected error: {ex.Message}");
        }
        finally
        {
            _isBusy = false;
            UpdateActionButtons();
        }
    }

    private async Task RenderTrackAsync(TrackDocument document, bool includeFitBounds)
    {
        await ExecuteScriptsAsync(MapScriptFactory.BuildRenderScripts(document, includeFitBounds: includeFitBounds));
    }

    private async Task ExecuteScriptsAsync(IEnumerable<string> scripts)
    {
        if (MapWebView.CoreWebView2 is null)
        {
            return;
        }

        foreach (var script in scripts)
        {
            await MapWebView.CoreWebView2.ExecuteScriptAsync(script);
        }
    }

    private async Task HandleMapClickSelectionAsync(double latitude, double longitude, bool shiftKey)
    {
        if (_isBusy || _currentDocument is null || _indexedPoints.Count == 0)
        {
            return;
        }

        var nearest = FindNearestPointIndex(latitude, longitude, ClickToleranceMeters);
        if (nearest is null)
        {
            SetStatus("No track point found within 20m tolerance.");
            return;
        }

        if (shiftKey)
        {
            if (_selectionStartIndex is null)
            {
                _selectionStartIndex = nearest.Value;
            }

            _selectionEndIndex = nearest.Value;
        }
        else
        {
            _selectionStartIndex = nearest.Value;
        }

        await UpdateSelectionVisualizationAsync();
    }

    private async Task HandleMapHoverAsync(double latitude, double longitude)
    {
        if (_isBusy || _currentDocument is null || _indexedPoints.Count == 0)
        {
            return;
        }

        var nearest = FindNearestPointIndex(latitude, longitude, HoverToleranceMeters);
        if (nearest is null)
        {
            await ExecuteScriptsAsync(MapScriptFactory.BuildClearHoverInfoScripts());
            return;
        }

        var indexedPoint = _indexedPoints[nearest.Value];
        var point = indexedPoint.Point;
        var speedKmh = TryGetSpeedKmh(indexedPoint.GlobalIndex);
        var extensionSummary = BuildExtensionsSummary(point.ExtensionsRawXml);

        var lines = new List<string>
        {
            string.Create(CultureInfo.InvariantCulture, $"Index: {indexedPoint.GlobalIndex} | Segment: {indexedPoint.SegmentIndex} | Punkt: {indexedPoint.PointIndex}"),
            string.Create(CultureInfo.InvariantCulture, $"Lat/Lon: {point.Latitude:F6}, {point.Longitude:F6}")
        };

        if (point.Time is not null)
        {
            lines.Add($"Datum/Uhrzeit: {point.Time.Value.LocalDateTime:yyyy-MM-dd HH:mm:ss}");
        }

        if (point.Elevation is not null)
        {
            lines.Add(string.Create(CultureInfo.InvariantCulture, $"Hoehe: {point.Elevation.Value:F1} m"));
        }

        if (speedKmh is not null)
        {
            lines.Add(string.Create(CultureInfo.InvariantCulture, $"Geschwindigkeit: {speedKmh.Value:F1} km/h"));
        }
        else
        {
            lines.Add("Geschwindigkeit: n/a");
        }

        if (!string.IsNullOrWhiteSpace(extensionSummary))
        {
            lines.Add($"Extensions: {extensionSummary}");
        }

        await ExecuteScriptsAsync(
            MapScriptFactory.BuildHoverInfoScripts(
                [point.Longitude, point.Latitude],
                "Punktinfo",
                lines));
    }

    private int? FindNearestPointIndex(double latitude, double longitude, double toleranceMeters)
    {
        var bestDistance = double.MaxValue;
        int? bestIndex = null;

        foreach (var indexedPoint in _indexedPoints)
        {
            var distance = GetDistanceMeters(
                latitude,
                longitude,
                indexedPoint.Point.Latitude,
                indexedPoint.Point.Longitude);

            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestIndex = indexedPoint.GlobalIndex;
            }
        }

        if (bestIndex is null || bestDistance > toleranceMeters)
        {
            return null;
        }

        return bestIndex.Value;
    }

    private async void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_isBusy || _currentDocument is null || _indexedPoints.Count == 0)
        {
            return;
        }

        if (e.Key == Key.S && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            e.Handled = true;
            await SaveCurrentTrackAsync();
            return;
        }

        if (e.Key == Key.Delete)
        {
            e.Handled = true;
            await TryDeleteSelectedRangeAsync();
            return;
        }

        var direction = e.Key switch
        {
            Key.Left => -1,
            Key.Right => 1,
            _ => 0
        };

        if (direction == 0)
        {
            return;
        }

        var useEndMarker = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
        var step = Keyboard.Modifiers.HasFlag(ModifierKeys.Control) ? FastStepSize : 1;

        if (!MoveMarker(useEndMarker, direction * step))
        {
            return;
        }

        e.Handled = true;
        await UpdateSelectionVisualizationAsync();
    }

    private bool MoveMarker(bool endMarker, int delta)
    {
        var maxIndex = _indexedPoints.Count - 1;
        if (maxIndex < 0)
        {
            return false;
        }

        if (endMarker)
        {
            if (_selectionEndIndex is null)
            {
                if (_selectionStartIndex is null)
                {
                    return false;
                }

                _selectionEndIndex = _selectionStartIndex.Value;
            }

            _selectionEndIndex = ClampIndex(_selectionEndIndex.Value + delta, maxIndex);
            return true;
        }

        if (_selectionStartIndex is null)
        {
            _selectionStartIndex = _selectionEndIndex ?? 0;
        }

        _selectionStartIndex = ClampIndex(_selectionStartIndex.Value + delta, maxIndex);
        return true;
    }

    private static int ClampIndex(int candidate, int maxIndex)
    {
        if (candidate < 0)
        {
            return 0;
        }

        if (candidate > maxIndex)
        {
            return maxIndex;
        }

        return candidate;
    }

    private async Task UpdateSelectionVisualizationAsync()
    {
        if (_currentDocument is null)
        {
            return;
        }

        var startCoordinate = GetCoordinateForIndex(_selectionStartIndex);
        var endCoordinate = GetCoordinateForIndex(_selectionEndIndex);

        var selectionCoordinates = new List<double[]>();
        var normalized = GetNormalizedSelection();
        if (normalized is not null)
        {
            var inclusiveEnd = normalized.EndIndexExclusive - 1;
            for (var index = normalized.StartIndex; index <= inclusiveEnd && index < _indexedPoints.Count; index++)
            {
                var point = _indexedPoints[index].Point;
                selectionCoordinates.Add([point.Longitude, point.Latitude]);
            }
        }

        await ExecuteScriptsAsync(MapScriptFactory.BuildSelectionScripts(selectionCoordinates, startCoordinate, endCoordinate));

        UpdateTrackSummary();
        UpdateActionButtons();
    }

    private double[]? GetCoordinateForIndex(int? index)
    {
        if (index is null || index < 0 || index >= _indexedPoints.Count)
        {
            return null;
        }

        var point = _indexedPoints[index.Value].Point;
        return [point.Longitude, point.Latitude];
    }

    private RangeSelection? GetNormalizedSelection()
    {
        if (_selectionStartIndex is null || _selectionEndIndex is null)
        {
            return null;
        }

        var normalized = TrackRangeOperations.NormalizeSelection(_selectionStartIndex.Value, _selectionEndIndex.Value);
        return normalized.Length > 0 ? normalized : null;
    }

    private void RebuildIndex()
    {
        _indexedPoints.Clear();

        if (_currentDocument is null)
        {
            return;
        }

        var globalIndex = 0;
        for (var segmentIndex = 0; segmentIndex < _currentDocument.Segments.Count; segmentIndex++)
        {
            var segment = _currentDocument.Segments[segmentIndex];
            for (var pointIndex = 0; pointIndex < segment.Points.Count; pointIndex++)
            {
                _indexedPoints.Add(new IndexedTrackPoint(globalIndex, segmentIndex, pointIndex, segment.Points[pointIndex]));
                globalIndex++;
            }
        }
    }

    private void ClearSelectionState()
    {
        _selectionStartIndex = null;
        _selectionEndIndex = null;
    }

    private void UpdateActionButtons()
    {
        OpenFileButton.IsEnabled = !_isBusy;
        SaveFileButton.IsEnabled = !_isBusy && _currentDocument is not null && _hasUnsavedChanges;
        var hasRange = GetNormalizedSelection() is not null;
        DeleteRangeButton.IsEnabled = !_isBusy && hasRange;
        ExportRangeButton.IsEnabled = !_isBusy && hasRange;
    }

    private void UpdateTrackSummary()
    {
        if (_currentDocument is null)
        {
            TrackSummaryTextBlock.Text = "No file loaded.";
            return;
        }

        var name = string.IsNullOrWhiteSpace(_currentDocument.Name) ? "(unnamed track)" : _currentDocument.Name;
        var summary = $"{name} | Segments: {_currentDocument.Segments.Count} | Points: {_currentDocument.TotalPoints:N0}";

        if (_selectionStartIndex is not null)
        {
            summary += $" | Start: {_selectionStartIndex.Value}";
        }

        if (_selectionEndIndex is not null)
        {
            summary += $" | End: {_selectionEndIndex.Value}";
        }

        var normalized = GetNormalizedSelection();
        if (normalized is not null)
        {
            summary += $" | Selected: {normalized.Length:N0}";
        }

        TrackSummaryTextBlock.Text = summary;
    }

    private async void DeleteRangeButton_Click(object sender, RoutedEventArgs e)
    {
        await TryDeleteSelectedRangeAsync();
    }

    private async Task TryDeleteSelectedRangeAsync()
    {
        if (_isBusy || _currentDocument is null || _selectionStartIndex is null || _selectionEndIndex is null)
        {
            return;
        }

        var normalized = GetNormalizedSelection();
        if (normalized is null)
        {
            SetStatus("Please select a non-empty range before deleting.");
            return;
        }

        if (normalized.Length >= _currentDocument.TotalPoints)
        {
            SetStatus("Cannot delete the entire track.");
            return;
        }

        var confirm = MessageBox.Show(
            this,
            $"Delete {normalized.Length:N0} points from the selected range?",
            "Confirm Delete Range",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes)
        {
            return;
        }

        _isBusy = true;
        UpdateActionButtons();

        try
        {
            var result = TrackRangeOperations.DeleteRange(_currentDocument, _selectionStartIndex.Value, _selectionEndIndex.Value);
            _currentDocument = result.ModifiedTrack;
            RebuildIndex();
            ClearSelectionState();

            await RenderTrackAsync(_currentDocument, includeFitBounds: false);
            await ExecuteScriptsAsync(MapScriptFactory.BuildClearSelectionScripts());

            UpdateTrackSummary();
            _hasUnsavedChanges = true;
            SetStatus($"Deleted {result.DeletedPoints:N0} points. Use Save GPX to persist changes.");
        }
        catch (Exception ex)
        {
            SetStatus($"Delete failed: {ex.Message}");
        }
        finally
        {
            _isBusy = false;
            UpdateActionButtons();
        }
    }

    private async void ExportRangeButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isBusy || _currentDocument is null || _selectionStartIndex is null || _selectionEndIndex is null)
        {
            return;
        }

        var normalized = GetNormalizedSelection();
        if (normalized is null)
        {
            SetStatus("Please select a non-empty range before exporting.");
            return;
        }

        var saveDialog = new SaveFileDialog
        {
            Title = "Export selected range as GPX",
            Filter = "GPX files (*.gpx)|*.gpx|All files (*.*)|*.*",
            AddExtension = true,
            DefaultExt = ".gpx",
            FileName = BuildDefaultExportFileName(_currentDocument.Name, normalized)
        };

        if (saveDialog.ShowDialog(this) != true)
        {
            return;
        }

        _isBusy = true;
        UpdateActionButtons();

        try
        {
            var exportDocument = TrackRangeOperations.ExtractRange(_currentDocument, _selectionStartIndex.Value, _selectionEndIndex.Value);
            await _gpxWriter.WriteAsync(exportDocument, saveDialog.FileName, CancellationToken.None);
            SetStatus($"Exported {normalized.Length:N0} points to '{Path.GetFileName(saveDialog.FileName)}'.");
        }
        catch (Exception ex)
        {
            SetStatus($"Export failed: {ex.Message}");
        }
        finally
        {
            _isBusy = false;
            UpdateActionButtons();
        }
    }

    private static string BuildDefaultExportFileName(string? trackName, RangeSelection selection)
    {
        var baseName = string.IsNullOrWhiteSpace(trackName) ? "track" : trackName;
        var invalid = Path.GetInvalidFileNameChars();
        var cleanName = string.Concat(baseName.Select(ch => invalid.Contains(ch) ? '_' : ch));
        return $"{cleanName}_{selection.StartIndex}_{selection.EndIndexExclusive}.gpx";
    }

    private async void SaveFileButton_Click(object sender, RoutedEventArgs e)
    {
        await SaveCurrentTrackAsync();
    }

    private async Task SaveCurrentTrackAsync()
    {
        if (_currentDocument is null)
        {
            return;
        }

        if (!_hasUnsavedChanges)
        {
            SetStatus("No unsaved changes.");
            return;
        }

        var saveDialog = new SaveFileDialog
        {
            Title = "Save GPX",
            Filter = "GPX files (*.gpx)|*.gpx|All files (*.*)|*.*",
            AddExtension = true,
            DefaultExt = ".gpx",
            OverwritePrompt = true,
            FileName = GetDefaultSaveFileName()
        };

        var initialDirectory = GetDefaultSaveDirectory();
        if (!string.IsNullOrWhiteSpace(initialDirectory) && Directory.Exists(initialDirectory))
        {
            saveDialog.InitialDirectory = initialDirectory;
        }

        if (saveDialog.ShowDialog(this) != true)
        {
            SetStatus("Save canceled. Changes are still unsaved.");
            return;
        }

        _isBusy = true;
        UpdateActionButtons();

        try
        {
            await _gpxWriter.WriteAsync(_currentDocument, saveDialog.FileName, CancellationToken.None);
            _currentFilePath = saveDialog.FileName;
            _hasUnsavedChanges = false;
            SetStatus($"Saved '{Path.GetFileName(saveDialog.FileName)}'.");
        }
        catch (Exception ex)
        {
            SetStatus($"Save failed: {ex.Message}");
        }
        finally
        {
            _isBusy = false;
            UpdateActionButtons();
        }
    }

    private string GetDefaultSaveFileName()
    {
        if (!string.IsNullOrWhiteSpace(_currentFilePath))
        {
            return Path.GetFileName(_currentFilePath);
        }

        var baseName = string.IsNullOrWhiteSpace(_currentDocument?.Name) ? "track" : _currentDocument.Name;
        var invalid = Path.GetInvalidFileNameChars();
        var cleanName = string.Concat(baseName.Select(ch => invalid.Contains(ch) ? '_' : ch));
        return $"{cleanName}.gpx";
    }

    private string? GetDefaultSaveDirectory()
    {
        if (string.IsNullOrWhiteSpace(_currentFilePath))
        {
            return null;
        }

        return Path.GetDirectoryName(_currentFilePath);
    }

    private static double GetDistanceMeters(double lat1, double lon1, double lat2, double lon2)
    {
        const double earthRadiusMeters = 6_371_000;

        var lat1Rad = DegreesToRadians(lat1);
        var lat2Rad = DegreesToRadians(lat2);
        var deltaLat = DegreesToRadians(lat2 - lat1);
        var deltaLon = DegreesToRadians(lon2 - lon1);

        var sinLat = Math.Sin(deltaLat / 2);
        var sinLon = Math.Sin(deltaLon / 2);
        var a = sinLat * sinLat + Math.Cos(lat1Rad) * Math.Cos(lat2Rad) * sinLon * sinLon;
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

        return earthRadiusMeters * c;
    }

    private static double DegreesToRadians(double degrees)
    {
        return degrees * Math.PI / 180.0;
    }

    private double? TryGetSpeedKmh(int currentGlobalIndex)
    {
        if (currentGlobalIndex <= 0 || currentGlobalIndex >= _indexedPoints.Count)
        {
            return null;
        }

        var current = _indexedPoints[currentGlobalIndex].Point;
        var previous = _indexedPoints[currentGlobalIndex - 1].Point;

        if (current.Time is null || previous.Time is null)
        {
            return null;
        }

        var deltaSeconds = (current.Time.Value - previous.Time.Value).TotalSeconds;
        if (deltaSeconds <= 0)
        {
            return null;
        }

        var meters = GetDistanceMeters(previous.Latitude, previous.Longitude, current.Latitude, current.Longitude);
        return meters / deltaSeconds * 3.6;
    }

    private static string? BuildExtensionsSummary(string? rawExtensionsXml)
    {
        if (string.IsNullOrWhiteSpace(rawExtensionsXml))
        {
            return null;
        }

        try
        {
            var extensionsElement = XElement.Parse(rawExtensionsXml, LoadOptions.None);
            var names = extensionsElement
                .Descendants()
                .Select(node => node.Name.LocalName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(6)
                .ToList();

            if (names.Count == 0)
            {
                return "vorhanden";
            }

            return string.Join(", ", names);
        }
        catch (Exception)
        {
            var compact = rawExtensionsXml
                .Replace(Environment.NewLine, " ", StringComparison.Ordinal)
                .Replace("\n", " ", StringComparison.Ordinal)
                .Trim();

            return compact.Length <= 120 ? compact : compact[..120] + "...";
        }
    }

    private void SetStatus(string message)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => StatusTextBlock.Text = message, DispatcherPriority.Normal);
            return;
        }

        StatusTextBlock.Text = message;
    }

    private sealed record IndexedTrackPoint(int GlobalIndex, int SegmentIndex, int PointIndex, TrackPoint Point);
}