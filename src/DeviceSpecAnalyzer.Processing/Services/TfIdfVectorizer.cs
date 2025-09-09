using DeviceSpecAnalyzer.Core.Interfaces;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace DeviceSpecAnalyzer.Processing.Services;

public class TfIdfVectorizer : ITfIdfVectorizer
{
    private readonly ILogger<TfIdfVectorizer> _logger;
    private readonly Regex _wordRegex;
    private readonly HashSet<string> _stopWords;
    private readonly HashSet<string> _technicalTerms;

    public TfIdfVectorizer(ILogger<TfIdfVectorizer> logger)
    {
        _logger = logger;
        _wordRegex = new Regex(@"\b[a-zA-Z][a-zA-Z0-9]*\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        
        _stopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "the", "and", "or", "but", "in", "on", "at", "to", "for", "of", "with", "by", "from",
            "this", "that", "these", "those", "is", "are", "was", "were", "be", "been", "being",
            "have", "has", "had", "do", "does", "did", "will", "would", "could", "should", "may",
            "might", "can", "shall", "must", "a", "an", "as", "if", "then", "than", "when", "where",
            "while", "how", "what", "which", "who", "whom", "whose", "why", "so", "up", "out", "off",
            "over", "under", "again", "further", "then", "once"
        };

        _technicalTerms = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "POCT1A", "ASTM", "HL7", "TCP", "IP", "UDP", "Serial", "RS232", "RS485", "Ethernet", "USB",
            "Message", "Header", "Field", "Record", "Frame", "ACK", "NAK", "ENQ", "EOT", "STX", "ETX",
            "LF", "CR", "CRLF", "ASCII", "Unicode", "UTF8", "XML", "JSON", "CSV", "Protocol",
            "Communication", "Interface", "Device", "Analyzer", "Laboratory", "Patient", "Result",
            "Order", "Observation", "Segment", "Component", "Value", "Units", "Reference", "Range",
            "Normal", "Abnormal", "Critical", "Panic", "High", "Low", "Positive", "Negative",
            "Calibration", "Quality", "Control", "QC", "Error", "Status", "Code", "ID", "Name",
            "Date", "Time", "Timestamp", "Version", "Manufacturer", "Model", "Software", "Hardware"
        };
    }

    public async Task<Dictionary<string, double>> CreateVectorAsync(string text)
    {
        return await Task.Run(() =>
        {
            var termFrequencies = GetTermFrequency(text);
            var vector = new Dictionary<string, double>();

            var totalTerms = termFrequencies.Values.Sum();
            
            foreach (var term in termFrequencies)
            {
                var tf = (double)term.Value / totalTerms;
                vector[term.Key] = tf;
            }

            return vector;
        });
    }

    public async Task<Dictionary<string, double>> CreateVectorAsync(string text, IEnumerable<string> vocabulary)
    {
        return await Task.Run(() =>
        {
            var termFrequencies = GetTermFrequency(text);
            var vector = new Dictionary<string, double>();
            var totalTerms = termFrequencies.Values.Sum();

            foreach (var term in vocabulary)
            {
                if (termFrequencies.TryGetValue(term, out var frequency))
                {
                    var tf = (double)frequency / totalTerms;
                    vector[term] = tf;
                }
                else
                {
                    vector[term] = 0.0;
                }
            }

            return vector;
        });
    }

    public async Task<IEnumerable<string>> ExtractVocabularyAsync(IEnumerable<string> documents)
    {
        return await Task.Run(() =>
        {
            var vocabulary = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var termDocumentFrequency = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var document in documents)
            {
                var terms = ExtractTerms(document).Distinct(StringComparer.OrdinalIgnoreCase);
                
                foreach (var term in terms)
                {
                    vocabulary.Add(term);
                    termDocumentFrequency.TryGetValue(term, out var freq);
                    termDocumentFrequency[term] = freq + 1;
                }
            }

            var documentCount = documents.Count();
            
            return vocabulary
                .Where(term => 
                {
                    var docFreq = termDocumentFrequency[term];
                    var docFreqRatio = (double)docFreq / documentCount;
                    
                    return _technicalTerms.Contains(term) || 
                           (docFreqRatio >= 0.05 && docFreqRatio <= 0.8 && term.Length >= 3);
                })
                .OrderBy(term => term)
                .ToList();
        });
    }

    public async Task<Dictionary<string, int>> GetTermFrequencyAsync(string text)
    {
        return await Task.Run(() => GetTermFrequency(text));
    }

    public async Task<Dictionary<string, double>> CalculateTfIdfAsync(string text, IEnumerable<string> corpus)
    {
        return await Task.Run(() =>
        {
            var allDocuments = corpus.Concat(new[] { text }).ToList();
            var documentCount = allDocuments.Count;
            
            var termFrequencies = GetTermFrequency(text);
            var totalTermsInDoc = termFrequencies.Values.Sum();
            
            var documentFrequencies = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            
            foreach (var document in allDocuments)
            {
                var uniqueTerms = ExtractTerms(document).Distinct(StringComparer.OrdinalIgnoreCase);
                foreach (var term in uniqueTerms)
                {
                    documentFrequencies.TryGetValue(term, out var freq);
                    documentFrequencies[term] = freq + 1;
                }
            }

            var tfidfVector = new Dictionary<string, double>();

            foreach (var term in termFrequencies.Keys)
            {
                var tf = (double)termFrequencies[term] / totalTermsInDoc;
                var df = documentFrequencies.GetValueOrDefault(term, 1);
                var idf = Math.Log((double)documentCount / df);
                var tfidf = tf * idf;

                if (tfidf > 0)
                {
                    tfidfVector[term] = tfidf;
                }
            }

            return tfidfVector;
        });
    }

    private Dictionary<string, int> GetTermFrequency(string text)
    {
        var termFrequencies = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        
        if (string.IsNullOrWhiteSpace(text))
            return termFrequencies;

        var terms = ExtractTerms(text);
        
        foreach (var term in terms)
        {
            termFrequencies.TryGetValue(term, out var frequency);
            termFrequencies[term] = frequency + 1;
        }

        return termFrequencies;
    }

    private IEnumerable<string> ExtractTerms(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return Enumerable.Empty<string>();

        var normalizedText = text.ToLowerInvariant();
        var matches = _wordRegex.Matches(normalizedText);
        
        return matches
            .Cast<Match>()
            .Select(m => m.Value)
            .Where(term => 
                term.Length >= 2 && 
                !_stopWords.Contains(term) &&
                !IsNumeric(term))
            .Select(term => 
            {
                if (_technicalTerms.Contains(term))
                    return _technicalTerms.First(t => t.Equals(term, StringComparison.OrdinalIgnoreCase));
                return term;
            });
    }

    private static bool IsNumeric(string term)
    {
        return term.All(char.IsDigit);
    }
}