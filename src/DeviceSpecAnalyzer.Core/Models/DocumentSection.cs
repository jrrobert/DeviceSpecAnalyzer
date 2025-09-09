using System.ComponentModel.DataAnnotations;

namespace DeviceSpecAnalyzer.Core.Models;

public class DocumentSection
{
    public int Id { get; set; }
    
    public int DocumentId { get; set; }
    
    public Document Document { get; set; } = null!;
    
    [MaxLength(100)]
    public string SectionType { get; set; } = string.Empty;
    
    [MaxLength(200)]
    public string? Title { get; set; }
    
    public string Content { get; set; } = string.Empty;
    
    public string? ParsedData { get; set; }
    
    public int PageNumber { get; set; }
    
    public int OrderIndex { get; set; }
    
    public DateTime CreatedAt { get; set; }
}

public static class DocumentSectionTypes
{
    public const string Introduction = "Introduction";
    public const string MessageFormat = "MessageFormat";
    public const string DataFields = "DataFields";
    public const string Communication = "Communication";
    public const string ErrorHandling = "ErrorHandling";
    public const string Examples = "Examples";
    public const string Appendix = "Appendix";
    public const string Unknown = "Unknown";
}