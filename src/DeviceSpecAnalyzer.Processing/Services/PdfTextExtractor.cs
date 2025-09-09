using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using Microsoft.Extensions.Logging;
using DeviceSpecAnalyzer.Core.Interfaces;
using System.Text;
using System.Text.RegularExpressions;

namespace DeviceSpecAnalyzer.Processing.Services;

public class PdfTextExtractor : IPdfTextExtractor
{
    private readonly ILogger<PdfTextExtractor> _logger;
    private readonly Regex _wordRegex = new(@"\b\w+\b", RegexOptions.Compiled);
    private readonly Regex _protocolKeywordsRegex = new(@"\b(?:POCT1?-?A|ASTM|HL7|TCP|IP|UDP|Serial|RS232|Ethernet|Message|Header|Field|Record|Frame|ACK|NAK|ENQ|EOT|STX|ETX|LF|CR)\b", 
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public PdfTextExtractor(ILogger<PdfTextExtractor> logger)
    {
        _logger = logger;
    }

    public async Task<PdfExtractionResult> ExtractTextAsync(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                return new PdfExtractionResult
                {
                    IsSuccess = false,
                    ErrorMessage = "File not found"
                };
            }

            using var fileStream = File.OpenRead(filePath);
            return await ExtractTextAsync(fileStream);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting text from PDF file: {FilePath}", filePath);
            return new PdfExtractionResult
            {
                IsSuccess = false,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<PdfExtractionResult> ExtractTextAsync(Stream pdfStream)
    {
        try
        {
            var result = new PdfExtractionResult();
            var allText = new StringBuilder();
            var pages = new List<PageContent>();

            await Task.Run(() =>
            {
                using var document = PdfDocument.Open(pdfStream);
                result.PageCount = document.NumberOfPages;

                foreach (var page in document.GetPages())
                {
                    var pageText = ExtractPageText(page);
                    var pageContent = new PageContent
                    {
                        PageNumber = page.Number,
                        Text = pageText,
                        WordCount = CountWords(pageText),
                        Keywords = ExtractKeywords(pageText)
                    };

                    pages.Add(pageContent);
                    allText.AppendLine(pageText);
                }

                // Extract metadata if available
                result.Metadata = ExtractMetadata(document);
            });

            result.ExtractedText = allText.ToString();
            result.WordCount = CountWords(result.ExtractedText);
            result.Pages = pages;
            result.IsSuccess = true;

            _logger.LogInformation("Successfully extracted text from PDF. Pages: {PageCount}, Words: {WordCount}", 
                result.PageCount, result.WordCount);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting text from PDF stream");
            return new PdfExtractionResult
            {
                IsSuccess = false,
                ErrorMessage = ex.Message
            };
        }
    }

    public bool IsPdfFile(string filePath)
    {
        if (!File.Exists(filePath))
            return false;

        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        if (extension != ".pdf")
            return false;

        try
        {
            using var fileStream = File.OpenRead(filePath);
            var header = new byte[4];
            var bytesRead = fileStream.Read(header, 0, 4);
            if (bytesRead != 4) return false;
            
            return header[0] == 0x25 && header[1] == 0x50 && header[2] == 0x44 && header[3] == 0x46;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> IsPdfValidAsync(string filePath)
    {
        try
        {
            if (!IsPdfFile(filePath))
                return false;

            await Task.Run(() =>
            {
                using var fileStream = File.OpenRead(filePath);
                using var document = PdfDocument.Open(fileStream);
                
                if (document.NumberOfPages == 0)
                    return false;
                    
                var firstPage = document.GetPage(1);
                return firstPage != null;
            });

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "PDF validation failed for file: {FilePath}", filePath);
            return false;
        }
    }

    private string ExtractPageText(Page page)
    {
        try
        {
            var words = page.GetWords();
            var text = string.Join(" ", words.Select(w => w.Text));
            
            return CleanupText(text);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error extracting text from page {PageNumber}", page.Number);
            return string.Empty;
        }
    }

    private string CleanupText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var cleaned = text
            .Replace("\r\n", "\n")
            .Replace("\r", "\n")
            .Replace('\t', ' ');

        var lines = cleaned.Split('\n')
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => Regex.Replace(line, @"\s+", " "));

        return string.Join("\n", lines);
    }

    private int CountWords(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0;

        return _wordRegex.Matches(text).Count;
    }

    private List<string> ExtractKeywords(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new List<string>();

        var protocolKeywords = _protocolKeywordsRegex.Matches(text)
            .Cast<Match>()
            .Select(m => m.Value.ToUpperInvariant())
            .Distinct()
            .ToList();

        var commonWords = new HashSet<string> { "THE", "AND", "OR", "BUT", "IN", "ON", "AT", "TO", "FOR", "OF", "WITH", "BY", "FROM", "THIS", "THAT", "IS", "ARE", "WAS", "WERE", "BE", "BEEN", "BEING", "HAVE", "HAS", "HAD", "DO", "DOES", "DID", "WILL", "WOULD", "COULD", "SHOULD", "MAY", "MIGHT", "CAN", "SHALL" };

        var allWords = _wordRegex.Matches(text.ToUpperInvariant())
            .Cast<Match>()
            .Select(m => m.Value)
            .Where(w => w.Length > 3 && !commonWords.Contains(w))
            .GroupBy(w => w)
            .Where(g => g.Count() > 1)
            .OrderByDescending(g => g.Count())
            .Take(20)
            .Select(g => g.Key)
            .ToList();

        return protocolKeywords.Concat(allWords).Distinct().ToList();
    }

    private Dictionary<string, object>? ExtractMetadata(PdfDocument document)
    {
        try
        {
            var metadata = new Dictionary<string, object>();

            if (document.Information != null)
            {
                var info = document.Information;
                
                if (!string.IsNullOrEmpty(info.Title))
                    metadata["Title"] = info.Title;
                
                if (!string.IsNullOrEmpty(info.Author))
                    metadata["Author"] = info.Author;
                
                if (!string.IsNullOrEmpty(info.Subject))
                    metadata["Subject"] = info.Subject;
                
                if (!string.IsNullOrEmpty(info.Creator))
                    metadata["Creator"] = info.Creator;
                
                if (!string.IsNullOrEmpty(info.Producer))
                    metadata["Producer"] = info.Producer;
                
                if (info.CreationDate != null)
                    metadata["CreationDate"] = info.CreationDate;
                
                if (info.ModifiedDate != null)
                    metadata["ModifiedDate"] = info.ModifiedDate;
            }

            metadata["PageCount"] = document.NumberOfPages;
            metadata["Version"] = "PDF";

            return metadata.Count > 0 ? metadata : null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error extracting PDF metadata");
            return null;
        }
    }
}