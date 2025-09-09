using System.ComponentModel.DataAnnotations;

namespace DeviceSpecAnalyzer.Core.Models;

public class SectionSimilarity
{
    public int Id { get; set; }
    
    public int SimilarityResultId { get; set; }
    
    public SimilarityResult SimilarityResult { get; set; } = null!;
    
    public int SourceSectionId { get; set; }
    
    public DocumentSection SourceSection { get; set; } = null!;
    
    public int TargetSectionId { get; set; }
    
    public DocumentSection TargetSection { get; set; } = null!;
    
    public double SimilarityScore { get; set; }
    
    [MaxLength(100)]
    public string MatchType { get; set; } = string.Empty;
    
    [MaxLength(500)]
    public string? MatchDetails { get; set; }
}