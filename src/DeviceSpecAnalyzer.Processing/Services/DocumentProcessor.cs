using DeviceSpecAnalyzer.Core.Interfaces;
using DeviceSpecAnalyzer.Core.Models;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Diagnostics;

namespace DeviceSpecAnalyzer.Processing.Services;

public class DocumentProcessor : IDocumentProcessor
{
    private readonly IPdfTextExtractor _pdfExtractor;
    private readonly IEnumerable<IProtocolParser> _protocolParsers;
    private readonly IDocumentRepository _documentRepository;
    private readonly ISimilarityCalculator _similarityCalculator;
    private readonly ILogger<DocumentProcessor> _logger;

    public DocumentProcessor(
        IPdfTextExtractor pdfExtractor,
        IEnumerable<IProtocolParser> protocolParsers,
        IDocumentRepository documentRepository,
        ISimilarityCalculator similarityCalculator,
        ILogger<DocumentProcessor> logger)
    {
        _pdfExtractor = pdfExtractor;
        _protocolParsers = protocolParsers;
        _documentRepository = documentRepository;
        _similarityCalculator = similarityCalculator;
        _logger = logger;
    }

    public async Task<bool> ProcessNewDocumentAsync(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                _logger.LogWarning("File not found: {FilePath}", filePath);
                return false;
            }

            var fileName = Path.GetFileName(filePath);
            
            if (await _documentRepository.ExistsAsync(fileName))
            {
                _logger.LogInformation("Document already exists, skipping: {FileName}", fileName);
                return false;
            }

            var fileHash = await CalculateFileHashAsync(filePath);
            
            if (await _documentRepository.HashExistsAsync(fileHash))
            {
                _logger.LogInformation("Document with same content already exists, skipping: {FileName}", fileName);
                return false;
            }

            var result = await ProcessDocumentAsync(filePath);
            
