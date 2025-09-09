using DeviceSpecAnalyzer.Core.Interfaces;
using DeviceSpecAnalyzer.Core.Models;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace DeviceSpecAnalyzer.Processing.Parsers;

public class AstmParser : IProtocolParser
{
    private readonly ILogger<AstmParser> _logger;
    
    public string ProtocolName => "ASTM";

    private readonly Regex _astmPatterns = new(@"\b(?:ASTM|E\d+|laboratory|LIS)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private readonly Regex _messagePatterns = new(@"(?:record|message|frame)\s+(?:type|format|structure)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private readonly Regex _versionPattern = new(@"(?:ASTM\s+)?(?:E\d+-?\d*|\d+\.\d+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public AstmParser(ILogger<AstmParser> logger)
    {
        _logger = logger;
    }

    public bool CanParse(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        return _astmPatterns.IsMatch(text) && _messagePatterns.IsMatch(text);
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

            _logger.LogInformation("Successfully parsed ASTM content. Messages: {MessageCount}, Fields: {FieldCount}", 
                result.MessageFormats.Count, result.DataFields.Count);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing ASTM content");
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
            var recordTypes = new[] { "H", "P", "O", "R", "C", "M", "S", "L" };
            int orderIndex = 0;

            foreach (var recordType in recordTypes)
            {
                var pattern = new Regex($@"{recordType}\s+Record.*?(?=\n\s*[A-Z]\s+Record|\n\s*\d+\.|\Z)", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                var matches = pattern.Matches(text);
                
                foreach (Match match in matches)
                {
                    if (match.Value.Length > 50)
                    {
                        sections.Add(new DocumentSection
                        {
                            DocumentId = documentId,
                            SectionType = DocumentSectionTypes.MessageFormat,
                            Title = $"{GetRecordTypeName(recordType)} Record",
                            Content = match.Value.Trim(),
                            OrderIndex = orderIndex++,
                            CreatedAt = DateTime.UtcNow
                        });
                    }
                }
            }

            var fieldPattern = new Regex(@"field\s+definitions?.*?(?=\n\s*\d+\.|\n\s*[A-Z][A-Z\s]+\n|\Z)", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            var fieldMatches = fieldPattern.Matches(text);
            
            foreach (Match match in fieldMatches)
            {
                if (match.Value.Length > 50)
                {
                    sections.Add(new DocumentSection
                    {
                        DocumentId = documentId,
                        SectionType = DocumentSectionTypes.DataFields,
                        Title = "Field Definitions",
                        Content = match.Value.Trim(),
                        OrderIndex = orderIndex++,
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }
        });

        return sections;
    }

    private string ExtractVersion(string text)
    {
        var match = _versionPattern.Match(text);
        return match.Success ? match.Value : "Unknown";
    }

    private List<MessageFormat> ExtractMessageFormats(string text)
    {
        var formats = new List<MessageFormat>();
        var recordTypes = new[] { 
            ("H", "Header Record"), 
            ("P", "Patient Record"), 
            ("O", "Order Record"), 
            ("R", "Result Record"), 
            ("C", "Comment Record"), 
            ("M", "Manufacturer Record"),
            ("S", "Scientific Record"),
            ("L", "Terminator Record")
        };
        
        foreach (var (type, name) in recordTypes)
        {
            var pattern = new Regex($@"{type}\s+Record.*?(?:format|structure)([^\.]*\.)", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            var match = pattern.Match(text);
            
            if (match.Success)
            {
                formats.Add(new MessageFormat
                {
                    Name = name,
                    Structure = type,
                    Description = match.Groups[1].Value.Trim()
                });
            }
        }

        return formats;
    }

    private List<DataField> ExtractDataFields(string text)
    {
        var fields = new List<DataField>();
        
        var fieldRegex = new Regex(@"(?:field\s+)?(\d+)[\.\)]\s+([^\n\(]+)(?:\s*\(([^)]+)\))?", RegexOptions.IgnoreCase);
        var matches = fieldRegex.Matches(text);
        
        foreach (Match match in matches)
        {
            var fieldNumber = match.Groups[1].Value;
            var fieldName = match.Groups[2].Value.Trim();
            var fieldType = match.Groups[3].Success ? match.Groups[3].Value.Trim() : "String";
            
            if (!string.IsNullOrEmpty(fieldName) && fieldName.Length < 100)
            {
                fields.Add(new DataField
                {
                    Name = $"Field {fieldNumber}: {fieldName}",
                    Type = fieldType,
                    Description = fieldName
                });
            }
        }

        return fields;
    }

    private List<CommunicationDetail> ExtractCommunicationDetails(string text)
    {
        var details = new List<CommunicationDetail>();
        
        if (text.ToLower().Contains("serial"))
        {
            details.Add(new CommunicationDetail
            {
                Type = "Serial",
                Protocol = "RS232/RS485",
                Description = "Serial communication interface"
            });
        }

        if (text.ToLower().Contains("tcp") || text.ToLower().Contains("ethernet"))
        {
            details.Add(new CommunicationDetail
            {
                Type = "Network",
                Protocol = "TCP/IP",
                Description = "Network communication interface"
            });
        }

        var baudPattern = new Regex(@"(\d+)\s*baud", RegexOptions.IgnoreCase);
        var baudMatch = baudPattern.Match(text);
        if (baudMatch.Success)
        {
            var existingSerial = details.FirstOrDefault(d => d.Type == "Serial");
            if (existingSerial != null)
            {
                existingSerial.Parameters["BaudRate"] = baudMatch.Groups[1].Value;
            }
        }

        return details;
    }

    private List<string> ExtractExamples(string text)
    {
        var examples = new List<string>();
        
        var examplePattern = new Regex(@"(?:example|sample)[:\s]*([^\n]*(?:\n[^\n]*){0,5})", RegexOptions.IgnoreCase);
        var matches = examplePattern.Matches(text);
        
        foreach (Match match in matches)
        {
            var example = match.Groups[1].Value.Trim();
            if (example.Length > 10 && example.Length < 500)
            {
                examples.Add(example);
            }
        }

        return examples;
    }

    private Dictionary<string, string> ExtractKeySections(string text)
    {
        var sections = new Dictionary<string, string>();
        
        var sectionPattern = new Regex(@"^\s*(\d+\.?\d*)\s+([A-Z][A-Za-z\s]+)$", RegexOptions.Multiline);
        var matches = sectionPattern.Matches(text);
        
        foreach (Match match in matches)
        {
            var number = match.Groups[1].Value.Trim();
            var title = match.Groups[2].Value.Trim();
            
            if (!sections.ContainsKey(number) && title.Length < 100)
            {
                sections[number] = title;
            }
        }

        return sections;
    }

    private string GetRecordTypeName(string recordType)
    {
        return recordType switch
        {
            "H" => "Header",
            "P" => "Patient",
            "O" => "Order",
            "R" => "Result",
            "C" => "Comment",
            "M" => "Manufacturer",
            "S" => "Scientific",
            "L" => "Terminator",
            _ => "Unknown"
        };
    }
}