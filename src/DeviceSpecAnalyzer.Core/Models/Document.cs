using System.ComponentModel.DataAnnotations;

namespace DeviceSpecAnalyzer.Core.Models;

public class Document
{
    public int Id { get; set; }
    
    [Required]
    [MaxLength(255)]
    public string FileName { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(255)]
    public string OriginalFileName { get; set; } = string.Empty;
    
    [MaxLength(100)]
    public string? Manufacturer { get; set; }
    
    [MaxLength(100)]
    public string? DeviceName { get; set; }
    
    [MaxLength(50)]
    public string? Protocol { get; set; }
    
    [MaxLength(50)]
    public string? Version { get; set; }
    
    public long FileSizeBytes { get; set; }
    
    [MaxLength(32)]
    public string? FileHash { get; set; }
    
    public DateTime UploadedAt { get; set; }
    
    public DateTime? ProcessedAt { get; set; }
    
    public DocumentStatus Status { get; set; } = DocumentStatus.Uploaded;
    
    [MaxLength(500)]
    public string? ProcessingError { get; set; }
    
    public DocumentContent? Content { get; set; }
    
    public ICollection<DocumentSection> Sections { get; set; } = new List<DocumentSection>();
    
    public ICollection<SimilarityResult> SourceSimilarities { get; set; } = new List<SimilarityResult>();
    
    public ICollection<SimilarityResult> TargetSimilarities { get; set; } = new List<SimilarityResult>();
    
    public ICollection<DeviceDriver> DeviceDrivers { get; set; } = new List<DeviceDriver>();
}

public enum DocumentStatus
{
    Uploaded,
    Processing,
    Processed,
    Failed
}

public enum ProtocolType
{
    Unknown,
    POCT1A,
    ASTM,
    HL7
}