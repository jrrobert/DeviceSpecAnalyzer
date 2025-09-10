using System.Text.RegularExpressions;
using System.Xml.Linq;
using DeviceSpecAnalyzer.Core.Models;

namespace DeviceSpecAnalyzer.Core.Services;

public class MessageParsingService : IMessageParsingService
{
    private readonly IMessageProfileService _profileService;
    private readonly Regex _xmlMessageRegex = new(@"<[A-Z]{3}\.R\d{2}[\s\S]*?</[A-Z]{3}\.R\d{2}>", RegexOptions.Multiline);
    private readonly Regex _astmMessageRegex = new(@"H\|\\\^&.*?L\|\d+\|N", RegexOptions.Multiline | RegexOptions.Singleline);

    public MessageParsingService(IMessageProfileService profileService)
    {
        _profileService = profileService;
    }

    public async Task<List<ParsedMessage>> ParseDocumentMessages(string documentText)
    {
        var messages = new List<ParsedMessage>();
        
        // Extract POCT1-A XML messages
        var xmlMatches = _xmlMessageRegex.Matches(documentText);
        foreach (Match match in xmlMatches)
        {
            var parsed = ParseSingleMessage(match.Value, ProtocolType.POCT1A);
            if (parsed.IsValid)
            {
                messages.Add(parsed);
            }
        }

        // Extract ASTM messages
        var astmMatches = _astmMessageRegex.Matches(documentText);
        foreach (Match match in astmMatches)
        {
            var parsed = ParseSingleMessage(match.Value, ProtocolType.ASTM);
            if (parsed.IsValid)
            {
                messages.Add(parsed);
            }
        }

        // Look for individual message examples in text
        await ExtractExampleMessages(documentText, messages);

        return messages.OrderBy(m => m.RawContent.Length).ToList();
    }

    public ProtocolType DetectProtocol(string messageContent)
    {
        // POCT1-A: XML format with specific message types
        if (messageContent.TrimStart().StartsWith("<?xml") || 
            Regex.IsMatch(messageContent, @"<[A-Z]{3}\.R\d{2}>"))
        {
            return ProtocolType.POCT1A;
        }

        // ASTM: Pipe-delimited format starting with H|
        if (messageContent.StartsWith("H|") && messageContent.Contains("\\^&"))
        {
            return ProtocolType.ASTM;
        }

        return ProtocolType.Unknown;
    }

    public ParsedMessage ParseSingleMessage(string messageContent, ProtocolType? forcedProtocol = null)
    {
        var protocol = forcedProtocol ?? DetectProtocol(messageContent);
        
        return protocol switch
        {
            ProtocolType.POCT1A => ParsePOCT1AMessage(messageContent),
            ProtocolType.ASTM => ParseASTMMessage(messageContent),
            _ => new ParsedMessage
            {
                RawContent = messageContent,
                Protocol = ProtocolType.Unknown,
                IsValid = false,
                Errors = new List<string> { "Unable to detect message protocol" }
            }
        };
    }

    private ParsedMessage ParsePOCT1AMessage(string xmlContent)
    {
        var message = new ParsedMessage
        {
            Protocol = ProtocolType.POCT1A,
            RawContent = xmlContent,
            Segments = new List<MessageSegment>()
        };

        try
        {
            // Clean XML content
            var cleanXml = xmlContent.Replace("<?xml version=\"1.0\" encoding=\"UTF-8\"?>", "").Trim();
            
            var doc = XDocument.Parse(cleanXml);
            var rootElement = doc.Root;
            
            if (rootElement == null)
            {
                message.Errors.Add("Invalid XML structure");
                return message;
            }

            message.MessageType = rootElement.Name.LocalName;
            
            // Get message profile from profile service
            var profile = _profileService.GetProfile(message.MessageType);
            message.Profile = profile;
            
            if (profile != null)
            {
                // Create main segment for the message type
                var mainSegment = new MessageSegment
                {
                    SegmentId = message.MessageType,
                    Name = profile.Name,
                    Description = profile.Description,
                    RawSegment = xmlContent,
                    Fields = new List<MessageField>()
                };

                // Parse XML elements to fields
                ParseXmlElements(rootElement, mainSegment.Fields, "");
                message.Segments.Add(mainSegment);
                
                // Extract key values using profile service
                message.KeyValues = _profileService.ExtractKeyValues(message);
                
                message.IsValid = true;
            }
            else
            {
                message.Errors.Add($"Unknown POCT1-A message type: {message.MessageType}");
            }
        }
        catch (Exception ex)
        {
            message.Errors.Add($"XML parsing error: {ex.Message}");
        }

        return message;
    }

