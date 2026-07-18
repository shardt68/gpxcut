using System.IO;
using System.Windows;
using GpxCut.Core.IO;
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
    private bool _isMapReady;
    private bool _isBusy;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
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
        if (string.Equals(e.TryGetWebMessageAsString(), "map-ready", StringComparison.OrdinalIgnoreCase))
        {
            _isMapReady = true;
            SetStatus("Map ready. Open a GPX file.");
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
        OpenFileButton.IsEnabled = false;
        SetStatus("Loading GPX...");
        TrackSummaryTextBlock.Text = "Parsing file...";

        try
        {
            var document = await _gpxReader.ReadAsync(openFileDialog.FileName, CancellationToken.None);

            var scripts = MapScriptFactory.BuildRenderScripts(document);
            foreach (var script in scripts)
            {
                await MapWebView.CoreWebView2.ExecuteScriptAsync(script);
            }

            var name = string.IsNullOrWhiteSpace(document.Name) ? "(unnamed track)" : document.Name;
            TrackSummaryTextBlock.Text = $"{name} | Segments: {document.Segments.Count} | Points: {document.TotalPoints:N0}";
            SetStatus("GPX loaded and rendered.");
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
            OpenFileButton.IsEnabled = true;
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
}