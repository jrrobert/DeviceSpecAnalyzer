using DeviceSpecAnalyzer.Core.Models;

namespace DeviceSpecAnalyzer.Core.Interfaces;

public interface IProtocolParser
{
    string ProtocolName { get; }
    bool CanParse(string text);
    Task<ProtocolParseResult> ParseAsync(string text);
    Task<IEnumerable<DocumentSection>> ExtractSectionsAsync(string text, int documentId);
}

public class ProtocolParseResult
{
    public string Protocol { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public List<MessageFormat> MessageFormats { get; set; } = new();
    public List<DataField> DataFields { get; set; } = new();
    public List<CommunicationDetail> CommunicationDetails { get; set; } = new();
    public List<string> Examples { get; set; } = new();
    public Dictionary<string, string> KeySections { get; set; } = new();
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
}

public class MessageFormat
{
    public string Name { get; set; } = string.Empty;
    public string Structure { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> Fields { get; set; } = new();
    public string? Example { get; set; }
}

public class DataField
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsRequired { get; set; }
    public string? DefaultValue { get; set; }
    public string? Format { get; set; }
    public int? MaxLength { get; set; }
}

public class CommunicationDetail
{
    public string Type { get; set; } = string.Empty;
    public string Protocol { get; set; } = string.Empty;
    public Dictionary<string, string> Parameters { get; set; } = new();
    public string Description { get; set; } = string.Empty;
}