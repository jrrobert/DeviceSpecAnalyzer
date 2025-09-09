using System.ComponentModel.DataAnnotations;

namespace DeviceSpecAnalyzer.Core.Models;

public class DocumentContent
{
    public int Id { get; set; }
    
    public int DocumentId { get; set; }
    
    public Document Document { get; set; } = null!;
    
    public string ExtractedText { get; set; } = string.Empty;
    
    [MaxLength(1000)]
    public string? Summary { get; set; }
    
    [MaxLength(2000)]
    public string? Keywords { get; set; }
    
    public int WordCount { get; set; }
    
    public int PageCount { get; set; }
    
    public string? TfIdfVector { get; set; }
    
    public DateTime CreatedAt { get; set; }
}