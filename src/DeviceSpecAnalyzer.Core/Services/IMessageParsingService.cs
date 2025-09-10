using DeviceSpecAnalyzer.Core.Models;

namespace DeviceSpecAnalyzer.Core.Services;

public interface IMessageParsingService
{
    Task<List<ParsedMessage>> ParseDocumentMessages(string documentText);
    Task<DocumentParsingResult> ParseDocumentMessagesAdvanced(string documentText);
    ProtocolType DetectProtocol(string messageContent);
    ParsedMessage ParseSingleMessage(string messageContent, ProtocolType? forcedProtocol = null);
    DocumentType DetectDocumentType(string documentText);
}