    public DocumentType DetectDocumentType(string documentText)
    {
        if (string.IsNullOrWhiteSpace(documentText))
            return DocumentType.Unknown;

        var text = documentText.ToLowerInvariant();
        
        // Look for specification indicators
        var specIndicators = new[]
        {
            "specification", "protocol", "standard", "message profile", "message catalog",
            "interface control document", "icd", "communication protocol",
            "page ", "section ", "example", "table ", "figure ", "appendix",
            "specification version", "document version", "revision",
            "connectivity protocol", "interface specification", "manual", "guide",
            "implementation", "requirements", "overview", "description",
            "vendor", "manufacturer", "device", "instrument"
        };

        // Look for trace/log indicators  
        var traceIndicators = new[]
        {
            "timestamp", "trace", "log", "session", "connection established",
            "transmission", "received", "sent", "sequence number",
            "real-time", "captured", "monitoring", "data flow", "datetime",
            "logged", "recording"
        };

        int specScore = specIndicators.Count(indicator => text.Contains(indicator));
        int traceScore = traceIndicators.Count(indicator => text.Contains(indicator));
        
        // Check message pattern density (high density = trace, low density = spec)
        var xmlMatches = _xmlMessageRegex.Matches(documentText);
        var messageCount = xmlMatches.Count;
        var textLength = documentText.Length;
        var messageDensity = textLength > 0 ? (double)messageCount / textLength * 1000 : 0;
        
        // Check for message type diversity (specs have multiple different message types)
        var messageTypes = xmlMatches.Cast<Match>()
            .Select(m => Regex.Match(m.Value, @"<([A-Z]{3}\.R\d{2})").Groups[1].Value)
            .Where(type => !string.IsNullOrEmpty(type))
            .Distinct()
            .Count();
        
        // High message diversity suggests specification document
        if (messageTypes >= 5)
            specScore += 3;
        else if (messageTypes >= 3)
            specScore += 2;
        else if (messageTypes <= 2 && messageCount > 50)
            traceScore += 2;
        
        // High message density typically indicates trace logs
        if (messageDensity > 1.0)
            traceScore += 3;
        else if (messageDensity > 0.5)
            traceScore += 2;
        else if (messageDensity < 0.1)
            specScore += 2;

        // Boost specification detection
        if (specScore > traceScore && specScore > 3)
            return DocumentType.Specification;
        else if (traceScore > specScore && traceScore > 3)
            return DocumentType.TraceLog;
        else if (specScore > traceScore)
            return DocumentType.Specification; // Default to specification if there's any evidence
        else if (traceScore > specScore)
            return DocumentType.TraceLog;
        else if (specScore > 0 || traceScore > 0)
            return DocumentType.Mixed;
            
        return DocumentType.Unknown;
    }

    public async Task<DocumentParsingResult> ParseDocumentMessagesAdvanced(string documentText)
    {
        var result = new DocumentParsingResult
        {
            DocumentType = DetectDocumentType(documentText)
        };

        // Parse all messages first
        var allMessages = await ParseDocumentMessages(documentText);
        result.Messages = allMessages;
        result.TotalExamples = allMessages.Count;

        if (result.DocumentType == DocumentType.Specification)
        {
            // Group examples by message type
            var messageGroups = allMessages
                .Where(m => m.IsValid)
                .GroupBy(m => m.MessageType)
                .ToList();

            foreach (var group in messageGroups)
            {
                var messageType = group.Key;
                var examples = group.ToList();
                result.MessageTypeCounts[messageType] = examples.Count;

                // Create a representative message type entry
                var representative = examples.First();
                representative.Examples = examples.Skip(1).ToList(); // Store other examples
                representative.IsSpecificationExample = false; // This is the main entry
                
                // Mark examples
                foreach (var example in examples.Skip(1))
                {
                    example.IsSpecificationExample = true;
                }

                result.MessageTypes.Add(representative);
            }

            result.AnalysisSummary = $"Specification document with {result.MessageTypes.Count} message types and {result.TotalExamples} examples total.";
        }
        else
        {
            // For trace logs, keep original behavior
            result.MessageTypes = allMessages;
            result.AnalysisSummary = $"Trace log with {allMessages.Count} message instances.";
        }

        return result;
    }

    private void ParseXmlElements(XElement element, List<MessageField> fields, string parentPath)
    {
        foreach (var child in element.Elements())
        {
            var fieldPath = string.IsNullOrEmpty(parentPath) ? child.Name.LocalName : $"{parentPath}.{child.Name.LocalName}";
            
            if (child.HasElements)
            {
                ParseXmlElements(child, fields, fieldPath);
            }
            else
            {
                var field = new MessageField
                {
                    FieldId = fieldPath,
                    Name = FormatFieldName(child.Name.LocalName),
                    Value = child.Attribute("V")?.Value ?? child.Value,
                    Description = GetPOCT1AFieldDescription(fieldPath)
                };
                
                fields.Add(field);
            }
        }
    }

