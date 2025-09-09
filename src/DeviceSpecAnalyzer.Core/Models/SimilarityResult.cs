using System.ComponentModel.DataAnnotations;

namespace DeviceSpecAnalyzer.Core.Models;

public class SimilarityResult
{
    public int Id { get; set; }
    
    public int SourceDocumentId { get; set; }
    
    public Document SourceDocument { get; set; } = null!;
    
    public int TargetDocumentId { get; set; }
    
    public Document TargetDocument { get; set; } = null!;
    
    public double OverallSimilarityScore { get; set; }
    
    public double KeywordSimilarity { get; set; }
    
    public double StructuralSimilarity { get; set; }
    
    public double SemanticSimilarity { get; set; }
    
    [MaxLength(50)]
    public string ComparisonMethod { get; set; } = string.Empty;
    
    public string? MatchedSections { get; set; }
    
    public string? DifferentSections { get; set; }
    
    [MaxLength(1000)]
    public string? Notes { get; set; }
    
    public DateTime ComputedAt { get; set; }
    
    public ICollection<SectionSimilarity> SectionSimilarities { get; set; } = new List<SectionSimilarity>();
}