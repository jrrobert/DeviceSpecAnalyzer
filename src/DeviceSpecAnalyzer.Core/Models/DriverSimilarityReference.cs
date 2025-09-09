using System.ComponentModel.DataAnnotations;

namespace DeviceSpecAnalyzer.Core.Models;

public class DriverSimilarityReference
{
    public int Id { get; set; }
    
    public int DeviceDriverId { get; set; }
    
    public DeviceDriver DeviceDriver { get; set; } = null!;
    
    public int ReferencedDriverId { get; set; }
    
    public DeviceDriver ReferencedDriver { get; set; } = null!;
    
    public double SimilarityScore { get; set; }
    
    [MaxLength(100)]
    public string ReferenceType { get; set; } = string.Empty;
    
    [MaxLength(500)]
    public string? ReferenceNotes { get; set; }
    
    public DateTime CreatedAt { get; set; }
}

public static class ReferenceTypes
{
    public const string BaseImplementation = "Base Implementation";
    public const string PartialReuse = "Partial Reuse";
    public const string MessageFormat = "Message Format";
    public const string DataParsing = "Data Parsing";
    public const string Communication = "Communication";
    public const string ErrorHandling = "Error Handling";
}