            if (result.IsSuccess)
            {
                _logger.LogInformation("Successfully processed new document: {FileName} (ID: {DocumentId})", 
                    fileName, result.DocumentId);
                return true;
            }
            else
            {
                _logger.LogError("Failed to process new document: {FileName}. Error: {Error}", 
                    fileName, result.ErrorMessage);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing new document: {FilePath}", filePath);
            return false;
        }
    }

    public async Task<bool> ProcessDocumentUpdateAsync(string filePath)
    {
        try
        {
            var fileName = Path.GetFileName(filePath);
            var existingDoc = await _documentRepository.GetByFileNameAsync(fileName);
            
            if (existingDoc == null)
            {
                _logger.LogInformation("Document not found in database, treating as new: {FileName}", fileName);
                return await ProcessNewDocumentAsync(filePath);
            }

            var newHash = await CalculateFileHashAsync(filePath);
            
            if (existingDoc.FileHash == newHash)
            {
                _logger.LogDebug("Document unchanged, skipping: {FileName}", fileName);
                return true;
            }

            existingDoc.Status = DocumentStatus.Processing;
            await _documentRepository.UpdateAsync(existingDoc);

            var result = await ProcessDocumentAsync(filePath);
            
            if (result.IsSuccess && result.DocumentId.HasValue)
            {
                _logger.LogInformation("Successfully updated document: {FileName} (ID: {DocumentId})", 
                    fileName, result.DocumentId);
                return true;
            }
            else
            {
                existingDoc.Status = DocumentStatus.Failed;
                existingDoc.ProcessingError = result.ErrorMessage;
                await _documentRepository.UpdateAsync(existingDoc);
                
                _logger.LogError("Failed to update document: {FileName}. Error: {Error}", 
                    fileName, result.ErrorMessage);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing document update: {FilePath}", filePath);
            return false;
        }
    }

    public async Task<bool> ProcessDocumentDeletionAsync(string filePath)
    {
        try
        {
            var fileName = Path.GetFileName(filePath);
            var existingDoc = await _documentRepository.GetByFileNameAsync(fileName);
            
            if (existingDoc != null)
            {
                await _documentRepository.DeleteAsync(existingDoc.Id);
                _logger.LogInformation("Deleted document from database: {FileName} (ID: {DocumentId})", 
                    fileName, existingDoc.Id);
                return true;
            }
            else
            {
                _logger.LogWarning("Document not found in database for deletion: {FileName}", fileName);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing document deletion: {FilePath}", filePath);
            return false;
        }
    }

    public async Task<ProcessingResult> ProcessDocumentAsync(string filePath)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new ProcessingResult
        {
            ProcessedAt = DateTime.UtcNow,
            ProcessedFilePath = filePath
        };

        try
        {
            _logger.LogInformation("Starting document processing: {FilePath}", filePath);

            if (!_pdfExtractor.IsPdfFile(filePath))
            {
                result.ErrorMessage = "File is not a valid PDF";
                return result;
            }

            if (!await _pdfExtractor.IsPdfValidAsync(filePath))
            {
                result.ErrorMessage = "PDF file is corrupted or invalid";
                return result;
            }

            var fileInfo = new FileInfo(filePath);
            var fileName = fileInfo.Name;
            var fileHash = await CalculateFileHashAsync(filePath);

            var existingDoc = await _documentRepository.GetByFileNameAsync(fileName);
            Document document;

            if (existingDoc == null)
            {
                document = new Document
                {
                    FileName = fileName,
                    OriginalFileName = fileName,
                    FileSizeBytes = fileInfo.Length,
                    FileHash = fileHash,
                    Status = DocumentStatus.Processing
                };

                document = await _documentRepository.AddAsync(document);
                _logger.LogDebug("Created new document record: {DocumentId}", document.Id);
            }
            else
            {
                document = existingDoc;
                document.FileSizeBytes = fileInfo.Length;
                document.FileHash = fileHash;
                document.Status = DocumentStatus.Processing;
                document.ProcessingError = null;
                await _documentRepository.UpdateAsync(document);
                _logger.LogDebug("Updated existing document record: {DocumentId}", document.Id);
            }

            var extractionResult = await _pdfExtractor.ExtractTextAsync(filePath);
            
            if (!extractionResult.IsSuccess)
            {
                document.Status = DocumentStatus.Failed;
                document.ProcessingError = extractionResult.ErrorMessage;
                await _documentRepository.UpdateAsync(document);
                
                result.ErrorMessage = extractionResult.ErrorMessage;
                return result;
            }

            document.Content = new DocumentContent
            {
                DocumentId = document.Id,
                ExtractedText = extractionResult.ExtractedText,
                WordCount = extractionResult.WordCount,
                PageCount = extractionResult.PageCount,
                CreatedAt = DateTime.UtcNow
            };

            var protocolInfo = await DetermineProtocolAsync(extractionResult.ExtractedText);
            document.Protocol = protocolInfo.Protocol;
            document.Manufacturer = ExtractManufacturer(extractionResult.ExtractedText);
            document.DeviceName = ExtractDeviceName(extractionResult.ExtractedText);
            document.Version = protocolInfo.Version;

            var protocolParser = _protocolParsers.FirstOrDefault(p => p.CanParse(extractionResult.ExtractedText));
            
            if (protocolParser != null)
            {
                _logger.LogDebug("Using protocol parser: {ParserName}", protocolParser.ProtocolName);
                
                var parseResult = await protocolParser.ParseAsync(extractionResult.ExtractedText);
                
                if (parseResult.IsSuccess)
                {
                    var keywords = parseResult.MessageFormats
                        .SelectMany(m => new[] { m.Name, m.Structure })
                        .Concat(parseResult.DataFields.Select(f => f.Name))
                        .Where(k => !string.IsNullOrEmpty(k))
                        .Distinct()
                        .Take(50);
                    
                    document.Content.Keywords = string.Join(", ", keywords);
                    document.Content.Summary = GenerateSummary(parseResult);
                }

                var sections = await protocolParser.ExtractSectionsAsync(extractionResult.ExtractedText, document.Id);
                document.Sections = sections.ToList();
            }
            else
            {
                document.Content.Keywords = string.Join(", ", extractionResult.Pages
                    .SelectMany(p => p.Keywords)
                    .Distinct()
                    .Take(50));
            }

            document.Status = DocumentStatus.Processed;
            document.ProcessedAt = DateTime.UtcNow;
            await _documentRepository.UpdateAsync(document);

            await PerformSimilarityAnalysisAsync(document);

            result.IsSuccess = true;
            result.DocumentId = document.Id;
            result.Metadata = new Dictionary<string, object>
            {
                { "PageCount", extractionResult.PageCount },
                { "WordCount", extractionResult.WordCount },
                { "Protocol", document.Protocol ?? "Unknown" },
                { "SectionCount", document.Sections.Count }
            };

            _logger.LogInformation("Successfully processed document: {FileName} (ID: {DocumentId}, Protocol: {Protocol})", 
                fileName, document.Id, document.Protocol);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing document: {FilePath}", filePath);
            result.ErrorMessage = ex.Message;
        }
        finally
        {
            stopwatch.Stop();
            result.ProcessingDuration = stopwatch.Elapsed;
        }

        return result;
    }

    private Task<(string Protocol, string Version)> DetermineProtocolAsync(string text)
    {
        var textLower = text.ToLowerInvariant();

        if (textLower.Contains("poct1") || textLower.Contains("poct-1") || textLower.Contains("point of care"))
            return Task.FromResult(("POCT1-A", ExtractVersion(text, @"poct1?-?a\s+(?:version\s+)?(\d+\.?\d*)")));

        if (textLower.Contains("astm") || textLower.Contains("e1381") || textLower.Contains("e1394"))
            return Task.FromResult(("ASTM", ExtractVersion(text, @"(?:astm\s+)?(?:e\d+-?\d*|\d+\.\d+)")));

        if (textLower.Contains("hl7") || textLower.Contains("health level") || textLower.Contains("fhir"))
            return Task.FromResult(("HL7", ExtractVersion(text, @"hl7\s+(?:version\s+)?(\d+\.?\d*)")));

        return Task.FromResult(("Unknown", "Unknown"));
    }

    private string ExtractVersion(string text, string pattern)
    {
        var match = System.Text.RegularExpressions.Regex.Match(text, pattern, 
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value : "Unknown";
    }

    private string? ExtractManufacturer(string text)
    {
        var manufacturers = new[] { "Abbott", "Siemens", "Roche", "Beckman", "Ortho", "Alere", "Quidel", "Nova", "Radiometer", "Instrumentation Laboratory" };
        
        var textLower = text.ToLowerInvariant();
        var manufacturer = manufacturers.FirstOrDefault(m => textLower.Contains(m.ToLowerInvariant()));
        
        return manufacturer;
    }

    private string? ExtractDeviceName(string text)
    {
        var devices = new[] { "Afinion", "i-STAT", "ID Now", "BNP", "Stratus", "Triage", "Piccolo", "EPOC", "ABL", "GEM" };
        
        var textLower = text.ToLowerInvariant();
        var device = devices.FirstOrDefault(d => textLower.Contains(d.ToLowerInvariant()));
        
        return device;
    }

    private string GenerateSummary(ProtocolParseResult parseResult)
    {
        var summary = $"{parseResult.Protocol} specification";
        
        if (!string.IsNullOrEmpty(parseResult.Version))
            summary += $" version {parseResult.Version}";
            
        if (parseResult.MessageFormats.Any())
            summary += $" with {parseResult.MessageFormats.Count} message formats";
            
        if (parseResult.DataFields.Any())
            summary += $" and {parseResult.DataFields.Count} data fields";
            
        return summary + ".";
    }

    private async Task PerformSimilarityAnalysisAsync(Document document)
    {
        try
        {
            var existingDocuments = await _documentRepository.GetAllAsync();
            var candidatesForComparison = existingDocuments
                .Where(d => d.Id != document.Id && d.Status == DocumentStatus.Processed && d.Content != null)
                .ToList();

            if (!candidatesForComparison.Any())
            {
                _logger.LogDebug("No existing documents to compare against for document {DocumentId}", document.Id);
                return;
            }

            var similarDocuments = await _similarityCalculator.FindSimilarDocumentsAsync(document, candidatesForComparison, 0.1);
            
            _logger.LogInformation("Found {Count} similar documents for document {DocumentId}", 
                similarDocuments.Count(), document.Id);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error performing similarity analysis for document {DocumentId}", document.Id);
        }
    }

    private async Task<string> CalculateFileHashAsync(string filePath)
    {
        using var fileStream = File.OpenRead(filePath);
        using var sha256 = SHA256.Create();
        var hashBytes = await Task.Run(() => sha256.ComputeHash(fileStream));
        return Convert.ToHexString(hashBytes);
    }
}