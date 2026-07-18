using System.IO;
using System.Windows;
using System.Text.Json;
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
    private bool _isMapReady;
    private bool _isBusy;

    private TrackDocument? _currentDocument;
    private readonly List<IndexedTrackPoint> _indexedPoints = [];
    private int? _selectionStartIndex;
    private int? _selectionEndIndex;

    private const double ClickToleranceMeters = 20.0;
    private const int FastStepSize = 10;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        PreviewKeyDown += OnPreviewKeyDown;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await InitializeMapAsync();
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
            var shiftKey = root.TryGetProperty("shiftKey", out var shiftNode) && shiftNode.GetBoolean();

            await HandleMapClickSelectionAsync(latitude, longitude, shiftKey);
        }
        catch (Exception)
        {
            // Ignore malformed or unexpected map messages.
        }
    }

    private async void OpenFileButton_Click(object sender, RoutedEventArgs e)
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

        var openFileDialog = new OpenFileDialog
        {
            Title = "Open GPX file",
            Filter = "GPX files (*.gpx)|*.gpx|All files (*.*)|*.*"
        };

        if (openFileDialog.ShowDialog(this) != true)
        {
            return;
        }

        _isBusy = true;
        UpdateActionButtons();
        SetStatus("Loading GPX...");
        TrackSummaryTextBlock.Text = "Parsing file...";

        try
        {
            var document = await _gpxReader.ReadAsync(openFileDialog.FileName, CancellationToken.None);

            _currentDocument = document;
            RebuildIndex();
            ClearSelectionState();

            await RenderTrackAsync(document);
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

    private async Task RenderTrackAsync(TrackDocument document)
    {
        await ExecuteScriptsAsync(MapScriptFactory.BuildRenderScripts(document));
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

            await RenderTrackAsync(_currentDocument);
            await ExecuteScriptsAsync(MapScriptFactory.BuildClearSelectionScripts());

            UpdateTrackSummary();
            SetStatus($"Deleted {result.DeletedPoints:N0} points.");
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