using System.IO;
using System.Diagnostics;
using System.Windows;
using System.Text.Json;
using System.Text;
using System.Globalization;
using System.Xml.Linq;
using System.Net.Http;
using System.Net;
using GpxCut.Core.IO;
using GpxCut.Core.Domain;
using GpxCut.Core.Editing;
using GpxCut.MapBridge.TrackRendering;
using Microsoft.Win32;
using Microsoft.Web.WebView2.Core;
using System.Windows.Threading;
using System.Windows.Input;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace GpxCut.App;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private static readonly object LogSync = new();
    private static readonly HttpClient WmsCapabilitiesHttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(5)
    };
    private static readonly HttpClient OpenGeoDataProxyHttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(10)
    };
    private static readonly Dictionary<string, (int MinZoom, int MaxZoom)> HessenZoomFallbackByLayerId =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["hessen-dop20-wms"] = (12, 15),
            ["hessen-dtk25-wms"] = (11, 16)
        };
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
    private const int MaxProfileSamples = 4_000;
    private const string DefaultStartupBasemapId = "osm-standard";
    private const string MapAssetsVirtualHost = "appassets.gpxcut";
    private const string TileProxyVirtualHost = "gpxcut-proxy.local";
    private const string OpenGeoDataProxyPathPrefix = "/proxy/opengeodata/";
    private const long WebViewDiskCacheLimitBytes = 256L * 1024L * 1024L;
    private const string ProfileModeElevationTime = "elevation-time";
    private const string ProfileModeElevationDistance = "elevation-distance";
    private const string ProfileModeSpeedTime = "speed-time";
    private const string ProfileModeSpeedDistance = "speed-distance";

    private bool _isProfileVisible;
    private string _profileMode = ProfileModeElevationTime;
    private LayerPolicyState _layerPolicyState = CreateFallbackLayerPolicyState();
    private bool _isUpdatingBasemapSelection;
    private string? _activeBasemapId;
    private string? _activeWebViewUserDataFolder;
    private ContextMenu? _basemapContextMenu;
    private readonly AppSettings _appSettings = LoadAppSettings();
    private readonly Dictionary<string, (int MinZoom, int MaxZoom)> _hessenZoomCache =
        new(StringComparer.OrdinalIgnoreCase);

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
        _profileMode = GetSelectedProfileModeFromUi();
        UpdateActionButtons();
        await InitializeMapAsync();
    }

    private async Task InitializeMapAsync()
    {
        var mapFilePath = ResolveMapHtmlPath();
        if (mapFilePath is null)
        {
            SetStatus("Map initialization failed: MapAssets/map.html not found.");
            LogError("MAP_INIT_ASSET", null, "MapAssets/map.html not found in known locations");
            return;
        }

        try
        {
            SetStatus("Initializing map...");

            _layerPolicyState = LoadLayerPolicyState(mapFilePath, DefaultStartupBasemapId);
            RefreshBasemapComboBox();

            var userDataFolders = BuildWebViewUserDataFolders();
            Exception? lastInitializeException = null;
            var layerBootstrapScript = BuildLayerBootstrapScript(_layerPolicyState);

            foreach (var folder in userDataFolders)
            {
                try
                {
                    Directory.CreateDirectory(folder);
                    var webViewEnvironment = await CoreWebView2Environment.CreateAsync(
                        userDataFolder: folder,
                        options: BuildWebViewEnvironmentOptions());
                    await MapWebView.EnsureCoreWebView2Async(webViewEnvironment);
                    await MapWebView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(layerBootstrapScript);
                    MapWebView.CoreWebView2.WebMessageReceived += CoreWebView2OnWebMessageReceived;
                    MapWebView.CoreWebView2.AddWebResourceRequestedFilter(
                        $"https://{MapAssetsVirtualHost}{OpenGeoDataProxyPathPrefix}*",
                        CoreWebView2WebResourceContext.All);
                    MapWebView.CoreWebView2.AddWebResourceRequestedFilter(
                        $"https://{TileProxyVirtualHost}{OpenGeoDataProxyPathPrefix}*",
                        CoreWebView2WebResourceContext.All);
                    MapWebView.CoreWebView2.WebResourceRequested += CoreWebView2OnWebResourceRequested;

                    _activeWebViewUserDataFolder = folder;
                    UpdateCacheInfoUi();
                    var mapAssetsDirectory = Path.GetDirectoryName(mapFilePath);
                    if (!string.IsNullOrWhiteSpace(mapAssetsDirectory))
                    {
                        MapWebView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                            MapAssetsVirtualHost,
                            mapAssetsDirectory,
                            CoreWebView2HostResourceAccessKind.Allow);

                        MapWebView.Source = new Uri($"https://{MapAssetsVirtualHost}/map.html", UriKind.Absolute);
                    }
                    else
                    {
                        MapWebView.Source = new Uri(mapFilePath, UriKind.Absolute);
                    }

                    SetStatus($"Initializing map... (WebView data: {folder})");
                    return;
                }
                catch (Exception ex)
                {
                    lastInitializeException = ex;
                    LogError("MAP_INIT_ATTEMPT", ex, folder);
                }
            }

            throw lastInitializeException ?? new InvalidOperationException("WebView2 initialization failed without a detailed exception.");
        }
        catch (Exception ex)
        {
            LogError("MAP_INIT", ex, "InitializeMapAsync");
            var hResultHex = $"0x{ex.HResult:X8}";
            SetStatus($"Map initialization failed ({hResultHex}): {ex.Message}");
        }
    }

    private static List<string> BuildWebViewUserDataFolders()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var tempPath = Path.GetTempPath();

        return
        [
            Path.Combine(localAppData, "GpxCut", "cache", "webview2"),
            Path.Combine(localAppData, "GpxCut", "cache", "webview2-fallback"),
            Path.Combine(tempPath, "GpxCut", "cache", "webview2")
        ];
    }

    private static CoreWebView2EnvironmentOptions BuildWebViewEnvironmentOptions()
    {
        return new CoreWebView2EnvironmentOptions
        {
            AdditionalBrowserArguments = $"--disk-cache-size={WebViewDiskCacheLimitBytes}"
        };
    }

    private void UpdateCacheInfoUi()
    {
        var cacheRoot = _activeWebViewUserDataFolder;
        var displayPath = string.IsNullOrWhiteSpace(cacheRoot) ? "n/a" : cacheRoot;
        var sizeLimitMiB = WebViewDiskCacheLimitBytes / (1024L * 1024L);
        BasemapCacheTextBlock.Text =
            $"Tile cache (WebView2): {displayPath} | Limit: {sizeLimitMiB} MiB | Mode: browser-like HTTP cache";
    }

    private static string? ResolveMapHtmlPath()
    {
        var candidates = new List<string>();

        if (!string.IsNullOrWhiteSpace(Environment.ProcessPath))
        {
            var processDir = Path.GetDirectoryName(Environment.ProcessPath);
            if (!string.IsNullOrWhiteSpace(processDir))
            {
                candidates.Add(Path.Combine(processDir, "MapAssets", "map.html"));
            }
        }

        candidates.Add(Path.Combine(AppContext.BaseDirectory, "MapAssets", "map.html"));
        candidates.Add(Path.Combine(Directory.GetCurrentDirectory(), "MapAssets", "map.html"));

        var process = Process.GetCurrentProcess();
        if (!string.IsNullOrWhiteSpace(process.MainModule?.FileName))
        {
            var moduleDir = Path.GetDirectoryName(process.MainModule.FileName);
            if (!string.IsNullOrWhiteSpace(moduleDir))
            {
                candidates.Add(Path.Combine(moduleDir, "MapAssets", "map.html"));
            }
        }

        foreach (var candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private string BuildLayerBootstrapScript(LayerPolicyState layerPolicyState)
    {
        var payloadJson = JsonSerializer.Serialize(new
        {
            basemap = layerPolicyState.EffectiveBasemap,
            loadedFromPolicy = layerPolicyState.LoadedFromPolicy,
            policyPath = layerPolicyState.LoadedFromPolicy ? layerPolicyState.PolicyPath : null
        });

        return $"window.__gpxcutLayerBootstrap = {payloadJson}; window.__gpxcutBasemapConfig = window.__gpxcutLayerBootstrap.basemap;";
    }

    private static LayerPolicyState LoadLayerPolicyState(string mapFilePath, string? preferredLayerId)
    {
        var fallback = CreateFallbackLayerPolicyState();
        var policyPath = ResolveLayerPoliciesPath(mapFilePath);
        if (string.IsNullOrWhiteSpace(policyPath) || !File.Exists(policyPath))
        {
            return fallback;
        }

        try
        {
            var json = File.ReadAllText(policyPath);
            var policy = JsonSerializer.Deserialize<LayerPolicyDocument>(
                json,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

            var validLayers = (policy?.Layers ?? [])
                .Where(IsValidXyzLayer)
                .Select(ToBasemapConfig)
                .ToList();

            if (validLayers.Count == 0)
            {
                LogError("MAP_POLICY_EMPTY", null, $"No valid XYZ layers in {policyPath}");
                return fallback;
            }

            BasemapLayerConfig? selected = null;
            if (!string.IsNullOrWhiteSpace(preferredLayerId))
            {
                selected = validLayers.FirstOrDefault(layer =>
                    string.Equals(layer.Id, preferredLayerId, StringComparison.OrdinalIgnoreCase) && layer.CacheAllowed);
            }

            if (!string.IsNullOrWhiteSpace(policy?.SelectedLayerId))
            {
                selected ??= validLayers.FirstOrDefault(layer =>
                    string.Equals(layer.Id, policy.SelectedLayerId, StringComparison.OrdinalIgnoreCase) && layer.CacheAllowed);
            }

            selected ??= validLayers.FirstOrDefault(layer =>
                policy?.Layers.Any(entry => entry.EnabledByDefault &&
                    string.Equals(entry.Id, layer.Id, StringComparison.OrdinalIgnoreCase)) == true && layer.CacheAllowed);
            selected ??= validLayers.FirstOrDefault(layer => layer.CacheAllowed);
            selected ??= validLayers[0];

            return new LayerPolicyState(
                EffectiveBasemap: selected,
                AvailableBasemaps: validLayers,
                LoadedFromPolicy: true,
                PolicyPath: policyPath);
        }
        catch (Exception ex)
        {
            LogError("MAP_POLICY_PARSE", ex, policyPath);
            return fallback;
        }
    }

    private static BasemapLayerConfig ToBasemapConfig(LayerPolicyEntry entry)
    {
        return new BasemapLayerConfig(
            Id: entry.Id,
            Name: entry.Name,
            Type: string.IsNullOrWhiteSpace(entry.Map?.Type) ? "xyz" : entry.Map!.Type,
            Tiles: entry.Map?.Tiles ?? [],
            TileSize: entry.Map?.TileSize ?? 256,
            WmtsZoomOffset: entry.Map?.WmtsZoomOffset,
            MinZoom: entry.Map?.MinZoom,
            MaxZoom: entry.Map?.MaxZoom,
            Attribution: string.IsNullOrWhiteSpace(entry.Attribution)
                ? "&copy; OpenStreetMap contributors"
                : entry.Attribution,
            LicenseName: string.IsNullOrWhiteSpace(entry.LicenseName) ? "Unknown" : entry.LicenseName,
            LicenseUrl: entry.LicenseUrl,
            CommercialUseAllowed: entry.CommercialUseAllowed,
            CacheAllowed: entry.CacheAllowed,
            CacheMode: string.IsNullOrWhiteSpace(entry.CacheMode) ? "unknown" : entry.CacheMode,
            MinTtlDays: entry.MinTtlDays,
            OfflinePrefetchAllowed: entry.OfflinePrefetchAllowed,
            Notes: entry.Notes,
            Bounds: entry.Map?.Bounds is null
                ? null
                : new BasemapBounds(
                    entry.Map.Bounds.MinLon,
                    entry.Map.Bounds.MinLat,
                    entry.Map.Bounds.MaxLon,
                    entry.Map.Bounds.MaxLat));
    }

    private static LayerPolicyState CreateFallbackLayerPolicyState()
    {
        var fallbackLayer = CreateFallbackLayer();
        return new LayerPolicyState(
            EffectiveBasemap: fallbackLayer,
            AvailableBasemaps: [fallbackLayer],
            LoadedFromPolicy: false,
            PolicyPath: null);
    }

    private void RefreshBasemapComboBox()
    {
        _isUpdatingBasemapSelection = true;
        try
        {
            var items = _layerPolicyState.AvailableBasemaps
                .Select(layer => new BasemapComboItem(layer.Id, layer.Name, layer))
                .ToList();

            BasemapComboBox.ItemsSource = items;

            var selected = items.FirstOrDefault(item =>
                string.Equals(item.Id, _layerPolicyState.EffectiveBasemap.Id, StringComparison.OrdinalIgnoreCase))
                ?? items.FirstOrDefault();

            BasemapComboBox.SelectedItem = selected;
            _activeBasemapId = selected?.Id;
            if (selected is not null)
            {
                ApplyBasemapUiHints(selected.Basemap);
            }
        }
        finally
        {
            _isUpdatingBasemapSelection = false;
        }

        UpdateActionButtons();
    }

    private static bool IsValidXyzLayer(LayerPolicyEntry entry)
    {
        if (entry.Map is null)
        {
            return false;
        }

        var layerType = entry.Map.Type ?? string.Empty;
        if (!string.Equals(layerType, "xyz", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(layerType, "wmts", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(layerType, "wms", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (entry.Map.Tiles is null || entry.Map.Tiles.Count == 0)
        {
            return false;
        }

        return entry.Map.Tiles.All(tile => !string.IsNullOrWhiteSpace(tile));
    }

    private static BasemapLayerConfig CreateFallbackLayer()
    {
        return new BasemapLayerConfig(
            Id: "osm-standard",
            Name: "OpenStreetMap Standard",
            Type: "xyz",
            Tiles: ["https://tile.openstreetmap.org/{z}/{x}/{y}.png"],
            TileSize: 256,
            WmtsZoomOffset: null,
            MinZoom: null,
            MaxZoom: null,
            Attribution: "&copy; OpenStreetMap contributors",
            LicenseName: "ODbL 1.0 (data)",
            LicenseUrl: "https://www.openstreetmap.org/copyright",
            CommercialUseAllowed: true,
            CacheAllowed: true,
            CacheMode: "http-headers",
            MinTtlDays: 7,
            OfflinePrefetchAllowed: false,
            Notes: "Interactive use only on tile.openstreetmap.org. No bulk prefetch/offline seeding.",
            Bounds: null);
    }

    private static string? ResolveLayerPoliciesPath(string mapFilePath)
    {
        var candidates = new List<string>();
        var mapAssetsDirectory = Path.GetDirectoryName(mapFilePath);
        if (!string.IsNullOrWhiteSpace(mapAssetsDirectory))
        {
            candidates.Add(Path.Combine(mapAssetsDirectory, "layer-policies.json"));
        }

        candidates.Add(Path.Combine(AppContext.BaseDirectory, "MapAssets", "layer-policies.json"));
        candidates.Add(Path.Combine(Directory.GetCurrentDirectory(), "MapAssets", "layer-policies.json"));

        foreach (var candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private void CoreWebView2OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        _ = HandleWebMessageAsync(e.WebMessageAsJson);
    }

    private async void CoreWebView2OnWebResourceRequested(object? sender, CoreWebView2WebResourceRequestedEventArgs e)
    {
        if (MapWebView.CoreWebView2 is null)
        {
            return;
        }

        if (!Uri.TryCreate(e.Request.Uri, UriKind.Absolute, out var requestUri))
        {
            return;
        }

        var isProxyHost = string.Equals(requestUri.Host, MapAssetsVirtualHost, StringComparison.OrdinalIgnoreCase)
            || string.Equals(requestUri.Host, TileProxyVirtualHost, StringComparison.OrdinalIgnoreCase);

        if (!isProxyHost
            || !requestUri.AbsolutePath.StartsWith(OpenGeoDataProxyPathPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var relativePath = requestUri.AbsolutePath[OpenGeoDataProxyPathPrefix.Length..];
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            e.Response = MapWebView.CoreWebView2.Environment.CreateWebResourceResponse(
                null,
                (int)HttpStatusCode.BadRequest,
                "Bad Request",
                "Content-Type: text/plain\r\n");
            return;
        }

        var targetUrl = $"https://opengeodata.lgl-bw.de/{relativePath}{requestUri.Query}";
        var deferral = e.GetDeferral();

        try
        {
            LogError("MAP_TILE_PROXY_HIT", null, targetUrl);
            using var request = new HttpRequestMessage(HttpMethod.Get, targetUrl);
            using var response = await OpenGeoDataProxyHttpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead);

            LogError("MAP_TILE_PROXY_STATUS", null, $"{(int)response.StatusCode} {targetUrl}");

            var responseStream = new MemoryStream();
            await response.Content.CopyToAsync(responseStream);
            responseStream.Position = 0;

            var headersBuilder = new StringBuilder();
            if (response.Content.Headers.ContentType is not null)
            {
                headersBuilder.Append("Content-Type: ")
                    .Append(response.Content.Headers.ContentType)
                    .Append("\r\n");
            }

            if (response.Headers.CacheControl is not null)
            {
                headersBuilder.Append("Cache-Control: ")
                    .Append(response.Headers.CacheControl)
                    .Append("\r\n");
            }

            if (response.Headers.ETag is not null)
            {
                headersBuilder.Append("ETag: ")
                    .Append(response.Headers.ETag)
                    .Append("\r\n");
            }

            if (response.Content.Headers.LastModified is not null)
            {
                headersBuilder.Append("Last-Modified: ")
                    .Append(response.Content.Headers.LastModified.Value.ToString("R", CultureInfo.InvariantCulture))
                    .Append("\r\n");
            }

            headersBuilder.Append("Access-Control-Allow-Origin: *\r\n");
            headersBuilder.Append("Access-Control-Allow-Methods: GET, HEAD, OPTIONS\r\n");

            e.Response = MapWebView.CoreWebView2.Environment.CreateWebResourceResponse(
                responseStream,
                (int)response.StatusCode,
                response.ReasonPhrase ?? "OK",
                headersBuilder.ToString());
        }
        catch (Exception ex)
        {
            LogError("MAP_TILE_PROXY", ex, targetUrl);
            var errorBody = Encoding.UTF8.GetBytes("Tile proxy request failed.");
            var errorStream = new MemoryStream(errorBody);
            e.Response = MapWebView.CoreWebView2.Environment.CreateWebResourceResponse(
                errorStream,
                (int)HttpStatusCode.BadGateway,
                "Bad Gateway",
                "Content-Type: text/plain; charset=utf-8\r\n");
        }
        finally
        {
            deferral.Complete();
        }
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
                    UpdateActionButtons();
                    if (!string.IsNullOrWhiteSpace(_startupFilePath))
                    {
                        await OpenTrackFileAsync(_startupFilePath);
                    }
                    else
                    {
                        SetStatus("Map ready. Open a GPX file.");
                    }
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

            if (string.Equals(messageType, "map-diagnostic", StringComparison.OrdinalIgnoreCase))
            {
                HandleMapDiagnosticMessage(root);
                return;
            }

            if (string.Equals(messageType, "map-empty-contextmenu", StringComparison.OrdinalIgnoreCase))
            {
                if (!root.TryGetProperty("x", out var xNode) || xNode.ValueKind != JsonValueKind.Number
                    || !root.TryGetProperty("y", out var yNode) || yNode.ValueKind != JsonValueKind.Number)
                {
                    return;
                }

                var contextLongitude = root.TryGetProperty("lng", out var contextLngNode) && contextLngNode.ValueKind == JsonValueKind.Number
                    ? contextLngNode.GetDouble()
                    : (double?)null;
                var contextLatitude = root.TryGetProperty("lat", out var contextLatNode) && contextLatNode.ValueKind == JsonValueKind.Number
                    ? contextLatNode.GetDouble()
                    : (double?)null;

                ShowBasemapContextMenu(xNode.GetDouble(), yNode.GetDouble(), contextLongitude, contextLatitude);
                return;
            }

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

            if (string.Equals(messageType, "profile-click", StringComparison.OrdinalIgnoreCase))
            {
                if (!root.TryGetProperty("index", out var indexNode) || indexNode.ValueKind != JsonValueKind.Number)
                {
                    return;
                }

                var profileShiftKey = root.TryGetProperty("shiftKey", out var profileShiftNode) && profileShiftNode.GetBoolean();
                var profileUseEndMarker = profileShiftKey || Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
                await HandleProfileClickSelectionAsync(indexNode.GetInt32(), profileUseEndMarker);
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
            LogError("MAP_MSG", null, "HandleWebMessageAsync malformed message");
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
            await RefreshProfileAsync();

            UpdateTrackSummary();
            SetStatus($"Loaded {document.TotalPoints:N0} points across {document.Segments.Count} segment(s).");
        }
        catch (GpxReadException ex)
        {
            LogError("GPX_READ", ex, filePath);
            TrackSummaryTextBlock.Text = "Load failed.";
            SetStatus($"GPX error: {ex.Message}");
        }
        catch (Exception ex)
        {
            LogError("LOAD", ex, filePath);
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

        var scriptsList = scripts as IReadOnlyList<string> ?? scripts.ToList();

        // Use progressive rendering for large script batches to avoid WebView2 timeouts
        if (scriptsList.Count > 20)
        {
            await ExecuteScriptsAsyncProgressive(scriptsList);
        }
        else
        {
            foreach (var script in scriptsList)
            {
                await MapWebView.CoreWebView2.ExecuteScriptAsync(script);
            }
        }
    }

    private async Task ApplyBasemapAsync(BasemapLayerConfig basemap)
    {
        var effectiveBasemap = await ResolveRuntimeBasemapAsync(basemap);
        var basemapJson = JsonSerializer.Serialize(effectiveBasemap);
        await ExecuteScriptsAsync([$"window.gpxcutMap.setBasemap({basemapJson});"]);
        _activeBasemapId = basemap.Id;
        ApplyBasemapUiHints(basemap);
        SaveSelectedBasemapId(basemap.Id);
    }

    private static bool IsHessenBasemap(BasemapLayerConfig basemap)
    {
        return basemap.Id.StartsWith("hessen-", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<BasemapLayerConfig> ResolveRuntimeBasemapAsync(BasemapLayerConfig basemap)
    {
        if (!IsHessenBasemap(basemap) || !string.Equals(basemap.Type, "wms", StringComparison.OrdinalIgnoreCase))
        {
            return basemap;
        }

        if (_hessenZoomCache.TryGetValue(basemap.Id, out var cachedRange))
        {
            return basemap with { MinZoom = cachedRange.MinZoom, MaxZoom = cachedRange.MaxZoom };
        }

        var resolvedRange = await TryResolveHessenZoomRangeFromCapabilitiesAsync(basemap);
        if (resolvedRange is null && HessenZoomFallbackByLayerId.TryGetValue(basemap.Id, out var fallbackRange))
        {
            resolvedRange = fallbackRange;
            LogError("MAP_HESSEN_ZOOM_FALLBACK", null, $"{basemap.Id} -> {fallbackRange.MinZoom}-{fallbackRange.MaxZoom}");
        }

        if (resolvedRange is null)
        {
            return basemap;
        }

        _hessenZoomCache[basemap.Id] = resolvedRange.Value;
        return basemap with
        {
            MinZoom = resolvedRange.Value.MinZoom,
            MaxZoom = resolvedRange.Value.MaxZoom
        };
    }

    private async Task<(int MinZoom, int MaxZoom)?> TryResolveHessenZoomRangeFromCapabilitiesAsync(BasemapLayerConfig basemap)
    {
        if (basemap.Tiles.Count == 0)
        {
            return null;
        }

        var template = basemap.Tiles[0];
        if (!Uri.TryCreate(template, UriKind.Absolute, out var tileUri))
        {
            return null;
        }

        var layerName = GetQueryParameterValue(tileUri, "LAYERS");
        if (string.IsNullOrWhiteSpace(layerName))
        {
            return null;
        }

        var capabilitiesUri = BuildWmsCapabilitiesUri(tileUri);
        try
        {
            var xml = await WmsCapabilitiesHttpClient.GetStringAsync(capabilitiesUri);
            var doc = XDocument.Parse(xml);

            var layerElement = doc
                .Descendants()
                .FirstOrDefault(element =>
                    string.Equals(element.Name.LocalName, "Layer", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(
                        element.Elements().FirstOrDefault(child =>
                            string.Equals(child.Name.LocalName, "Name", StringComparison.OrdinalIgnoreCase))?.Value,
                        layerName,
                        StringComparison.OrdinalIgnoreCase));

            if (layerElement is null)
            {
                return null;
            }

            var scaleHint = layerElement.Elements().FirstOrDefault(element =>
                string.Equals(element.Name.LocalName, "ScaleHint", StringComparison.OrdinalIgnoreCase));

            if (scaleHint is not null)
            {
                var minAttr = scaleHint.Attribute("min")?.Value;
                var maxAttr = scaleHint.Attribute("max")?.Value;

                if (double.TryParse(minAttr, NumberStyles.Float, CultureInfo.InvariantCulture, out var minHint)
                    && double.TryParse(maxAttr, NumberStyles.Float, CultureInfo.InvariantCulture, out var maxHint))
                {
                    var minZoom = ClampZoom((int)Math.Ceiling(ZoomFromScaleHint(maxHint)));
                    var maxZoom = ClampZoom((int)Math.Floor(ZoomFromScaleHint(minHint)));
                    if (maxZoom >= minZoom)
                    {
                        return (minZoom, maxZoom);
                    }
                }
            }

            var minScaleDenominatorText = layerElement.Elements().FirstOrDefault(element =>
                string.Equals(element.Name.LocalName, "MinScaleDenominator", StringComparison.OrdinalIgnoreCase))?.Value;
            var maxScaleDenominatorText = layerElement.Elements().FirstOrDefault(element =>
                string.Equals(element.Name.LocalName, "MaxScaleDenominator", StringComparison.OrdinalIgnoreCase))?.Value;

            if (double.TryParse(minScaleDenominatorText, NumberStyles.Float, CultureInfo.InvariantCulture, out var minScaleDenominator)
                && double.TryParse(maxScaleDenominatorText, NumberStyles.Float, CultureInfo.InvariantCulture, out var maxScaleDenominator))
            {
                var minZoom = ClampZoom((int)Math.Ceiling(ZoomFromScaleDenominator(maxScaleDenominator)));
                var maxZoom = ClampZoom((int)Math.Floor(ZoomFromScaleDenominator(minScaleDenominator)));
                if (maxZoom >= minZoom)
                {
                    return (minZoom, maxZoom);
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            LogError("MAP_HESSEN_ZOOM_CAPA", ex, capabilitiesUri.ToString());
            return null;
        }
    }

    private static Uri BuildWmsCapabilitiesUri(Uri tileUri)
    {
        var language = GetQueryParameterValue(tileUri, "language");
        var query = string.IsNullOrWhiteSpace(language)
            ? "SERVICE=WMS&REQUEST=GetCapabilities&VERSION=1.1.1"
            : $"language={Uri.EscapeDataString(language)}&SERVICE=WMS&REQUEST=GetCapabilities&VERSION=1.1.1";

        var builder = new UriBuilder(tileUri)
        {
            Query = query
        };

        return builder.Uri;
    }

    private static string? GetQueryParameterValue(Uri uri, string key)
    {
        if (string.IsNullOrWhiteSpace(uri.Query))
        {
            return null;
        }

        var query = uri.Query.TrimStart('?');
        foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separatorIndex = pair.IndexOf('=');
            if (separatorIndex <= 0)
            {
                continue;
            }

            var name = Uri.UnescapeDataString(pair[..separatorIndex]);
            if (!string.Equals(name, key, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return Uri.UnescapeDataString(pair[(separatorIndex + 1)..]);
        }

        return null;
    }

    private static int ClampZoom(int zoom)
    {
        return Math.Clamp(zoom, 0, 22);
    }

    private static double ZoomFromScaleHint(double scaleHint)
    {
        if (scaleHint <= 0)
        {
            return 22;
        }

        var metersPerPixel = scaleHint / Math.Sqrt(2.0);
        return Math.Log(156543.03392804097 / metersPerPixel, 2);
    }

    private static double ZoomFromScaleDenominator(double scaleDenominator)
    {
        if (scaleDenominator <= 0)
        {
            return 22;
        }

        var metersPerPixel = scaleDenominator * 0.00028;
        return Math.Log(156543.03392804097 / metersPerPixel, 2);
    }

    private void HandleMapDiagnosticMessage(JsonElement root)
    {
        var category = root.TryGetProperty("category", out var categoryNode)
            ? categoryNode.GetString() ?? "unknown"
            : "unknown";
        var message = root.TryGetProperty("message", out var messageNode)
            ? messageNode.GetString() ?? ""
            : string.Empty;
        var sourceId = root.TryGetProperty("sourceId", out var sourceNode)
            ? sourceNode.GetString() ?? ""
            : string.Empty;
        var basemapId = root.TryGetProperty("basemapId", out var basemapNode)
            ? basemapNode.GetString() ?? ""
            : string.Empty;
        var zoomText = root.TryGetProperty("zoom", out var zoomNode) && zoomNode.ValueKind == JsonValueKind.Number
            ? zoomNode.GetDouble().ToString("0.00", CultureInfo.InvariantCulture)
            : "n/a";

        if (string.Equals(category, "tile-zoom", StringComparison.OrdinalIgnoreCase))
        {
            var serverZoomText = root.TryGetProperty("serverZoom", out var serverZoomNode) && serverZoomNode.ValueKind == JsonValueKind.Number
                ? serverZoomNode.GetDouble().ToString("0", CultureInfo.InvariantCulture)
                : "n/a";
            var basemapText = string.IsNullOrWhiteSpace(basemapId) ? "n/a" : basemapId;
            BasemapZoomTextBlock.Text = $"Basemap zoom: map={zoomText}, server={serverZoomText}, id={basemapText}";
            return;
        }

        if (string.Equals(category, "tile-error", StringComparison.OrdinalIgnoreCase))
        {
            LogError("MAP_TILE", null, $"basemap={basemapId} source={sourceId} zoom={zoomText} message={message}");
            return;
        }

        if (string.Equals(category, "viewport-clamped", StringComparison.OrdinalIgnoreCase))
        {
            LogError("MAP_VIEWPORT_CLAMP", null, $"zoom={zoomText}");
            return;
        }

        if (!string.IsNullOrWhiteSpace(message))
        {
            LogError("MAP_DIAGNOSTIC", null, $"category={category} message={message}");
        }
    }

    private void ApplyBasemapUiHints(BasemapLayerConfig basemap)
    {
        var ttlText = basemap.MinTtlDays is null ? "n/a" : $">= {basemap.MinTtlDays.Value}d";
        BasemapComboBox.ToolTip =
            $"License: {basemap.LicenseName}\n" +
            $"Commercial use: {(basemap.CommercialUseAllowed ? "yes" : "no")}\n" +
            $"Cache allowed: {(basemap.CacheAllowed ? "yes" : "no")} ({basemap.CacheMode}, min TTL {ttlText})\n" +
            $"Offline prefetch: {(basemap.OfflinePrefetchAllowed ? "yes" : "no")}";

        var licenseText = basemap.LicenseName;
        if (!string.IsNullOrWhiteSpace(basemap.LicenseUrl))
        {
            licenseText += $" ({basemap.LicenseUrl})";
        }

        var notesText = string.IsNullOrWhiteSpace(basemap.Notes)
            ? string.Empty
            : $" | Notes: {basemap.Notes}";

        BasemapPolicyTextBlock.Text =
            $"Basemap policy - {basemap.Name}: " +
            $"License: {licenseText} | " +
            $"Commercial: {(basemap.CommercialUseAllowed ? "yes" : "no")} | " +
            $"Cache: {(basemap.CacheAllowed ? "yes" : "no")} ({basemap.CacheMode}, min TTL {ttlText}) | " +
            $"Offline prefetch: {(basemap.OfflinePrefetchAllowed ? "yes" : "no")}" +
            notesText;
    }

    private void SaveSelectedBasemapId(string basemapId)
    {
        try
        {
            _appSettings.SelectedBasemapId = basemapId;
            var settingsDirectory = GetSettingsDirectory();
            Directory.CreateDirectory(settingsDirectory);
            var settingsPath = Path.Combine(settingsDirectory, "app-settings.json");
            var json = JsonSerializer.Serialize(_appSettings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(settingsPath, json);
        }
        catch (Exception ex)
        {
            LogError("APP_SETTINGS_SAVE", ex, basemapId);
        }
    }

    private static AppSettings LoadAppSettings()
    {
        try
        {
            var settingsPath = Path.Combine(GetSettingsDirectory(), "app-settings.json");
            if (!File.Exists(settingsPath))
            {
                return new AppSettings();
            }

            var json = File.ReadAllText(settingsPath);
            return JsonSerializer.Deserialize<AppSettings>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    private static string GetSettingsDirectory()
    {
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GpxCut", "settings");
    }

    /// <summary>
    /// Execute scripts progressively in batches with delays to avoid WebView2 timeouts on large datasets.
    /// This is automatically triggered when script count exceeds 20.
    /// </summary>
    private async Task ExecuteScriptsAsyncProgressive(IReadOnlyList<string> scripts)
    {
        if (MapWebView.CoreWebView2 is null)
        {
            return;
        }

        const int chunkSize = 10; // Execute 10 scripts per batch
        const int delayMs = 10;   // 10ms delay between batches for WebView2 to process

        for (int i = 0; i < scripts.Count; i += chunkSize)
        {
            var batch = scripts.Skip(i).Take(chunkSize);
            
            foreach (var script in batch)
            {
                await MapWebView.CoreWebView2.ExecuteScriptAsync(script);
            }

            // Add delay between batches if there are more scripts
            if (i + chunkSize < scripts.Count)
            {
                await Task.Delay(delayMs);
            }
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

    private async Task HandleProfileClickSelectionAsync(int globalIndex, bool useEndMarker)
    {
        if (_isBusy || _currentDocument is null || _indexedPoints.Count == 0)
        {
            return;
        }

        if (globalIndex < 0 || globalIndex >= _indexedPoints.Count)
        {
            return;
        }

        if (useEndMarker)
        {
            if (_selectionStartIndex is null)
            {
                _selectionStartIndex = globalIndex;
            }

            _selectionEndIndex = globalIndex;
        }
        else
        {
            _selectionStartIndex = globalIndex;
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
        await ExecuteScriptsAsync(MapScriptFactory.BuildProfileSelectionScripts(_selectionStartIndex, _selectionEndIndex));

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
        SplitTrackButton.IsEnabled = !_isBusy && _currentDocument is not null && GetSplitIndex() is not null;
        var hasRange = GetNormalizedSelection() is not null;
        DeleteRangeButton.IsEnabled = !_isBusy && hasRange;
        ExportRangeButton.IsEnabled = !_isBusy && hasRange;
        ShowProfileCheckBox.IsEnabled = !_isBusy && _currentDocument is not null;
        ProfileModeComboBox.IsEnabled = !_isBusy && _currentDocument is not null && _isProfileVisible;
        BasemapComboBox.IsEnabled = !_isBusy && _isMapReady && _layerPolicyState.AvailableBasemaps.Count > 0;
    }

    private async void BasemapComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingBasemapSelection || !_isMapReady || _isBusy)
        {
            return;
        }

        if (BasemapComboBox.SelectedItem is not BasemapComboItem item)
        {
            return;
        }

        if (string.Equals(item.Id, _activeBasemapId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        await TryApplyBasemapSelectionAsync(item.Basemap, item.Name);
    }

    private void ShowBasemapContextMenu(double x, double y, double? longitude, double? latitude)
    {
        if (!_isMapReady || _isBusy)
        {
            return;
        }

        _basemapContextMenu?.SetCurrentValue(ContextMenu.IsOpenProperty, false);

        var menu = new ContextMenu
        {
            PlacementTarget = MapWebView,
            Placement = PlacementMode.RelativePoint,
            HorizontalOffset = x,
            VerticalOffset = y
        };

        var availableAtPoint = _layerPolicyState.AvailableBasemaps
            .Where(basemap => IsBasemapAvailableAtPoint(basemap, longitude, latitude))
            .ToList();

        foreach (var basemap in availableAtPoint)
        {
            var isActive = string.Equals(basemap.Id, _activeBasemapId, StringComparison.OrdinalIgnoreCase);
            var menuItem = new MenuItem
            {
                Header = isActive ? $"{basemap.Name} (active)" : basemap.Name,
                IsEnabled = !isActive
            };

            var selectedBasemap = basemap;
            menuItem.Click += async (_, _) =>
            {
                await TryApplyBasemapSelectionAsync(selectedBasemap, selectedBasemap.Name);
            };

            menu.Items.Add(menuItem);
        }

        if (menu.Items.Count == 0)
        {
            menu.Items.Add(new MenuItem
            {
                Header = "No basemaps available",
                IsEnabled = false
            });
        }

        menu.Closed += (_, _) =>
        {
            if (ReferenceEquals(_basemapContextMenu, menu))
            {
                _basemapContextMenu = null;
            }
        };

        _basemapContextMenu = menu;
        menu.IsOpen = true;
    }

    private static bool IsBasemapAvailableAtPoint(BasemapLayerConfig basemap, double? longitude, double? latitude)
    {
        if (basemap.Bounds is null || longitude is null || latitude is null)
        {
            return true;
        }

        return longitude.Value >= basemap.Bounds.MinLon
            && longitude.Value <= basemap.Bounds.MaxLon
            && latitude.Value >= basemap.Bounds.MinLat
            && latitude.Value <= basemap.Bounds.MaxLat;
    }

    private async Task<bool> TryApplyBasemapSelectionAsync(BasemapLayerConfig basemap, string displayName)
    {
        if (string.Equals(basemap.Id, _activeBasemapId, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!basemap.CacheAllowed)
        {
            var confirm = MessageBox.Show(
                this,
                "This basemap does not allow local tile caching by policy.\n\nUse it anyway for this session?",
                "Basemap Policy Warning",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes)
            {
                RevertBasemapSelection();
                SetStatus("Basemap change canceled by policy confirmation.");
                return false;
            }
        }

        try
        {
            await ApplyBasemapAsync(basemap);
            SyncBasemapComboSelection(basemap.Id);
            var cacheLabel = basemap.CacheAllowed ? basemap.CacheMode : "disallowed";
            SetStatus($"Basemap switched to '{displayName}' (cache: {cacheLabel}, offline prefetch: {(basemap.OfflinePrefetchAllowed ? "allowed" : "not allowed")}).");
            return true;
        }
        catch (Exception ex)
        {
            LogError("MAP_BASEMAP_SWITCH", ex, basemap.Id);
            SetStatus($"Basemap switch failed: {ex.Message}");
            return false;
        }
    }

    private void SyncBasemapComboSelection(string basemapId)
    {
        if (BasemapComboBox.ItemsSource is not IEnumerable<BasemapComboItem> items)
        {
            return;
        }

        var selected = items.FirstOrDefault(item =>
            string.Equals(item.Id, basemapId, StringComparison.OrdinalIgnoreCase));
        if (selected is null)
        {
            return;
        }

        _isUpdatingBasemapSelection = true;
        try
        {
            BasemapComboBox.SelectedItem = selected;
        }
        finally
        {
            _isUpdatingBasemapSelection = false;
        }
    }

    private void RevertBasemapSelection()
    {
        if (string.IsNullOrWhiteSpace(_activeBasemapId) || BasemapComboBox.ItemsSource is not IEnumerable<BasemapComboItem> items)
        {
            return;
        }

        var selected = items.FirstOrDefault(item =>
            string.Equals(item.Id, _activeBasemapId, StringComparison.OrdinalIgnoreCase));
        if (selected is null)
        {
            return;
        }

        _isUpdatingBasemapSelection = true;
        try
        {
            BasemapComboBox.SelectedItem = selected;
        }
        finally
        {
            _isUpdatingBasemapSelection = false;
        }
    }

    private int? GetSplitIndex()
    {
        if (_currentDocument is null)
        {
            return null;
        }

        var candidate = _selectionStartIndex;
        if (candidate is null)
        {
            var normalized = GetNormalizedSelection();
            candidate = normalized?.StartIndex;
        }

        if (candidate is null)
        {
            return null;
        }

        if (candidate <= 0 || candidate >= _currentDocument.TotalPoints)
        {
            return null;
        }

        return candidate;
    }

    private async void SplitTrackButton_Click(object sender, RoutedEventArgs e)
    {
        await SplitTrackAsync();
    }

    private async Task SplitTrackAsync()
    {
        if (_isBusy || _currentDocument is null)
        {
            return;
        }

        var splitIndex = GetSplitIndex();
        if (splitIndex is null)
        {
            SetStatus("Set a valid start marker between first and last point for split.");
            return;
        }

        var defaultPart1 = BuildSplitFileName(_currentDocument.Name, part: 1);
        var part1Dialog = new SaveFileDialog
        {
            Title = "Save first split track",
            Filter = "GPX files (*.gpx)|*.gpx|All files (*.*)|*.*",
            AddExtension = true,
            DefaultExt = ".gpx",
            OverwritePrompt = true,
            FileName = defaultPart1
        };

        var initialDirectory = GetDefaultSaveDirectory();
        if (!string.IsNullOrWhiteSpace(initialDirectory) && Directory.Exists(initialDirectory))
        {
            part1Dialog.InitialDirectory = initialDirectory;
        }

        if (part1Dialog.ShowDialog(this) != true)
        {
            SetStatus("Split canceled.");
            return;
        }

        var part2Dialog = new SaveFileDialog
        {
            Title = "Save second split track",
            Filter = "GPX files (*.gpx)|*.gpx|All files (*.*)|*.*",
            AddExtension = true,
            DefaultExt = ".gpx",
            OverwritePrompt = true,
            FileName = BuildSplitFileName(_currentDocument.Name, part: 2),
            InitialDirectory = Path.GetDirectoryName(part1Dialog.FileName)
        };

        if (part2Dialog.ShowDialog(this) != true)
        {
            SetStatus("Split canceled.");
            return;
        }

        _isBusy = true;
        UpdateActionButtons();

        try
        {
            var split = TrackRangeOperations.SplitAtIndex(_currentDocument, splitIndex.Value);
            await _gpxWriter.WriteAsync(split.FirstPart, part1Dialog.FileName, CancellationToken.None);
            await _gpxWriter.WriteAsync(split.SecondPart, part2Dialog.FileName, CancellationToken.None);

            SetStatus(
                $"Split exported at index {splitIndex.Value}: '{Path.GetFileName(part1Dialog.FileName)}' and '{Path.GetFileName(part2Dialog.FileName)}'.");
        }
        catch (Exception ex)
        {
            LogError("SPLIT", ex, $"index={splitIndex.Value}");
            SetStatus($"Split failed: {ex.Message}");
        }
        finally
        {
            _isBusy = false;
            UpdateActionButtons();
        }
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
            await RefreshProfileAsync();

            UpdateTrackSummary();
            _hasUnsavedChanges = true;
            SetStatus($"Deleted {result.DeletedPoints:N0} points. Use Save GPX to persist changes.");
        }
        catch (Exception ex)
        {
            LogError("DELETE", ex, "TryDeleteSelectedRangeAsync");
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
            LogError("EXPORT", ex, "ExportRangeButton_Click");
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
            LogError("SAVE", ex, saveDialog.FileName);
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

    private static string BuildSplitFileName(string? trackName, int part)
    {
        var baseName = string.IsNullOrWhiteSpace(trackName) ? "track" : trackName;
        var invalid = Path.GetInvalidFileNameChars();
        var cleanName = string.Concat(baseName.Select(ch => invalid.Contains(ch) ? '_' : ch));
        return $"{cleanName}_part{part}.gpx";
    }

    private string? GetDefaultSaveDirectory()
    {
        if (string.IsNullOrWhiteSpace(_currentFilePath))
        {
            return null;
        }

        return Path.GetDirectoryName(_currentFilePath);
    }

    private async void ShowProfileCheckBox_Checked(object sender, RoutedEventArgs e)
    {
        _isProfileVisible = true;
        UpdateActionButtons();
        await RefreshProfileAsync();
    }

    private async void ShowProfileCheckBox_Unchecked(object sender, RoutedEventArgs e)
    {
        _isProfileVisible = false;
        UpdateActionButtons();
        await RefreshProfileAsync();
    }

    private async void ProfileModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _profileMode = GetSelectedProfileModeFromUi();
        if (!_isProfileVisible)
        {
            return;
        }

        await RefreshProfileAsync();
    }

    private string GetSelectedProfileModeFromUi()
    {
        return ProfileModeComboBox.SelectedIndex switch
        {
            1 => ProfileModeElevationDistance,
            2 => ProfileModeSpeedTime,
            3 => ProfileModeSpeedDistance,
            _ => ProfileModeElevationTime
        };
    }

    private async Task RefreshProfileAsync()
    {
        if (!_isMapReady)
        {
            return;
        }

        if (!_isProfileVisible)
        {
            await ExecuteScriptsAsync(MapScriptFactory.BuildProfileVisibilityScripts(false));
            return;
        }

        if (_currentDocument is null || _indexedPoints.Count == 0)
        {
            await ExecuteScriptsAsync(MapScriptFactory.BuildClearProfileScripts());
            await ExecuteScriptsAsync(MapScriptFactory.BuildProfileVisibilityScripts(true));
            return;
        }

        var profilePayload = BuildProfilePayload();
        var samples = DownsampleProfileSamples(profilePayload.Samples, MaxProfileSamples);
        var payloadJson = JsonSerializer.Serialize(new
        {
            mode = _profileMode,
            xAxis = profilePayload.XAxis,
            yAxis = profilePayload.YAxis,
            xLabel = profilePayload.XLabel,
            yLabel = profilePayload.YLabel,
            points = samples.Select(sample => new { index = sample.Index, x = sample.X, y = sample.Y })
        });

        await ExecuteScriptsAsync(MapScriptFactory.BuildSetProfileDataScripts(payloadJson));
        await ExecuteScriptsAsync(MapScriptFactory.BuildProfileSelectionScripts(_selectionStartIndex, _selectionEndIndex));
        await ExecuteScriptsAsync(MapScriptFactory.BuildProfileVisibilityScripts(true));
    }

    private ProfilePayload BuildProfilePayload()
    {
        return _profileMode switch
        {
            ProfileModeElevationDistance => new ProfilePayload(
                BuildElevationDistanceProfileSamples(),
                "distance",
                "elevation",
                "Distance",
                "Elevation (m)"),
            ProfileModeSpeedTime => new ProfilePayload(
                BuildSpeedTimeProfileSamples(),
                "time",
                "speed",
                "Time",
                "Speed (km/h)"),
            ProfileModeSpeedDistance => new ProfilePayload(
                BuildSpeedDistanceProfileSamples(),
                "distance",
                "speed",
                "Distance",
                "Speed (km/h)"),
            _ => new ProfilePayload(
                BuildElevationTimeProfileSamples(),
                "time",
                "elevation",
                "Time",
                "Elevation (m)")
        };
    }

    private List<ProfileSample> BuildElevationTimeProfileSamples()
    {
        var samples = new List<ProfileSample>(_indexedPoints.Count);
        DateTimeOffset? origin = null;

        foreach (var indexed in _indexedPoints)
        {
            var point = indexed.Point;
            if (point.Elevation is null || point.Time is null)
            {
                continue;
            }

            origin ??= point.Time.Value;
            var seconds = (point.Time.Value - origin.Value).TotalSeconds;
            samples.Add(new ProfileSample(indexed.GlobalIndex, seconds, point.Elevation.Value));
        }

        return samples;
    }

    private List<ProfileSample> BuildElevationDistanceProfileSamples()
    {
        var samples = new List<ProfileSample>(_indexedPoints.Count);
        var distanceMeters = 0.0;

        for (var index = 0; index < _indexedPoints.Count; index++)
        {
            var current = _indexedPoints[index].Point;
            if (index > 0)
            {
                var previous = _indexedPoints[index - 1].Point;
                distanceMeters += GetDistanceMeters(previous.Latitude, previous.Longitude, current.Latitude, current.Longitude);
            }

            if (current.Elevation is null)
            {
                continue;
            }

            samples.Add(new ProfileSample(index, distanceMeters, current.Elevation.Value));
        }

        return samples;
    }

    private List<ProfileSample> BuildSpeedTimeProfileSamples()
    {
        var samples = new List<ProfileSample>(_indexedPoints.Count);
        DateTimeOffset? origin = null;

        for (var index = 1; index < _indexedPoints.Count; index++)
        {
            var current = _indexedPoints[index].Point;
            if (current.Time is not null)
            {
                origin ??= current.Time.Value;
            }

            var speedKmh = TryGetSpeedKmh(index);
            if (speedKmh is null || current.Time is null || origin is null)
            {
                continue;
            }

            var seconds = (current.Time.Value - origin.Value).TotalSeconds;
            samples.Add(new ProfileSample(index, seconds, speedKmh.Value));
        }

        return samples;
    }

    private List<ProfileSample> BuildSpeedDistanceProfileSamples()
    {
        var samples = new List<ProfileSample>(_indexedPoints.Count);
        var distanceMeters = 0.0;

        for (var index = 0; index < _indexedPoints.Count; index++)
        {
            var current = _indexedPoints[index].Point;
            if (index > 0)
            {
                var previous = _indexedPoints[index - 1].Point;
                distanceMeters += GetDistanceMeters(previous.Latitude, previous.Longitude, current.Latitude, current.Longitude);
            }

            var speedKmh = TryGetSpeedKmh(index);
            if (speedKmh is null)
            {
                continue;
            }

            samples.Add(new ProfileSample(index, distanceMeters, speedKmh.Value));
        }

        return samples;
    }

    private static List<ProfileSample> DownsampleProfileSamples(IReadOnlyList<ProfileSample> samples, int maxSamples)
    {
        if (samples.Count <= maxSamples || maxSamples <= 0)
        {
            return [.. samples];
        }

        var result = new List<ProfileSample>(maxSamples + 1);
        var step = (int)Math.Ceiling(samples.Count / (double)maxSamples);

        for (var index = 0; index < samples.Count; index += step)
        {
            result.Add(samples[index]);
        }

        var last = samples[^1];
        if (result.Count == 0 || result[^1].Index != last.Index)
        {
            result.Add(last);
        }

        return result;
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

    private static void LogError(string code, Exception? ex, string context)
    {
        try
        {
            var logRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GpxCut", "logs");
            Directory.CreateDirectory(logRoot);

            var logPath = Path.Combine(logRoot, $"gpxcut-{DateTime.UtcNow:yyyyMMdd}.log");
            var line =
                $"{DateTime.UtcNow:O} | {code} | {context} | {ex?.GetType().Name ?? "Info"} | {ex?.Message ?? "n/a"}{Environment.NewLine}";

            lock (LogSync)
            {
                File.AppendAllText(logPath, line);
            }
        }
        catch
        {
            // Logging must never break user workflows.
        }
    }

    private sealed record ProfileSample(int Index, double X, double Y);

    private sealed record ProfilePayload(
        List<ProfileSample> Samples,
        string XAxis,
        string YAxis,
        string XLabel,
        string YLabel);

    private sealed record BasemapLayerConfig(
        string Id,
        string Name,
        string Type,
        List<string> Tiles,
        int TileSize,
        int? WmtsZoomOffset,
        int? MinZoom,
        int? MaxZoom,
        string Attribution,
        string LicenseName,
        string? LicenseUrl,
        bool CommercialUseAllowed,
        bool CacheAllowed,
        string CacheMode,
        int? MinTtlDays,
        bool OfflinePrefetchAllowed,
        string? Notes,
        BasemapBounds? Bounds);

    private sealed record BasemapBounds(double MinLon, double MinLat, double MaxLon, double MaxLat);

    private sealed record LayerPolicyState(
        BasemapLayerConfig EffectiveBasemap,
        List<BasemapLayerConfig> AvailableBasemaps,
        bool LoadedFromPolicy,
        string? PolicyPath);

    private sealed record BasemapComboItem(
        string Id,
        string Name,
        BasemapLayerConfig Basemap)
    {
        public string DisplayName => Basemap.CacheAllowed ? Name : $"{Name} [No cache]";
    }

    private sealed class LayerPolicyDocument
    {
        public string? SelectedLayerId { get; set; }

        public List<LayerPolicyEntry> Layers { get; set; } = [];
    }

    private sealed class LayerPolicyEntry
    {
        public string Id { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public bool EnabledByDefault { get; set; }

        public string Attribution { get; set; } = "&copy; OpenStreetMap contributors";

        public string LicenseName { get; set; } = "Unknown";

        public string? LicenseUrl { get; set; }

        public bool CommercialUseAllowed { get; set; } = true;

        public bool CacheAllowed { get; set; } = true;

        public string CacheMode { get; set; } = "http-headers";

        public int? MinTtlDays { get; set; }

        public bool OfflinePrefetchAllowed { get; set; }

        public string? Notes { get; set; }

        public LayerPolicyMap? Map { get; set; }
    }

    private sealed class LayerPolicyMap
    {
        public string Type { get; set; } = "xyz";

        public List<string>? Tiles { get; set; }

        public int? TileSize { get; set; }

        public int? WmtsZoomOffset { get; set; }

        public int? MinZoom { get; set; }

        public int? MaxZoom { get; set; }

        public LayerPolicyBounds? Bounds { get; set; }
    }

    private sealed class LayerPolicyBounds
    {
        public double MinLon { get; set; }

        public double MinLat { get; set; }

        public double MaxLon { get; set; }

        public double MaxLat { get; set; }
    }

    private sealed class AppSettings
    {
        public string? SelectedBasemapId { get; set; }
    }

    private sealed record IndexedTrackPoint(int GlobalIndex, int SegmentIndex, int PointIndex, TrackPoint Point);
}