    private ParsedMessage ParseASTMMessage(string astmContent)
    {
        var message = new ParsedMessage
        {
            Protocol = ProtocolType.ASTM,
            RawContent = astmContent,
            MessageType = "ASTM Message",
            Segments = new List<MessageSegment>()
        };

        try
        {
            var lines = astmContent.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                var segmentType = line.Substring(0, 1);
                
                if (MessageDefinitions.ASTM_Segments.TryGetValue(segmentType, out var segmentInfo))
                {
                    var segment = new MessageSegment
                    {
                        SegmentId = segmentType,
                        Name = segmentInfo.Name,
                        Description = segmentInfo.Description,
                        RawSegment = line,
                        Fields = new List<MessageField>()
                    };

                    ParseASTMSegment(line, segment);
                    message.Segments.Add(segment);
                }
            }
            
            message.IsValid = message.Segments.Any();
        }
        catch (Exception ex)
        {
            message.Errors.Add($"ASTM parsing error: {ex.Message}");
        }

        return message;
    }

    private void ParseASTMSegment(string segmentLine, MessageSegment segment)
    {
        var fields = segmentLine.Split('|');
        var segmentType = segment.SegmentId;

        if (!MessageDefinitions.ASTM_Fields.TryGetValue(segmentType, out var fieldDefs))
            return;

        for (int i = 0; i < Math.Min(fields.Length, fieldDefs.Count + 1); i++)
        {
            if (i < fieldDefs.Count)
            {
                var fieldDef = fieldDefs[i];
                var value = i < fields.Length ? fields[i] : "";
                
                segment.Fields.Add(new MessageField
                {
                    FieldId = fieldDef.Id,
                    Name = fieldDef.Name,
                    Value = value,
                    Description = fieldDef.Description,
                    IsRequired = fieldDef.Required
                });
            }
        }
    }

    private async Task ExtractExampleMessages(string documentText, List<ParsedMessage> messages)
    {
        // Look for XML examples in code blocks or examples
        var xmlExamples = Regex.Matches(documentText, @"<[A-Z]{3}\.R\d{2}[\s\S]*?</[A-Z]{3}\.R\d{2}>", RegexOptions.Multiline);
        foreach (Match match in xmlExamples)
        {
            if (!messages.Any(m => m.RawContent.Trim() == match.Value.Trim()))
            {
                var parsed = ParseSingleMessage(match.Value, ProtocolType.POCT1A);
                if (parsed.IsValid)
                {
                    messages.Add(parsed);
                }
            }
        }

        // Look for ASTM examples
        var astmExamples = Regex.Matches(documentText, @"H\|[^\r\n]+", RegexOptions.Multiline);
        foreach (Match match in astmExamples)
        {
            var fullMessage = ExtractFullASTMMessage(documentText, match.Index);
            if (!string.IsNullOrEmpty(fullMessage) && !messages.Any(m => m.RawContent.Contains(fullMessage)))
            {
                var parsed = ParseSingleMessage(fullMessage, ProtocolType.ASTM);
                if (parsed.IsValid)
                {
                    messages.Add(parsed);
                }
            }
        }
    }

    private string ExtractFullASTMMessage(string text, int startIndex)
    {
        var lines = new List<string>();
        var textLines = text.Split('\n');
        
        // Find the line containing the start index
        int currentPos = 0;
        int lineIndex = 0;
        
        foreach (var line in textLines)
        {
            if (currentPos + line.Length >= startIndex)
            {
                break;
            }
            currentPos += line.Length + 1; // +1 for newline
            lineIndex++;
        }

        // Extract ASTM message from H record to L record
        bool inMessage = false;
        for (int i = lineIndex; i < textLines.Length; i++)
        {
            var line = textLines[i].Trim();
            
            if (line.StartsWith("H|"))
            {
                inMessage = true;
                lines.Add(line);
            }
            else if (inMessage && (line.StartsWith("P|") || line.StartsWith("O|") || line.StartsWith("C|") || line.StartsWith("R|")))
            {
                lines.Add(line);
            }
            else if (inMessage && line.StartsWith("L|"))
            {
                lines.Add(line);
                break;
            }
            else if (inMessage && !string.IsNullOrEmpty(line) && !line.StartsWith("Example") && !line.StartsWith("Sofia:") && !line.StartsWith("LIS:"))
            {
                // Stop if we hit non-ASTM content
                break;
            }
        }

        return lines.Any() ? string.Join("\n", lines) : "";
    }

    private string FormatFieldName(string xmlElementName)
    {
        // Convert XML element names to readable format
        var parts = xmlElementName.Split('_');
        return string.Join(" ", parts.Select(p => char.ToUpper(p[0]) + p.Substring(1).ToLower()));
    }

    private string GetPOCT1AFieldDescription(string fieldPath)
    {
        // Look up field descriptions from our definitions
        foreach (var msgFields in MessageDefinitions.POCT1A_Fields.Values)
        {
            var field = msgFields.FirstOrDefault(f => f.Id == fieldPath);
            if (field != default)
            {
                return field.Description;
            }
        }
        
        return "Field description not available";
    }
}