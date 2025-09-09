namespace DeviceSpecAnalyzer.Core.Interfaces;

public interface IPdfTextExtractor
{
    Task<PdfExtractionResult> ExtractTextAsync(string filePath);
    Task<PdfExtractionResult> ExtractTextAsync(Stream pdfStream);
    bool IsPdfFile(string filePath);
    Task<bool> IsPdfValidAsync(string filePath);
}

public class PdfExtractionResult
{
    public string ExtractedText { get; set; } = string.Empty;
    public int PageCount { get; set; }
    public int WordCount { get; set; }
    public List<PageContent> Pages { get; set; } = new();
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
}

public class PageContent
{
    public int PageNumber { get; set; }
    public string Text { get; set; } = string.Empty;
    public int WordCount { get; set; }
    public List<string> Keywords { get; set; } = new();
}