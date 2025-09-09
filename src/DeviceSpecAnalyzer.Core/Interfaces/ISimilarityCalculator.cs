using DeviceSpecAnalyzer.Core.Models;

namespace DeviceSpecAnalyzer.Core.Interfaces;

public interface ISimilarityCalculator
{
    Task<double> CalculateCosineSimilarityAsync(string text1, string text2);
    Task<SimilarityResult> CompareDocumentsAsync(Document sourceDoc, Document targetDoc);
    Task<IEnumerable<SimilarityResult>> FindSimilarDocumentsAsync(Document sourceDoc, IEnumerable<Document> candidates, double threshold = 0.1);
    Task<TfIdfVector> CreateTfIdfVectorAsync(string text);
    Task<TfIdfVector> CreateTfIdfVectorAsync(string text, IEnumerable<string> vocabulary);
}

public interface ITfIdfVectorizer
{
    Task<Dictionary<string, double>> CreateVectorAsync(string text);
    Task<Dictionary<string, double>> CreateVectorAsync(string text, IEnumerable<string> vocabulary);
    Task<IEnumerable<string>> ExtractVocabularyAsync(IEnumerable<string> documents);
    Task<Dictionary<string, int>> GetTermFrequencyAsync(string text);
    Task<Dictionary<string, double>> CalculateTfIdfAsync(string text, IEnumerable<string> corpus);
}

public class TfIdfVector
{
    public Dictionary<string, double> Vector { get; set; } = new();
    public double Magnitude { get; set; }
    public int TermCount { get; set; }
    public string Text { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class DocumentSimilarityContext
{
    public Document SourceDocument { get; set; } = null!;
    public Document TargetDocument { get; set; } = null!;
    public string ComparisonMethod { get; set; } = string.Empty;
    public Dictionary<string, object>? AdditionalMetadata { get; set; }
}