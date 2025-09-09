using DeviceSpecAnalyzer.Core.Interfaces;
using DeviceSpecAnalyzer.Core.Models;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace DeviceSpecAnalyzer.Processing.Parsers;

public class Poct1AParser : IProtocolParser
{
    private readonly ILogger<Poct1AParser> _logger;
    
    public string ProtocolName => "POCT1-A";

    private readonly Regex _poct1APatterns = new(@"\b(?:POCT1?-?A|Point.of.Care|POC)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private readonly Regex _messagePatterns = new(@"(?:message|frame|record|header|data)\s+(?:format|structure|layout|definition)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private readonly Regex _fieldPatterns = new(@"(?:field|element|component|parameter)\s+(?:name|type|description|definition)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private readonly Regex _versionPattern = new(@"POCT1?-?A\s+(?:version\s+)?(\d+\.?\d*)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public Poct1AParser(ILogger<Poct1AParser> logger)
    {
        _logger = logger;
    }

    public bool CanParse(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        return _poct1APatterns.IsMatch(text) && 
               (_messagePatterns.IsMatch(text) || _fieldPatterns.IsMatch(text));
    }

    public async Task<ProtocolParseResult> ParseAsync(string text)
    {
        try
        {
            var result = new ProtocolParseResult
            {
                Protocol = ProtocolName,
                IsSuccess = true
            };

            await Task.Run(() =>
            {
                result.Version = ExtractVersion(text);
                result.MessageFormats = ExtractMessageFormats(text);
                result.DataFields = ExtractDataFields(text);
                result.CommunicationDetails = ExtractCommunicationDetails(text);
                result.Examples = ExtractExamples(text);
                result.KeySections = ExtractKeySections(text);
            });

            _logger.LogInformation("Successfully parsed POCT1-A content. Messages: {MessageCount}, Fields: {FieldCount}", 
                result.MessageFormats.Count, result.DataFields.Count);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing POCT1-A content");
            return new ProtocolParseResult
            {
                Protocol = ProtocolName,
                IsSuccess = false,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<IEnumerable<DocumentSection>> ExtractSectionsAsync(string text, int documentId)
    {
        var sections = new List<DocumentSection>();

        await Task.Run(() =>
        {
            var sectionPatterns = new Dictionary<string, Regex>
            {
                { DocumentSectionTypes.MessageFormat, new Regex(@"(?:message|frame)\s+(?:format|structure).*?(?=\n\s*\d+\.|\n\s*[A-Z][A-Z\s]+\n|\Z)", RegexOptions.Singleline | RegexOptions.IgnoreCase) },
                { DocumentSectionTypes.DataFields, new Regex(@"(?:data\s+)?fields?.*?(?=\n\s*\d+\.|\n\s*[A-Z][A-Z\s]+\n|\Z)", RegexOptions.Singleline | RegexOptions.IgnoreCase) },
                { DocumentSectionTypes.Communication, new Regex(@"communication.*?(?=\n\s*\d+\.|\n\s*[A-Z][A-Z\s]+\n|\Z)", RegexOptions.Singleline | RegexOptions.IgnoreCase) },
                { DocumentSectionTypes.Examples, new Regex(@"examples?.*?(?=\n\s*\d+\.|\n\s*[A-Z][A-Z\s]+\n|\Z)", RegexOptions.Singleline | RegexOptions.IgnoreCase) }
            };

            int orderIndex = 0;
            foreach (var pattern in sectionPatterns)
            {
                var matches = pattern.Value.Matches(text);
                foreach (Match match in matches)
                {
                    if (match.Value.Length > 50)
                    {
                        sections.Add(new DocumentSection
                        {
                            DocumentId = documentId,
                            SectionType = pattern.Key,
                            Content = match.Value.Trim(),
                            OrderIndex = orderIndex++,
                            CreatedAt = DateTime.UtcNow
                        });
                    }
                }
            }
        });

        return sections;
    }

    private string ExtractVersion(string text)
    {
        var match = _versionPattern.Match(text);
        return match.Success ? match.Groups[1].Value : "Unknown";
    }

    private List<MessageFormat> ExtractMessageFormats(string text)
    {
        var formats = new List<MessageFormat>();
        
        var messageRegex = new Regex(@"(?:message|frame|record)\s+(?:type|format|name):\s*([^\n]+)", RegexOptions.IgnoreCase);
        var matches = messageRegex.Matches(text);
        
        foreach (Match match in matches)
        {
            var messageName = match.Groups[1].Value.Trim();
            if (!string.IsNullOrEmpty(messageName))
            {
                formats.Add(new MessageFormat
                {
                    Name = messageName,
                    Description = ExtractMessageDescription(text, messageName)
                });
            }
        }

        return formats;
    }

    private List<DataField> ExtractDataFields(string text)
    {
        var fields = new List<DataField>();
        
        var fieldRegex = new Regex(@"(?:field|element)\s+(\w+)(?:\s*\(([^)]+)\))?\s*:\s*([^\n]+)", RegexOptions.IgnoreCase);
        var matches = fieldRegex.Matches(text);
        
        foreach (Match match in matches)
        {
            fields.Add(new DataField
            {
                Name = match.Groups[1].Value.Trim(),
                Type = match.Groups[2].Success ? match.Groups[2].Value.Trim() : "String",
                Description = match.Groups[3].Value.Trim()
            });
        }

        return fields;
    }

    private List<CommunicationDetail> ExtractCommunicationDetails(string text)
    {
        var details = new List<CommunicationDetail>();
        
        var tcpRegex = new Regex(@"TCP/IP.*?port\s+(\d+)", RegexOptions.IgnoreCase);
        var tcpMatch = tcpRegex.Match(text);
        if (tcpMatch.Success)
        {
            details.Add(new CommunicationDetail
            {
                Type = "TCP/IP",
                Protocol = "TCP",
                Parameters = new Dictionary<string, string> { { "Port", tcpMatch.Groups[1].Value } },
                Description = "TCP/IP network communication"
            });
        }

        var serialRegex = new Regex(@"serial.*?(?:RS232|COM\d+)", RegexOptions.IgnoreCase);
        if (serialRegex.IsMatch(text))
        {
            details.Add(new CommunicationDetail
            {
                Type = "Serial",
                Protocol = "RS232",
                Description = "Serial port communication"
            });
        }

        return details;
    }

    private List<string> ExtractExamples(string text)
    {
        var examples = new List<string>();
        
        var exampleRegex = new Regex(@"(?:example|sample)[\s\S]*?(?=\n\s*(?:\d+\.|\w+:)|\Z)", RegexOptions.IgnoreCase);
        var matches = exampleRegex.Matches(text);
        
        foreach (Match match in matches)
        {
            var example = match.Value.Trim();
            if (example.Length > 20 && example.Length < 1000)
            {
                examples.Add(example);
            }
        }

        return examples;
    }

    private Dictionary<string, string> ExtractKeySections(string text)
    {
        var sections = new Dictionary<string, string>();
        
        var sectionRegex = new Regex(@"^\s*(\d+\.?\d*)\s+([A-Z][A-Z\s]+)$", RegexOptions.Multiline);
        var matches = sectionRegex.Matches(text);
        
        foreach (Match match in matches)
        {
            var sectionNumber = match.Groups[1].Value.Trim();
            var sectionTitle = match.Groups[2].Value.Trim();
            
            if (!sections.ContainsKey(sectionNumber))
            {
                sections[sectionNumber] = sectionTitle;
            }
        }

        return sections;
    }

    private string ExtractMessageDescription(string text, string messageName)
    {
        var descriptionRegex = new Regex($@"{Regex.Escape(messageName)}.*?(?:description|purpose):\s*([^\n]+)", RegexOptions.IgnoreCase);
        var match = descriptionRegex.Match(text);
        return match.Success ? match.Groups[1].Value.Trim() : string.Empty;
    }
}