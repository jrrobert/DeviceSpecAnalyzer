using DeviceSpecAnalyzer.Core.Interfaces;
using DeviceSpecAnalyzer.Core.Models;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace DeviceSpecAnalyzer.Processing.Parsers;

public class Hl7Parser : IProtocolParser
{
    private readonly ILogger<Hl7Parser> _logger;
    
    public string ProtocolName => "HL7";

    private readonly Regex _hl7Patterns = new(@"\b(?:HL7|Health\s+Level\s+Seven|FHIR|MLLP)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private readonly Regex _messagePatterns = new(@"(?:message|segment|field)\s+(?:type|format|structure)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private readonly Regex _versionPattern = new(@"HL7\s+(?:version\s+)?(\d+\.?\d*)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public Hl7Parser(ILogger<Hl7Parser> logger)
    {
        _logger = logger;
    }

    public bool CanParse(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        return _hl7Patterns.IsMatch(text) && _messagePatterns.IsMatch(text);
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

            _logger.LogInformation("Successfully parsed HL7 content. Messages: {MessageCount}, Fields: {FieldCount}", 
                result.MessageFormats.Count, result.DataFields.Count);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing HL7 content");
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
            var commonSegments = new[] { "MSH", "PID", "PV1", "OBR", "OBX", "NTE", "MSA", "ERR" };
            int orderIndex = 0;

            foreach (var segment in commonSegments)
            {
                var pattern = new Regex($@"{segment}\s+(?:segment|message).*?(?=\n\s*[A-Z]{{3}}\s+|\n\s*\d+\.|\Z)", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                var matches = pattern.Matches(text);
                
                foreach (Match match in matches)
                {
                    if (match.Value.Length > 50)
                    {
                        sections.Add(new DocumentSection
                        {
                            DocumentId = documentId,
                            SectionType = DocumentSectionTypes.MessageFormat,
                            Title = $"{segment} Segment",
                            Content = match.Value.Trim(),
                            OrderIndex = orderIndex++,
                            CreatedAt = DateTime.UtcNow
                        });
                    }
                }
            }

            var messageTypes = new[] { "ADT", "ORU", "ORM", "ACK", "QRY", "DSR" };
            
            foreach (var msgType in messageTypes)
            {
                var pattern = new Regex($@"{msgType}.*?message.*?(?=\n\s*[A-Z]{{3}}\s+|\n\s*\d+\.|\Z)", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                var matches = pattern.Matches(text);
                
                foreach (Match match in matches)
                {
                    if (match.Value.Length > 50)
                    {
                        sections.Add(new DocumentSection
                        {
                            DocumentId = documentId,
                            SectionType = DocumentSectionTypes.MessageFormat,
                            Title = $"{msgType} Message Type",
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
        if (match.Success)
            return match.Groups[1].Value;
            
        if (text.ToLower().Contains("fhir"))
            return "FHIR";
            
        return "2.x";
    }

    private List<MessageFormat> ExtractMessageFormats(string text)
    {
        var formats = new List<MessageFormat>();
        
        var messageTypes = new Dictionary<string, string>
        {
            { "ADT", "Admit/Discharge/Transfer" },
            { "ORU", "Observation Result" },
            { "ORM", "Order Message" },
            { "ACK", "Acknowledgment" },
            { "QRY", "Query" },
            { "DSR", "Display Response" }
        };
        
        foreach (var msgType in messageTypes)
        {
            var pattern = new Regex($@"{msgType.Key}.*?(?:message|format)([^\.]*\.)", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            var match = pattern.Match(text);
            
            if (match.Success)
            {
                formats.Add(new MessageFormat
                {
                    Name = msgType.Value,
                    Structure = msgType.Key,
                    Description = match.Groups[1].Value.Trim()
                });
            }
        }

        var segments = new[] { "MSH", "PID", "PV1", "OBR", "OBX", "NTE" };
        
        foreach (var segment in segments)
        {
            var pattern = new Regex($@"{segment}\s+(?:segment|field).*?(?:description|definition)[:\s]*([^\n]*(?:\n[^\n]*)?)", RegexOptions.IgnoreCase);
            var match = pattern.Match(text);
            
            if (match.Success)
            {
                formats.Add(new MessageFormat
                {
                    Name = $"{segment} Segment",
                    Structure = segment,
                    Description = match.Groups[1].Value.Trim()
                });
            }
        }

        return formats;
    }

    private List<DataField> ExtractDataFields(string text)
    {
        var fields = new List<DataField>();
        
        var fieldPattern = new Regex(@"(\w{2,3})\s*[\.\-]\s*(\d+)\s+([^\n\(]+)(?:\s*\(([^)]+)\))?", RegexOptions.IgnoreCase);
        var matches = fieldPattern.Matches(text);
        
        foreach (Match match in matches)
        {
            var segment = match.Groups[1].Value;
            var fieldNum = match.Groups[2].Value;
            var fieldName = match.Groups[3].Value.Trim();
            var fieldType = match.Groups[4].Success ? match.Groups[4].Value.Trim() : "String";
            
            if (!string.IsNullOrEmpty(fieldName) && fieldName.Length < 100)
            {
                fields.Add(new DataField
                {
                    Name = $"{segment}.{fieldNum} {fieldName}",
                    Type = fieldType,
                    Description = fieldName
                });
            }
        }

        var dataTypePattern = new Regex(@"(?:data\s+type|field\s+type):\s*(\w+)\s*[:\-]\s*([^\n]+)", RegexOptions.IgnoreCase);
        var typeMatches = dataTypePattern.Matches(text);
        
        foreach (Match match in typeMatches)
        {
            var typeName = match.Groups[1].Value.Trim();
            var typeDesc = match.Groups[2].Value.Trim();
            
            if (!fields.Any(f => f.Type == typeName))
            {
                fields.Add(new DataField
                {
                    Name = typeName,
                    Type = "DataType",
                    Description = typeDesc
                });
            }
        }

        return fields;
    }

    private List<CommunicationDetail> ExtractCommunicationDetails(string text)
    {
        var details = new List<CommunicationDetail>();
        
        if (text.ToLower().Contains("mllp"))
        {
            details.Add(new CommunicationDetail
            {
                Type = "MLLP",
                Protocol = "TCP/IP",
                Description = "Minimal Lower Layer Protocol over TCP/IP"
            });
        }

        if (text.ToLower().Contains("http") || text.ToLower().Contains("fhir"))
        {
            details.Add(new CommunicationDetail
            {
                Type = "HTTP",
                Protocol = "REST",
                Description = "RESTful web services (typically for FHIR)"
            });
        }

        var portPattern = new Regex(@"port\s+(\d+)", RegexOptions.IgnoreCase);
        var portMatch = portPattern.Match(text);
        if (portMatch.Success)
        {
            var existingTcp = details.FirstOrDefault(d => d.Type == "MLLP");
            if (existingTcp != null)
            {
                existingTcp.Parameters["Port"] = portMatch.Groups[1].Value;
            }
        }

        return details;
    }

    private List<string> ExtractExamples(string text)
    {
        var examples = new List<string>();
        
        var hl7MsgPattern = new Regex(@"MSH\|[^\r\n]*(?:\r?\n[A-Z]{2,3}\|[^\r\n]*)*", RegexOptions.Multiline);
        var msgMatches = hl7MsgPattern.Matches(text);
        
        foreach (Match match in msgMatches)
        {
            var example = match.Value.Trim();
            if (example.Length > 50 && example.Length < 2000)
            {
                examples.Add(example);
            }
        }

        var examplePattern = new Regex(@"(?:example|sample)[:\s]*([^\n]*(?:\n[^\n]*){1,10})", RegexOptions.IgnoreCase);
        var exampleMatches = examplePattern.Matches(text);
        
        foreach (Match match in exampleMatches)
        {
            var example = match.Groups[1].Value.Trim();
            if (example.Length > 20 && example.Length < 1000 && !examples.Contains(example))
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

        var hl7Sections = new Dictionary<string, string>
        {
            { "Message", "HL7 Message Structure" },
            { "Segment", "Segment Definitions" },
            { "Field", "Field Definitions" },
            { "DataType", "Data Type Definitions" },
            { "Acknowledgment", "Acknowledgment Processing" }
        };

        foreach (var section in hl7Sections)
        {
            if (text.ToLower().Contains(section.Key.ToLower()) && !sections.ContainsValue(section.Value))
            {
                sections[section.Key] = section.Value;
            }
        }

        return sections;
    }
}