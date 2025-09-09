using System.ComponentModel.DataAnnotations;

namespace DeviceSpecAnalyzer.Core.Models;

public class DeviceDriver
{
    public int Id { get; set; }
    
    public int DocumentId { get; set; }
    
    public Document Document { get; set; } = null!;
    
    [Required]
    [MaxLength(100)]
    public string DriverName { get; set; } = string.Empty;
    
    [MaxLength(50)]
    public string? Version { get; set; }
    
    [MaxLength(500)]
    public string? Description { get; set; }
    
    [MaxLength(100)]
    public string? ImplementationStatus { get; set; }
    
    public string? ImplementationNotes { get; set; }
    
    [MaxLength(200)]
    public string? CodeRepository { get; set; }
    
    [MaxLength(100)]
    public string? DeveloperName { get; set; }
    
    public DateTime? DevelopmentStarted { get; set; }
    
    public DateTime? DevelopmentCompleted { get; set; }
    
    public DateTime CreatedAt { get; set; }
    
    public DateTime? UpdatedAt { get; set; }
    
    public ICollection<DriverSimilarityReference> SimilarityReferences { get; set; } = new List<DriverSimilarityReference>();
}

public static class ImplementationStatus
{
    public const string NotStarted = "Not Started";
    public const string InProgress = "In Progress";
    public const string Testing = "Testing";
    public const string Completed = "Completed";
    public const string Deprecated = "Deprecated";
}