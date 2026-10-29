using System.Text.RegularExpressions;
using FixMyCampus.Application.DTOs.Tickets;
using FixMyCampus.Application.Interfaces;
using FixMyCampus.Domain.Entities;
using FixMyCampus.Domain.Enums;
using FixMyCampus.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FixMyCampus.Infrastructure.Services;

public class DuplicateDetectionService : IDuplicateDetectionService
{
    private readonly ApplicationDbContext _context;

    // Thresholds for classification
    public const double ConfirmedThreshold = 0.60;
    public const double PotentialThreshold = 0.38;

    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "the", "a", "an", "is", "are", "was", "were", "in", "on", "at", "to", "for", "of",
        "and", "or", "it", "this", "that", "there", "has", "have", "had", "my", "our",
        "their", "please", "help", "from", "with", "by", "be", "been", "being", "we", "i",
        "you", "they", "he", "she", "so", "some", "any", "here", "just", "very", "too",
        "can", "could", "would", "should", "all", "its"
    };

    private static readonly (string Pattern, string Replacement)[] PhraseNormalizations =
    [
        (@"\b(stopped working|stops working|stop working)\b", "broken"),
        (@"\b(is not working|not working|doesn't work|does not work|wont work|won't work)\b", "broken"),
        (@"\b(out of order|malfunctioning|malfunctions|defective|faulty|busted)\b", "broken"),
        (@"\b(water leaking|water dripping|dripping|leakage|leaked|leaks)\b", "leak"),
        (@"\b(backed up|clogged up|clogging|clogs|blocked drain)\b", "clog"),
        (@"\b(no heat|freezing cold|too cold)\b", "heat defect"),
        (@"\b(no ac|no air conditioning|too hot|not cooling)\b", "ac defect"),
        (@"\b(flickering|flickers|flicker|burned out|burnt out)\b", "light defect")
    ];

    public DuplicateDetectionService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<DuplicateCheckResponse> CheckDuplicatesAsync(
        DuplicateCheckRequest request,
        CancellationToken cancellationToken = default)
    {
        return await CheckDuplicatesAsync(
            request.Category,
            request.Building,
            request.Floor,
            request.Room,
            request.Title,
            request.Description,
            cancellationToken);
    }

    public async Task<DuplicateCheckResponse> CheckDuplicatesAsync(
        string category,
        string building,
        string? floor,
        string? room,
        string title,
        string description,
        CancellationToken cancellationToken = default)
    {
        var response = new DuplicateCheckResponse();

        if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(description))
        {
            return response;
        }

        // Query active tickets in the same building and category
        var candidateBuilding = building?.Trim() ?? string.Empty;
        var candidateCategory = category?.Trim() ?? string.Empty;

        var activeTickets = await _context.Tickets
            .AsNoTracking()
            .Where(t => TicketStatusExtensions.ActiveStatuses.Contains(t.Status))
            .Where(t => t.Building.ToLower() == candidateBuilding.ToLower()
                     && t.Category.ToLower() == candidateCategory.ToLower())
            .ToListAsync(cancellationToken);

        if (activeTickets.Count == 0)
        {
            return response;
        }

        var candidateNormRoom = NormalizeRoom(room);
        var candidateNormFloor = NormalizeFloor(floor);

        var candidateCombinedText = $"{title} {description}".Trim();

        var matches = new List<DuplicateTicketMatchDto>();

        foreach (var existing in activeTickets)
        {
            // 1. Check Location Match
            var existingNormRoom = NormalizeRoom(existing.Room);
            var existingNormFloor = NormalizeFloor(existing.Floor);

            // If both tickets specify a room and they differ, they are in different rooms -> NOT a duplicate
            if (!string.IsNullOrEmpty(candidateNormRoom) && !string.IsNullOrEmpty(existingNormRoom))
            {
                if (!string.Equals(candidateNormRoom, existingNormRoom, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
            }

            // If both specify floors and they differ -> NOT a duplicate
            if (!string.IsNullOrEmpty(candidateNormFloor) && !string.IsNullOrEmpty(existingNormFloor))
            {
                if (!string.Equals(candidateNormFloor, existingNormFloor, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
            }

            // 2. Compute Text Similarity
            var existingCombinedText = $"{existing.Title} {existing.Description}".Trim();
            var similarity = ComputeTextSimilarity(candidateCombinedText, existingCombinedText, title, existing.Title);

            if (similarity < PotentialThreshold)
            {
                continue;
            }

            var isConfirmed = similarity >= ConfirmedThreshold;
            var matchLevel = isConfirmed ? "Confirmed" : "Potential";
            var matchReason = isConfirmed
                ? $"Active issue #{existing.Id.ToString()[..8]} at {existing.Building} {existing.Room ?? ""} describes the same problem ({Math.Round(similarity * 100)}% match)."
                : $"Active issue #{existing.Id.ToString()[..8]} at {existing.Building} {existing.Room ?? ""} has similar wording or equipment ({Math.Round(similarity * 100)}% match).";

            matches.Add(new DuplicateTicketMatchDto
            {
                TicketId = existing.Id,
                TicketNumber = $"T-{existing.Id.ToString()[..8].ToUpperInvariant()}",
                Title = existing.Title,
                Description = existing.Description,
                Status = existing.Status.ToString(),
                Category = existing.Category,
                Building = existing.Building,
                Floor = existing.Floor,
                Room = existing.Room,
                CreatedAt = existing.CreatedAt,
                SimilarityScore = Math.Round(similarity, 3),
                MatchLevel = matchLevel,
                Reason = matchReason
            });
        }

        if (matches.Count > 0)
        {
            // Sort matches: Confirmed first, then highest similarity score
            var orderedMatches = matches
                .OrderByDescending(m => m.MatchLevel == "Confirmed")
                .ThenByDescending(m => m.SimilarityScore)
                .ToList();

            response.Matches = orderedMatches;
            response.PrimaryMatch = orderedMatches.First();
            response.IsDuplicate = orderedMatches.Any(m => m.MatchLevel == "Confirmed");
            response.HasPotentialDuplicates = !response.IsDuplicate && orderedMatches.Any(m => m.MatchLevel == "Potential");

            if (response.IsDuplicate)
            {
                response.Message = $"An active ticket for this problem already exists in {response.PrimaryMatch.Building} {response.PrimaryMatch.Room ?? ""}.";
            }
            else if (response.HasPotentialDuplicates)
            {
                response.Message = "Potential duplicate active tickets were detected for this location. Please review them before continuing.";
            }
        }

        return response;
    }

    public static double ComputeTextSimilarity(
        string text1,
        string text2,
        string title1 = "",
        string title2 = "")
    {
        var norm1 = NormalizeAndStem(text1);
        var norm2 = NormalizeAndStem(text2);

        if (string.IsNullOrWhiteSpace(norm1) || string.IsNullOrWhiteSpace(norm2))
        {
            return 0.0;
        }

        // Exact match after normalization
        if (string.Equals(norm1, norm2, StringComparison.OrdinalIgnoreCase))
        {
            return 1.0;
        }

        var tokens1 = Tokenize(norm1);
        var tokens2 = Tokenize(norm2);

        if (tokens1.Count == 0 || tokens2.Count == 0)
        {
            return 0.0;
        }

        // Token Dice coefficient
        var tokenDice = ComputeDiceSimilarity(tokens1, tokens2);

        // Character 3-gram similarity
        var trigramDice = ComputeTrigramSimilarity(norm1, norm2);

        var combinedScore = (0.65 * tokenDice) + (0.35 * trigramDice);

        // If titles are also available, compute title similarity bonus
        if (!string.IsNullOrWhiteSpace(title1) && !string.IsNullOrWhiteSpace(title2))
        {
            var normTitle1 = NormalizeAndStem(title1);
            var normTitle2 = NormalizeAndStem(title2);

            if (string.Equals(normTitle1, normTitle2, StringComparison.OrdinalIgnoreCase))
            {
                combinedScore = Math.Max(combinedScore, 0.95);
            }
            else
            {
                var titleTokens1 = Tokenize(normTitle1);
                var titleTokens2 = Tokenize(normTitle2);
                if (titleTokens1.Count > 0 && titleTokens2.Count > 0)
                {
                    var titleDice = ComputeDiceSimilarity(titleTokens1, titleTokens2);
                    combinedScore = Math.Max(combinedScore, (0.4 * titleDice) + (0.6 * combinedScore));
                }
            }
        }

        return Math.Clamp(combinedScore, 0.0, 1.0);
    }

    public static string NormalizeAndStem(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;

        var lower = text.ToLowerInvariant();

        // Apply phrase normalizations (e.g. "not working" -> "broken")
        foreach (var (pattern, replacement) in PhraseNormalizations)
        {
            lower = Regex.Replace(lower, pattern, replacement, RegexOptions.IgnoreCase);
        }

        // Remove punctuation and special characters
        lower = Regex.Replace(lower, @"[^a-z0-9\s]", " ");

        // Split, remove stop words, and apply basic suffix stemming
        var words = lower.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var stemmedWords = new List<string>();

        foreach (var word in words)
        {
            if (word.Length <= 1) continue;
            if (StopWords.Contains(word)) continue;

            stemmedWords.Add(StemWord(word));
        }

        return string.Join(" ", stemmedWords);
    }

    public static string StemWord(string word)
    {
        if (word.Length <= 3) return word;

        // Basic English suffix stemming
        if (word.EndsWith("ing") && word.Length > 5)
        {
            return word[..^3];
        }
        if (word.EndsWith("ed") && word.Length > 4)
        {
            return word[..^2];
        }
        if (word.EndsWith("es") && word.Length > 4)
        {
            return word[..^2];
        }
        if (word.EndsWith('s') && !word.EndsWith("ss") && word.Length > 3)
        {
            return word[..^1];
        }

        return word;
    }

    public static List<string> Tokenize(string text)
    {
        return text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static double ComputeDiceSimilarity(IReadOnlyList<string> set1, IReadOnlyList<string> set2)
    {
        if (set1.Count == 0 || set2.Count == 0) return 0.0;

        var hash1 = new HashSet<string>(set1, StringComparer.OrdinalIgnoreCase);
        var hash2 = new HashSet<string>(set2, StringComparer.OrdinalIgnoreCase);

        var intersection = 0;
        foreach (var item in hash1)
        {
            if (hash2.Contains(item)) intersection++;
        }

        return (2.0 * intersection) / (hash1.Count + hash2.Count);
    }

    public static double ComputeTrigramSimilarity(string str1, string str2)
    {
        var tri1 = GetTrigrams(str1);
        var tri2 = GetTrigrams(str2);

        if (tri1.Count == 0 || tri2.Count == 0) return 0.0;

        var intersection = 0;
        foreach (var t in tri1)
        {
            if (tri2.Contains(t)) intersection++;
        }

        return (2.0 * intersection) / (tri1.Count + tri2.Count);
    }

    private static HashSet<string> GetTrigrams(string text)
    {
        var cleaned = text.Replace(" ", "");
        var set = new HashSet<string>();
        if (cleaned.Length < 3)
        {
            if (cleaned.Length > 0) set.Add(cleaned);
            return set;
        }

        for (var i = 0; i <= cleaned.Length - 3; i++)
        {
            set.Add(cleaned.Substring(i, 3));
        }
        return set;
    }

    public static string NormalizeRoom(string? room)
    {
        if (string.IsNullOrWhiteSpace(room)) return string.Empty;

        var r = room.Trim().ToLowerInvariant();
        r = Regex.Replace(r, @"^(room|rm|rm\.|#)[\s\-_]*", "", RegexOptions.IgnoreCase);
        r = Regex.Replace(r, @"[^a-z0-9]", "");
        return r;
    }

    public static string NormalizeFloor(string? floor)
    {
        if (string.IsNullOrWhiteSpace(floor)) return string.Empty;

        var f = floor.Trim().ToLowerInvariant();
        f = Regex.Replace(f, @"^(floor|fl|fl\.|level|lvl)[\s\-_]*", "", RegexOptions.IgnoreCase);
        f = Regex.Replace(f, @"[^a-z0-9]", "");
        return f;
    }
}
