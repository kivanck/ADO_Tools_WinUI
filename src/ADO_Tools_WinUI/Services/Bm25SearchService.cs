using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using ADO_Tools_WinUI.Models;

namespace ADO_Tools_WinUI.Services
{
    /// <summary>
    /// BM25 (Okapi BM25) keyword search over cached work item text.
    /// Automatically down-weights corpus-common terms like "Rail" or "Designer"
    /// via inverse document frequency.
    /// </summary>
    public sealed class Bm25SearchService
    {
        private const double K1 = 1.2;
        private const double B = 0.75;

        private readonly List<DocEntry> _docs = new();
        private readonly Dictionary<string, int> _docFreq = new();
        private double _avgDocLength;
        private int _totalDocs;

        private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
        {
            "a", "an", "and", "are", "as", "at", "be", "but", "by", "for",
            "from", "has", "have", "he", "in", "is", "it", "its", "of", "on",
            "or", "not", "that", "the", "to", "was", "were", "will", "with",
            "this", "they", "then", "than", "when", "which", "who", "how",
            "use", "using", "used", "can", "does", "do", "did", "been",
            "should", "would", "could", "may", "might", "shall", "about",
            "into", "through", "during", "before", "after", "above", "below",
            "between", "out", "off", "over", "under", "again", "further",
            "also", "just", "so", "no", "yes", "all", "each", "every",
            "both", "few", "more", "most", "other", "some", "such", "only",
            "own", "same", "too", "very", "any", "if", "up", "what", "while"
        };

        public int DocumentCount => _totalDocs;

        /// <summary>
        /// Returns all unique terms in the index vocabulary (stop words already excluded).
        /// Used for auto-suggest in the search box.
        /// </summary>
        public IReadOnlyCollection<string> GetVocabulary() => _docFreq.Keys;

        /// <summary>
        /// Build the BM25 index from cache entries.
        /// Uses SearchableText if available, otherwise falls back to Title.
        /// </summary>
        public void BuildIndex(List<EmbeddingCacheEntry> entries)
        {
            _docs.Clear();
            _docFreq.Clear();

            foreach (var entry in entries)
            {
                string text = !string.IsNullOrWhiteSpace(entry.SearchableText)
                    ? entry.SearchableText
                    : entry.Title;

                var tokens = Tokenize(text);
                var termFreq = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                foreach (var token in tokens)
                {
                    termFreq.TryGetValue(token, out int count);
                    termFreq[token] = count + 1;
                }

                _docs.Add(new DocEntry
                {
                    CacheEntry = entry,
                    TermFrequencies = termFreq,
                    Length = tokens.Count
                });

                foreach (var term in termFreq.Keys)
                {
                    _docFreq.TryGetValue(term, out int df);
                    _docFreq[term] = df + 1;
                }
            }

            _totalDocs = _docs.Count;
            _avgDocLength = _totalDocs > 0 ? _docs.Average(d => d.Length) : 1.0;
        }

        /// <summary>
        /// Search the index and return scored results.
        /// Supports quoted phrases — e.g. "open dialog" must appear literally.
        /// </summary>
        public List<Bm25SearchResult> Search(string query, int topN = 20, bool excludeDone = false)
        {
            if (_totalDocs == 0 || string.IsNullOrWhiteSpace(query))
                return new List<Bm25SearchResult>();

            // Extract quoted phrases for mandatory filtering
            var requiredPhrases = ExtractQuotedPhrases(query);

            // Tokenize the full query (minus quote characters) so all terms contribute to scoring
            var queryTerms = Tokenize(query.Replace("\"", ""));
            if (queryTerms.Count == 0)
                return new List<Bm25SearchResult>();

            var results = new List<Bm25SearchResult>();

            foreach (var doc in _docs)
            {
                if (excludeDone)
                {
                    var state = doc.CacheEntry.State;
                    if (string.Equals(state, "Done", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(state, "Closed", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(state, "Removed", StringComparison.OrdinalIgnoreCase))
                        continue;
                }

                // Skip items that don't contain all quoted phrases
                if (!MatchesRequiredPhrases(doc.CacheEntry, requiredPhrases))
                    continue;

                double score = 0;
                foreach (var term in queryTerms)
                {
                    if (!doc.TermFrequencies.TryGetValue(term, out int tf))
                        continue;

                    _docFreq.TryGetValue(term, out int df);
                    double idf = Math.Log((_totalDocs - df + 0.5) / (df + 0.5) + 1.0);
                    double tfNorm = (tf * (K1 + 1)) / (tf + K1 * (1 - B + B * doc.Length / _avgDocLength));
                    score += idf * tfNorm;
                }

                if (score > 0)
                {
                    results.Add(new Bm25SearchResult
                    {
                        CacheEntry = doc.CacheEntry,
                        Score = score
                    });
                }
            }

            return results
                .OrderByDescending(r => r.Score)
                .Take(topN)
                .ToList();
        }

        /// <summary>
        /// Extracts double-quoted phrases from a query string.
        /// </summary>
        private static List<string> ExtractQuotedPhrases(string query)
        {
            var phrases = new List<string>();
            foreach (Match match in Regex.Matches(query, "\"([^\"]+)\""))
            {
                string phrase = match.Groups[1].Value.Trim();
                if (phrase.Length > 0)
                    phrases.Add(phrase);
            }
            return phrases;
        }

        /// <summary>
        /// Returns true if the cache entry's searchable text contains ALL required phrases (case-insensitive).
        /// </summary>
        private static bool MatchesRequiredPhrases(EmbeddingCacheEntry entry, List<string> requiredPhrases)
        {
            if (requiredPhrases.Count == 0)
                return true;

            string text = !string.IsNullOrWhiteSpace(entry.SearchableText)
                ? entry.SearchableText
                : entry.Title;

            foreach (var phrase in requiredPhrases)
            {
                if (!text.Contains(phrase, StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            return true;
        }

        private static List<string> Tokenize(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return new List<string>();

            // Lowercase, strip non-alphanumeric (keep letters, digits, and hyphens within words)
            text = text.ToLowerInvariant();
            var words = Regex.Split(text, @"[^a-z0-9\-]+")
                .Where(w => w.Length >= 2 && !StopWords.Contains(w))
                .ToList();

            return words;
        }

        private class DocEntry
        {
            public EmbeddingCacheEntry CacheEntry { get; set; } = null!;
            public Dictionary<string, int> TermFrequencies { get; set; } = new();
            public int Length { get; set; }
        }
    }

    public class Bm25SearchResult
    {
        public EmbeddingCacheEntry CacheEntry { get; set; } = null!;
        public double Score { get; set; }
    }
}
