using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using GpxCut.Core.Domain;

namespace GpxCut.Core.IO;

public sealed class GpxWriter
{
    public async Task WriteAsync(TrackDocument document, string filePath, CancellationToken cancellationToken)
    {
        if (document is null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("An output file path must be provided.", nameof(filePath));
        }

        if (document.TotalPoints <= 0)
        {
            throw new InvalidOperationException("Cannot export an empty track document.");
        }

        var settings = new XmlWriterSettings
        {
            Async = true,
            Indent = true,
            Encoding = System.Text.Encoding.UTF8
        };

        await using var stream = File.Create(filePath);
        await using var writer = XmlWriter.Create(stream, settings);

        await writer.WriteStartDocumentAsync();
        await writer.WriteStartElementAsync(null, "gpx", "http://www.topografix.com/GPX/1/1");
        await writer.WriteAttributeStringAsync(null, "version", null, "1.1");
        await writer.WriteAttributeStringAsync(null, "creator", null, "gpxcut");

        await WriteMetadataAsync(writer, document, cancellationToken);
        await writer.WriteStartElementAsync(null, "trk", null);

        if (!string.IsNullOrWhiteSpace(document.Name))
        {
            await writer.WriteElementStringAsync(null, "name", null, document.Name);
        }

        if (!string.IsNullOrWhiteSpace(document.Description))
        {
            await writer.WriteElementStringAsync(null, "desc", null, document.Description);
        }

        foreach (var segment in document.Segments)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (segment.Points.Count == 0)
            {
                continue;
            }

            await writer.WriteStartElementAsync(null, "trkseg", null);
            foreach (var point in segment.Points)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await WritePointAsync(writer, point);
            }

            await writer.WriteEndElementAsync();
        }

        await writer.WriteEndElementAsync();
        await writer.WriteEndElementAsync();
        await writer.WriteEndDocumentAsync();
        await writer.FlushAsync();
    }

    private static async Task WriteMetadataAsync(XmlWriter writer, TrackDocument document, CancellationToken cancellationToken)
    {
        await writer.WriteStartElementAsync(null, "metadata", null);

        if (!string.IsNullOrWhiteSpace(document.Name))
        {
            await writer.WriteElementStringAsync(null, "name", null, document.Name);
        }

        if (!string.IsNullOrWhiteSpace(document.Description))
        {
            await writer.WriteElementStringAsync(null, "desc", null, document.Description);
        }

        if (document.Metadata.StartTime is not null)
        {
            await writer.WriteElementStringAsync(
                null,
                "time",
                null,
                document.Metadata.StartTime.Value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
        }

        if (document.Metadata.HasBounds)
        {
            await writer.WriteStartElementAsync(null, "bounds", null);
            await writer.WriteAttributeStringAsync(null, "minlat", null, document.Metadata.MinLatitude!.Value.ToString("G17", CultureInfo.InvariantCulture));
            await writer.WriteAttributeStringAsync(null, "minlon", null, document.Metadata.MinLongitude!.Value.ToString("G17", CultureInfo.InvariantCulture));
            await writer.WriteAttributeStringAsync(null, "maxlat", null, document.Metadata.MaxLatitude!.Value.ToString("G17", CultureInfo.InvariantCulture));
            await writer.WriteAttributeStringAsync(null, "maxlon", null, document.Metadata.MaxLongitude!.Value.ToString("G17", CultureInfo.InvariantCulture));
            await writer.WriteEndElementAsync();
        }

        cancellationToken.ThrowIfCancellationRequested();
        await writer.WriteEndElementAsync();
    }

    private static async Task WritePointAsync(XmlWriter writer, TrackPoint point)
    {
        await writer.WriteStartElementAsync(null, "trkpt", null);
        await writer.WriteAttributeStringAsync(null, "lat", null, point.Latitude.ToString("G17", CultureInfo.InvariantCulture));
        await writer.WriteAttributeStringAsync(null, "lon", null, point.Longitude.ToString("G17", CultureInfo.InvariantCulture));

        if (point.Elevation is not null)
        {
            await writer.WriteElementStringAsync(null, "ele", null, point.Elevation.Value.ToString("G17", CultureInfo.InvariantCulture));
        }

        if (point.Time is not null)
        {
            await writer.WriteElementStringAsync(null, "time", null, point.Time.Value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
        }

        if (!string.IsNullOrWhiteSpace(point.ExtensionsRawXml))
        {
            var extensions = XElement.Parse(point.ExtensionsRawXml, LoadOptions.None);
            await writer.WriteRawAsync(extensions.ToString(SaveOptions.DisableFormatting));
        }

        await writer.WriteEndElementAsync();
    }
}