using System.Globalization;
using System.Xml.Linq;
using System.Xml;
using GpxCut.Core.Domain;

namespace GpxCut.Core.IO;

public sealed class GpxReader
{
    public async Task<TrackDocument> ReadAsync(string filePath, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("A GPX file path must be provided.", nameof(filePath));
        }

        if (!File.Exists(filePath))
        {
            throw new GpxReadException($"The file '{filePath}' was not found.");
        }

        var settings = new XmlReaderSettings
        {
            Async = true,
            IgnoreComments = true,
            IgnoreWhitespace = true,
            DtdProcessing = DtdProcessing.Prohibit
        };

        try
        {
            await using var stream = File.OpenRead(filePath);
            using var reader = XmlReader.Create(stream, settings);

            var segments = new List<TrackSegment>();
            var metadata = new TrackMetadata();
            List<TrackPoint>? currentSegmentPoints = null;
            var currentSegmentDepth = -1;

            string? trackName = null;
            string? trackDescription = null;

            var advanceReader = true;
            while (true)
            {
                if (advanceReader)
                {
                    if (!await reader.ReadAsync())
                    {
                        break;
                    }
                }

                advanceReader = true;
                cancellationToken.ThrowIfCancellationRequested();

                if (reader.NodeType == XmlNodeType.Element)
                {
                    if (IsName(reader, "name"))
                    {
                        var value = await reader.ReadElementContentAsStringAsync();
                        if (trackName is null)
                        {
                            trackName = value;
                        }

                        advanceReader = false;
                        continue;
                    }

                    if (IsName(reader, "desc"))
                    {
                        var value = await reader.ReadElementContentAsStringAsync();
                        if (trackDescription is null)
                        {
                            trackDescription = value;
                        }

                        advanceReader = false;
                        continue;
                    }

                    if (IsName(reader, "trkseg"))
                    {
                        currentSegmentPoints = new List<TrackPoint>();
                        currentSegmentDepth = reader.Depth;

                        if (reader.IsEmptyElement)
                        {
                            currentSegmentPoints = null;
                            currentSegmentDepth = -1;
                        }

                        continue;
                    }

                    if (IsName(reader, "trkpt") && currentSegmentPoints is not null)
                    {
                        var point = await ReadPointFromOuterXmlAsync(reader);
                        currentSegmentPoints.Add(point);
                        metadata.IncludePoint(point.Latitude, point.Longitude, point.Time);
                        advanceReader = false;
                        continue;
                    }
                }

                if (
                    reader.NodeType == XmlNodeType.EndElement &&
                    IsName(reader, "trkseg") &&
                    currentSegmentPoints is not null &&
                    reader.Depth == currentSegmentDepth)
                {
                    if (currentSegmentPoints.Count > 0)
                    {
                        segments.Add(new TrackSegment { Points = currentSegmentPoints });
                    }

                    currentSegmentPoints = null;
                    currentSegmentDepth = -1;
                }
            }

            if (segments.Count == 0)
            {
                throw new GpxReadException("No track segments were found in the GPX file.");
            }

            var document = new TrackDocument
            {
                Name = trackName,
                Description = trackDescription,
                Segments = segments,
                Metadata = metadata
            };

            if (document.TotalPoints == 0)
            {
                throw new GpxReadException("The GPX file does not contain any track points.");
            }

            return document;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (GpxReadException)
        {
            throw;
        }
        catch (XmlException ex)
        {
            throw new GpxReadException("The GPX file contains invalid XML.", ex);
        }
        catch (IOException ex)
        {
            throw new GpxReadException("The GPX file could not be read.", ex);
        }
    }

    private static bool IsName(XmlReader reader, string expectedName)
    {
        return string.Equals(reader.LocalName, expectedName, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<TrackPoint> ReadPointFromOuterXmlAsync(XmlReader reader)
    {
        var outerXml = await reader.ReadOuterXmlAsync();
        if (string.IsNullOrWhiteSpace(outerXml))
        {
            throw new GpxReadException("Encountered an empty track point node.");
        }

        var element = XElement.Parse(outerXml, LoadOptions.None);
        var latitudeText = element.Attribute("lat")?.Value;
        var longitudeText = element.Attribute("lon")?.Value;

        if (!TryParseCoordinate(latitudeText, out var latitude) || !TryParseCoordinate(longitudeText, out var longitude))
        {
            throw new GpxReadException("Encountered a track point without valid latitude/longitude values.");
        }

        double? elevation = null;
        var eleElement = element.Elements().FirstOrDefault(x => string.Equals(x.Name.LocalName, "ele", StringComparison.OrdinalIgnoreCase));
        if (eleElement is not null && double.TryParse(eleElement.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedElevation))
        {
            elevation = parsedElevation;
        }

        DateTimeOffset? timestamp = null;
        var timeElement = element.Elements().FirstOrDefault(x => string.Equals(x.Name.LocalName, "time", StringComparison.OrdinalIgnoreCase));
        if (timeElement is not null && DateTimeOffset.TryParse(timeElement.Value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsedTime))
        {
            timestamp = parsedTime;
        }

        string? extensionsRawXml = null;
        var extensionsElement = element.Elements().FirstOrDefault(x => string.Equals(x.Name.LocalName, "extensions", StringComparison.OrdinalIgnoreCase));
        if (extensionsElement is not null)
        {
            extensionsRawXml = extensionsElement.ToString(SaveOptions.DisableFormatting);
        }

        return new TrackPoint
        {
            Latitude = latitude,
            Longitude = longitude,
            Elevation = elevation,
            Time = timestamp,
            ExtensionsRawXml = extensionsRawXml
        };
    }

    private static bool TryParseCoordinate(string? input, out double value)
    {
        return double.TryParse(input, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }
}
