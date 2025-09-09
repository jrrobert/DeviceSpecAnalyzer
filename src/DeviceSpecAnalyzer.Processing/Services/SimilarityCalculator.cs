using DeviceSpecAnalyzer.Core.Interfaces;
using DeviceSpecAnalyzer.Core.Models;
using Microsoft.Extensions.Logging;

namespace DeviceSpecAnalyzer.Processing.Services;

public class SimilarityCalculator : ISimilarityCalculator
{
    private readonly ITfIdfVectorizer _tfidfVectorizer;
    private readonly ILogger<SimilarityCalculator> _logger;

    public SimilarityCalculator(ITfIdfVectorizer tfidfVectorizer, ILogger<SimilarityCalculator> logger)
    {
        _tfidfVectorizer = tfidfVectorizer;
        _logger = logger;
    }

    public async Task<double> CalculateCosineSimilarityAsync(string text1, string text2)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(text1) || string.IsNullOrWhiteSpace(text2))
                return 0.0;

            var corpus = new[] { text1, text2 };
            var vocabulary = await _tfidfVectorizer.ExtractVocabularyAsync(corpus);

            var vector1 = await _tfidfVectorizer.CreateVectorAsync(text1, vocabulary);
            var vector2 = await _tfidfVectorizer.CreateVectorAsync(text2, vocabulary);

            return CalculateCosineSimilarity(vector1, vector2);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating cosine similarity");
            return 0.0;
        }
    }

    public async Task<SimilarityResult> CompareDocumentsAsync(Document sourceDoc, Document targetDoc)
    {
        try
        {
            var result = new SimilarityResult
            {
                SourceDocumentId = sourceDoc.Id,
                TargetDocumentId = targetDoc.Id,
                ComparisonMethod = "TF-IDF Cosine Similarity",
                ComputedAt = DateTime.UtcNow
            };

            if (sourceDoc.Content == null || targetDoc.Content == null)
            {
                _logger.LogWarning("Cannot compare documents without content. Source: {SourceId}, Target: {TargetId}", 
                    sourceDoc.Id, targetDoc.Id);
                return result;
            }

            var sourceText = sourceDoc.Content.ExtractedText;
            var targetText = targetDoc.Content.ExtractedText;

            if (string.IsNullOrWhiteSpace(sourceText) || string.IsNullOrWhiteSpace(targetText))
            {
                return result;
            }

            result.OverallSimilarityScore = await CalculateCosineSimilarityAsync(sourceText, targetText);
            result.KeywordSimilarity = await CalculateKeywordSimilarity(sourceDoc.Content.Keywords, targetDoc.Content.Keywords);
            result.StructuralSimilarity = CalculateStructuralSimilarity(sourceDoc, targetDoc);
            result.SemanticSimilarity = await CalculateSemanticSimilarity(sourceText, targetText);

            result.MatchedSections = await FindMatchedSections(sourceDoc.Sections, targetDoc.Sections);
            result.Notes = GenerateComparisonNotes(sourceDoc, targetDoc, result);

            _logger.LogInformation("Document comparison completed. Source: {SourceId}, Target: {TargetId}, Similarity: {Score:F3}", 
                sourceDoc.Id, targetDoc.Id, result.OverallSimilarityScore);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error comparing documents. Source: {SourceId}, Target: {TargetId}", 
                sourceDoc.Id, targetDoc.Id);
            return new SimilarityResult
            {
                SourceDocumentId = sourceDoc.Id,
                TargetDocumentId = targetDoc.Id,
                ComparisonMethod = "TF-IDF Cosine Similarity",
                ComputedAt = DateTime.UtcNow
            };
        }
    }

    public async Task<IEnumerable<SimilarityResult>> FindSimilarDocumentsAsync(Document sourceDoc, IEnumerable<Document> candidates, double threshold = 0.1)
    {
        var results = new List<SimilarityResult>();

        if (sourceDoc.Content == null)
        {
            _logger.LogWarning("Cannot find similar documents for document without content: {DocumentId}", sourceDoc.Id);
            return results;
        }

        foreach (var candidate in candidates.Where(d => d.Id != sourceDoc.Id && d.Content != null))
        {
            try
            {
                var similarity = await CompareDocumentsAsync(sourceDoc, candidate);
                
                if (similarity.OverallSimilarityScore >= threshold)
                {
                    results.Add(similarity);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error comparing document {SourceId} with candidate {CandidateId}", 
                    sourceDoc.Id, candidate.Id);
            }
        }

        return results.OrderByDescending(r => r.OverallSimilarityScore);
    }

    public async Task<TfIdfVector> CreateTfIdfVectorAsync(string text)
    {
        var vector = await _tfidfVectorizer.CreateVectorAsync(text);
        var magnitude = CalculateMagnitude(vector);

        return new TfIdfVector
        {
            Vector = vector,
            Magnitude = magnitude,
            TermCount = vector.Count,
            Text = text.Length > 1000 ? text[..1000] + "..." : text,
            CreatedAt = DateTime.UtcNow
        };
    }

    public async Task<TfIdfVector> CreateTfIdfVectorAsync(string text, IEnumerable<string> vocabulary)
    {
        var vector = await _tfidfVectorizer.CreateVectorAsync(text, vocabulary);
        var magnitude = CalculateMagnitude(vector);

        return new TfIdfVector
        {
            Vector = vector,
            Magnitude = magnitude,
            TermCount = vector.Count(kvp => kvp.Value > 0),
            Text = text.Length > 1000 ? text[..1000] + "..." : text,
            CreatedAt = DateTime.UtcNow
        };
    }

    private double CalculateCosineSimilarity(Dictionary<string, double> vector1, Dictionary<string, double> vector2)
    {
        var dotProduct = 0.0;
        var magnitude1 = 0.0;
        var magnitude2 = 0.0;

        var allTerms = vector1.Keys.Union(vector2.Keys);

        foreach (var term in allTerms)
        {
            var value1 = vector1.GetValueOrDefault(term, 0.0);
            var value2 = vector2.GetValueOrDefault(term, 0.0);

            dotProduct += value1 * value2;
            magnitude1 += value1 * value1;
            magnitude2 += value2 * value2;
        }

        magnitude1 = Math.Sqrt(magnitude1);
        magnitude2 = Math.Sqrt(magnitude2);

        if (magnitude1 == 0.0 || magnitude2 == 0.0)
            return 0.0;

        return dotProduct / (magnitude1 * magnitude2);
    }

    private double CalculateMagnitude(Dictionary<string, double> vector)
    {
        var sumOfSquares = vector.Values.Sum(v => v * v);
        return Math.Sqrt(sumOfSquares);
    }

    private Task<double> CalculateKeywordSimilarity(string? keywords1, string? keywords2)
    {
        if (string.IsNullOrWhiteSpace(keywords1) || string.IsNullOrWhiteSpace(keywords2))
            return Task.FromResult(0.0);

        var set1 = keywords1.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(k => k.Trim().ToLowerInvariant())
            .ToHashSet();

        var set2 = keywords2.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(k => k.Trim().ToLowerInvariant())
            .ToHashSet();

        var intersection = set1.Intersect(set2).Count();
        var union = set1.Union(set2).Count();

        return Task.FromResult(union == 0 ? 0.0 : (double)intersection / union);
    }

    private double CalculateStructuralSimilarity(Document doc1, Document doc2)
    {
        var protocolMatch = string.Equals(doc1.Protocol, doc2.Protocol, StringComparison.OrdinalIgnoreCase) ? 1.0 : 0.0;
        var manufacturerMatch = string.Equals(doc1.Manufacturer, doc2.Manufacturer, StringComparison.OrdinalIgnoreCase) ? 1.0 : 0.0;
        
        var pageDifference = doc1.Content != null && doc2.Content != null 
            ? Math.Abs(doc1.Content.PageCount - doc2.Content.PageCount) 
            : 0;
        var pagesSimilarity = pageDifference <= 5 ? 1.0 - (pageDifference / 20.0) : 0.5;

        return (protocolMatch * 0.5) + (manufacturerMatch * 0.3) + (pagesSimilarity * 0.2);
    }

    private Task<double> CalculateSemanticSimilarity(string text1, string text2)
    {
        var commonTerms = new[] { "message", "field", "record", "data", "protocol", "communication", "device", "interface" };
        
        var text1Lower = text1.ToLowerInvariant();
        var text2Lower = text2.ToLowerInvariant();
        
        var matches = commonTerms.Count(term => text1Lower.Contains(term) && text2Lower.Contains(term));
        
        return Task.FromResult((double)matches / commonTerms.Length);
    }

    private Task<string?> FindMatchedSections(ICollection<DocumentSection> sections1, ICollection<DocumentSection> sections2)
    {
        if (!sections1.Any() || !sections2.Any())
            return Task.FromResult<string?>(null);

        var matches = new List<string>();

        var sectionTypes1 = sections1.Select(s => s.SectionType).Distinct().ToHashSet();
        var sectionTypes2 = sections2.Select(s => s.SectionType).Distinct().ToHashSet();

        var commonTypes = sectionTypes1.Intersect(sectionTypes2);

        foreach (var type in commonTypes)
        {
            matches.Add(type);
        }

        return Task.FromResult(matches.Any() ? string.Join(", ", matches) : null);
    }

    private string GenerateComparisonNotes(Document sourceDoc, Document targetDoc, SimilarityResult result)
    {
        var notes = new List<string>();

        if (result.OverallSimilarityScore > 0.8)
            notes.Add("Very high similarity - documents are very similar");
        else if (result.OverallSimilarityScore > 0.6)
            notes.Add("High similarity - documents share many common features");
        else if (result.OverallSimilarityScore > 0.3)
            notes.Add("Moderate similarity - documents have some common features");
        else
            notes.Add("Low similarity - documents are quite different");

        if (string.Equals(sourceDoc.Protocol, targetDoc.Protocol, StringComparison.OrdinalIgnoreCase))
            notes.Add($"Same protocol: {sourceDoc.Protocol}");

        if (string.Equals(sourceDoc.Manufacturer, targetDoc.Manufacturer, StringComparison.OrdinalIgnoreCase))
            notes.Add($"Same manufacturer: {sourceDoc.Manufacturer}");

        if (result.KeywordSimilarity > 0.5)
            notes.Add("High keyword overlap");

        return string.Join("; ", notes);
    }
}